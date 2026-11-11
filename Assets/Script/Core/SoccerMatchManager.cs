using System.Collections;
using System.Collections.Generic;
using Soccer3D.CameraSystem;
using Soccer3D.Data;
using Soccer3D.Effects;
using Soccer3D.Gameplay;
using Soccer3D.UI;
using UnityEngine;
using UnityEngine.EventSystems;
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
        public float matchDurationSeconds = 180f;

        private MatchState _state = MatchState.PreMatchStats;
        private int _playerScore;
        private int _aiScore;
        private float _remainingTime;

        // 방금 골을 먹힌(실점한) 팀에게 킥오프 공 소유권을 지급하기 위한 상태 추적
        private bool _lastConcededIsPlayer = true;

        private enum SetPieceType
        {
            DirectFreeKick,
            PenaltyKick,
            DistantPassKick
        }

        // 반칙 & 세트피스 상태 (직접 프리킥 / 페널티킥 / 장거리 노월 빌드업 프리킥 지원!)
        private Coroutine _foulSetupCoroutine;
        private SetPieceType _fkSetPieceType = SetPieceType.DirectFreeKick;
        private SoccerPlayerController _fkKicker;
        private readonly List<SoccerPlayerController> _fkWallDefenders = new List<SoccerPlayerController>();
        private Vector3 _fkBallPos;
        private Vector3 _fkGoalCenter;
        private Vector3 _fkAimPoint;
        private bool _fkIsCharging;
        private float _fkCharge01;
        private bool _fkCurveMode;
        private float _fkAiWaitTimer;
        private bool _fkExecutingStrike;

        private GameObject _stadiumRoot;
        private GoalTrigger _leftGoal;   // -1 (블루/플레이어 팀 수비 골대)
        private GoalTrigger _rightGoal;  // +1 (레드/AI 팀 수비 골대)

        private SoccerBall _ball;

        // 11 vs 11 정규 풀 매치 로스터: 각 팀 필드플레이어 10명 + 골키퍼 1명 = 총 11명씩 (합산 22명)!
        public const int OutfieldCountPerTeam = 10;

        private FormationPreset _blueFormation;
        private FormationPreset _redFormation;

        private SoccerPlayerController _humanPlayer;
        private readonly List<SoccerPlayerController> _blueDefenders = new List<SoccerPlayerController>(9);
        private GoalkeeperAI _playerKeeper;

        private SoccerPlayerController _aiPlayer;
        private readonly List<SoccerPlayerController> _redDefenders = new List<SoccerPlayerController>(9);
        private GoalkeeperAI _aiKeeper;

        private readonly List<SoccerPlayerController> _blueOutfield = new List<SoccerPlayerController>(10);
        private readonly List<SoccerPlayerController> _redOutfield = new List<SoccerPlayerController>(10);
        private readonly List<SoccerPlayerController> _allOutfield = new List<SoccerPlayerController>(20);

        private DynamicSoccerCamera _dynamicCam;
        private SoccerUIManager _ui;

        private static TacticalRole MapSlotToTacticalRole(FormationSlot slot)
        {
            if (slot.line == PositionLine.Forward)
                return TacticalRole.Striker;
            if (slot.normLaneZ > 0.22f)
                return TacticalRole.LeftDefender;
            if (slot.normLaneZ < -0.22f)
                return TacticalRole.RightDefender;
            return TacticalRole.CenterDefender;
        }

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

            // 2. FIFA 정규 규격(105m x 68m) 3D 경기장 & 양쪽 골대 생성
            _stadiumRoot = PitchBuilder.BuildStadium(out _leftGoal, out _rightGoal);
            _leftGoal.OnGoalScored += HandleGoalTriggered;
            _rightGoal.OnGoalScored += HandleGoalTriggered;

            // 3. 축구공 생성
            GameObject ballObj = new GameObject("SoccerBall");
            ballObj.AddComponent<SphereCollider>();
            ballObj.AddComponent<Rigidbody>();
            _ball = ballObj.AddComponent<SoccerBall>();
            _ball.BuildVisuals();

            // 4. 양쪽 강화형 AI 골키퍼 생성 (팀당 1명씩)
            GameObject myGkObj = new GameObject("Blue_Goalkeeper_AI");
            _playerKeeper = myGkObj.AddComponent<GoalkeeperAI>();
            _playerKeeper.Initialize(-1, _ball, new Color(0.2f, 0.7f, 1f));

            GameObject aiGkObj = new GameObject("Red_Goalkeeper_AI");
            _aiKeeper = aiGkObj.AddComponent<GoalkeeperAI>();
            _aiKeeper.Initialize(+1, _ball, new Color(1f, 0.3f, 0.3f));

            // 5. 블루 팀 필드 선수 10명 생성 (+ 골키퍼 1명 = 총 11명!)
            _blueOutfield.Clear();
            _blueDefenders.Clear();

            GameObject humanObj = new GameObject("Blue_Player_1");
            _humanPlayer = humanObj.AddComponent<SoccerPlayerController>();
            _humanPlayer.targetGoalkeeper = _aiKeeper;
            _blueOutfield.Add(_humanPlayer);

            for (int i = 0; i < OutfieldCountPerTeam - 1; i++)
            {
                GameObject mateObj = new GameObject($"Blue_Player_{i + 2}");
                SoccerPlayerController mate = mateObj.AddComponent<SoccerPlayerController>();
                mate.targetGoalkeeper = _aiKeeper;
                _blueDefenders.Add(mate);
                _blueOutfield.Add(mate);
            }

            // 6. 레드 팀 필드 선수 10명 생성 (+ 골키퍼 1명 = 총 11명!)
            _redOutfield.Clear();
            _redDefenders.Clear();

            GameObject aiObj = new GameObject("Red_Player_1");
            _aiPlayer = aiObj.AddComponent<SoccerPlayerController>();
            _aiPlayer.targetGoalkeeper = _playerKeeper;
            _redOutfield.Add(_aiPlayer);

            for (int i = 0; i < OutfieldCountPerTeam - 1; i++)
            {
                GameObject defObj = new GameObject($"Red_Player_{i + 2}");
                SoccerPlayerController def = defObj.AddComponent<SoccerPlayerController>();
                def.targetGoalkeeper = _playerKeeper;
                _redDefenders.Add(def);
                _redOutfield.Add(def);
            }

            // 7. 전체 20명 필드 플레이어 팀/상대 참조 리스트 상호 연결 (+ 골키퍼 2명 = 11 vs 11 총 22명!)
            _allOutfield.Clear();
            _allOutfield.AddRange(_blueOutfield);
            _allOutfield.AddRange(_redOutfield);

            WireTeamRelationships(_blueOutfield, _redOutfield);
            WireTeamRelationships(_redOutfield, _blueOutfield);

            // 8. 1인칭 메인 카메라 연결
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
            _dynamicCam.humanController = _humanPlayer;

            // 9. UI 매니저 생성 및 이벤트 콜백 연결
            GameObject uiObj = new GameObject("SoccerUIManager");
            _ui = uiObj.AddComponent<SoccerUIManager>();
            _ui.Initialize(
                onCloseStatsAndStart: OnStatWindowExited,
                onRerollPlayers: RerollRandomPlayersPreview,
                onNextMatch: BeginNewMatchWithRandomPlayers
            );

            for (int i = 0; i < _blueOutfield.Count; i++)
            {
                SoccerPlayerController bp = _blueOutfield[i];
                bp.OnShotFeedback += (msg, weak) => _ui.ShowShotToast(msg, weak);
                bp.OnHumanPassExecuted += receiver =>
                {
                    if (receiver != null && _blueOutfield.Contains(receiver))
                        SwitchControlledBluePlayer(receiver, showToast: false);
                };
            }

            for (int i = 0; i < _allOutfield.Count; i++)
            {
                _allOutfield[i].OnFoulCommitted += HandleFoulCommitted;
            }

            _aiKeeper.OnSaveMade += (_, diving) =>
            {
                if (_state == MatchState.Playing)
                    _ui.ShowShotToast(diving ? "🧤 슈퍼 다이빙 세이브!! 골키퍼가 구석 코스를 끝까지 막아냈습니다!" : "🧤 상대 골키퍼 정면 선방! 구석을 노리거나 감아차기/오버헤드킥을 시도해보세요!", false);
            };
            _playerKeeper.OnSaveMade += (_, diving) =>
            {
                if (_state == MatchState.Playing && diving)
                    _ui.ShowShotToast("🧤 우리 팀 골키퍼 환상적인 슈퍼 세이브!!", false);
            };
        }

        private static void WireTeamRelationships(List<SoccerPlayerController> sameTeam, List<SoccerPlayerController> enemyTeam)
        {
            for (int i = 0; i < sameTeam.Count; i++)
            {
                SoccerPlayerController p = sameTeam[i];
                p.teammates.Clear();
                for (int j = 0; j < sameTeam.Count; j++)
                {
                    if (sameTeam[j] != p)
                        p.teammates.Add(sameTeam[j]);
                }

                p.opponents.Clear();
                p.opponents.AddRange(enemyTeam);
                if (enemyTeam.Count > 0)
                    p.opponent = enemyTeam[0];
            }
        }

        private void SetAllOutfieldControls(bool enabled)
        {
            for (int i = 0; i < _allOutfield.Count; i++)
            {
                if (_allOutfield[i] != null)
                    _allOutfield[i].SetControlsEnabled(enabled);
            }
        }

        private void ResetAllOutfieldForKickoff()
        {
            for (int i = 0; i < _allOutfield.Count; i++)
            {
                if (_allOutfield[i] != null)
                    _allOutfield[i].ResetStateForKickoff();
            }
        }

        /// <summary>
        /// 매 판 시작할 때마다 11 vs 11 양 팀 포메이션·선발 라인업·포지션 특화 스탯 완전 랜덤 생성!
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
            _lastConcededIsPlayer = true;
            _fkExecutingStrike = false;
            _fkIsCharging = false;
            _state = MatchState.PreMatchStats;
            SetCursorLocked(false);

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
            // 새 매치 시작 시 기본 1P 조작 선수를 1번 최전방 공격수로 초기화
            _humanPlayer = _blueOutfield[0];
            _aiPlayer = _redOutfield[0];
            if (_dynamicCam != null)
            {
                _dynamicCam.playerTarget = _humanPlayer.transform;
                _dynamicCam.humanController = _humanPlayer;
            }

            // 판마다 양 팀 포메이션 랜덤 추첨! (4-3-3, 4-4-2, 4-2-3-1, 3-5-2, 3-4-3)
            _blueFormation = PlayerProfileGenerator.PickRandomFormation();
            _redFormation = PlayerProfileGenerator.PickRandomFormation();

            HashSet<string> usedNames = new HashSet<string>();

            // 양 팀 골키퍼(#1 GK) 랜덤 프로필 배정
            if (_playerKeeper != null)
            {
                PlayerProfile blueGkProf = PlayerProfileGenerator.GenerateGoalkeeperProfile(true, usedNames);
                _playerKeeper.ApplyProfile(blueGkProf);
            }
            if (_aiKeeper != null)
            {
                PlayerProfile redGkProf = PlayerProfileGenerator.GenerateGoalkeeperProfile(false, usedNames);
                _aiKeeper.ApplyProfile(redGkProf);
            }

            PlayerProfile blueLeader = null;
            PlayerProfile redLeader = null;

            for (int i = 0; i < _blueOutfield.Count; i++)
            {
                FormationSlot slot = _blueFormation.outfieldSlots[i % _blueFormation.outfieldSlots.Length];
                PlayerProfile prof = PlayerProfileGenerator.GenerateProfileForSlot(true, slot, i, usedNames, blueLeader);
                if (i == 0)
                {
                    blueLeader = prof;
                }
                TacticalRole role = MapSlotToTacticalRole(slot);
                _blueOutfield[i].SetupPlayer(prof, aiControlled: i != 0, attackSign: +1, matchBall: _ball, role: role, formationSlot: slot, squadIndex: i);
            }

            for (int i = 0; i < _redOutfield.Count; i++)
            {
                FormationSlot slot = _redFormation.outfieldSlots[i % _redFormation.outfieldSlots.Length];
                PlayerProfile prof = PlayerProfileGenerator.GenerateProfileForSlot(false, slot, i, usedNames, redLeader);
                if (i == 0)
                {
                    redLeader = prof;
                    // 아군 블루팀 유니폼과 적군 레드팀 유니폼이 확실히 대비되도록 보정
                    if (blueLeader != null && Vector3.Distance(
                            new Vector3(blueLeader.jerseyPrimaryColor.r, blueLeader.jerseyPrimaryColor.g, blueLeader.jerseyPrimaryColor.b),
                            new Vector3(redLeader.jerseyPrimaryColor.r, redLeader.jerseyPrimaryColor.g, redLeader.jerseyPrimaryColor.b)) < 0.45f)
                    {
                        blueLeader.jerseyPrimaryColor = new Color(0.10f, 0.44f, 0.96f);
                        redLeader.jerseyPrimaryColor = new Color(0.92f, 0.16f, 0.22f);
                    }
                }
                TacticalRole role = MapSlotToTacticalRole(slot);
                _redOutfield[i].SetupPlayer(prof, aiControlled: true, attackSign: -1, matchBall: _ball, role: role, formationSlot: slot, squadIndex: i);
            }

            // 프리매치 11 vs 11 선발 라인업 소개: 실제 포메이션 위치에 22명 전원을 배치하여 GK부터 1명씩 3D 클로즈업 소개!
            SetupKickoffPositions(playerGetsBallFirst: DetermineKickoffPlayerGetsBall());
            SetAllOutfieldControls(false);
            _ball.ResetAtPosition(new Vector3(0f, SoccerBall.BallRadius, 0f));

            if (_dynamicCam != null)
                _dynamicCam.SetPreMatchPreview(true);

            if (_ui != null)
            {
                _ui.PopulateMatchLineupsAndProfiles(_blueFormation, _redFormation, _playerKeeper, _blueOutfield, _aiKeeper, _redOutfield);
                _ui.PopulatePlayerProfiles(_humanPlayer.Profile, _aiPlayer.Profile);
            }
        }

        /// <summary>
        /// 킥오프 볼 소유권 판정:
        /// 전체 점수(진 사람) 기준이 아니라, 실제 축구 룰처럼 **방금 골을 먹힌 팀(_lastConcededIsPlayer)**에게 100% 공을 지급!
        /// </summary>
        private bool DetermineKickoffPlayerGetsBall()
        {
            return _lastConcededIsPlayer;
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnStatWindowExited()
        {
            if (_dynamicCam != null)
                _dynamicCam.SetPreMatchPreview(false);

            if (_state == MatchState.PreMatchStats)
            {
                bool playerGetsBall = DetermineKickoffPlayerGetsBall();
                SetupKickoffPositions(playerGetsBallFirst: playerGetsBall);
                _state = MatchState.Playing;
                SetCursorLocked(true);
                SetAllOutfieldControls(true);
                _ui.UpdateControlledPlayerHudInfo(_humanPlayer);
                _ui.UpdateTacticalMinimap(_humanPlayer, _blueOutfield, _aiPlayer, _redDefenders, _playerKeeper, _aiKeeper, _ball);
                string blueFormName = _blueFormation != null ? _blueFormation.formationName : "4-3-3";
                string redFormName = _redFormation != null ? _redFormation.formationName : "4-4-2";
                _ui.ShowShotToast($"⚽ 11 vs 11 매치 킥오프! [블루 {blueFormName} vs 레드 {redFormName}] | [C / 1~0] 10명 선수 전환!", false);
            }
            else if (_state == MatchState.PausedPeekStats)
            {
                _state = MatchState.Playing;
                Time.timeScale = 1f;
                SetCursorLocked(true);
                SetAllOutfieldControls(true);
            }
        }

        /// <summary>
        /// 블루팀 필드 플레이어 10명(1~0번 키 및 C 스마트 전환) 간의 자유로운 실시간 1인칭 선수 전환!
        /// </summary>
        public void SwitchControlledBluePlayer(SoccerPlayerController nextPlayer, bool showToast = true)
        {
            if (nextPlayer == null || nextPlayer == _humanPlayer || !_blueOutfield.Contains(nextPlayer))
                return;
            if (_humanPlayer != null && _humanPlayer.IsPerformingAcrobatic)
                return;

            SoccerPlayerController prevPlayer = _humanPlayer;
            if (prevPlayer != null)
                prevPlayer.OnSwitchedToAI();

            _humanPlayer = nextPlayer;
            _humanPlayer.OnSwitchedToHuman(smartAlignView: true);

            if (_dynamicCam != null)
            {
                _dynamicCam.NotifyPlayerSwitched(0.22f);
                _dynamicCam.playerTarget = _humanPlayer.transform;
                _dynamicCam.humanController = _humanPlayer;
            }

            if (_ui != null)
            {
                _ui.UpdateControlledPlayerHudInfo(_humanPlayer);
                if (showToast && _humanPlayer.Profile != null)
                {
                    string posLabel = !string.IsNullOrEmpty(_humanPlayer.Profile.positionCode)
                        ? _humanPlayer.Profile.positionCode
                        : _humanPlayer.AssignedSlot.positionCode;
                    _ui.ShowShotToast($"🔄 [선수 전환] [{posLabel}] #{_humanPlayer.Profile.jerseyNumber} {_humanPlayer.Profile.playerName} 직접 조작! ([1~0] 번호 / [C] 스마트 전환)", false);
                }
            }
        }

        private void HandlePlayerSwitchingInput(Keyboard kb)
        {
            if (_humanPlayer == null || _humanPlayer.IsPerformingAcrobatic || _humanPlayer.IsChargingShot)
                return;

            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame && _blueOutfield.Count > 0) { SwitchControlledBluePlayer(_blueOutfield[0]); return; }
                if (kb.digit2Key.wasPressedThisFrame && _blueOutfield.Count > 1) { SwitchControlledBluePlayer(_blueOutfield[1]); return; }
                if (kb.digit3Key.wasPressedThisFrame && _blueOutfield.Count > 2) { SwitchControlledBluePlayer(_blueOutfield[2]); return; }
                if (kb.digit4Key.wasPressedThisFrame && _blueOutfield.Count > 3) { SwitchControlledBluePlayer(_blueOutfield[3]); return; }
                if (kb.digit5Key.wasPressedThisFrame && _blueOutfield.Count > 4) { SwitchControlledBluePlayer(_blueOutfield[4]); return; }
                if (kb.digit6Key.wasPressedThisFrame && _blueOutfield.Count > 5) { SwitchControlledBluePlayer(_blueOutfield[5]); return; }
                if (kb.digit7Key.wasPressedThisFrame && _blueOutfield.Count > 6) { SwitchControlledBluePlayer(_blueOutfield[6]); return; }
                if (kb.digit8Key.wasPressedThisFrame && _blueOutfield.Count > 7) { SwitchControlledBluePlayer(_blueOutfield[7]); return; }
                if (kb.digit9Key.wasPressedThisFrame && _blueOutfield.Count > 8) { SwitchControlledBluePlayer(_blueOutfield[8]); return; }
                if (kb.digit0Key.wasPressedThisFrame && _blueOutfield.Count > 9) { SwitchControlledBluePlayer(_blueOutfield[9]); return; }
            }

            bool smartSwitchPressed = SoccerPlayerController.ReadPlayerSwitchPressedThisFrame();

            // 피파식 Q 선수 전환 지원: 내가 공을 소유하지 않았고 루즈볼 셀프 플릭 반경(2.2m) 밖일 때 Q키를 누르면 즉시 선수 전환!
            if (!smartSwitchPressed && kb != null && kb.qKey.wasPressedThisFrame && _ball != null && _ball.CurrentOwner != _humanPlayer)
            {
                float planarDistBall = Vector2.Distance(
                    new Vector2(_humanPlayer.transform.position.x, _humanPlayer.transform.position.z),
                    new Vector2(_ball.transform.position.x, _ball.transform.position.z));
                bool nearLooseGroundBall = _ball.CurrentOwner == null && !_ball.IsAirborne && planarDistBall <= 2.20f;
                if (!nearLooseGroundBall)
                    smartSwitchPressed = true;
            }

            if (!smartSwitchPressed || _ball == null)
                return;

            // 마우스 포인터(또는 1인칭 조준 크로스헤어)에 가장 가까이 있는 우리 팀 선수로 전환하되,
            // 마우스 포인터 근처에 여러 명의 선수가 모여 있다면 그중 공과 가장 가까이 있는 선수로 전환!
            Camera cam = Camera.main;
            Vector2 pointerScreenPos = (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null)
                ? Mouse.current.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            Ray pointerRay = cam != null
                ? cam.ScreenPointToRay(pointerScreenPos)
                : new Ray(_humanPlayer.GetFirstPersonEyePosition(), _humanPlayer.GetFirstPersonAimDirection());

            float minPointerAngle = float.MaxValue;
            SoccerPlayerController closestToPointerMate = null;
            List<SoccerPlayerController> validTeammates = new List<SoccerPlayerController>(3);
            List<float> pointerAngles = new List<float>(3);

            for (int i = 0; i < _blueOutfield.Count; i++)
            {
                SoccerPlayerController bp = _blueOutfield[i];
                if (bp == null || bp == _humanPlayer) continue;

                Vector3 targetChest = bp.transform.position + Vector3.up * 1.0f;
                Vector3 toMate = targetChest - pointerRay.origin;
                float angleDeg = toMate.sqrMagnitude > 0.001f
                    ? Vector3.Angle(pointerRay.direction, toMate.normalized)
                    : 0f;

                validTeammates.Add(bp);
                pointerAngles.Add(angleDeg);

                if (angleDeg < minPointerAngle)
                {
                    minPointerAngle = angleDeg;
                    closestToPointerMate = bp;
                }
            }

            // 마우스 포인터 근처(포인터로부터 16도 이내 또는 최단 각도 선수와 9.5도 이내 반경)에 있는 후보 선수들 수집
            float nearPointerThresholdDeg = Mathf.Max(16.0f, minPointerAngle + 9.5f);
            List<SoccerPlayerController> nearPointerCandidates = new List<SoccerPlayerController>(3);
            for (int i = 0; i < validTeammates.Count; i++)
            {
                if (pointerAngles[i] <= nearPointerThresholdDeg)
                    nearPointerCandidates.Add(validTeammates[i]);
            }

            SoccerPlayerController targetMate = closestToPointerMate;

            // 만약 마우스 포인터 근처에 여러 사람이 있다면 -> 그중 공과 가장 가까이 있는 사람으로 변경!
            if (nearPointerCandidates.Count >= 2)
            {
                float bestBallDist = float.MaxValue;
                Vector2 ballXZ = new Vector2(_ball.transform.position.x, _ball.transform.position.z);
                for (int i = 0; i < nearPointerCandidates.Count; i++)
                {
                    SoccerPlayerController cand = nearPointerCandidates[i];
                    float dBall = Vector2.Distance(new Vector2(cand.transform.position.x, cand.transform.position.z), ballXZ);
                    if (dBall < bestBallDist)
                    {
                        bestBallDist = dBall;
                        targetMate = cand;
                    }
                }
            }
            else if (nearPointerCandidates.Count == 1)
            {
                targetMate = nearPointerCandidates[0];
            }

            if (targetMate != null)
                SwitchControlledBluePlayer(targetMate);
        }

        private void SetupKickoffPositions(bool playerGetsBallFirst)
        {
            if (ImpactVfxManager.Instance != null)
                ImpactVfxManager.Instance.StopBallAura();
            if (_dynamicCam != null)
                _dynamicCam.SetFreeKickCamera(false);
            if (_ui != null)
                _ui.UpdateFreeKickHud(false, false, Vector3.zero, 0f, false);

            ResetAllOutfieldForKickoff();
            _playerKeeper.ResetPosition();
            _aiKeeper.ResetPosition();

            // 105m x 68m 정규 구장 11 vs 11 랜덤 포메이션 킥오프 진형 배치! (kickoffDepthX, kickoffLaneZ는 미터 단위 좌표)
            for (int i = 0; i < _blueOutfield.Count; i++)
            {
                SoccerPlayerController bp = _blueOutfield[i];
                if (bp == null || bp == _humanPlayer) continue;
                FormationSlot slot = bp.AssignedSlot;
                float x = Mathf.Clamp(slot.kickoffDepthX, -ShotCalculator.PitchHalfLength + 6f, -3.5f);
                float z = Mathf.Clamp(slot.kickoffLaneZ, -ShotCalculator.PitchHalfWidth + 4f, ShotCalculator.PitchHalfWidth - 4f);
                bp.transform.position = new Vector3(x, 0f, z);
                bp.transform.rotation = Quaternion.LookRotation(Vector3.right);
            }

            for (int i = 0; i < _redOutfield.Count; i++)
            {
                SoccerPlayerController rp = _redOutfield[i];
                if (rp == null || rp == _aiPlayer) continue;
                FormationSlot slot = rp.AssignedSlot;
                float x = Mathf.Clamp(-slot.kickoffDepthX, 3.5f, ShotCalculator.PitchHalfLength - 6f);
                float z = Mathf.Clamp(-slot.kickoffLaneZ, -ShotCalculator.PitchHalfWidth + 4f, ShotCalculator.PitchHalfWidth - 4f);
                rp.transform.position = new Vector3(x, 0f, z);
                rp.transform.rotation = Quaternion.LookRotation(Vector3.left);
            }

            if (playerGetsBallFirst)
            {
                _humanPlayer.transform.position = new Vector3(-2.0f, 0f, 0f);
                _humanPlayer.AlignFirstPersonToDirection(Vector3.right, 14.5f);

                // FIFA 9.15m 센터서클 밖에서 상대 대기 -> 골 먹힌 팀(또는 킥오프 팀)이 100% 안전하게 공을 소유하고 출발!
                _aiPlayer.transform.position = new Vector3(11.8f, 0f, 0f);
                _aiPlayer.transform.rotation = Quaternion.LookRotation(Vector3.left);

                _ball.ResetAtPosition(new Vector3(-0.85f, SoccerBall.BallRadius, 0f));
                _ball.SetOwner(_humanPlayer);
            }
            else
            {
                _humanPlayer.transform.position = new Vector3(-11.8f, 0f, 0f);
                _humanPlayer.AlignFirstPersonToDirection(Vector3.right, 14.5f);

                _aiPlayer.transform.position = new Vector3(2.0f, 0f, 0f);
                _aiPlayer.transform.rotation = Quaternion.LookRotation(Vector3.left);

                _ball.ResetAtPosition(new Vector3(1.15f, SoccerBall.BallRadius, 0f));
                _ball.SetOwner(_aiPlayer);
            }
        }

        /// <summary>
        /// 공이 이미 골라인을 통과했거나 유효슈팅 궤적으로 골문을 향해 날아가고 있는지 판정 (어드밴티지 / 동시 골 인정용)
        /// </summary>
        private bool IsBallScoringOrEnteringGoal( out int scoringGoalSign )
        {
            scoringGoalSign = 0;
            if (_ball == null) return false;

            Vector3 p = _ball.transform.position;
            Vector3 v = _ball.Rb != null ? _ball.Rb.linearVelocity : Vector3.zero;

            bool withinGoalMouthYZ = Mathf.Abs(p.z) <= (ShotCalculator.GoalHalfWidth + 0.15f)
                                  && p.y <= (ShotCalculator.GoalHeight + 0.20f);

            // 1) 이미 골라인 근처 또는 골문 안쪽으로 진입한 경우
            if (withinGoalMouthYZ && Mathf.Abs(p.x) >= (ShotCalculator.PitchHalfLength - SoccerBall.BallRadius * 1.2f))
            {
                scoringGoalSign = p.x >= 0f ? 1 : -1;
                return true;
            }

            // 2) 슈팅된 공이 골문 안쪽을 향해 빠르게 날아가고 있어 곧 골라인을 통과하는 경우 (어드밴티지 룰)
            if (_ball.CurrentOwner == null && Mathf.Abs(v.x) > 4.5f)
            {
                int dirSign = v.x > 0f ? 1 : -1;
                float goalX = dirSign * ShotCalculator.PitchHalfLength;
                float distX = (goalX - p.x) * dirSign;
                if (distX >= -2.5f && distX <= 18.0f)
                {
                    float tToGoal = Mathf.Max(0f, distX / Mathf.Abs(v.x));
                    if (tToGoal <= 0.75f)
                    {
                        float predZ = p.z + v.z * tToGoal + 0.5f * _ball.ActiveCurveAcceleration.z * tToGoal * tToGoal;
                        float predY = p.y + v.y * tToGoal - 0.5f * 9.81f * tToGoal * tToGoal;
                        if (Mathf.Abs(predZ) <= (ShotCalculator.GoalHalfWidth + 0.25f) &&
                            predY >= -0.2f && predY <= (ShotCalculator.GoalHeight + 0.25f))
                        {
                            // 공격 팀이 찬 공이 상대 골문으로 들어가거나 자책골 궤적인 경우 반칙으로 끊지 않음
                            scoringGoalSign = dirSign;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void HandleFoulCommitted(FoulEventArgs foul)
        {
            if (_state != MatchState.Playing)
                return;

            // 골이 들어가는 동시에 반칙이 발생했거나 슈팅이 골문으로 빨려 들어가는 중이면 반칙으로 골을 취소하지 않고 득점(어드밴티지) 우선!
            if (IsBallScoringOrEnteringGoal(out int immediateGoalSign))
            {
                if (Mathf.Abs(_ball.transform.position.x) >= (ShotCalculator.PitchHalfLength - SoccerBall.BallRadius * 1.2f))
                {
                    HandleGoalTriggered(immediateGoalSign);
                }
                return;
            }

            _foulSetupCoroutine = StartCoroutine(FoulAndFreeKickSetupRoutine(foul));
        }

        private IEnumerator FoulAndFreeKickSetupRoutine(FoulEventArgs foul)
        {
            _state = MatchState.FoulSequence;
            SetAllOutfieldControls(false);
            _ui.UpdatePlayerWorldHud(_humanPlayer);

            string foulDesc = foul.isFromBehind
                ? "위험한 백태클 반칙!!"
                : (foul.isSlideTackle ? "거친 슬라이딩 태클 반칙!!" : "깊숙한 다리 걸기 반칙!!");
            string offenderName = foul.offender != null && foul.offender.Profile != null
                ? foul.offender.Profile.playerName
                : "수비수";

            bool awardedToBlue = foul.victim != null && foul.victim.attackingGoalSign > 0;
            int atkSign = awardedToBlue ? 1 : -1;
            _fkGoalCenter = new Vector3(atkSign * ShotCalculator.PitchHalfLength, 0f, 0f);

            // 1. 페널티 박스 내부 반칙 판정 (FIFA 규격 105m x 68m 구장 기준: 골라인 앞 16.5m 이내 & 좌우 폭 20.16m 이내)
            bool isPenaltyBoxFoul = (foul.foulSpot.x * atkSign) >= (ShotCalculator.PitchHalfLength - 16.5f)
                && Mathf.Abs(foul.foulSpot.z) <= 20.16f;

            // 2. 반칙 발생 실제 좌표 산출 (터치라인/골라인 안쪽으로만 최소 여백 보정)
            float actualFkX = Mathf.Clamp(foul.foulSpot.x, -ShotCalculator.PitchHalfLength + 2.2f, ShotCalculator.PitchHalfLength - 2.2f);
            float actualFkZ = Mathf.Clamp(foul.foulSpot.z, -ShotCalculator.PitchHalfWidth + 2.2f, ShotCalculator.PitchHalfWidth - 2.2f);
            float distToAttackingGoal = Vector2.Distance(new Vector2(actualFkX, actualFkZ), new Vector2(_fkGoalCenter.x, _fkGoalCenter.z));

            if (isPenaltyBoxFoul)
            {
                _fkSetPieceType = SetPieceType.PenaltyKick;
            }
            else if (distToAttackingGoal > 32.5f)
            {
                // 골문까지 거리가 먼 지점(하프라인, 자기 진영, 중원 등 32.5m 초과)은 실제 축구 룰처럼 수비벽 없이 키커 1명이 공을 잡아놓고 동료에게 패스로 전개!
                _fkSetPieceType = SetPieceType.DistantPassKick;
            }
            else
            {
                _fkSetPieceType = SetPieceType.DirectFreeKick;
            }

            string awardSide;
            if (_fkSetPieceType == SetPieceType.PenaltyKick)
            {
                awardSide = awardedToBlue
                    ? "🚨 페널티 박스 내 반칙! 우리 팀 페널티킥(PK) 선언!!"
                    : "🚨 페널티 박스 내 반칙! 상대 팀 페널티킥(PK) 선언!!";
            }
            else if (_fkSetPieceType == SetPieceType.DistantPassKick)
            {
                awardSide = awardedToBlue
                    ? "우리 팀 프리킥 선언! (수비벽 없음 — 패스 빌드업 전개)"
                    : "상대 팀 프리킥 선언! (장거리 빌드업)";
            }
            else
            {
                awardSide = awardedToBlue ? "우리 팀 직접 프리킥 찬스!" : "상대 팀 직접 프리킥 선언!";
            }

            _ui.ShowGoalCelebrationBanner($"🟨 삐익—! {foulDesc}", $"{offenderName} 경고!  ▶  {awardSide}");

            float foulWaitElapsed = 0f;
            while (foulWaitElapsed < 1.35f)
            {
                if (_state != MatchState.FoulSequence)
                    yield break;
                foulWaitElapsed += Time.deltaTime;
                yield return null;
            }

            if (_state != MatchState.FoulSequence)
                yield break;

            _ui.HideGoalCelebrationBanner();

            // 우리 팀이 얻은 세트피스는 현재 조작 중인 플레이어(1P)가 전담 키커로 직접 수행!
            _fkKicker = awardedToBlue ? _humanPlayer : _aiPlayer;
            _fkWallDefenders.Clear();

            // 수비벽(3-Man Wall)은 오직 골문 근처 직접 프리킥(DirectFreeKick)일 때만 세움! (페널티킥 & 장거리 프리킥은 수비벽 없음!)
            if (_fkSetPieceType == SetPieceType.DirectFreeKick)
            {
                if (awardedToBlue)
                {
                    for (int i = 0; i < _redDefenders.Count && _fkWallDefenders.Count < 3; i++)
                    {
                        if (_redDefenders[i] != null)
                            _fkWallDefenders.Add(_redDefenders[i]);
                    }
                }
                else
                {
                    // 상대 직접 프리킥 시 플레이어(1P)가 3인 수비벽 중앙에 서서 Space 점프 블로킹을 주도!
                    List<SoccerPlayerController> otherBlues = new List<SoccerPlayerController>(3);
                    for (int i = 0; i < _blueOutfield.Count; i++)
                    {
                        if (_blueOutfield[i] != null && _blueOutfield[i] != _humanPlayer)
                            otherBlues.Add(_blueOutfield[i]);
                    }

                    if (otherBlues.Count > 0) _fkWallDefenders.Add(otherBlues[0]);
                    _fkWallDefenders.Add(_humanPlayer);
                    if (otherBlues.Count > 1) _fkWallDefenders.Add(otherBlues[1]);
                }
            }

            _fkExecutingStrike = false;
            _fkIsCharging = false;
            _fkCharge01 = 0f;
            _fkCurveMode = false;
            _fkAiWaitTimer = _fkSetPieceType == SetPieceType.DistantPassKick ? 1.05f : 1.45f;

            // 세트피스 볼 위치 확정:
            // - 페널티킥(PK): FIFA 정규 11.0m 페널티 스팟 정중앙!
            // - 일반/장거리 프리킥: 반칙을 실제로 당한 바로 그 위치(actualFkX, actualFkZ)!
            float fkX;
            float fkZ;
            if (_fkSetPieceType == SetPieceType.PenaltyKick)
            {
                fkX = atkSign * (ShotCalculator.PitchHalfLength - 11.0f);
                fkZ = 0f;
            }
            else
            {
                fkX = actualFkX;
                fkZ = actualFkZ;
            }
            _fkBallPos = new Vector3(fkX, SoccerBall.BallRadius, fkZ);

            ResetAllOutfieldForKickoff();
            SetAllOutfieldControls(false);
            _playerKeeper.ResetPosition();
            _aiKeeper.ResetPosition();
            _ball.ResetAtPosition(_fkBallPos);

            Vector3 toGoal = (_fkGoalCenter - _fkBallPos);
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude < 0.01f) toGoal = Vector3.right * atkSign;
            toGoal.Normalize();
            Vector3 kickRight = Vector3.Cross(Vector3.up, toGoal).normalized;

            // 키커 스탠스 배치 (인간 키커는 공이 항상 정면 발 앞에 위치하도록 정후방 1.25m 정렬!)
            if (!_fkKicker.isAI)
            {
                _fkKicker.transform.position = new Vector3(_fkBallPos.x, 0f, _fkBallPos.z) - toGoal * 1.25f;
                _fkKicker.transform.rotation = Quaternion.LookRotation(toGoal);
            }
            else
            {
                float runupSide = (_fkKicker.Profile != null && _fkKicker.Profile.preferredFoot == PreferredFoot.Left) ? 0.45f : -0.45f;
                _fkKicker.transform.position = new Vector3(_fkBallPos.x, 0f, _fkBallPos.z) - toGoal * 1.85f + kickRight * runupSide;
                _fkKicker.transform.rotation = Quaternion.LookRotation(toGoal);
            }

            if (_fkSetPieceType == SetPieceType.PenaltyKick)
            {
                // [페널티킥(PK) 전술 배치]: 수비벽 없이 키커 vs 골키퍼 완벽 1대1 대결!
                // 나머지 모든 필드플레이어는 FIFA 규정에 따라 페널티 박스(16.5m) 및 페널티 아크(9.15m) 바깥 라인에 정렬!
                float outsideArcX = atkSign * (ShotCalculator.PitchHalfLength - 20.6f);
                float[] pkLateralSlots = { -16.5f, -13.0f, -9.5f, -6.2f, -3.0f, 3.0f, 6.2f, 9.5f, 13.0f, 16.5f };
                int slotIdx = 0;
                for (int i = 0; i < _allOutfield.Count; i++)
                {
                    SoccerPlayerController p = _allOutfield[i];
                    if (p == null || p == _fkKicker)
                        continue;

                    float zPos = pkLateralSlots[slotIdx % pkLateralSlots.Length];
                    float xPos = outsideArcX - atkSign * ((slotIdx / pkLateralSlots.Length) * 3.2f + (slotIdx % 2) * 0.85f);
                    p.transform.position = new Vector3(xPos, 0f, zPos);
                    p.transform.rotation = Quaternion.LookRotation(toGoal);
                    slotIdx++;
                }
            }
            else if (_fkSetPieceType == SetPieceType.DistantPassKick)
            {
                // [장거리 프리킥 전술 배치]: 수비벽 없음! 키커 한 명이 공을 잡아놓고 우리 팀 동료들에게 패스를 줄 수 있도록 오픈 패스 길 확보!
                int mateIdx = 0;
                int oppIdx = 0;
                for (int i = 0; i < _allOutfield.Count; i++)
                {
                    SoccerPlayerController p = _allOutfield[i];
                    if (p == null || p == _fkKicker)
                        continue;

                    if (p.attackingGoalSign == atkSign)
                    {
                        float xMate;
                        float zMate;
                        if (mateIdx == 0)
                        {
                            xMate = Mathf.Clamp(fkX + atkSign * 10.5f, -ShotCalculator.PitchHalfLength + 6f, ShotCalculator.PitchHalfLength - 11f);
                            zMate = Mathf.Clamp(Mathf.Lerp(fkZ, 0f, 0.45f), -ShotCalculator.PitchHalfWidth + 4f, ShotCalculator.PitchHalfWidth - 4f);
                        }
                        else if (mateIdx == 1)
                        {
                            xMate = Mathf.Clamp(fkX + atkSign * 7.8f, -ShotCalculator.PitchHalfLength + 6f, ShotCalculator.PitchHalfLength - 11f);
                            float leftZ = fkZ - 11.5f;
                            if (leftZ < -ShotCalculator.PitchHalfWidth + 4f) leftZ = fkZ + 17.5f;
                            zMate = Mathf.Clamp(leftZ, -ShotCalculator.PitchHalfWidth + 4f, ShotCalculator.PitchHalfWidth - 4f);
                        }
                        else if (mateIdx == 2)
                        {
                            xMate = Mathf.Clamp(fkX + atkSign * 8.5f, -ShotCalculator.PitchHalfLength + 6f, ShotCalculator.PitchHalfLength - 11f);
                            float rightZ = fkZ + 11.5f;
                            if (rightZ > ShotCalculator.PitchHalfWidth - 4f) rightZ = fkZ - 17.5f;
                            zMate = Mathf.Clamp(rightZ, -ShotCalculator.PitchHalfWidth + 4f, ShotCalculator.PitchHalfWidth - 4f);
                        }
                        else
                        {
                            FormationSlot slot = p.AssignedSlot;
                            xMate = Mathf.Clamp(fkX + atkSign * (slot.normDepthX * 26f + 6f), -ShotCalculator.PitchHalfLength + 7f, ShotCalculator.PitchHalfLength - 9f);
                            zMate = Mathf.Clamp(slot.normLaneZ * (ShotCalculator.PitchHalfWidth * 0.72f) * atkSign, -ShotCalculator.PitchHalfWidth + 4.5f, ShotCalculator.PitchHalfWidth - 4.5f);
                        }

                        p.transform.position = new Vector3(xMate, 0f, zMate);
                        Vector3 lookAtBall = _fkBallPos - p.transform.position;
                        lookAtBall.y = 0f;
                        p.transform.rotation = Quaternion.LookRotation(lookAtBall.sqrMagnitude > 0.01f ? lookAtBall.normalized : -toGoal);
                        mateIdx++;
                    }
                    else
                    {
                        // 상대 필드플레이어들: 수비벽을 세우지 않고 볼에서 최소 13.5m 이상 떨어진 수비 진영에 포진!
                        FormationSlot slot = p.AssignedSlot;
                        float xDef = Mathf.Clamp(fkX + atkSign * (14.5f + Mathf.Abs(slot.normDepthX) * 18f + (oppIdx % 3) * 2.2f), -ShotCalculator.PitchHalfLength + 7f, ShotCalculator.PitchHalfLength - 7f);
                        float zDef = Mathf.Clamp(slot.normLaneZ * (ShotCalculator.PitchHalfWidth * 0.70f) * (-atkSign), -ShotCalculator.PitchHalfWidth + 4.5f, ShotCalculator.PitchHalfWidth - 4.5f);
                        p.transform.position = new Vector3(xDef, 0f, zDef);
                        Vector3 lookAtBall = _fkBallPos - p.transform.position;
                        lookAtBall.y = 0f;
                        p.transform.rotation = Quaternion.LookRotation(lookAtBall.sqrMagnitude > 0.01f ? lookAtBall.normalized : -toGoal);
                        oppIdx++;
                    }
                }
            }
            else
            {
                // [근거리 직접 프리킥(DirectFreeKick) 전술 배치]: FIFA 정규 9.15m 거리에 3인 수비벽(3-Man Wall) 배치!
                Vector3 wallCenter = new Vector3(_fkBallPos.x, 0f, _fkBallPos.z) + toGoal * 9.15f;
                for (int i = 0; i < _fkWallDefenders.Count; i++)
                {
                    float lateralOffset = (i - 1) * 0.82f;
                    SoccerPlayerController wd = _fkWallDefenders[i];
                    wd.transform.position = wallCenter + kickRight * lateralOffset;
                    wd.transform.rotation = Quaternion.LookRotation(-toGoal);
                }

                // 수비벽/키커가 아닌 동료 및 상대 선수 전술 배치:
                float openSide = fkZ >= 0f ? -1f : 1f;
                int kickerMateIdx = 0;
                int otherSupportIdx = 0;
                for (int i = 0; i < _allOutfield.Count; i++)
                {
                    SoccerPlayerController p = _allOutfield[i];
                    if (p == _fkKicker || _fkWallDefenders.Contains(p))
                        continue;

                    if (p.attackingGoalSign == atkSign)
                    {
                        if (kickerMateIdx == 0)
                        {
                            float xShort = Mathf.Clamp(fkX + atkSign * 4.2f, -ShotCalculator.PitchHalfLength + 8f, ShotCalculator.PitchHalfLength - 11f);
                            float zShort = Mathf.Clamp(fkZ + openSide * 9.6f, -25f, 25f);
                            p.transform.position = new Vector3(xShort, 0f, zShort);
                            Vector3 lookBack = (_fkBallPos - p.transform.position);
                            lookBack.y = 0f;
                            p.transform.rotation = Quaternion.LookRotation(lookBack.sqrMagnitude > 0.01f ? lookBack.normalized : toGoal);
                        }
                        else if (kickerMateIdx <= 3)
                        {
                            float boxEdgeX = _fkGoalCenter.x - atkSign * (13.2f + (kickerMateIdx - 1) * 1.6f);
                            float boxZ = (kickerMateIdx - 2) * 6.2f;
                            p.transform.position = new Vector3(boxEdgeX, 0f, boxZ);
                            p.transform.rotation = Quaternion.LookRotation((_fkGoalCenter - p.transform.position).normalized);
                        }
                        else
                        {
                            FormationSlot slot = p.AssignedSlot;
                            float xSup = Mathf.Clamp(slot.normDepthX * (ShotCalculator.PitchHalfLength * 0.65f) * atkSign, -ShotCalculator.PitchHalfLength + 8f, ShotCalculator.PitchHalfLength - 16f);
                            float zSup = Mathf.Clamp(slot.normLaneZ * (ShotCalculator.PitchHalfWidth * 0.72f) * atkSign, -26f, 26f);
                            p.transform.position = new Vector3(xSup, 0f, zSup);
                            p.transform.rotation = Quaternion.LookRotation(toGoal);
                        }
                        kickerMateIdx++;
                    }
                    else
                    {
                        float sideSign = (otherSupportIdx % 2 == 0) ? -openSide : openSide;
                        float zSpot = Mathf.Clamp(fkZ * 0.3f + sideSign * (7.5f + (otherSupportIdx % 4) * 4.2f), -26f, 26f);
                        float xSpot = Mathf.Clamp(fkX + atkSign * (8.5f + (otherSupportIdx / 2) * 2.2f), -ShotCalculator.PitchHalfLength + 6f, ShotCalculator.PitchHalfLength - 6f);
                        p.transform.position = new Vector3(xSpot, 0f, zSpot);
                        p.transform.rotation = Quaternion.LookRotation(-toGoal);
                        otherSupportIdx++;
                    }
                }
            }

            _fkAimPoint = new Vector3(_fkGoalCenter.x, 1.55f, Mathf.Clamp(-fkZ * 0.16f, -2.3f, 2.3f));

            if (!_fkKicker.isAI)
            {
                float initialPitch = _fkSetPieceType == SetPieceType.DistantPassKick ? 12.5f : 16.5f;
                _humanPlayer.AlignFirstPersonToDirection(toGoal, initialPitch);
                if (_dynamicCam != null)
                    _dynamicCam.SetFreeKickCamera(true, _fkBallPos, Vector3.Lerp(_fkGoalCenter + Vector3.up * 1.25f, _fkAimPoint, 0.45f), humanIsKicker: true);
            }
            else
            {
                Vector3 humanLookDir = (_fkBallPos - _humanPlayer.transform.position);
                humanLookDir.y = 0f;
                if (humanLookDir.sqrMagnitude < 0.01f) humanLookDir = -toGoal;
                _humanPlayer.AlignFirstPersonToDirection(humanLookDir.normalized, 4.5f);
                if (_dynamicCam != null)
                    _dynamicCam.SetFreeKickCamera(true, _fkBallPos, _fkBallPos + Vector3.up * 0.90f, humanIsKicker: false);
            }

            _ui.UpdateTacticalMinimap(_humanPlayer, _blueOutfield, _aiPlayer, _redDefenders, _playerKeeper, _aiKeeper, _ball);
            _state = MatchState.FreeKick;
        }

        private void TriggerWallGroupJump()
        {
            for (int i = 0; i < _fkWallDefenders.Count; i++)
            {
                if (_fkWallDefenders[i] != null)
                    _fkWallDefenders[i].TriggerDefensiveWallJump();
            }
        }

        private void UpdateFreeKickMode(float dt)
        {
            if (_fkExecutingStrike || _fkKicker == null)
                return;

            // 프리킥 준비 중에는 공이 절대로 굴러가거나 선수 뒤로 빠지지 않도록 세트피스 스팟(_fkBallPos)에 고정!
            if (_ball != null)
            {
                _ball.transform.position = _fkBallPos;
                if (_ball.Rb != null)
                {
                    _ball.Rb.linearVelocity = Vector3.zero;
                    _ball.Rb.angularVelocity = Vector3.zero;
                }
            }

            _ui.UpdateTacticalMinimap(_humanPlayer, _blueOutfield, _aiPlayer, _redDefenders, _playerKeeper, _aiKeeper, _ball);

            if (!_fkKicker.isAI)
            {
                // 1. 1인칭 키커 360도 마우스 시선 회전 (동료를 바라보면 좌클릭 패스 가능! 슛 드래그 중에는 시점 고정해 커브 제스처 집중!)
                _fkKicker.UpdateFreeKickHumanLookAndAim(dt, lockLookForCurveDrag: _fkIsCharging);

                // 시야를 어느 방향으로 돌리든 공(_fkBallPos)을 중심으로 키커가 스탠스를 회전하여 공이 항상 선수 정면(1.25m 앞)에 있도록 보장!
                Vector3 flatLook = Quaternion.Euler(0f, _fkKicker.FirstPersonYaw, 0f) * Vector3.forward;
                flatLook.y = 0f;
                if (flatLook.sqrMagnitude < 0.001f)
                    flatLook = Vector3.right * _fkKicker.attackingGoalSign;
                flatLook.Normalize();
                _fkKicker.transform.position = new Vector3(_fkBallPos.x, 0f, _fkBallPos.z) - flatLook * 1.25f;

                bool isPenalty = _fkSetPieceType == SetPieceType.PenaltyKick;
                bool isDistantPass = _fkSetPieceType == SetPieceType.DistantPassKick;

                if (isDistantPass)
                {
                    // [장거리 프리킥 빌드업 모드]: 수비벽 없이 키커 1명이 공을 잡아놓고 마우스로 동료 방향을 바라보며 패스로 전개!
                    SoccerPlayerController passMate = (_fkKicker.CurrentAimTargetType == AimTargetType.Teammate && _fkKicker.CurrentAimTargetPlayer != null)
                        ? _fkKicker.CurrentAimTargetPlayer
                        : _fkKicker.FindBestPassTarget(_fkKicker.GetFirstPersonAimDirection(), preferForward: true);

                    if (_dynamicCam != null)
                    {
                        Vector3 lookPt = passMate != null
                            ? passMate.transform.position + Vector3.up * 1.15f
                            : _fkBallPos + _fkKicker.GetFirstPersonAimDirection() * 14f;
                        _dynamicCam.SetFreeKickLookPoint(lookPt);
                    }

                    // [좌클릭] 또는 [우클릭]: 바라보는 동료에게 즉시 프리킥 패스 연결! ([Q + 우클릭] 시 전방 롱크로스 지원)
                    if (SoccerPlayerController.ReadShootPressedThisFrame())
                    {
                        StartCoroutine(ExecuteFreeKickPassSequence(passMate));
                        return;
                    }

                    if (SoccerPlayerController.ReadCrossPressedThisFrame())
                    {
                        bool holdQ = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.qKey.isPressed;
                        if (holdQ)
                        {
                            StartCoroutine(ExecuteFreeKickCrossSequence(passMate));
                        }
                        else
                        {
                            StartCoroutine(ExecuteFreeKickPassSequence(passMate));
                        }
                        return;
                    }

                    _ui.UpdatePlayerWorldHud(_humanPlayer);
                    _ui.UpdateFreeKickHud(
                        true,
                        true,
                        _fkAimPoint,
                        0f,
                        false,
                        _fkKicker,
                        isPenaltyKick: false,
                        isDistantPassKick: true,
                        suggestedPassMate: passMate);
                    return;
                }

                if (!_fkIsCharging)
                {
                    // 시선이 골문 쪽을 향할 때 1인칭 조준선이 가리키는 골문 지점으로 프리킥/페널티킥 타겟 자동 동기화
                    Vector3 eye = _fkKicker.GetFirstPersonEyePosition();
                    Vector3 aimDir = _fkKicker.GetFirstPersonAimDirection();
                    if (aimDir.x * _fkKicker.attackingGoalSign > 0.15f)
                    {
                        float tPlane = (_fkGoalCenter.x - eye.x) / aimDir.x;
                        if (tPlane > 1f && tPlane < 95f)
                        {
                            Vector3 hitGoal = eye + aimDir * tPlane;
                            if (Mathf.Abs(hitGoal.z) <= ShotCalculator.GoalHalfWidth + 3.2f)
                            {
                                _fkAimPoint.z = Mathf.Lerp(_fkAimPoint.z, Mathf.Clamp(hitGoal.z, -ShotCalculator.GoalHalfWidth + 0.20f, ShotCalculator.GoalHalfWidth - 0.20f), 1f - Mathf.Exp(-18f * dt));
                                _fkAimPoint.y = Mathf.Lerp(_fkAimPoint.y, Mathf.Clamp(hitGoal.y, 0.32f, ShotCalculator.GoalHeight - 0.14f), 1f - Mathf.Exp(-18f * dt));
                            }
                        }
                    }
                }

                // WASD로도 골문 타겟 미세 조준 지원
                Vector2 aimMove = SoccerPlayerController.ReadMoveInput();
                Camera cam = Camera.main;
                Vector3 camRight = cam != null ? cam.transform.right : new Vector3(0f, 0f, -1f);
                camRight.y = 0f;
                if (camRight.sqrMagnitude < 0.001f) camRight = new Vector3(0f, 0f, -1f);
                camRight.Normalize();

                _fkAimPoint += camRight * (aimMove.x * 4.8f * dt);
                _fkAimPoint.y += aimMove.y * 2.6f * dt;
                _fkAimPoint.x = _fkGoalCenter.x;
                _fkAimPoint.z = Mathf.Clamp(_fkAimPoint.z, -ShotCalculator.GoalHalfWidth + 0.20f, ShotCalculator.GoalHalfWidth - 0.20f);
                _fkAimPoint.y = Mathf.Clamp(_fkAimPoint.y, 0.32f, ShotCalculator.GoalHeight - 0.14f);

                if (_dynamicCam != null)
                    _dynamicCam.SetFreeKickLookPoint(Vector3.Lerp(_fkGoalCenter + Vector3.up * 1.20f, _fkAimPoint, 0.45f));

                bool holdZ = SoccerPlayerController.ReadHoldZ();

                // 직접 프리킥일 때 마우스 우클릭: 기본 [우클릭]은 동료에게 땅볼 패스! ([Q + 우클릭] 조합 시 페널티 박스 크로스!)
                if (!isPenalty && SoccerPlayerController.ReadPassPressedThisFrame() && !_fkIsCharging)
                {
                    SoccerPlayerController aimedMate = _fkKicker.CurrentAimTargetType == AimTargetType.Teammate
                        ? _fkKicker.CurrentAimTargetPlayer
                        : null;
                    bool holdQ = UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.qKey.isPressed;
                    if (holdQ)
                    {
                        StartCoroutine(ExecuteFreeKickCrossSequence(aimedMate));
                    }
                    else
                    {
                        StartCoroutine(ExecuteFreeKickPassSequence(aimedMate));
                    }
                    return;
                }

                // 마우스 좌클릭: (직접 프리킥 시 동료 조준이면 패스), 그 외에는 직접 슈팅 & 포켓몬고 드래그 감아차기 차징 시작!
                if (SoccerPlayerController.ReadShootPressedThisFrame() && !_fkIsCharging)
                {
                    if (!isPenalty && _fkKicker.CurrentAimTargetType == AimTargetType.Teammate && _fkKicker.CurrentAimTargetPlayer != null)
                    {
                        StartCoroutine(ExecuteFreeKickPassSequence(_fkKicker.CurrentAimTargetPlayer));
                        return;
                    }

                    _fkIsCharging = true;
                    _fkCharge01 = 0.14f;
                    _fkCurveMode = holdZ;
                    _fkKicker.BeginExternalCurveDragTracking();
                }

                if (_fkIsCharging)
                {
                    _fkKicker.UpdateExternalCurveDragTracking(dt);
                    float liveBias = _fkKicker.DragCurveBias;
                    _fkCurveMode = holdZ || Mathf.Abs(liveBias) >= 0.70f;

                    if (SoccerPlayerController.ReadShootHeld())
                    {
                        _fkCharge01 = Mathf.Clamp01(_fkCharge01 + dt / 0.68f);
                        if (_fkCharge01 >= 1.0f)
                        {
                            float finalBias = _fkKicker.EndExternalCurveDragTracking();
                            bool useCurve = holdZ || Mathf.Abs(finalBias) >= 0.70f;
                            StartCoroutine(ExecuteFreeKickStrikeSequence(_fkAimPoint, useCurve, _fkCharge01, finalBias));
                            return;
                        }
                    }

                    if (SoccerPlayerController.ReadShootReleasedThisFrame())
                    {
                        float finalBias = _fkKicker.EndExternalCurveDragTracking();
                        bool useCurve = holdZ || Mathf.Abs(finalBias) >= 0.70f;
                        float finalCharge = Mathf.Abs(finalBias) >= 0.70f
                            ? Mathf.Max(0.68f, _fkCharge01)
                            : Mathf.Max(0.35f, _fkCharge01);
                        StartCoroutine(ExecuteFreeKickStrikeSequence(_fkAimPoint, useCurve, finalCharge, finalBias));
                        return;
                    }
                }

                _ui.UpdatePlayerWorldHud(_humanPlayer);
                _ui.UpdateFreeKickHud(
                    true,
                    true,
                    _fkAimPoint,
                    _fkIsCharging ? _fkCharge01 : 0f,
                    _fkIsCharging ? _fkCurveMode : holdZ,
                    _fkKicker,
                    isPenaltyKick: isPenalty,
                    isDistantPassKick: false,
                    suggestedPassMate: null);
            }
            else
            {
                bool isPenalty = _fkSetPieceType == SetPieceType.PenaltyKick;
                bool isDistantPass = _fkSetPieceType == SetPieceType.DistantPassKick;

                _ui.UpdatePlayerWorldHud(_humanPlayer);
                _ui.UpdateFreeKickHud(true, false, Vector3.zero, 0f, false, null, isPenaltyKick: isPenalty, isDistantPassKick: isDistantPass);

                if (_fkWallDefenders.Count > 0 && SoccerPlayerController.ReadJumpOrDashSpacePressedThisFrame())
                {
                    TriggerWallGroupJump();
                }

                _fkAiWaitTimer -= dt;
                if (_fkAiWaitTimer <= 0f)
                {
                    if (isDistantPass)
                    {
                        // AI 장거리 프리킥: 수비벽 없이 동료에게 빌드업 패스로 전개!
                        StartCoroutine(ExecuteFreeKickPassSequence(null));
                    }
                    else
                    {
                        float targetZ = (Random.value < 0.5f ? -1f : 1f) * Random.Range(2.10f, 3.25f);
                        float targetY = Random.Range(1.40f, 2.15f);
                        bool aiCurve = _fkKicker.Profile != null && _fkKicker.Profile.curve >= 76 && Random.value < 0.68f;
                        float aiCharge = Random.Range(0.58f, 0.85f);
                        StartCoroutine(ExecuteFreeKickStrikeSequence(new Vector3(_fkGoalCenter.x, targetY, targetZ), aiCurve, aiCharge, 0f));
                    }
                }
            }
        }

        private IEnumerator ExecuteFreeKickStrikeSequence(Vector3 aimPoint, bool isCurve, float charge01, float customCurveBias = 0f)
        {
            _fkExecutingStrike = true;
            _fkIsCharging = false;
            _ui.UpdateFreeKickHud(false, false, Vector3.zero, 0f, false);

            Vector3 toGoal = (_fkGoalCenter - _fkBallPos);
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude < 0.01f) toGoal = Vector3.right * _fkKicker.attackingGoalSign;
            toGoal.Normalize();

            Vector3 startKickerPos = _fkKicker.transform.position;
            Vector3 runDir = new Vector3(_fkBallPos.x - startKickerPos.x, 0f, _fkBallPos.z - startKickerPos.z);
            if (runDir.sqrMagnitude < 0.001f) runDir = toGoal;
            runDir.Normalize();
            // 도움닫기 시 공보다 앞으로 넘어가지 않도록 공 뒤쪽 0.90m 지점에서 정확히 타격!
            Vector3 strikeKickerPos = new Vector3(_fkBallPos.x, 0f, _fkBallPos.z) - runDir * 0.90f;

            float runDuration = 0.20f;
            float elapsed = 0f;
            while (elapsed < runDuration)
            {
                float stepDt = Time.deltaTime;
                elapsed += stepDt;
                float t = Mathf.Clamp01(elapsed / runDuration);
                if (_ball != null)
                    _ball.transform.position = _fkBallPos;
                _fkKicker.transform.position = Vector3.Lerp(startKickerPos, strikeKickerPos, t);
                if (_fkKicker.visualRig != null)
                    _fkKicker.visualRig.UpdateProceduralAnimation(runDir * 6.5f, false, stepDt);

                if (_fkKicker.isAI && SoccerPlayerController.ReadJumpOrDashSpacePressedThisFrame())
                {
                    TriggerWallGroupJump();
                }

                yield return null;
            }

            if (_ball != null)
                _ball.transform.position = _fkBallPos;

            for (int i = 0; i < _fkWallDefenders.Count; i++)
            {
                SoccerPlayerController wd = _fkWallDefenders[i];
                if (wd == null) continue;
                if (!_fkKicker.isAI && Random.value < 0.72f)
                    wd.TriggerDefensiveWallJump();
                wd.ActivateWallBlockCheck(0.65f);
            }

            _state = MatchState.Playing;
            _fkExecutingStrike = false;

            _fkKicker.ExecuteFreeKickStrike(aimPoint, isCurve, charge01, customCurveBias);

            yield return new WaitForSeconds(0.28f);

            if (_state == MatchState.Playing)
            {
                if (_dynamicCam != null)
                    _dynamicCam.SetFreeKickCamera(false);
                SetAllOutfieldControls(true);
            }
        }

        private IEnumerator ExecuteFreeKickPassSequence(SoccerPlayerController explicitReceiver = null)
        {
            _fkExecutingStrike = true;
            _fkIsCharging = false;
            _ui.UpdateFreeKickHud(false, false, Vector3.zero, 0f, false);

            SoccerPlayerController kicker = _fkKicker;
            Vector3 searchDir = kicker.isAI
                ? (Vector3.right * kicker.attackingGoalSign)
                : kicker.GetFirstPersonAimDirection();
            SoccerPlayerController receiver = explicitReceiver != null
                ? explicitReceiver
                : kicker.FindBestPassTarget(searchDir, preferForward: true);

            if (receiver == null)
            {
                for (int i = 0; i < _allOutfield.Count; i++)
                {
                    SoccerPlayerController candidate = _allOutfield[i];
                    if (candidate != null && candidate != kicker && candidate.attackingGoalSign == kicker.attackingGoalSign)
                    {
                        receiver = candidate;
                        break;
                    }
                }
            }

            Vector3 startPos = kicker.transform.position;
            Vector3 runDir = new Vector3(_fkBallPos.x - startPos.x, 0f, _fkBallPos.z - startPos.z);
            if (runDir.sqrMagnitude < 0.001f) runDir = Vector3.right * kicker.attackingGoalSign;
            runDir.Normalize();
            Vector3 strikePos = new Vector3(_fkBallPos.x, 0f, _fkBallPos.z) - runDir * 0.88f;

            float runDuration = 0.14f;
            float elapsed = 0f;
            while (elapsed < runDuration)
            {
                elapsed += Time.deltaTime;
                if (_ball != null)
                    _ball.transform.position = _fkBallPos;
                kicker.transform.position = Vector3.Lerp(startPos, strikePos, Mathf.Clamp01(elapsed / runDuration));
                yield return null;
            }

            if (_ball != null)
                _ball.transform.position = _fkBallPos;

            _state = MatchState.Playing;
            _fkExecutingStrike = false;

            if (_dynamicCam != null)
                _dynamicCam.SetFreeKickCamera(false);

            // 먼저 공을 정면에서 정확히 패스 발사 (공이 받는 선수의 발에 닿는 순간 자동으로 조작 전환!)
            if (receiver != null)
            {
                string rName = receiver.Profile != null ? receiver.Profile.playerName : "동료 선수";
                kicker.ExecuteGroundPass(receiver, isThroughPass: false);
                if (!kicker.isAI)
                {
                    _ui.ShowShotToast($"🎯 프리킥 세트피스 패스! {rName} 선수에게 연결 (볼이 발에 닿는 순간 자동 전환!)", false);
                }
            }

            SetAllOutfieldControls(true);
        }

        private IEnumerator ExecuteFreeKickCrossSequence(SoccerPlayerController explicitRunner = null)
        {
            _fkExecutingStrike = true;
            _fkIsCharging = false;
            _ui.UpdateFreeKickHud(false, false, Vector3.zero, 0f, false);

            SoccerPlayerController kicker = _fkKicker;

            // 페널티 박스로 가장 빠르게 침투할 아군 러너 선정 (조준한 동료 우선, 없으면 박스 앞 대기 중인 동료 선정!)
            SoccerPlayerController runner = explicitRunner;
            if (runner == null || runner == kicker)
            {
                float bestDist = float.MaxValue;
                Vector2 boxSpotXZ = new Vector2(_fkGoalCenter.x - kicker.attackingGoalSign * 10.0f, 0f);
                for (int i = 0; i < _blueOutfield.Count; i++)
                {
                    SoccerPlayerController bp = _blueOutfield[i];
                    if (bp == null || bp == kicker) continue;
                    float d = Vector2.Distance(new Vector2(bp.transform.position.x, bp.transform.position.z), boxSpotXZ);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        runner = bp;
                    }
                }
            }

            Vector3 startPos = kicker.transform.position;
            Vector3 toBall = new Vector3(_fkBallPos.x - startPos.x, 0f, _fkBallPos.z - startPos.z);
            if (toBall.sqrMagnitude < 0.01f) toBall = Vector3.right * kicker.attackingGoalSign;
            toBall.Normalize();
            Vector3 strikePos = new Vector3(_fkBallPos.x, 0f, _fkBallPos.z) - toBall * 0.88f;

            float runDuration = 0.16f;
            float elapsed = 0f;
            while (elapsed < runDuration)
            {
                float stepDt = Time.deltaTime;
                elapsed += stepDt;
                if (_ball != null)
                    _ball.transform.position = _fkBallPos;
                kicker.transform.position = Vector3.Lerp(startPos, strikePos, Mathf.Clamp01(elapsed / runDuration));
                if (kicker.visualRig != null)
                    kicker.visualRig.UpdateProceduralAnimation(toBall * 6.0f, false, stepDt);
                yield return null;
            }

            if (_ball != null)
                _ball.transform.position = _fkBallPos;

            // 즉시 인플레이로 전환하고 물리 기반 크로스 발사 + AI 동료 박스 침투 & 다이빙 파워 헤딩 연결!
            _state = MatchState.Playing;
            _fkExecutingStrike = false;

            if (_dynamicCam != null)
                _dynamicCam.SetFreeKickCamera(false);

            kicker.ExecuteOpenPlayCross(runner, forceBoxCross: true);
            SetAllOutfieldControls(true);

            if (runner != null)
            {
                string rName = runner.Profile != null ? runner.Profile.playerName : "동료 선수";
                _ui.ShowShotToast($"🚀 프리킥 크로스! {rName} 박스 침투 헤딩 찬스!! ([C] 누르면 직접 공중 슈팅 전환!)", false);
            }
        }

        private void HandleGoalTriggered(int goalSideSign)
        {
            // 인플레이(Playing) 중이거나, 슛 직후 동시에 반칙 판정(FoulSequence)이 났을 때도 골이 들어가면 반칙을 취소하고 골을 인정!
            if (_state != MatchState.Playing && _state != MatchState.FoulSequence)
                return;

            if (_foulSetupCoroutine != null)
            {
                StopCoroutine(_foulSetupCoroutine);
                _foulSetupCoroutine = null;
            }

            StartCoroutine(GoalScoredSequence(goalSideSign));
        }

        private IEnumerator GoalScoredSequence(int goalSideSign)
        {
            _state = MatchState.GoalCelebration;
            SetAllOutfieldControls(false);
            _ui.UpdateFreeKickHud(false, false, Vector3.zero, 0f, false);

            bool playerScored = goalSideSign > 0; // +1은 오른쪽 레드 골대 -> 블루(플레이어) 팀 득점!
            if (playerScored)
                _playerScore++;
            else
                _aiScore++;

            _lastConcededIsPlayer = !playerScored;
            _ui.UpdateScoreAndTimer(_playerScore, _aiScore, _remainingTime);

            ShotMetadata lastShot = _ball.LastShot;
            SoccerPlayerController scorer = (lastShot.shooter != null && (lastShot.shooter.attackingGoalSign > 0) == playerScored)
                ? lastShot.shooter
                : (playerScored ? _humanPlayer : _aiPlayer);

            bool wasAcrobatic = lastShot.shotType == ShotType.OverheadKick || lastShot.shotType == ShotType.AcrobaticVolley;

            Time.timeScale = 0.35f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            if (_dynamicCam != null)
            {
                _dynamicCam.TriggerImpactShake(0.68f, 0.42f);
                _dynamicCam.TriggerGoalCelebrationFocus(scorer.transform, 3.1f);
            }

            List<SoccerPlayerController> celebratingTeam = playerScored ? _blueOutfield : _redOutfield;
            for (int i = 0; i < celebratingTeam.Count; i++)
            {
                celebratingTeam[i].PlayGoalCelebration();
            }

            Vector3 goalCenter = new Vector3(goalSideSign * ShotCalculator.PitchHalfLength, 0f, 0f);
            Color teamColor = scorer.Profile != null ? scorer.Profile.jerseyPrimaryColor : Color.yellow;
            if (GoalCelebrationManager.Instance != null)
            {
                GoalCelebrationManager.Instance.PlayGoalSpectacle(goalCenter, teamColor, wasAcrobatic);
            }

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
            string scorerName = scorer.Profile != null ? scorer.Profile.playerName : (playerScored ? "우리 팀" : "상대 팀");
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

            // 점수상 지고 있는 사람이 아니라, 방금 골을 먹힌 팀(_lastConcededIsPlayer)에게 100% 킥오프 볼 소유권 지급!
            bool concededTeamIsPlayer = DetermineKickoffPlayerGetsBall();
            SetupKickoffPositions(playerGetsBallFirst: concededTeamIsPlayer);
            _state = MatchState.Playing;
            SetAllOutfieldControls(true);

            if (concededTeamIsPlayer)
                _ui.ShowShotToast("⚽ 실점한 우리 팀에게 킥오프 볼 소유권이 지급되었습니다!", false);
            else
                _ui.ShowShotToast("⚽ 실점한 상대 팀에게 킥오프 볼 소유권이 넘어갔습니다!", false);
        }

        private void FinishMatch()
        {
            _state = MatchState.MatchEnded;
            SetCursorLocked(false);
            SetAllOutfieldControls(false);
            if (_dynamicCam != null)
                _dynamicCam.SetFreeKickCamera(false);
            _ui.UpdateFreeKickHud(false, false, Vector3.zero, 0f, false);
            Time.timeScale = 1f;

            string pName = _humanPlayer.Profile != null ? _humanPlayer.Profile.playerName : "BLUE TEAM";
            string aName = _aiPlayer.Profile != null ? _aiPlayer.Profile.playerName : "RED TEAM";
            _ui.ShowMatchEndScreen(_playerScore, _aiScore, pName, aName);
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;

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

            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(false);
            }
            else if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (!overUi && (_state == MatchState.Playing || _state == MatchState.FreeKick))
                {
                    SetCursorLocked(true);
                }
            }

            if (_state == MatchState.FreeKick)
            {
                if (kb != null && kb.tKey.wasPressedThisFrame)
                {
                    BeginNewMatchWithRandomPlayers();
                    return;
                }
                UpdateFreeKickMode(Time.deltaTime);
                return;
            }

            if (_state == MatchState.Playing)
            {
                HandlePlayerSwitchingInput(kb);

                _remainingTime -= Time.deltaTime;
                _ui.UpdateScoreAndTimer(_playerScore, _aiScore, _remainingTime);
                _ui.UpdatePlayerWorldHud(_humanPlayer);
                _ui.UpdateTacticalMinimap(_humanPlayer, _blueOutfield, _aiPlayer, _redDefenders, _playerKeeper, _aiKeeper, _ball);

                if (kb != null)
                {
                    if (kb.tabKey.wasPressedThisFrame)
                    {
                        _state = MatchState.PausedPeekStats;
                        SetCursorLocked(false);
                        SetAllOutfieldControls(false);
                        Time.timeScale = 0f;
                        if (_dynamicCam != null)
                            _dynamicCam.SetPreMatchPreview(true);
                        _ui.UpdateControlledPlayerHudInfo(_humanPlayer);
                        _ui.ShowPreMatchStats(isMidGamePeek: true);
                        return;
                    }

                    if (kb.tKey.wasPressedThisFrame)
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
