using System;
using System.Collections;
using Soccer3D.CameraSystem;
using Soccer3D.Data;
using Soccer3D.Effects;
using Soccer3D.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Soccer3D.Gameplay
{
    public struct FoulEventArgs
    {
        public SoccerPlayerController offender;
        public SoccerPlayerController victim;
        public Vector3 foulSpot;
        public bool isSlideTackle;
        public bool isFromBehind;
    }

    public class SoccerPlayerController : MonoBehaviour
    {
        public bool isAI;
        public int attackingGoalSign = 1; // 플레이어 = +1 (+X 골문 공격), AI = -1 (-X 골문 공격)
        public PlayerProfile Profile { get; private set; }

        public SoccerBall ball;
        public SoccerPlayerController opponent;
        public GoalkeeperAI targetGoalkeeper;
        public PlayerVisualRig visualRig;

        // UI 표시용 상태 프로퍼티
        public bool IsChargingShot => _isChargingShot;
        public float CurrentShotCharge01 => _shotCharge01;
        public bool IsFinesseCharge => _chargingFinesse;
        public bool CanDoAcrobaticNow { get; private set; }
        public ShotType AvailableAcrobaticType { get; private set; }
        public bool IsPerformingAcrobatic => _isInAcrobaticRoutine;
        public bool IsJumpingInWall => _wallJumpTimer > 0.08f;

        public event Action<string, bool> OnShotFeedback; // (메시지, 약발여부)
        public event Action<FoulEventArgs> OnFoulCommitted;

        private Vector3 _planarVelocity;
        private bool _isChargingShot;
        private float _shotCharge01;
        private bool _chargingFinesse;
        private bool _isInAcrobaticRoutine;
        private float _tackleCooldown;
        private float _stunTimer;
        private float _flickCooldown;
        private float _wallJumpTimer;
        private float _wallBlockWindowTimer;
        private bool _controlsEnabled;

        // AI 내부 타이머
        private float _aiDecisionTimer;
        private float _aiStrafeOffsetZ;

        public void SetupPlayer(PlayerProfile profile, bool aiControlled, int attackSign, SoccerBall matchBall)
        {
            Profile = profile;
            isAI = aiControlled;
            attackingGoalSign = attackSign;
            ball = matchBall;

            visualRig = GetComponent<PlayerVisualRig>();
            if (visualRig == null)
                visualRig = gameObject.AddComponent<PlayerVisualRig>();

            visualRig.BuildFromProfile(profile, isGoalkeeper: false);
            ResetStateForKickoff();
        }

        public void SetControlsEnabled(bool enabled)
        {
            _controlsEnabled = enabled;
            if (!enabled)
            {
                _isChargingShot = false;
                _shotCharge01 = 0f;
                _planarVelocity = Vector3.zero;
            }
        }

        public void ResetStateForKickoff()
        {
            StopAllCoroutines();
            if (DynamicSoccerCamera.Instance != null)
                DynamicSoccerCamera.Instance.EndAcrobaticWindupZoom();
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;

            _planarVelocity = Vector3.zero;
            _isChargingShot = false;
            _shotCharge01 = 0f;
            _isInAcrobaticRoutine = false;
            _tackleCooldown = 0f;
            _stunTimer = 0f;
            _flickCooldown = 0f;
            _wallJumpTimer = 0f;
            _wallBlockWindowTimer = 0f;

            if (visualRig != null)
            {
                visualRig.ResetToIdle();
                visualRig.SetChargingFootAura(false, PreferredFoot.Right);
            }
        }

        public void PlayGoalCelebration()
        {
            _planarVelocity = Vector3.zero;
            if (visualRig != null)
                visualRig.TriggerCelebration(3.0f);
        }

        public void PlayFoulFall(Vector3 pushVel)
        {
            _isChargingShot = false;
            _shotCharge01 = 0f;
            _planarVelocity = new Vector3(pushVel.x, 0f, pushVel.z) * 0.45f;
            _stunTimer = 1.35f;
            if (visualRig != null)
                visualRig.TriggerFoulFall(1.30f);
        }

        public void TriggerDefensiveWallJump()
        {
            if (_wallJumpTimer > 0.1f) return;
            _wallJumpTimer = 0.62f;
            if (visualRig != null)
                visualRig.TriggerWallJump(0.62f);
        }

        public void ActivateWallBlockCheck(float duration = 0.58f)
        {
            _wallBlockWindowTimer = duration;
        }

        public void ExecuteFreeKickStrike(Vector3 aimedGoalPoint, bool isCurveShot, float charge01)
        {
            if (ball == null) return;

            Vector3 goalCenter = new Vector3(attackingGoalSign * ShotCalculator.PitchHalfLength, 0f, 0f);
            Vector3 toGoal = (goalCenter - transform.position);
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(toGoal.normalized);

            ComputedShotResult result = ShotCalculator.ComputeFreeKickShot(
                this, ball, attackingGoalSign, aimedGoalPoint, isCurveShot, charge01);

            if (visualRig != null)
                visualRig.TriggerNormalKick(result.metadata.footUsed, isCurveShot);

            Vector3 footWorld = visualRig != null
                ? visualRig.GetFootWorldPosition(result.metadata.footUsed)
                : ball.transform.position;

            ball.LaunchShot(
                result.metadata,
                result.launchVelocity,
                result.magnusAcceleration,
                result.curveDuration,
                result.spinAngularVelocity);

            if (ImpactVfxManager.Instance != null)
            {
                ImpactVfxManager.Instance.TriggerShotImpactVfx(
                    footWorld, ball, result.launchVelocity.normalized, result.metadata.vfxType, isAcrobatic: true);
            }

            if (DynamicSoccerCamera.Instance != null)
                DynamicSoccerCamera.Instance.TriggerImpactShake(0.38f + charge01 * 0.18f, 0.26f);

            if (GoalCelebrationManager.Instance != null)
                GoalCelebrationManager.Instance.TriggerBriefImpactBloom(1.25f);

            OnShotFeedback?.Invoke(result.feedbackSummary, false);
        }

        public void ApplyBriefStumble(Vector3 pushVel, float stunDuration = 0.38f)
        {
            _stunTimer = Mathf.Max(_stunTimer, stunDuration);
            _planarVelocity = new Vector3(pushVel.x, 0f, pushVel.z);
            _isChargingShot = false;
            _shotCharge01 = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_tackleCooldown > 0f) _tackleCooldown -= dt;
            if (_flickCooldown > 0f) _flickCooldown -= dt;
            if (_wallJumpTimer > 0f) _wallJumpTimer -= dt;
            if (_wallBlockWindowTimer > 0f)
            {
                _wallBlockWindowTimer -= dt;
                CheckWallDeflection();
            }

            if (!_controlsEnabled)
            {
                if (_planarVelocity.sqrMagnitude > 0.01f)
                {
                    _planarVelocity = Vector3.Lerp(_planarVelocity, Vector3.zero, dt * 6f);
                    MovePlanarWithBounds(_planarVelocity * dt);
                }
                if (visualRig != null)
                    visualRig.UpdateProceduralAnimation(Vector3.zero, false, dt);
                return;
            }

            if (_isInAcrobaticRoutine)
            {
                if (visualRig != null)
                    visualRig.UpdateProceduralAnimation(Vector3.zero, false, Time.unscaledDeltaTime);
                return;
            }

            // 실시간 오버헤드킥 / 아크로바틱 가능 자세 체크
            Vector3 goalCenter = new Vector3(attackingGoalSign * ShotCalculator.PitchHalfLength, 0f, 0f);
            bool holdingAcroMod = !isAI && ReadHoldC();
            CanDoAcrobaticNow = ShotCalculator.CheckAcrobaticOpportunity(this, ball, goalCenter, holdingAcroMod, out ShotType acroType);
            AvailableAcrobaticType = acroType;

            if (_stunTimer > 0f)
            {
                _stunTimer -= dt;
                _planarVelocity = Vector3.Lerp(_planarVelocity, Vector3.zero, dt * 7f);
                MovePlanarWithBounds(_planarVelocity * dt);
                if (visualRig != null)
                    visualRig.UpdateProceduralAnimation(_planarVelocity, false, dt);
                return;
            }

            if (isAI)
                UpdateAI(dt, goalCenter);
            else
                UpdateHumanInput(dt, goalCenter);

            UpdateBallPossessionAndDribble(dt);
            ResolveBodyJostleWithOpponent(dt);
        }

        private void UpdateHumanInput(float dt, Vector3 goalCenter)
        {
            Vector2 moveInput = ReadMoveInput();
            bool sprinting = ReadSprint();
            bool holdZ = ReadHoldZ();
            bool holdC = ReadHoldC();

            bool shootPressed = ReadShootPressedThisFrame();
            bool shootHeld = ReadShootHeld();
            bool shootReleased = ReadShootReleasedThisFrame();

            bool lobAPressed = ReadLobAPressedThisFrame();
            bool flickQPressed = ReadFlickQPressedThisFrame();
            bool quickSPressed = ReadPassSPressedThisFrame();
            bool knockShiftPressed = ReadKnockOnShiftPressedThisFrame();
            bool standingTackleSpacePressed = ReadStandingTackleSpacePressedThisFrame();

            // 1. 이동 물리 (키·몸무게·스탯에 따른 가속도/회전 체감 적용!)
            ApplyMovementFeel(moveInput, sprinting, holdC, goalCenter, dt);

            bool hasBall = ball != null && ball.CurrentOwner == this;
            float groundDistToBall = ball != null
                ? Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(ball.transform.position.x, ball.transform.position.z))
                : 999f;
            bool nearPlayableGroundBall = ball != null && ball.CurrentOwner == null && !ball.IsAirborne && groundDistToBall <= 1.75f;

            // 2. Q키: 셀프 레인보우/볼 띄우기 (즉시 오버헤드킥 각 만들기!)
            if (flickQPressed && _flickCooldown <= 0f && (hasBall || nearPlayableGroundBall))
            {
                _flickCooldown = 0.55f;
                _isChargingShot = false;

                // 머리 위 + 살짝 골문 쪽으로 붕 띄우고, 선수는 오버헤드킥 하기 좋게 골문을 등지는 자세로 스냅!
                Vector3 toGoal = (goalCenter - transform.position).normalized;
                Vector3 popVel = toGoal * 1.4f + Vector3.up * 7.4f;
                ball.PopUpForAcrobatic(this, popVel);

                // 등지는 각도로 자연스럽게 피벗
                Vector3 awayFromGoal = -toGoal;
                awayFromGoal.y = 0f;
                if (awayFromGoal.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(awayFromGoal);

                OnShotFeedback?.Invoke("✨ 볼 띄우기 성공! 공중에서 [D] 키를 눌러 오버헤드킥을 쏘세요!", false);
                return;
            }

            // 3. A키: 공격 시 셀프 로빙 띄우기 / 수비 시 슬라이딩 태클
            if (lobAPressed)
            {
                if ((hasBall || nearPlayableGroundBall) && _flickCooldown <= 0f)
                {
                    _flickCooldown = 0.55f;
                    _isChargingShot = false;
                    Vector3 lobDir = moveInput.sqrMagnitude > 0.1f
                        ? new Vector3(moveInput.x, 0f, moveInput.y).normalized
                        : transform.forward;
                    Vector3 lobVel = lobDir * 4.2f + Vector3.up * 7.8f;
                    ball.PopUpForAcrobatic(this, lobVel);
                    OnShotFeedback?.Invoke("🎈 셀프 로빙 패스! 낙하 타이밍에 [D] 오버헤드킥 / 발리슛!", false);
                    return;
                }
                else if (!hasBall && _tackleCooldown <= 0f)
                {
                    PerformTackle(isSlide: true);
                    return;
                }
            }

            // 4. Shift키: 치고 달리기(스루 터치) / S키: 급정거 방향전환
            if (knockShiftPressed && (hasBall || nearPlayableGroundBall) && _flickCooldown <= 0f)
            {
                _flickCooldown = 0.45f;
                Vector3 burstDir = moveInput.sqrMagnitude > 0.1f
                    ? new Vector3(moveInput.x, 0f, moveInput.y).normalized
                    : transform.forward;
                ball.ReleaseFromOwner(this, 0.16f);
                ball.Rb.linearVelocity = burstDir * (Profile.EffectiveTopSpeed * 1.28f) + Vector3.up * 0.55f;
                _planarVelocity = burstDir * (Profile.EffectiveTopSpeed * 1.15f);
            }
            else if (quickSPressed && hasBall)
            {
                // 볼 밟고 급정지 턴
                _planarVelocity *= 0.25f;
            }

            // 5. Space바: 수비 시 스탠딩 태클
            if (standingTackleSpacePressed && !hasBall && _tackleCooldown <= 0f)
            {
                PerformTackle(isSlide: false);
                return;
            }

            // 6. D키(슛 키): 공중볼 아크로바틱 자세면 즉시 오버헤드킥/발리 발동!!
            if (shootPressed)
            {
                if (CanDoAcrobaticNow)
                {
                    _isChargingShot = false;
                    _shotCharge01 = 0f;
                    StartCoroutine(ExecuteAcrobaticKickRoutine(AvailableAcrobaticType, moveInput));
                    return;
                }

                if (hasBall || nearPlayableGroundBall)
                {
                    _isChargingShot = true;
                    _shotCharge01 = 0.08f;
                    _chargingFinesse = holdZ;
                }
            }

            // 지상 슛 게이지 차징 & 발사
            if (_isChargingShot)
            {
                if (holdZ) _chargingFinesse = true;

                // 차징 도중에 공이 떠서 아크로바틱 조건이 되면 즉시 아크로바틱 전환!
                if (CanDoAcrobaticNow)
                {
                    _isChargingShot = false;
                    StartCoroutine(ExecuteAcrobaticKickRoutine(AvailableAcrobaticType, moveInput));
                    return;
                }

                if (shootHeld)
                {
                    _shotCharge01 = Mathf.Clamp01(_shotCharge01 + dt / 0.56f);
                    if (_shotCharge01 >= 1.0f)
                    {
                        FireGroundShot(moveInput);
                    }
                }

                if (shootReleased && _isChargingShot)
                {
                    FireGroundShot(moveInput);
                }
            }
        }

        private void ApplyMovementFeel(Vector2 moveInput, bool sprinting, bool holdBackToGoalC, Vector3 goalCenter, float dt)
        {
            Vector3 wishDir = new Vector3(moveInput.x, 0f, moveInput.y);
            if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

            float topSpeed = Profile != null ? Profile.EffectiveTopSpeed : 8.0f;
            float speedMult = sprinting ? 1.0f : 0.74f;
            if (holdBackToGoalC) speedMult = 0.56f;
            if (ball != null && ball.CurrentOwner == this) speedMult *= 0.93f; // 드리블 중 미세 감속

            Vector3 targetVel = wishDir * (topSpeed * speedMult);
            float accel = Profile != null ? Profile.EffectiveAcceleration : 20f;
            float decel = accel * 1.18f;

            float rate = wishDir.sqrMagnitude > 0.02f ? accel : decel;
            _planarVelocity = Vector3.MoveTowards(_planarVelocity, targetVel, rate * dt);

            MovePlanarWithBounds(_planarVelocity * dt);

            // 회전 반응성 (민첩성 + 키·몸무게 체감 반영!)
            float turnSpeed = Profile != null ? Profile.EffectiveTurnSpeed : 12f;
            if (holdBackToGoalC)
            {
                // C키를 누르면 골문을 등지는 자세(오버헤드킥 준비 자세!)를 자연스럽게 유지
                Vector3 backDir = -(goalCenter - transform.position);
                backDir.y = 0f;
                if (backDir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(backDir.normalized), dt * turnSpeed);
            }
            else if (wishDir.sqrMagnitude > 0.03f)
            {
                Quaternion targetRot = Quaternion.LookRotation(wishDir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * turnSpeed);
            }

            if (visualRig != null)
                visualRig.UpdateProceduralAnimation(_planarVelocity, sprinting && wishDir.sqrMagnitude > 0.1f, dt);
        }

        private void FireGroundShot(Vector2 aimInput)
        {
            _isChargingShot = false;
            float charge = Mathf.Clamp(_shotCharge01, 0.20f, 1.0f);
            _shotCharge01 = 0f;

            if (ball == null) return;

            float d = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(ball.transform.position.x, ball.transform.position.z));
            if (ball.CurrentOwner != this && d > 2.2f)
                return;

            // 골문 쪽으로 상체 회전
            Vector3 goalCenter = new Vector3(attackingGoalSign * ShotCalculator.PitchHalfLength, 0f, 0f);
            Vector3 aimDir = (goalCenter - transform.position);
            aimDir.y = 0f;
            if (aimDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(aimDir.normalized);

            ShotType sType = _chargingFinesse ? ShotType.FinesseCurve : ShotType.Normal;
            ComputedShotResult result = ShotCalculator.ComputeShot(
                this, ball, targetGoalkeeper, attackingGoalSign, sType, charge, aimInput);

            if (visualRig != null)
                visualRig.TriggerNormalKick(result.metadata.footUsed, _chargingFinesse);

            Vector3 footWorld = visualRig != null
                ? visualRig.GetFootWorldPosition(result.metadata.footUsed)
                : ball.transform.position;

            ball.LaunchShot(
                result.metadata,
                result.launchVelocity,
                result.magnusAcceleration,
                result.curveDuration,
                result.spinAngularVelocity);

            if (ImpactVfxManager.Instance != null)
            {
                ImpactVfxManager.Instance.TriggerShotImpactVfx(
                    footWorld, ball, result.launchVelocity.normalized, result.metadata.vfxType, isAcrobatic: false);
            }

            if (DynamicSoccerCamera.Instance != null)
                DynamicSoccerCamera.Instance.TriggerImpactShake(0.28f + charge * 0.16f, 0.22f);

            if (GoalCelebrationManager.Instance != null)
                GoalCelebrationManager.Instance.TriggerBriefImpactBloom(0.95f);

            OnShotFeedback?.Invoke(result.feedbackSummary, result.metadata.usedWeakFoot);
        }

        /// <summary>
        /// 오버헤드킥 & 시저스 발리 시네마틱 시퀀스:
        /// 1) 자세 잡을 때 카메라 줌 인 + 화면 부르르 떨림 + 슬로모션 도약!
        /// 2) 발끝이 공에 닿는 정점 순간! 히트스톱 + 불꽃/레이저 랜덤 이펙트 폭발 + 초고속 슈팅 발사!
        /// </summary>
        private IEnumerator ExecuteAcrobaticKickRoutine(ShotType acrobaticType, Vector2 aimInput)
        {
            if (ball == null) yield break;

            _isInAcrobaticRoutine = true;
            _planarVelocity = Vector3.zero;
            ball.SetOwner(null);

            Vector3 goalCenter = new Vector3(attackingGoalSign * ShotCalculator.PitchHalfLength, 0f, 0f);
            PreferredFoot strikeFoot = ShotCalculator.DetermineStrikingFoot(this, ball.transform.position, goalCenter, acrobaticType, aimInput);

            // 오버헤드킥이면 골문을 등지는 방향, 시저스 발리면 골문을 향한 측면 각도로 자세 정렬!
            Vector3 toGoal = (goalCenter - transform.position);
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude < 0.01f) toGoal = Vector3.right * attackingGoalSign;
            toGoal.Normalize();

            if (acrobaticType == ShotType.OverheadKick)
                transform.rotation = Quaternion.LookRotation(-toGoal, Vector3.up);
            else
                transform.rotation = Quaternion.LookRotation(toGoal, Vector3.up);

            // 공 타격 정점 위치 계산 (선수 머리 위 1.65~1.95m 지점)
            Vector3 startPlayerPos = transform.position;
            Vector3 ballStartPos = ball.transform.position;
            Vector3 strikeApexBallPos = new Vector3(
                Mathf.Lerp(startPlayerPos.x, ballStartPos.x, 0.65f),
                Mathf.Clamp(ballStartPos.y, 1.55f, 2.25f),
                Mathf.Lerp(startPlayerPos.z, ballStartPos.z, 0.65f)
            );
            Vector3 targetPlayerGroundPos = new Vector3(strikeApexBallPos.x, 0f, strikeApexBallPos.z) - (-toGoal * 0.25f);

            // 카메라 줌인 + 화면 떨림 시작! (플레이어/AI 모두 연출하되 플레이어일 때 더욱 강렬하게!)
            if (DynamicSoccerCamera.Instance != null)
            {
                DynamicSoccerCamera.Instance.BeginAcrobaticWindupZoom(transform, isAI ? 0.14f : 0.22f);
            }

            if (visualRig != null)
            {
                if (acrobaticType == ShotType.OverheadKick)
                    visualRig.TriggerOverheadKick(strikeFoot, 0.76f);
                else
                    visualRig.TriggerScissorsVolley(strikeFoot, 0.68f);
            }

            // 슬로모션 자세 잡기 구간 (실제 시간 약 0.34초 동안 극적인 공중 도약!)
            Time.timeScale = 0.32f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            ball.Rb.linearVelocity = Vector3.zero;
            ball.Rb.angularVelocity = Vector3.zero;

            float windupRealtime = 0.34f;
            float elapsed = 0f;
            while (elapsed < windupRealtime)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / windupRealtime);
                transform.position = Vector3.Lerp(startPlayerPos, targetPlayerGroundPos, t);
                ball.transform.position = Vector3.Lerp(ballStartPos, strikeApexBallPos, t);
                yield return null;
            }

            // === 발이 공에 닿는 정점 임팩트 프레임!! ===
            Time.timeScale = 0.05f; // 짜릿한 0.045초 히트스톱!
            if (DynamicSoccerCamera.Instance != null)
            {
                DynamicSoccerCamera.Instance.EndAcrobaticWindupZoom();
                DynamicSoccerCamera.Instance.TriggerImpactShake(0.58f, 0.34f);
            }
            if (GoalCelebrationManager.Instance != null)
            {
                GoalCelebrationManager.Instance.TriggerBriefImpactBloom(1.65f);
            }

            ComputedShotResult shotResult = ShotCalculator.ComputeShot(
                this, ball, targetGoalkeeper, attackingGoalSign, acrobaticType, 0.84f, aimInput);

            Vector3 footContactPos = visualRig != null
                ? visualRig.GetFootWorldPosition(strikeFoot)
                : strikeApexBallPos;

            if (ImpactVfxManager.Instance != null)
            {
                ImpactVfxManager.Instance.TriggerShotImpactVfx(
                    footContactPos,
                    ball,
                    shotResult.launchVelocity.normalized,
                    shotResult.metadata.vfxType,
                    isAcrobatic: true);
            }

            yield return new WaitForSecondsRealtime(0.048f);

            // 정상 속도로 복귀하며 대포알 아크로바틱 슛 발사!!
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;

            ball.LaunchShot(
                shotResult.metadata,
                shotResult.launchVelocity,
                shotResult.magnusAcceleration,
                shotResult.curveDuration,
                shotResult.spinAngularVelocity);

            OnShotFeedback?.Invoke("🌟 " + shotResult.feedbackSummary, shotResult.metadata.usedWeakFoot);

            // 착지 후 모션 마무리 대기
            yield return new WaitForSeconds(0.36f);
            _isInAcrobaticRoutine = false;
        }

        private void PerformTackle(bool isSlide)
        {
            _tackleCooldown = isSlide ? 0.95f : 0.65f;
            if (visualRig != null)
                visualRig.TriggerTackle();

            Vector3 lungeDir = transform.forward;
            if (ball != null)
            {
                Vector3 toBall = ball.transform.position - transform.position;
                toBall.y = 0f;
                if (toBall.sqrMagnitude > 0.01f && toBall.magnitude < 4.5f)
                {
                    lungeDir = toBall.normalized;
                    transform.rotation = Quaternion.LookRotation(lungeDir);
                }
            }

            _planarVelocity = lungeDir * (isSlide ? 9.6f : 6.8f);

            if (ball == null) return;

            // === 위험한 태클(백태클 / 공보다 다리를 먼저 가격하는 거친 태클) 반칙 판정 ===
            if (opponent != null)
            {
                Vector2 myXZ = new Vector2(transform.position.x, transform.position.z);
                Vector2 oppXZ = new Vector2(opponent.transform.position.x, opponent.transform.position.z);
                Vector2 ballXZ = new Vector2(ball.transform.position.x, ball.transform.position.z);

                float distToOpp = Vector2.Distance(myXZ, oppXZ);
                float distPlanarBall = Vector2.Distance(myXZ, ballXZ);
                float oppDistBall = Vector2.Distance(oppXZ, ballXZ);

                float bodyContactReach = isSlide ? 2.42f : 1.80f;
                if (distToOpp <= bodyContactReach)
                {
                    Vector3 oppToTackler = transform.position - opponent.transform.position;
                    oppToTackler.y = 0f;
                    float approachDot = oppToTackler.sqrMagnitude > 0.001f
                        ? Vector3.Dot(opponent.transform.forward, oppToTackler.normalized)
                        : 1f;

                    bool isBackTackle = approachDot < -0.14f;
                    bool hitBodyWithoutCleanBall = (distToOpp < distPlanarBall - 0.10f) || (distPlanarBall > (isSlide ? 2.0f : 1.50f));
                    bool oppInPlayWithBall = (ball.CurrentOwner == opponent) || (oppDistBall < 2.3f);

                    float foulChance = 0f;
                    if (isSlide && isBackTackle)
                        foulChance = 0.90f; // 뒤에서 거는 슬라이딩 백태클은 매우 위험!
                    else if (isSlide && hitBodyWithoutCleanBall)
                        foulChance = 0.72f; // 공 대신 선수 다리를 먼저 쓸어버린 깊은 슬라이딩 태클
                    else if (!isSlide && isBackTackle && (hitBodyWithoutCleanBall || UnityEngine.Random.value < 0.6f))
                        foulChance = 0.62f; // 뒤에서 깊숙이 다리 걸기
                    else if (!isSlide && hitBodyWithoutCleanBall && oppInPlayWithBall && distToOpp < 1.38f)
                        foulChance = 0.36f; // 늦은 타이밍의 발 걸기

                    if (foulChance > 0f && UnityEngine.Random.value < foulChance)
                    {
                        if (ball.CurrentOwner != null)
                            ball.ReleaseFromOwner(ball.CurrentOwner, 0.5f);
                        ball.Rb.linearVelocity = lungeDir * 2.6f + Vector3.up * 0.85f;

                        opponent.PlayFoulFall(lungeDir * 3.8f);
                        if (DynamicSoccerCamera.Instance != null)
                            DynamicSoccerCamera.Instance.TriggerImpactShake(0.44f, 0.28f);

                        OnFoulCommitted?.Invoke(new FoulEventArgs
                        {
                            offender = this,
                            victim = opponent,
                            foulSpot = opponent.transform.position,
                            isSlideTackle = isSlide,
                            isFromBehind = isBackTackle
                        });
                        return;
                    }
                }
            }

            float distToBall = Vector3.Distance(transform.position, ball.transform.position);
            float reach = isSlide ? 2.35f : 1.85f;

            if (distToBall <= reach && !ball.IsAirborne)
            {
                SoccerPlayerController prevOwner = ball.CurrentOwner;
                if (prevOwner != null && prevOwner != this)
                {
                    // 몸싸움 스탯 비교 반영
                    float myPhys = Profile != null ? Profile.physical : 75f;
                    float oppPhys = prevOwner.Profile != null ? prevOwner.Profile.physical : 75f;
                    float winProb = Mathf.Clamp(0.65f + (myPhys - oppPhys) * 0.008f, 0.35f, 0.92f);

                    if (UnityEngine.Random.value <= winProb)
                    {
                        prevOwner.ApplyBriefStumble(lungeDir * 3.5f, 0.42f);
                        ball.ReleaseFromOwner(prevOwner, 0.45f);
                        ball.SetOwner(this);
                        if (!isAI)
                            OnShotFeedback?.Invoke("💥 깔끔한 태클 성공! 볼 탈취!", false);
                    }
                }
                else if (prevOwner == null)
                {
                    ball.SetOwner(this);
                }
            }
        }

        private void CheckWallDeflection()
        {
            if (ball == null || ball.CurrentOwner != null) return;
            if (ball.Rb.linearVelocity.sqrMagnitude < 36f) return;

            float planarDist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(ball.transform.position.x, ball.transform.position.z));

            float maxBlockHeight = (IsJumpingInWall ? 2.48f : 1.72f) * (Profile != null ? Profile.HeightScale : 1f);
            if (planarDist <= 0.62f && ball.transform.position.y <= maxBlockHeight)
            {
                _wallBlockWindowTimer = 0f;
                Vector3 rebound = new Vector3(
                    attackingGoalSign * UnityEngine.Random.Range(5.5f, 9.5f),
                    UnityEngine.Random.Range(6.8f, 9.5f),
                    UnityEngine.Random.Range(-4.5f, 4.5f)
                );
                ball.Rb.linearVelocity = rebound;
                ball.Rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 20f;

                if (DynamicSoccerCamera.Instance != null)
                    DynamicSoccerCamera.Instance.TriggerImpactShake(0.35f, 0.22f);

                if (!isAI)
                    OnShotFeedback?.Invoke("🧱 수비벽 점프 블로킹 성공!! 세컨볼 찬스!", false);
                else if (opponent != null)
                    opponent.OnShotFeedback?.Invoke("🧱 프리킥이 수비벽에 맞고 튀어 올랐습니다! 공중에서 [D] 오버헤드킥 찬스!!", false);
            }
        }

        private void UpdateBallPossessionAndDribble(float dt)
        {
            if (ball == null || _isInAcrobaticRoutine) return;

            if (ball.CurrentOwner == this)
            {
                // 드리블 중 발 앞에 볼 위치 유지 (민첩성 높을수록 발에 착 달라붙음!)
                float agilityNorm = Profile != null ? Mathf.InverseLerp(45f, 99f, Profile.agility) : 0.6f;
                float leadDist = Mathf.Lerp(0.78f, 0.58f, agilityNorm);

                // 주발 쪽 발 앞에 살짝 얹어 드리블 디테일 구현
                float footOffsetSide = (Profile == null || Profile.preferredFoot == PreferredFoot.Right) ? 0.14f : -0.14f;
                Vector3 targetBallPos = transform.position
                                      + transform.forward * leadDist
                                      + transform.right * footOffsetSide
                                      + Vector3.up * SoccerBall.BallRadius;

                ball.transform.position = Vector3.Lerp(ball.transform.position, targetBallPos, 1f - Mathf.Exp(-24f * dt));
                ball.Rb.linearVelocity = Vector3.zero;

                // 구르는 방향 시각적 회전
                if (_planarVelocity.sqrMagnitude > 0.05f)
                {
                    Vector3 rollAxis = Vector3.Cross(Vector3.up, _planarVelocity.normalized);
                    ball.transform.Rotate(rollAxis, _planarVelocity.magnitude * 220f * dt, Space.World);
                }
                return;
            }

            // 루즈볼 소유권 획득 체크 (공중볼은 오버헤드킥 찬스를 살리기 위해 바닥 근처(<=0.48m)일 때만 자동 트래핑!)
            if (ball.CurrentOwner == null && ball.HeightAboveGround <= 0.48f && ball.CanBeClaimedBy(this))
            {
                float speed = ball.Rb.linearVelocity.magnitude;
                if (speed < 15.5f)
                {
                    float dist = Vector2.Distance(
                        new Vector2(transform.position.x, transform.position.z),
                        new Vector2(ball.transform.position.x, ball.transform.position.z));
                    if (dist <= 0.92f)
                    {
                        ball.SetOwner(this);
                    }
                }
            }
        }

        private void ResolveBodyJostleWithOpponent(float dt)
        {
            if (opponent == null) return;
            Vector3 diff = transform.position - opponent.transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            float minSep = 0.78f;
            if (dist < minSep && dist > 0.001f)
            {
                float myWeight = Profile != null ? Profile.weightKg : 75f;
                float oppWeight = opponent.Profile != null ? opponent.Profile.weightKg : 75f;
                float myShare = oppWeight / Mathf.Max(1f, myWeight + oppWeight);
                Vector3 push = diff.normalized * ((minSep - dist) * myShare);
                MovePlanarWithBounds(push);
            }
        }

        private void UpdateAI(float dt, Vector3 aiTargetGoal)
        {
            if (ball == null) return;

            _aiDecisionTimer -= dt;
            if (_aiDecisionTimer <= 0f)
            {
                _aiDecisionTimer = UnityEngine.Random.Range(0.28f, 0.55f);
                _aiStrafeOffsetZ = UnityEngine.Random.Range(-4.5f, 4.5f);
            }

            bool aiHasBall = ball.CurrentOwner == this;
            bool playerHasBall = opponent != null && ball.CurrentOwner == opponent;

            // AI도 공중볼 찬스가 오면 멋지게 오버헤드킥 시도!
            if (CanDoAcrobaticNow && Vector3.Distance(transform.position, aiTargetGoal) < 22f && UnityEngine.Random.value < 0.12f)
            {
                StartCoroutine(ExecuteAcrobaticKickRoutine(AvailableAcrobaticType, Vector2.zero));
                return;
            }

            if (aiHasBall)
            {
                float distToGoal = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(aiTargetGoal.x, aiTargetGoal.z));

                Vector3 targetPoint = new Vector3(aiTargetGoal.x, 0f, _aiStrafeOffsetZ);
                Vector3 moveDir = (targetPoint - transform.position);
                moveDir.y = 0f;
                if (moveDir.sqrMagnitude > 0.01f) moveDir.Normalize();

                ApplyMovementFeel(new Vector2(moveDir.x, moveDir.z), sprinting: distToGoal > 13f, holdBackToGoalC: false, aiTargetGoal, dt);

                // 슈팅 거리 진입 시 스탯에 맞는 슛 선택 (가끔 볼 띄워 오버헤드킥도 시도!)
                if (distToGoal < 19.5f && UnityEngine.Random.value < dt * 1.45f)
                {
                    if (Profile != null && Profile.acrobatics >= 85 && distToGoal < 15f && _flickCooldown <= 0f && UnityEngine.Random.value < 0.28f)
                    {
                        _flickCooldown = 1.5f;
                        Vector3 popVel = moveDir * 1.2f + Vector3.up * 7.2f;
                        ball.PopUpForAcrobatic(this, popVel);
                        return;
                    }

                    _chargingFinesse = Profile != null && Profile.curve >= 82 && UnityEngine.Random.value < 0.5f;
                    _shotCharge01 = UnityEngine.Random.Range(0.52f, 0.85f);
                    FireGroundShot(new Vector2(moveDir.x, UnityEngine.Random.Range(-1f, 1f)));
                }
            }
            else if (playerHasBall)
            {
                // 수비: 플레이어가 아크로바틱 셋업할 여유도 적당히 주면서 압박 & 때때로 거친 슬라이딩 태클도 시도!
                Vector3 defendSpot = opponent.transform.position + new Vector3(attackingGoalSign * 0.65f, 0f, 0f);
                Vector3 dir = defendSpot - transform.position;
                dir.y = 0f;
                float distToOpp = dir.magnitude;
                if (dir.sqrMagnitude > 0.01f) dir.Normalize();

                ApplyMovementFeel(new Vector2(dir.x, dir.z) * 0.88f, sprinting: distToOpp > 5f, holdBackToGoalC: false, aiTargetGoal, dt);

                if (distToOpp < 2.05f && _tackleCooldown <= 0f && UnityEngine.Random.value < dt * 1.65f)
                {
                    bool recklessSlide = distToOpp > 1.35f || UnityEngine.Random.value < 0.36f;
                    PerformTackle(isSlide: recklessSlide);
                }
            }
            else
            {
                // 루즈볼 추격 (플레이어가 방금 띄운 볼이면 잠깐 반응 딜레이)
                bool playerJustPopped = ball.IsAirborne && ball.LastTouchPlayer == opponent;
                Vector3 chaseTarget = ball.transform.position;
                chaseTarget.y = 0f;
                Vector3 dir = chaseTarget - transform.position;
                if (dir.sqrMagnitude > 0.01f) dir.Normalize();

                float chaseScale = playerJustPopped ? 0.55f : 0.94f;
                ApplyMovementFeel(new Vector2(dir.x, dir.z) * chaseScale, sprinting: !playerJustPopped, holdBackToGoalC: false, aiTargetGoal, dt);
            }
        }

        private void MovePlanarWithBounds(Vector3 delta)
        {
            Vector3 p = transform.position + delta;
            p.x = Mathf.Clamp(p.x, -ShotCalculator.PitchHalfLength + 0.6f, ShotCalculator.PitchHalfLength - 0.6f);
            p.z = Mathf.Clamp(p.z, -ShotCalculator.PitchHalfWidth + 0.6f, ShotCalculator.PitchHalfWidth - 0.6f);
            p.y = 0f;
            transform.position = p;
        }

        #region FIFA Input Readers (New Input System: Keyboard + Gamepad)

        public static Vector2 ReadMoveInput()
        {
            Vector2 v = Vector2.zero;
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rightArrowKey.isPressed) v.x += 1f;
                if (kb.leftArrowKey.isPressed) v.x -= 1f;
                if (kb.upArrowKey.isPressed) v.y += 1f;
                if (kb.downArrowKey.isPressed) v.y -= 1f;
            }

            Gamepad gp = Gamepad.current;
            if (gp != null && v.sqrMagnitude < 0.01f)
            {
                v = gp.leftStick.ReadValue();
            }

            if (v.sqrMagnitude > 1f) v.Normalize();
            return v;
        }

        public static bool ReadSprint()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.eKey.isPressed)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.rightTrigger.ReadValue() > 0.35f;
        }

        public static bool ReadHoldZ()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.zKey.isPressed)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.rightShoulder.isPressed;
        }

        public static bool ReadHoldC()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.cKey.isPressed)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.leftTrigger.ReadValue() > 0.35f;
        }

        public static bool ReadShootPressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.dKey.wasPressedThisFrame)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.buttonEast.wasPressedThisFrame;
        }

        public static bool ReadShootHeld()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.dKey.isPressed)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.buttonEast.isPressed;
        }

        public static bool ReadShootReleasedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.dKey.wasReleasedThisFrame)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.buttonEast.wasReleasedThisFrame;
        }

        public static bool ReadLobAPressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.aKey.wasPressedThisFrame)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.buttonWest.wasPressedThisFrame;
        }

        public static bool ReadFlickQPressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.qKey.wasPressedThisFrame)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && (gp.rightStickButton.wasPressedThisFrame || gp.leftShoulder.wasPressedThisFrame);
        }

        public static bool ReadPassSPressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.sKey.wasPressedThisFrame)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.buttonSouth.wasPressedThisFrame;
        }

        public static bool ReadKnockOnShiftPressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame))
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.buttonNorth.wasPressedThisFrame;
        }

        public static bool ReadStandingTackleSpacePressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.buttonSouth.wasPressedThisFrame;
        }

        #endregion
    }
}

