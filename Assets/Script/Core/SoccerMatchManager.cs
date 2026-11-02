using System.Collections;
using Soccer3D.CameraSystem;
using Soccer3D.Data;
using Soccer3D.Effects;
using Soccer3D.Gameplay;
using Soccer3D.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Soccer3D.Core
{
    public class SoccerMatchManager : MonoBehaviour
    {
        private enum MatchState
        {
            PreMatchStats,
            Playing,
            PausedPeekStats,
            FoulSequence,
            FreeKick,
            GoalCelebration,
            MatchEnded
        }

        [Header("경기 설정")]
        public float matchDurationSeconds = 120f;

        private MatchState _state = MatchState.PreMatchStats;
        private int _playerScore;
        private int _aiScore;
        private float _remainingTime;

        // "공은 무조건 진 사람한테 가게" 보장용 상태 추적
        private bool _previousMatchLoserIsPlayer = true;
        private bool _lastConcededIsPlayer = true;

        // 반칙 & 직접 프리킥 세트피스 상태
        private SoccerPlayerController _fkKicker;
        private SoccerPlayerController _fkWallDefender;
        private Vector3 _fkBallPos;
        private Vector3 _fkGoalCenter;
        private Vector3 _fkAimPoint;
        private bool _fkIsCharging;
        private float _fkCharge01;
        private bool _fkCurveMode;
        private float _fkAiWaitTimer;
        private bool _fkExecutingStrike;

        private GameObject _stadiumRoot;
        private GoalTrigger _leftGoal;   // -1 (플레이어 수비 골대)
        private GoalTrigger _rightGoal;  // +1 (AI 수비 골대)

        private SoccerBall _ball;
        private SoccerPlayerController _humanPlayer;
        private SoccerPlayerController _aiPlayer;
        private GoalkeeperAI _playerKeeper;
        private GoalkeeperAI _aiKeeper;

        private DynamicSoccerCamera _dynamicCam;
        private SoccerUIManager _ui;

        private void Start()
        {
            InitializeWorldIfNeeded();
            BeginNewMatchWithRandomPlayers();
        }

        public void InitializeWorldIfNeeded()
        {
            if (_stadiumRoot != null) return;

            // 1. 이펙트 매니저 준비
            if (ImpactVfxManager.Instance == null)
                new GameObject("ImpactVfxManager").AddComponent<ImpactVfxManager>();
            if (GoalCelebrationManager.Instance == null)
                new GameObject("GoalCelebrationManager").AddComponent<GoalCelebrationManager>();

            // 2. 3D 경기장 & 양쪽 골대 생성
            _stadiumRoot = PitchBuilder.BuildStadium(out _leftGoal, out _rightGoal);
            _leftGoal.OnGoalScored += HandleGoalTriggered;
            _rightGoal.OnGoalScored += HandleGoalTriggered;

            // 3. 축구공 생성
            GameObject ballObj = new GameObject("SoccerBall");
            ballObj.AddComponent<SphereCollider>();
            ballObj.AddComponent<Rigidbody>();
            _ball = ballObj.AddComponent<SoccerBall>();
            _ball.BuildVisuals();

            // 4. 양쪽 AI 골키퍼 생성
            GameObject myGkObj = new GameObject("Player_Goalkeeper_AI");
            _playerKeeper = myGkObj.AddComponent<GoalkeeperAI>();
            _playerKeeper.Initialize(-1, _ball, new Color(0.2f, 0.7f, 1f));

            GameObject aiGkObj = new GameObject("Opponent_Goalkeeper_AI");
            _aiKeeper = aiGkObj.AddComponent<GoalkeeperAI>();
            _aiKeeper.Initialize(+1, _ball, new Color(1f, 0.3f, 0.3f));

            // 5. 플레이어(1P) & 상대 AI 필드 선수 생성
            GameObject humanObj = new GameObject("Player_1P");
            _humanPlayer = humanObj.AddComponent<SoccerPlayerController>();
            _humanPlayer.targetGoalkeeper = _aiKeeper;

            GameObject aiObj = new GameObject("Opponent_AI_1v1");
            _aiPlayer = aiObj.AddComponent<SoccerPlayerController>();
            _aiPlayer.targetGoalkeeper = _playerKeeper;

            _humanPlayer.opponent = _aiPlayer;
            _aiPlayer.opponent = _humanPlayer;

            // 6. 카메라 연결
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                mainCam = camObj.AddComponent<Camera>();
            }
            _dynamicCam = mainCam.GetComponent<DynamicSoccerCamera>();
            if (_dynamicCam == null)
                _dynamicCam = mainCam.gameObject.AddComponent<DynamicSoccerCamera>();

            _dynamicCam.playerTarget = _humanPlayer.transform;
            _dynamicCam.aiTarget = _aiPlayer.transform;
            _dynamicCam.ballTarget = _ball.transform;

            // 7. UI 매니저 생성 및 콜백 연결
            GameObject uiObj = new GameObject("SoccerUIManager");
            _ui = uiObj.AddComponent<SoccerUIManager>();
            _ui.Initialize(
                onCloseStatsAndStart: OnStatWindowExited,
                onRerollPlayers: RerollRandomPlayersPreview,
                onNextMatch: BeginNewMatchWithRandomPlayers
            );

            _humanPlayer.OnShotFeedback += (msg, weak) => _ui.ShowShotToast(msg, weak);
            _humanPlayer.OnFoulCommitted += HandleFoulCommitted;
            _aiPlayer.OnFoulCommitted += HandleFoulCommitted;
        }

        /// <summary>
        /// 매 판 시작할 때마다 양쪽 선수 이름·헤어스타일·키·몸무게·주발·세부스탯 완전 랜덤 생성!
        /// </summary>
        public void BeginNewMatchWithRandomPlayers()
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;

            if (ImpactVfxManager.Instance != null)
                ImpactVfxManager.Instance.StopBallAura();
            if (_dynamicCam != null)
                _dynamicCam.SetFreeKickCamera(false);

            _playerScore = 0;
            _aiScore = 0;
            _remainingTime = matchDurationSeconds;
            _lastConcededIsPlayer = _previousMatchLoserIsPlayer;
            _fkExecutingStrike = false;
            _fkIsCharging = false;
            _state = MatchState.PreMatchStats;

            RollAndApplyRandomProfiles();

            _ui.UpdateFreeKickHud(false, false, Vector3.zero, 0f, false);
            _ui.HideGoalCelebrationBanner();
            _ui.UpdateScoreAndTimer(_playerScore, _aiScore, _remainingTime);
            _ui.ShowPreMatchStats(isMidGamePeek: false);
        }

        private void RerollRandomPlayersPreview()
        {
            if (_state != MatchState.PreMatchStats)
            {
                BeginNewMatchWithRandomPlayers();
                return;
            }
            RollAndApplyRandomProfiles();
        }

        private void RollAndApplyRandomProfiles()
        {
            PlayerProfile myProfile = PlayerProfileGenerator.GenerateRandom(isPlayerTeam: true);
            PlayerProfile aiProfile = PlayerProfileGenerator.GenerateRandom(isPlayerTeam: false, excludeName: myProfile.playerName);

            _humanPlayer.SetupPlayer(myProfile, aiControlled: false, attackSign: +1, matchBall: _ball);
            _aiPlayer.SetupPlayer(aiProfile, aiControlled: true, attackSign: -1, matchBall: _ball);

            // 프리매치 스탯 확인 배경 쇼케이스 위치 배치
            _humanPlayer.SetControlsEnabled(false);
            _aiPlayer.SetControlsEnabled(false);

            _humanPlayer.transform.position = new Vector3(-1.8f, 0f, -0.5f);
            _humanPlayer.transform.rotation = Quaternion.Euler(0f, 145f, 0f);

            _aiPlayer.transform.position = new Vector3(1.8f, 0f, -0.5f);
            _aiPlayer.transform.rotation = Quaternion.Euler(0f, -145f, 0f);

            _ball.ResetAtPosition(new Vector3(0f, SoccerBall.BallRadius, -1.1f));
            _playerKeeper.ResetPosition();
            _aiKeeper.ResetPosition();

            if (_dynamicCam != null)
                _dynamicCam.SetPreMatchPreview(true);

            if (_ui != null)
                _ui.PopulatePlayerProfiles(myProfile, aiProfile);
        }

        /// <summary>
        /// "공은 무조건 진 사람한테 가게" 판정:
        /// 현재 지고 있는 쪽(점수가 낮은 쪽)이 무조건 공을 소유하며, 동점일 때는 방금 실점한 쪽(또는 직전 판 패배자)이 소유!
        /// </summary>
        private bool DetermineLoserGetsBall()
        {
            if (_playerScore < _aiScore) return true;
            if (_aiScore < _playerScore) return false;
            return _lastConcededIsPlayer;
        }

        /// <summary>
        /// 스탯 창에서 [나가기] 버튼을 누르면 즉시 창을 닫고 경기 시작(또는 재개)!
        /// </summary>
        private void OnStatWindowExited()
        {
            if (_dynamicCam != null)
                _dynamicCam.SetPreMatchPreview(false);

            if (_state == MatchState.PreMatchStats)
            {
                bool playerGetsBall = DetermineLoserGetsBall();
                SetupKickoffPositions(playerGetsBallFirst: playerGetsBall);
                _state = MatchState.Playing;
                _humanPlayer.SetControlsEnabled(true);
                _aiPlayer.SetControlsEnabled(true);
                _ui.ShowShotToast("⚽ 킥오프! [Q] 또는 [A]로 공을 띄운 뒤 공중에서 [D]를 누르면 오버헤드킥이 나갑니다!", false);
            }
            else if (_state == MatchState.PausedPeekStats)
            {
                _state = MatchState.Playing;
                Time.timeScale = 1f;
                _humanPlayer.SetControlsEnabled(true);
                _aiPlayer.SetControlsEnabled(true);
            }
        }

        private void SetupKickoffPositions(bool playerGetsBallFirst)
        {
            if (ImpactVfxManager.Instance != null)
                ImpactVfxManager.Instance.StopBallAura();
            if (_dynamicCam != null)
                _dynamicCam.SetFreeKickCamera(false);
            if (_ui != null)
                _ui.UpdateFreeKickHud(false, false, Vector3.zero, 0f, false);

            _humanPlayer.ResetStateForKickoff();
            _aiPlayer.ResetStateForKickoff();
            _playerKeeper.ResetPosition();
            _aiKeeper.ResetPosition();

            if (playerGetsBallFirst)
            {
                _humanPlayer.transform.position = new Vector3(-1.8f, 0f, 0f);
                _humanPlayer.transform.rotation = Quaternion.LookRotation(Vector3.right);

                // 상대는 충분히 멀리 물러나 있어 진 사람이 100% 안전하게 공을 소유하고 출발!
                _aiPlayer.transform.position = new Vector3(10.5f, 0f, 0f);
                _aiPlayer.transform.rotation = Quaternion.LookRotation(Vector3.left);

                _ball.ResetAtPosition(new Vector3(-1.1f, SoccerBall.BallRadius, 0f));
                _ball.SetOwner(_humanPlayer);
            }
            else
            {
                _humanPlayer.transform.position = new Vector3(-10.5f, 0f, 0f);
                _humanPlayer.transform.rotation = Quaternion.LookRotation(Vector3.right);

                _aiPlayer.transform.position = new Vector3(1.8f, 0f, 0f);
                _aiPlayer.transform.rotation = Quaternion.LookRotation(Vector3.left);

                _ball.ResetAtPosition(new Vector3(1.1f, SoccerBall.BallRadius, 0f));
                _ball.SetOwner(_aiPlayer);
            }
        }

        private void HandleFoulCommitted(FoulEventArgs foul)
        {
            if (_state != MatchState.Playing)
                return;

            StartCoroutine(FoulAndFreeKickSetupRoutine(foul));
        }

        private IEnumerator FoulAndFreeKickSetupRoutine(FoulEventArgs foul)
        {
            _state = MatchState.FoulSequence;
            _humanPlayer.SetControlsEnabled(false);
            _aiPlayer.SetControlsEnabled(false);
            _ui.UpdatePlayerWorldHud(_humanPlayer);

            string foulDesc = foul.isFromBehind
                ? "위험한 백태클 반칙!!"
                : (foul.isSlideTackle ? "거친 슬라이딩 태클 반칙!!" : "깊숙한 다리 걸기 반칙!!");
            string offenderName = foul.offender != null && foul.offender.Profile != null
                ? foul.offender.Profile.playerName
                : "선수";
            string awardSide = !foul.victim.isAI ? "내 선수 직접 프리킥 찬스!" : "AI 상대 직접 프리킥 선언!";

            _ui.ShowGoalCelebrationBanner($"🟨 삐익—! {foulDesc}", $"{offenderName} 경고!  ▶  {awardSide}");

            yield return new WaitForSeconds(1.35f);

            _ui.HideGoalCelebrationBanner();

            // 직접 프리킥 위치 정렬 (슈팅과 수비벽 연출이 가장 박진감 넘치는 구역으로 보정)
            _fkKicker = foul.victim;
            _fkWallDefender = foul.offender;
            _fkExecutingStrike = false;
            _fkIsCharging = false;
            _fkCharge01 = 0f;
            _fkCurveMode = false;
            _fkAiWaitTimer = 1.45f;

            int atkSign = _fkKicker.attackingGoalSign;
            _fkGoalCenter = new Vector3(atkSign * ShotCalculator.PitchHalfLength, 0f, 0f);

            float fkX = Mathf.Clamp(foul.foulSpot.x * atkSign, -1.5f, 16.5f) * atkSign;
            float fkZ = Mathf.Clamp(foul.foulSpot.z, -12.0f, 12.0f);
            _fkBallPos = new Vector3(fkX, SoccerBall.BallRadius, fkZ);

            _humanPlayer.ResetStateForKickoff();
            _aiPlayer.ResetStateForKickoff();
            _humanPlayer.SetControlsEnabled(false);
            _aiPlayer.SetControlsEnabled(false);
            _playerKeeper.ResetPosition();
            _aiKeeper.ResetPosition();

            _ball.ResetAtPosition(_fkBallPos);

            Vector3 toGoal = (_fkGoalCenter - _fkBallPos);
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude < 0.01f) toGoal = Vector3.right * atkSign;
            toGoal.Normalize();
            Vector3 kickRight = Vector3.Cross(Vector3.up, toGoal).normalized;

            // 키커는 주발 인사이드 각도에 맞춰 공 뒤쪽 사선에 도움닫기 스탠스 배치
            float runupSide = (_fkKicker.Profile != null && _fkKicker.Profile.preferredFoot == PreferredFoot.Left) ? 0.50f : -0.50f;
            _fkKicker.transform.position = new Vector3(_fkBallPos.x, 0f, _fkBallPos.z) - toGoal * 2.05f + kickRight * runupSide;
            _fkKicker.transform.rotation = Quaternion.LookRotation(toGoal);

            // 반칙을 범한 상대 선수는 1인 수비벽으로 공 앞 6.4m 지점 배치!
            float wallBias = Mathf.Clamp(-fkZ * 0.04f, -0.35f, 0.35f);
            _fkWallDefender.transform.position = new Vector3(_fkBallPos.x, 0f, _fkBallPos.z) + toGoal * 6.4f + kickRight * wallBias;
            _fkWallDefender.transform.rotation = Quaternion.LookRotation(-toGoal);

            _fkAimPoint = new Vector3(_fkGoalCenter.x, 1.55f, Mathf.Clamp(-fkZ * 0.18f, -2.2f, 2.2f));

            if (_dynamicCam != null)
                _dynamicCam.SetFreeKickCamera(true, _fkBallPos, _fkGoalCenter);

            _state = MatchState.FreeKick;
        }

        private void UpdateFreeKickMode(float dt)
        {
            if (_fkExecutingStrike || _fkKicker == null)
                return;

            if (!_fkKicker.isAI)
            {
                // 플레이어 직접 프리킥 조준 & 게이지 차징
                Vector2 aimMove = SoccerPlayerController.ReadMoveInput();
                Camera cam = Camera.main;
                Vector3 camRight = cam != null ? cam.transform.right : new Vector3(0f, 0f, -1f);
                camRight.y = 0f;
                if (camRight.sqrMagnitude < 0.001f) camRight = new Vector3(0f, 0f, -1f);
                camRight.Normalize();

                _fkAimPoint += camRight * (aimMove.x * 5.4f * dt);
                _fkAimPoint.y += aimMove.y * 2.9f * dt;
                _fkAimPoint.x = _fkGoalCenter.x;
                _fkAimPoint.z = Mathf.Clamp(_fkAimPoint.z, -ShotCalculator.GoalHalfWidth + 0.22f, ShotCalculator.GoalHalfWidth - 0.22f);
                _fkAimPoint.y = Mathf.Clamp(_fkAimPoint.y, 0.35f, ShotCalculator.GoalHeight - 0.16f);

                bool holdZ = SoccerPlayerController.ReadHoldZ();

                if (SoccerPlayerController.ReadShootPressedThisFrame())
                {
                    _fkIsCharging = true;
                    _fkCharge01 = 0.10f;
                    _fkCurveMode = holdZ;
                }

                if (_fkIsCharging)
                {
                    if (holdZ) _fkCurveMode = true;

                    if (SoccerPlayerController.ReadShootHeld())
                    {
                        _fkCharge01 = Mathf.Clamp01(_fkCharge01 + dt / 0.62f);
                        if (_fkCharge01 >= 1.0f)
                        {
                            StartCoroutine(ExecuteFreeKickStrikeSequence(_fkAimPoint, _fkCurveMode, _fkCharge01));
                            return;
                        }
                    }

                    if (SoccerPlayerController.ReadShootReleasedThisFrame())
                    {
                        StartCoroutine(ExecuteFreeKickStrikeSequence(_fkAimPoint, _fkCurveMode, Mathf.Max(0.25f, _fkCharge01)));
                        return;
                    }
                }

                _ui.UpdateFreeKickHud(true, true, _fkAimPoint, _fkIsCharging ? _fkCharge01 : 0f, _fkIsCharging ? _fkCurveMode : holdZ);
            }
            else
            {
                // AI 직접 프리킥: 플레이어는 수비벽에서 Space 키로 점프 블로킹 가능!
                _ui.UpdateFreeKickHud(true, false, Vector3.zero, 0f, false);

                if (SoccerPlayerController.ReadStandingTackleSpacePressedThisFrame() && _fkWallDefender != null)
                {
                    _fkWallDefender.TriggerDefensiveWallJump();
                }

                _fkAiWaitTimer -= dt;
                if (_fkAiWaitTimer <= 0f)
                {
                    float targetZ = (Random.value < 0.5f ? -1f : 1f) * Random.Range(2.15f, 3.35f);
                    float targetY = Random.Range(1.42f, 2.22f);
                    bool aiCurve = _fkKicker.Profile != null && _fkKicker.Profile.curve >= 76 && Random.value < 0.68f;
                    float aiCharge = Random.Range(0.58f, 0.85f);
                    StartCoroutine(ExecuteFreeKickStrikeSequence(new Vector3(_fkGoalCenter.x, targetY, targetZ), aiCurve, aiCharge));
                }
            }
        }

        private IEnumerator ExecuteFreeKickStrikeSequence(Vector3 aimPoint, bool isCurve, float charge01)
        {
            _fkExecutingStrike = true;
            _fkIsCharging = false;
            _ui.UpdateFreeKickHud(false, false, Vector3.zero, 0f, false);

            Vector3 toGoal = (_fkGoalCenter - _fkBallPos);
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude < 0.01f) toGoal = Vector3.right * _fkKicker.attackingGoalSign;
            toGoal.Normalize();

            Vector3 startKickerPos = _fkKicker.transform.position;
            Vector3 strikeKickerPos = new Vector3(_fkBallPos.x, 0f, _fkBallPos.z) - toGoal * 0.62f;

            // 도움닫기 스텝 연출 (0.24초)
            float runDuration = 0.24f;
            float elapsed = 0f;
            while (elapsed < runDuration)
            {
                float stepDt = Time.deltaTime;
                elapsed += stepDt;
                float t = Mathf.Clamp01(elapsed / runDuration);
                _fkKicker.transform.position = Vector3.Lerp(startKickerPos, strikeKickerPos, t);
                if (_fkKicker.visualRig != null)
                    _fkKicker.visualRig.UpdateProceduralAnimation(toGoal * 6.5f, false, stepDt);

                // 도움닫기 중에도 플레이어 수비벽 점프 입력 허용!
                if (_fkWallDefender != null && !_fkWallDefender.isAI && SoccerPlayerController.ReadStandingTackleSpacePressedThisFrame())
                {
                    _fkWallDefender.TriggerDefensiveWallJump();
                }

                yield return null;
            }

            // AI 수비벽은 슈팅 직전 타이밍에 점프 시도!
            if (_fkWallDefender != null)
            {
                if (_fkWallDefender.isAI && Random.value < 0.65f)
                    _fkWallDefender.TriggerDefensiveWallJump();

                _fkWallDefender.ActivateWallBlockCheck(0.58f);
            }

            // 골 트리거가 정상 감지되도록 슈팅 발사 직전 Playing 상태로 전환!
            _state = MatchState.Playing;
            _fkExecutingStrike = false;

            _fkKicker.ExecuteFreeKickStrike(aimPoint, isCurve, charge01);

            yield return new WaitForSeconds(0.32f);

            if (_state == MatchState.Playing)
            {
                if (_dynamicCam != null)
                    _dynamicCam.SetFreeKickCamera(false);
                _humanPlayer.SetControlsEnabled(true);
                _aiPlayer.SetControlsEnabled(true);
            }
        }

        private void HandleGoalTriggered(int goalSideSign)
        {
            if (_state != MatchState.Playing)
                return;

            StartCoroutine(GoalScoredSequence(goalSideSign));
        }

        private IEnumerator GoalScoredSequence(int goalSideSign)
        {
            _state = MatchState.GoalCelebration;
            _humanPlayer.SetControlsEnabled(false);
            _aiPlayer.SetControlsEnabled(false);
            _ui.UpdateFreeKickHud(false, false, Vector3.zero, 0f, false);

            bool playerScored = goalSideSign > 0; // +1은 오른쪽 AI 골대 -> 플레이어 득점!
            if (playerScored)
                _playerScore++;
            else
                _aiScore++;

            _lastConcededIsPlayer = !playerScored;
            _ui.UpdateScoreAndTimer(_playerScore, _aiScore, _remainingTime);

            SoccerPlayerController scorer = playerScored ? _humanPlayer : _aiPlayer;
            ShotMetadata lastShot = _ball.LastShot;
            bool wasAcrobatic = lastShot.shotType == ShotType.OverheadKick || lastShot.shotType == ShotType.AcrobaticVolley;

            // 극적인 골 임팩트 슬로모션 & 카메라 쉐이크!
            Time.timeScale = 0.35f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            if (_dynamicCam != null)
            {
                _dynamicCam.TriggerImpactShake(0.68f, 0.42f);
                _dynamicCam.TriggerGoalCelebrationFocus(scorer.transform, 3.1f);
            }

            scorer.PlayGoalCelebration();

            Vector3 goalCenter = new Vector3(goalSideSign * ShotCalculator.PitchHalfLength, 0f, 0f);
            Color teamColor = scorer.Profile != null ? scorer.Profile.jerseyPrimaryColor : Color.yellow;
            if (GoalCelebrationManager.Instance != null)
            {
                GoalCelebrationManager.Instance.PlayGoalSpectacle(goalCenter, teamColor, wasAcrobatic);
            }

            // 화려한 골 배너 텍스트 구성
            string headline;
            if (lastShot.shotType == ShotType.OverheadKick)
                headline = "🔥 오버헤드킥 원더골!! 🔥";
            else if (lastShot.shotType == ShotType.AcrobaticVolley)
                headline = "⚡ 환상의 시저스 발리 골!! ⚡";
            else if (lastShot.shotType == ShotType.FinesseCurve)
                headline = "🌀 예술적인 감아차기 골!! 🌀";
            else
                headline = "⚽ GOOOAAAL!! ⚽";

            int kmh = Mathf.Max(65, Mathf.RoundToInt(lastShot.initialSpeedMps * 3.6f));
            string scorerName = scorer.Profile != null ? scorer.Profile.playerName : (playerScored ? "내 선수" : "AI");
            string footNote = lastShot.usedWeakFoot ? "약발 극복 극적 골!" : $"주발({(lastShot.footUsed == PreferredFoot.Right ? "오른발" : "왼발")}) 완벽 임팩트!";
            string sub = $"{scorerName}  |  {ShotCalculator.GetVfxKoreanName(lastShot.vfxType)} ({kmh} km/h)  |  {footNote}";

            _ui.ShowGoalCelebrationBanner(headline, sub);

            yield return new WaitForSecondsRealtime(0.75f);
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;

            yield return new WaitForSecondsRealtime(2.35f);

            _ui.HideGoalCelebrationBanner();

            if (_remainingTime <= 0f)
            {
                FinishMatch();
                yield break;
            }

            // 무조건 지고 있는 사람(동점이면 방금 골 먹힌 사람)에게 공이 직행!
            bool loserGetsBall = DetermineLoserGetsBall();
            SetupKickoffPositions(playerGetsBallFirst: loserGetsBall);
            _state = MatchState.Playing;
            _humanPlayer.SetControlsEnabled(true);
            _aiPlayer.SetControlsEnabled(true);

            if (loserGetsBall)
                _ui.ShowShotToast("⚽ 지고 있는 내 선수에게 우선 볼 소유권이 지급되었습니다!", false);
            else
                _ui.ShowShotToast("⚽ 지고 있는 상대 선수에게 볼 소유권이 넘어갔습니다!", false);
        }

        private void FinishMatch()
        {
            _state = MatchState.MatchEnded;
            _humanPlayer.SetControlsEnabled(false);
            _aiPlayer.SetControlsEnabled(false);
            if (_dynamicCam != null)
                _dynamicCam.SetFreeKickCamera(false);
            _ui.UpdateFreeKickHud(false, false, Vector3.zero, 0f, false);
            Time.timeScale = 1f;

            if (_playerScore < _aiScore)
                _previousMatchLoserIsPlayer = true;
            else if (_aiScore < _playerScore)
                _previousMatchLoserIsPlayer = false;

            string pName = _humanPlayer.Profile != null ? _humanPlayer.Profile.playerName : "PLAYER";
            string aName = _aiPlayer.Profile != null ? _aiPlayer.Profile.playerName : "AI";
            _ui.ShowMatchEndScreen(_playerScore, _aiScore, pName, aName);
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;

            // 스탯창 열려 있을 때 Space / Enter / Escape 키로도 간편하게 창 나가고 경기 시작 가능!
            if (_state == MatchState.PreMatchStats || _state == MatchState.PausedPeekStats)
            {
                if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                {
                    if (_ui != null && _ui.IsStatWindowOpen)
                    {
                        _ui.CloseStatWindowAndStart();
                    }
                }
                return;
            }

            if (_state == MatchState.FreeKick)
            {
                if (kb != null && kb.rKey.wasPressedThisFrame)
                {
                    BeginNewMatchWithRandomPlayers();
                    return;
                }
                UpdateFreeKickMode(Time.deltaTime);
                return;
            }

            if (_state == MatchState.Playing)
            {
                _remainingTime -= Time.deltaTime;
                _ui.UpdateScoreAndTimer(_playerScore, _aiScore, _remainingTime);
                _ui.UpdatePlayerWorldHud(_humanPlayer);

                if (kb != null)
                {
                    // Tab 키: 경기 중 언제든 스탯 다시 열람
                    if (kb.tabKey.wasPressedThisFrame)
                    {
                        _state = MatchState.PausedPeekStats;
                        _humanPlayer.SetControlsEnabled(false);
                        _aiPlayer.SetControlsEnabled(false);
                        Time.timeScale = 0f;
                        _ui.ShowPreMatchStats(isMidGamePeek: true);
                        return;
                    }

                    // R 키: 즉시 새 판 (랜덤 선수 재추첨)
                    if (kb.rKey.wasPressedThisFrame)
                    {
                        BeginNewMatchWithRandomPlayers();
                        return;
                    }
                }

                if (_remainingTime <= 0f)
                {
                    FinishMatch();
                }
            }
        }
    }
}
