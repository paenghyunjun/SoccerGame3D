using System;
using System.Collections;
using System.Collections.Generic;
using Soccer3D.CameraSystem;
using Soccer3D.Data;
using Soccer3D.Effects;
using Soccer3D.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Soccer3D.Gameplay
{
    public enum TacticalRole
    {
        Striker = 0,
        LeftDefender = 1,
        CenterDefender = 2,
        RightDefender = 3
    }

    public struct FoulEventArgs
    {
        public SoccerPlayerController offender;
        public SoccerPlayerController victim;
        public Vector3 foulSpot;
        public bool isSlideTackle;
        public bool isFromBehind;
    }

    public enum AimTargetType
    {
        None = 0,
        Opponent = 1,
        Teammate = 2
    }

    public class SoccerPlayerController : MonoBehaviour
    {
        public bool isAI;
        public int attackingGoalSign = 1; // 블루팀(플레이어 팀) = +1 (+X 골문 공격), 레드팀(AI 팀) = -1 (-X 골문 공격)
        public TacticalRole tacticalRole = TacticalRole.Striker;
        public FormationSlot AssignedSlot { get; private set; }
        public int SquadIndex { get; private set; }
        public PlayerProfile Profile { get; private set; }

        public SoccerBall ball;
        public SoccerPlayerController opponent; // 매 프레임 가장 가까운 상대 선수로 자동 갱신
        public readonly List<SoccerPlayerController> teammates = new List<SoccerPlayerController>();
        public readonly List<SoccerPlayerController> opponents = new List<SoccerPlayerController>();
        public GoalkeeperAI targetGoalkeeper;
        public PlayerVisualRig visualRig;

        // UI 표시용 상태 프로퍼티
        public bool IsChargingShot => _isChargingShot;
        public float CurrentShotCharge01 => _shotCharge01;
        public bool IsFinesseCharge => _chargingFinesse;
        public bool CanDoAcrobaticNow { get; private set; }
        public bool IsAimingAtBall { get; private set; }
        public bool IsBallInAcrobaticRangeWithoutAim { get; private set; }
        public ShotType AvailableAcrobaticType { get; private set; }
        public bool IsPerformingAcrobatic => _isInAcrobaticRoutine;
        public bool IsJumpingInWall => _wallJumpTimer > 0.08f || _jumpHeightOffset > 0.25f;
        public bool IsHeadering { get; set; }
        public bool IsJumping => _jumpHeightOffset > 0.03f;
        public float JumpHeightOffset => _jumpHeightOffset;
        public bool IsBlitzDashing => _blitzDashTimer > 0f;
        public float BlitzDashCooldownRemaining => Mathf.Max(0f, _dashStealCooldown);

        // 에임 타겟팅 및 포켓몬고 드래그 감아차기 프로퍼티
        public AimTargetType CurrentAimTargetType { get; private set; } = AimTargetType.None;
        public SoccerPlayerController CurrentAimTargetPlayer { get; private set; }
        public SoccerPlayerController BlitzDashCandidateTarget { get; private set; }
        public bool IsDraggingCurveShot => _isDraggingCurveShot;
        public Vector2 DragCursorOffset => _dragCursorOffset;
        public float DragCurveBias => ComputeCurrentDragCurveBias();
        public bool IsCurveSpinReady => _isDraggingCurveShot && Mathf.Abs(ComputeCurrentDragCurveBias()) >= 0.72f;

        // 선수 전환 스마트 타겟 추적용 (방금 패스/크로스를 보낸 동료 기억)
        public SoccerPlayerController LastIntendedReceiver { get; private set; }
        public float LastPassOrCrossTime { get; private set; } = -99f;

        // 1인칭 시점 전용 상태 프로퍼티
        public float FirstPersonYaw { get; private set; } = 90f;
        public float FirstPersonEulerPitch { get; private set; } = 14.5f;
        public Vector3 PlanarVelocity => _planarVelocity;
        public bool IsSprintingNow { get; private set; }

        public event Action<string, bool> OnShotFeedback; // (메시지, 약발여부)
        public event Action<FoulEventArgs> OnFoulCommitted;
        public event Action<SoccerPlayerController> OnHumanPassExecuted; // 패스 시 받는 동료로 즉시 자동 조작 전환 이벤트

        private Vector3 _planarVelocity;
        private bool _isChargingShot;
        private float _shotCharge01;
        private bool _chargingFinesse;
        private bool _isInAcrobaticRoutine;

        // 포켓몬고 스타일 스핀 & 드래그 감아차기 상태
        private bool _isDraggingCurveShot;
        private Vector2 _dragCursorOffset;
        private float _peakSwipeVelX;
        private float _circularSpinMomentum;
        private float _dragDuration;

        // 점프(Space 단타) & 순간 돌진 탈취(Space 2연타) & 어깨 몸싸움 탈취(F) 상태
        private float _jumpHeightOffset;
        private float _jumpVerticalVel;
        private float _spaceDoubleTapWindow;
        private float _blitzDashTimer;
        private Vector3 _blitzDashDir;
        private float _blitzDashSpeed;
        private SoccerPlayerController _blitzDashTargetPlayer;
        private float _dashStealCooldown;
        private float _shoulderBargeCooldown;

        private float _tackleCooldown;
        private float _stunTimer;
        private float _flickCooldown;
        private float _wallJumpTimer;
        private float _wallBlockWindowTimer;
        private bool _controlsEnabled;
        private float _eHoldTime;
        private bool _wasEPressedInPossession;
        private bool _qUsedAsModifier;
        private bool _finesseShotRequested;

        // AI 내부 타이머 및 크로스 침투 / 탈압박 드리블 상태
        private float _aiDecisionTimer;
        private float _aiStrafeOffsetZ;
        private float _aiBallHoldTimer;
        private float _passCooldown;
        private float _aiEvasiveDribbleTimer;
        private float _aiEvasiveDribbleCooldown;
        private Vector3 _aiEvasiveDribbleDir;
        private bool _hasIncomingCross;
        private Vector3 _incomingCrossSpot;
        private float _incomingCrossTimer;
        private float _incomingPassAssistTimer;
        private SoccerPlayerController _incomingPassSender;
        private bool _autoSwitchToMeOnTouch;
        private float _qInfiltrateRunTimer;
        private Vector3 _qInfiltrateTarget;

        public bool IsPerformingEvasiveDribble => _aiEvasiveDribbleTimer > 0f;

        // 1인칭 플레이어 전용 발밑 3D 공 방향 네비게이션 화살표
        private Transform _groundBallArrowRoot;
        private Renderer[] _groundBallArrowRenderers;
        private Material _groundBallArrowMat;

        public void SetupPlayer(
            PlayerProfile profile,
            bool aiControlled,
            int attackSign,
            SoccerBall matchBall,
            TacticalRole role = TacticalRole.Striker,
            FormationSlot? formationSlot = null,
            int squadIndex = 0)
        {
            Profile = profile;
            isAI = aiControlled;
            attackingGoalSign = attackSign;
            tacticalRole = role;
            ball = matchBall;
            SquadIndex = squadIndex;

            if (formationSlot.HasValue)
            {
                AssignedSlot = formationSlot.Value;
            }
            else
            {
                AssignedSlot = role switch
                {
                    TacticalRole.Striker => new FormationSlot("ST", PositionLine.Forward, 0.36f, 0.0f, -2.2f, 0.0f),
                    TacticalRole.LeftDefender => new FormationSlot("LB", PositionLine.Defender, -0.54f, -0.72f, -31.0f, -21.5f),
                    TacticalRole.CenterDefender => new FormationSlot("CB", PositionLine.Defender, -0.66f, 0.0f, -35.5f, 0.0f),
                    TacticalRole.RightDefender => new FormationSlot("RB", PositionLine.Defender, -0.54f, 0.72f, -31.0f, 21.5f),
                    _ => new FormationSlot("CM", PositionLine.Midfielder, -0.06f, 0.0f, -15.0f, 0.0f)
                };
            }

            visualRig = GetComponent<PlayerVisualRig>();
            if (visualRig == null)
                visualRig = gameObject.AddComponent<PlayerVisualRig>();

            visualRig.BuildFromProfile(profile, isGoalkeeper: false);

            if (!isAI)
                EnsureGroundBallArrow();

            ResetStateForKickoff();
        }

        private void EnsureGroundBallArrow()
        {
            if (_groundBallArrowRoot != null) return;

            GameObject root = new GameObject("PlayerGroundBallArrow3D");
            _groundBallArrowRoot = root.transform;

            _groundBallArrowMat = MaterialHelper.CreateHdrUnlitMaterial(new Color(1.0f, 0.86f, 0.18f), 2.5f);

            // 전방(+Z 로컬)을 가리키는 입체 네온 화살표 (몸통 + 좌우 화살촉 날개)
            GameObject shaft = CreateArrowPiece("Shaft", _groundBallArrowRoot,
                new Vector3(0f, 0f, -0.10f), new Vector3(0.11f, 0.025f, 0.44f), Quaternion.identity, _groundBallArrowMat);
            GameObject wingL = CreateArrowPiece("WingL", _groundBallArrowRoot,
                new Vector3(-0.14f, 0f, 0.14f), new Vector3(0.09f, 0.028f, 0.36f), Quaternion.Euler(0f, 38f, 0f), _groundBallArrowMat);
            GameObject wingR = CreateArrowPiece("WingR", _groundBallArrowRoot,
                new Vector3(0.14f, 0f, 0.14f), new Vector3(0.09f, 0.028f, 0.36f), Quaternion.Euler(0f, -38f, 0f), _groundBallArrowMat);

            _groundBallArrowRenderers = new[]
            {
                shaft.GetComponent<Renderer>(),
                wingL.GetComponent<Renderer>(),
                wingR.GetComponent<Renderer>()
            };
            _groundBallArrowRoot.gameObject.SetActive(false);
        }

        private static GameObject CreateArrowPiece(string name, Transform parent, Vector3 localPos, Vector3 localScale, Quaternion localRot, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Collider c = go.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private void OnDestroy()
        {
            if (_groundBallArrowRoot != null)
            {
                if (Application.isPlaying) Destroy(_groundBallArrowRoot.gameObject);
                else DestroyImmediate(_groundBallArrowRoot.gameObject);
            }
        }

        public void SetControlsEnabled(bool enabled)
        {
            _controlsEnabled = enabled;
            if (!enabled)
            {
                _isChargingShot = false;
                _shotCharge01 = 0f;
                _isDraggingCurveShot = false;
                _dragCursorOffset = Vector2.zero;
                _peakSwipeVelX = 0f;
                _circularSpinMomentum = 0f;
                _blitzDashTimer = 0f;
                _planarVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// 인플레이 중 이 선수로 조작 전환(Player Switch)될 때 1인칭 시야 및 네비게이션 화살표를 즉시 활성화
        /// </summary>
        public void OnSwitchedToHuman(bool smartAlignView = true)
        {
            isAI = false;
            _hasIncomingCross = false;
            _incomingCrossTimer = 0f;
            _isChargingShot = false;
            _shotCharge01 = 0f;
            _isDraggingCurveShot = false;
            _dragCursorOffset = Vector2.zero;
            _peakSwipeVelX = 0f;
            _circularSpinMomentum = 0f;

            EnsureGroundBallArrow();
            if (visualRig != null)
                visualRig.SetFirstPersonViewMode(true);

            if (smartAlignView)
            {
                if (ball != null && (ball.CurrentOwner == this || _incomingPassAssistTimer > 0f))
                {
                    // 패스를 받아 자동 전환되었거나 이미 볼을 소유 중인 경우:
                    // 카메라가 등 뒤(180도 반대)로 휙 돌아가 WASD 이동 방향이 꼬이지 않도록 공격 골문 방향(또는 전방 대각선) 유지!
                    Vector3 attackDir = Vector3.right * attackingGoalSign;
                    if (ball.CurrentOwner != this)
                    {
                        Vector3 toBall = ball.transform.position - transform.position;
                        toBall.y = 0f;
                        if (toBall.sqrMagnitude > 0.04f && Vector3.Dot(toBall.normalized, attackDir) > -0.15f)
                            attackDir = (attackDir * 1.35f + toBall.normalized * 0.45f).normalized;
                    }
                    AlignFirstPersonToDirection(attackDir, 13.5f);
                }
                else if (ball != null)
                {
                    Vector3 toBall = ball.transform.position - transform.position;
                    toBall.y = 0f;
                    if (toBall.sqrMagnitude > 0.04f)
                        AlignFirstPersonToDirection(toBall.normalized, 12.5f);
                    else
                        AlignFirstPersonToDirection(Vector3.right * attackingGoalSign, 13.0f);
                }
            }
        }

        /// <summary>
        /// 다른 동료에게 조작권을 넘기고 AI 자동 플레이로 전환될 때 호출
        /// </summary>
        public void OnSwitchedToAI()
        {
            isAI = true;
            _isChargingShot = false;
            _shotCharge01 = 0f;
            _isDraggingCurveShot = false;
            _dragCursorOffset = Vector2.zero;
            _peakSwipeVelX = 0f;
            _circularSpinMomentum = 0f;
            _blitzDashTimer = 0f;
            CurrentAimTargetType = AimTargetType.None;
            CurrentAimTargetPlayer = null;
            BlitzDashCandidateTarget = null;

            if (_groundBallArrowRoot != null)
                _groundBallArrowRoot.gameObject.SetActive(false);

            if (visualRig != null)
                visualRig.SetFirstPersonViewMode(false);
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
            _isDraggingCurveShot = false;
            _dragCursorOffset = Vector2.zero;
            _peakSwipeVelX = 0f;
            _circularSpinMomentum = 0f;
            _dragDuration = 0f;
            _jumpHeightOffset = 0f;
            _jumpVerticalVel = 0f;
            _spaceDoubleTapWindow = 0f;
            _blitzDashTimer = 0f;
            _blitzDashTargetPlayer = null;
            _dashStealCooldown = 0f;
            _shoulderBargeCooldown = 0f;
            _isInAcrobaticRoutine = false;
            _tackleCooldown = 0f;
            _stunTimer = 0f;
            _flickCooldown = 0f;
            _wallJumpTimer = 0f;
            _wallBlockWindowTimer = 0f;
            _aiBallHoldTimer = 0f;
            _passCooldown = 0f;
            _aiEvasiveDribbleTimer = 0f;
            _aiEvasiveDribbleCooldown = 0f;
            _aiEvasiveDribbleDir = Vector3.zero;
            _hasIncomingCross = false;
            _incomingCrossTimer = 0f;
            _incomingPassAssistTimer = 0f;
            _incomingPassSender = null;
            _autoSwitchToMeOnTouch = false;
            _qInfiltrateRunTimer = 0f;
            LastIntendedReceiver = null;
            IsSprintingNow = false;
            IsHeadering = false;

            if (!isAI)
            {
                EnsureGroundBallArrow();
                AlignFirstPersonToDirection(Vector3.right * attackingGoalSign, 14.5f);
            }
            else if (_groundBallArrowRoot != null)
            {
                _groundBallArrowRoot.gameObject.SetActive(false);
            }

            if (visualRig != null)
            {
                visualRig.ResetToIdle();
                visualRig.SetChargingFootAura(false, PreferredFoot.Right);
            }
        }

        public void AlignFirstPersonToDirection(Vector3 worldDir, float eulerPitch = 14.5f)
        {
            Vector3 flat = new Vector3(worldDir.x, 0f, worldDir.z);
            if (flat.sqrMagnitude > 0.0001f)
            {
                Quaternion q = Quaternion.LookRotation(flat.normalized, Vector3.up);
                FirstPersonYaw = q.eulerAngles.y;
                transform.rotation = q;
            }
            FirstPersonEulerPitch = Mathf.Clamp(eulerPitch, -48f, 56f);
        }

        public Quaternion GetFirstPersonLookRotation()
        {
            return Quaternion.Euler(FirstPersonEulerPitch, FirstPersonYaw, 0f);
        }

        public Vector3 GetFirstPersonAimDirection()
        {
            if (DynamicSoccerCamera.Instance != null && DynamicSoccerCamera.Instance.UseThirdPersonBroadcast && !isAI)
            {
                Vector2 mi = ReadMoveInput();
                if (mi.sqrMagnitude > 0.04f)
                    return new Vector3(mi.x, 0f, mi.y).normalized;

                Camera cam = Camera.main;
                if (cam != null && Mouse.current != null && Cursor.lockState != CursorLockMode.Locked)
                {
                    Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
                    Plane ground = new Plane(Vector3.up, Vector3.zero);
                    if (ground.Raycast(ray, out float enter) && enter > 0.1f)
                    {
                        Vector3 toHit = ray.GetPoint(enter) - transform.position;
                        toHit.y = 0f;
                        if (toHit.sqrMagnitude > 0.36f)
                            return toHit.normalized;
                    }
                }
                return transform.forward;
            }
            return GetFirstPersonLookRotation() * Vector3.forward;
        }

        public Vector3 GetFirstPersonEyePosition()
        {
            Vector3 flatFwd = Quaternion.Euler(0f, FirstPersonYaw, 0f) * Vector3.forward;
            if (visualRig != null)
                return visualRig.GetFirstPersonEyeWorldPosition(flatFwd);
            float hScale = Profile != null ? Profile.HeightScale : 1f;
            return transform.position + Vector3.up * (1.54f * hScale) + flatFwd * 0.16f;
        }

        public void PlayGoalCelebration()
        {
            _planarVelocity = Vector3.zero;
            if (visualRig != null)
            {
                var styles = (PlayerVisualRig.CelebrationStyle[])System.Enum.GetValues(typeof(PlayerVisualRig.CelebrationStyle));
                var chosen = styles[UnityEngine.Random.Range(0, styles.Length)];
                visualRig.TriggerCelebration(3.0f, chosen);
            }
        }

        public void PlayFoulFall(Vector3 pushVel)
        {
            _isChargingShot = false;
            _shotCharge01 = 0f;
            _isDraggingCurveShot = false;
            _dragCursorOffset = Vector2.zero;
            _peakSwipeVelX = 0f;
            _circularSpinMomentum = 0f;
            _dragDuration = 0f;
            _blitzDashTimer = 0f;
            _jumpHeightOffset = 0f;
            _jumpVerticalVel = 0f;
            _planarVelocity = new Vector3(pushVel.x, 0f, pushVel.z) * 0.45f;
            _stunTimer = 1.45f;
            if (visualRig != null)
            {
                visualRig.SetJumpHeightOffset(0f);
                visualRig.TriggerFoulFall(1.42f);
            }
        }

        public void TriggerDefensiveWallJump()
        {
            if (_wallJumpTimer > 0.1f) return;
            _wallJumpTimer = 0.62f;
            _jumpVerticalVel = 5.8f;
            if (visualRig != null)
                visualRig.TriggerWallJump(0.62f);
        }

        public void ActivateWallBlockCheck(float duration = 0.58f)
        {
            _wallBlockWindowTimer = duration;
        }

        public void ExecuteFreeKickStrike(Vector3 aimedGoalPoint, bool isCurveShot, float charge01, float customCurveBias = 0f)
        {
            if (ball == null) return;

            Vector3 goalCenter = new Vector3(attackingGoalSign * ShotCalculator.PitchHalfLength, 0f, 0f);
            Vector3 toGoal = (goalCenter - transform.position);
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(toGoal.normalized);
                if (!isAI)
                    FirstPersonYaw = transform.rotation.eulerAngles.y;
            }

            ComputedShotResult result = ShotCalculator.ComputeFreeKickShot(
                this, ball, attackingGoalSign, aimedGoalPoint, isCurveShot, charge01, customCurveBias);

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
            _isDraggingCurveShot = false;
            _dragCursorOffset = Vector2.zero;
            _peakSwipeVelX = 0f;
            _circularSpinMomentum = 0f;
            _blitzDashTimer = 0f;
            _dragDuration = 0f;
            if (visualRig != null)
                visualRig.TriggerStumble(Mathf.Clamp(stunDuration, 0.32f, 0.52f));
        }

        /// <summary>
        /// 프리킥 세트피스 중 키커(인간 플레이어)의 자유로운 360도 마우스 시선 회전 및 포켓몬고 드래그 트래킹 지원
        /// </summary>
        public void UpdateFreeKickHumanLookAndAim(float dt, bool lockLookForCurveDrag)
        {
            if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null && !lockLookForCurveDrag)
            {
                Vector2 md = Mouse.current.delta.ReadValue();
                if (md.sqrMagnitude > 0.0001f)
                {
                    FirstPersonYaw += md.x * 0.14f;
                    FirstPersonEulerPitch = Mathf.Clamp(FirstPersonEulerPitch - md.y * 0.14f, -45f, 52f);
                }
            }

            if (ReadBallFocusHeld() && ball != null)
            {
                Vector3 toGoal = new Vector3(attackingGoalSign * ShotCalculator.PitchHalfLength, 1.4f, 0f) - GetFirstPersonEyePosition();
                Vector3 flat = new Vector3(toGoal.x, 0f, toGoal.z);
                if (flat.sqrMagnitude > 0.01f)
                    FirstPersonYaw = Mathf.LerpAngle(FirstPersonYaw, Quaternion.LookRotation(flat.normalized).eulerAngles.y, dt * 10f);
            }

            transform.rotation = Quaternion.Euler(0f, FirstPersonYaw, 0f);
            UpdateAimTarget();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_tackleCooldown > 0f) _tackleCooldown -= dt;
            if (_flickCooldown > 0f) _flickCooldown -= dt;
            if (_passCooldown > 0f) _passCooldown -= dt;
            if (_aiEvasiveDribbleTimer > 0f) _aiEvasiveDribbleTimer -= dt;
            if (_aiEvasiveDribbleCooldown > 0f) _aiEvasiveDribbleCooldown -= dt;
            if (_dashStealCooldown > 0f) _dashStealCooldown -= dt;
            if (_shoulderBargeCooldown > 0f) _shoulderBargeCooldown -= dt;
            if (_spaceDoubleTapWindow > 0f) _spaceDoubleTapWindow -= Time.unscaledDeltaTime;
            if (_incomingCrossTimer > 0f)
            {
                _incomingCrossTimer -= dt;
                if (_incomingCrossTimer <= 0f)
                    _hasIncomingCross = false;
            }
            if (_incomingPassAssistTimer > 0f)
            {
                _incomingPassAssistTimer -= dt;
                if (_incomingPassAssistTimer <= 0f)
                {
                    _incomingPassSender = null;
                    _autoSwitchToMeOnTouch = false;
                }
            }
            if (_qInfiltrateRunTimer > 0f) _qInfiltrateRunTimer -= dt;
            if (_wallJumpTimer > 0f) _wallJumpTimer -= dt;
            if (_wallBlockWindowTimer > 0f)
            {
                _wallBlockWindowTimer -= dt;
                CheckWallDeflection();
            }

            UpdateVerticalJumpPhysics(dt);
            RefreshNearestOpponent();
            UpdateAimTarget();
            UpdateGroundBallArrowVisual();
            if (!isAI && visualRig != null)
                visualRig.UpdateControlDirectionArrow(ReadMoveInput(), transform.forward);

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

            // 실시간 오버헤드킥 / 아크로바틱 가능 자세 체크 (1인칭 플레이어는 마우스 에임이 공에 가 있을 때만 발동!)
            Vector3 goalCenter = new Vector3(attackingGoalSign * ShotCalculator.PitchHalfLength, 0f, 0f);
            bool holdingAcroMod = !isAI && (_jumpHeightOffset > 0.25f || ReadHoldAcrobaticPosture());
            IsAimingAtBall = true;
            bool inRangeAny = ShotCalculator.CheckAcrobaticOpportunity(this, ball, goalCenter, holdingAcroMod, out ShotType acroType, requireMouseAimOnBall: false);
            CanDoAcrobaticNow = inRangeAny;
            IsBallInAcrobaticRangeWithoutAim = false;
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
            ResolveBodyJostleWithAllPlayers(dt);
        }

        private void UpdateVerticalJumpPhysics(float dt)
        {
            if (_jumpHeightOffset > 0f || _jumpVerticalVel != 0f)
            {
                _jumpHeightOffset += _jumpVerticalVel * dt;
                _jumpVerticalVel -= 20.5f * dt;

                if (_jumpHeightOffset <= 0f)
                {
                    _jumpHeightOffset = 0f;
                    _jumpVerticalVel = 0f;
                }
            }

            if (visualRig != null)
                visualRig.SetJumpHeightOffset(_jumpHeightOffset);

            // 점프 중 공중 패스/크로스 자동 점프 인터셉트 (슈팅이나 아크로바틱 동작 중이 아닐 때)
            if (_controlsEnabled && _jumpHeightOffset > 0.22f && !_isInAcrobaticRoutine && !_isChargingShot &&
                ball != null && ball.CurrentOwner == null && ball.CanBeClaimedBy(this))
            {
                float h = ball.HeightAboveGround;
                if (h >= 0.72f && h <= 2.95f && ball.Rb.linearVelocity.magnitude < 27.5f && ball.LastTouchPlayer != this)
                {
                    float planarDist = Vector2.Distance(
                        new Vector2(transform.position.x, transform.position.z),
                        new Vector2(ball.transform.position.x, ball.transform.position.z));

                    if (planarDist <= 1.38f)
                    {
                        SoccerPlayerController prevLastTouch = ball.LastTouchPlayer;
                        bool interceptedOpponent = prevLastTouch != null && prevLastTouch.attackingGoalSign != attackingGoalSign;
                        bool shouldSwitchOnTouch = isAI && (
                            _autoSwitchToMeOnTouch ||
                            (prevLastTouch != null && !prevLastTouch.isAI && prevLastTouch != this && prevLastTouch.attackingGoalSign == attackingGoalSign));

                        _incomingPassAssistTimer = 0f;
                        _incomingPassSender = null;
                        _autoSwitchToMeOnTouch = false;
                        ball.SetOwner(this);

                        if (shouldSwitchOnTouch)
                        {
                            OnHumanPassExecuted?.Invoke(this);
                            string myName = Profile != null ? Profile.playerName : "동료 선수";
                            OnShotFeedback?.Invoke($"⚽ [패스 연결 완료!] {myName} 선수가 공을 이어받아 조작 전환!", false);
                        }
                        else if (!isAI && interceptedOpponent)
                        {
                            OnShotFeedback?.Invoke("🦘 [점프 인터셉트!] 공중볼을 점프로 깔끔하게 낚아챘습니다!", false);
                        }
                    }
                }
            }
        }

        public void TriggerManualJump()
        {
            if (_jumpHeightOffset > 0.08f) return;
            float acroNorm = Profile != null ? Mathf.InverseLerp(45f, 99f, Profile.acrobatics) : 0.6f;
            _jumpVerticalVel = Mathf.Lerp(6.1f, 7.0f, acroNorm);
        }

        /// <summary>
        /// 1인칭 플레이어의 마우스 에임(화면 중앙 조준선)이 현재 공 위에 정확히 올라가 있는지 판정
        /// </summary>
        public bool CheckMouseAimOnBall()
        {
            if (isAI) return true;
            if (ball == null) return false;

            Vector3 eye = GetFirstPersonEyePosition();
            Vector3 aimDir = GetFirstPersonAimDirection();
            if (aimDir.sqrMagnitude < 0.0001f)
                return false;
            aimDir.Normalize();

            Vector3 toBall = ball.transform.position - eye;
            float dist3D = toBall.magnitude;
            if (dist3D < 0.05f)
                return true;

            // 공이 카메라 정면 시야 쪽에 있어야 함 (등 뒤나 옆이면 발동 안 됨)
            float proj = Vector3.Dot(toBall, aimDir);
            if (proj <= 0.05f)
                return false;

            Vector3 closestOnRay = eye + aimDir * proj;
            float perpDist = Vector3.Distance(closestOnRay, ball.transform.position);
            float angleDeg = Vector3.Angle(aimDir, toBall);

            // 공 반지름(0.35m) + 공중 타격 에임 판정 반경(총 0.58~0.72m) 이내에 마우스 에임이 들어왔을 때만 true!
            float allowedRadius = Mathf.Lerp(0.72f, 0.56f, Mathf.InverseLerp(1.2f, 4.0f, dist3D));
            return perpDist <= allowedRadius && angleDeg <= 19.5f;
        }

        public bool CanDoJumpHeaderNow()
        {
            if (ball == null || ball.CurrentOwner == this) return false;

            float hScale = Profile != null ? Profile.HeightScale : 1f;
            float headY = 1.54f * hScale;
            float h = ball.HeightAboveGround;
            if (Mathf.Abs(h - headY) > 0.52f) return false;

            float planarDist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(ball.transform.position.x, ball.transform.position.z));

            return planarDist <= 1.80f;
        }

        public void ExecuteHeaderShot()
        {
            if (ball == null) return;

            _isChargingShot = false;
            _isDraggingCurveShot = false;
            _hasIncomingCross = false;
            _incomingCrossTimer = 0f;
            IsHeadering = true;

            if (visualRig != null)
                visualRig.TriggerHeader(0.56f);

            Vector3 goalCenter = new Vector3(attackingGoalSign * ShotCalculator.PitchHalfLength, 0f, 0f);
            Vector3 toGoal = goalCenter - transform.position;
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude > 0.01f && isAI)
                transform.rotation = Quaternion.LookRotation(toGoal.normalized, Vector3.up);

            ComputedShotResult result = ShotCalculator.ComputeHeaderShot(this, ball, targetGoalkeeper, attackingGoalSign);
            ball.LaunchShot(
                result.metadata,
                result.launchVelocity,
                result.magnusAcceleration,
                result.curveDuration,
                result.spinAngularVelocity);

            if (ImpactVfxManager.Instance != null)
            {
                ImpactVfxManager.Instance.TriggerShotImpactVfx(
                    ball.transform.position,
                    ball,
                    result.launchVelocity.normalized,
                    result.metadata.vfxType,
                    isAcrobatic: true);
            }

            if (DynamicSoccerCamera.Instance != null)
                DynamicSoccerCamera.Instance.TriggerImpactShake(isAI ? 0.20f : 0.30f, 0.26f);

            if (GoalCelebrationManager.Instance != null)
                GoalCelebrationManager.Instance.TriggerBriefImpactBloom(1.30f);

            OnShotFeedback?.Invoke(result.feedbackSummary, false);
            IsHeadering = false;
        }

        /// <summary>
        /// 스페이스 두 번(Space × 2): 순식간에 돌진해서 공을 뺏는 전광석화 돌진 탈취(Blitz Dash Steal)!
        /// </summary>
        private void StartBlitzDashSteal()
        {
            if (ball == null) return;

            _dashStealCooldown = 2.15f;
            _jumpHeightOffset = 0f;
            _jumpVerticalVel = 0f;
            if (visualRig != null)
                visualRig.SetJumpHeightOffset(0f);

            _blitzDashTargetPlayer = null;
            Vector3 targetWorld = transform.position + transform.forward * 4.2f;

            // 1순위: 조준한 상대 / 돌진 후보 상대 (과도한 워프 방지를 위해 합리적 사거리 8.2m로 조정)
            SoccerPlayerController candOpp = (CurrentAimTargetType == AimTargetType.Opponent && CurrentAimTargetPlayer != null)
                ? CurrentAimTargetPlayer
                : BlitzDashCandidateTarget;

            if (candOpp != null && Vector3.Distance(transform.position, candOpp.transform.position) <= 8.2f &&
                (ball.CurrentOwner == candOpp || Vector3.Distance(candOpp.transform.position, ball.transform.position) <= 3.0f))
            {
                _blitzDashTargetPlayer = candOpp;
                targetWorld = candOpp.transform.position;
            }
            else if (ball.CurrentOwner != null && ball.CurrentOwner.attackingGoalSign != attackingGoalSign &&
                     Vector3.Distance(transform.position, ball.CurrentOwner.transform.position) <= 8.2f)
            {
                // 2순위: 현재 볼을 소유한 상대 선수에게 돌진!
                _blitzDashTargetPlayer = ball.CurrentOwner;
                targetWorld = ball.CurrentOwner.transform.position;
            }
            else if (ball.CurrentOwner == null && Vector3.Distance(transform.position, ball.transform.position) <= 8.2f)
            {
                // 3순위: 루즈볼을 향해 돌진!
                targetWorld = ball.transform.position;
            }

            Vector3 dir = targetWorld - transform.position;
            dir.y = 0f;
            float dist = dir.magnitude;
            if (dist < 0.05f)
            {
                dir = transform.forward;
                dist = 3.8f;
            }
            else
            {
                dir /= dist;
            }

            float baseTop = Profile != null ? Profile.EffectiveTopSpeed : 8.0f;
            _jumpHeightOffset = 0f;
            _jumpVerticalVel = 0f;
            _blitzDashDir = dir;
            _blitzDashTimer = 0.30f;
            _blitzDashSpeed = Mathf.Clamp(baseTop * 1.62f, 12.8f, 15.6f);

            if (!isAI)
            {
                float dashYaw = Quaternion.LookRotation(_blitzDashDir, Vector3.up).eulerAngles.y;
                FirstPersonYaw = Mathf.LerpAngle(FirstPersonYaw, dashYaw, 0.72f);
                transform.rotation = Quaternion.Euler(0f, FirstPersonYaw, 0f);
            }

            if (visualRig != null)
            {
                visualRig.SetJumpHeightOffset(0f);
                visualRig.TriggerBlitzDash(0.30f);
            }

            if (DynamicSoccerCamera.Instance != null)
                DynamicSoccerCamera.Instance.TriggerBlitzDashFov(0.24f);

            if (ImpactVfxManager.Instance != null)
                ImpactVfxManager.Instance.TriggerDashBurstVfx(transform.position + Vector3.up * 0.65f, _blitzDashDir, isHeavyBlitz: true);
        }

        private void UpdateActiveBlitzDash(float dt)
        {
            _blitzDashTimer -= dt;

            // 이동 중 실시간으로 공 또는 볼 소유자 위치 추적 보정!
            if (ball != null)
            {
                Vector3 chasePoint = (_blitzDashTargetPlayer != null && ball.CurrentOwner == _blitzDashTargetPlayer)
                    ? _blitzDashTargetPlayer.transform.position
                    : ball.transform.position;

                Vector3 toTarget = chasePoint - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.04f)
                {
                    float trackRate = (_blitzDashTargetPlayer != null && _blitzDashTargetPlayer.IsPerformingEvasiveDribble) ? 3.2f : 6.5f;
                    _blitzDashDir = Vector3.Slerp(_blitzDashDir, toTarget.normalized, dt * trackRate).normalized;
                    if (!isAI)
                    {
                        float wantYaw = Quaternion.LookRotation(_blitzDashDir, Vector3.up).eulerAngles.y;
                        FirstPersonYaw = Mathf.LerpAngle(FirstPersonYaw, wantYaw, dt * 12f);
                        transform.rotation = Quaternion.Euler(0f, FirstPersonYaw, 0f);
                    }
                }
            }

            float baseTop = Profile != null ? Profile.EffectiveTopSpeed : 8.0f;
            float dashProgress = Mathf.Clamp01(_blitzDashTimer / 0.30f);
            float currentDashSpeed = Mathf.Lerp(baseTop * 0.92f, _blitzDashSpeed, dashProgress);

            _planarVelocity = _blitzDashDir * currentDashSpeed;
            MovePlanarWithBounds(_planarVelocity * dt);

            if (visualRig != null)
                visualRig.UpdateProceduralAnimation(_planarVelocity, true, dt);

            if (ball == null) return;

            float planarDistToBall = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(ball.transform.position.x, ball.transform.position.z));

            SoccerPlayerController prevOwner = ball.CurrentOwner;
            float stealRadius = (prevOwner != null && prevOwner.IsPerformingEvasiveDribble) ? 1.08f : 1.62f;

            // 돌진 도중 공 반경 내 진입 시 볼 탈취 (단, 상대가 순간 탈압박 드리블로 꺾어 피하면 빗나갈 수 있음!)
            if (planarDistToBall <= stealRadius && ball.HeightAboveGround <= 2.15f)
            {
                if (prevOwner != null && prevOwner.attackingGoalSign == attackingGoalSign)
                    return; // 같은 팀 동료의 공은 뺏지 않음

                string stolenFromName = "루즈볼";
                if (prevOwner != null && prevOwner != this)
                {
                    stolenFromName = prevOwner.Profile != null ? prevOwner.Profile.playerName : "상대 선수";
                    prevOwner.ApplyBriefStumble(_blitzDashDir * 3.8f + transform.right * 1.2f, 0.56f);
                    ball.ReleaseFromOwner(prevOwner, 0.65f);
                }

                ball.SetOwner(this);
                _blitzDashTimer = 0f;

                // 탈취 직후 상대 골문을 향해 자연스럽게 치고 나갈 수 있도록 탄력 유지
                float exitSpeed = Profile != null ? Profile.EffectiveTopSpeed * 0.92f : 7.8f;
                _planarVelocity = transform.forward * exitSpeed;

                if (ImpactVfxManager.Instance != null)
                    ImpactVfxManager.Instance.TriggerDashBurstVfx(ball.transform.position + Vector3.up * 0.4f, transform.forward, isHeavyBlitz: true);

                if (DynamicSoccerCamera.Instance != null)
                    DynamicSoccerCamera.Instance.TriggerImpactShake(0.24f, 0.22f);

                if (!isAI)
                {
                    string msg = prevOwner != null
                        ? $"⚡ [순간 돌진 탈취!!] 전광석화 대시로 {stolenFromName} 선수의 공을 순식간에 뺏었습니다!"
                        : "⚡ [순간 돌진 회수!] 초고속 대시로 루즈볼을 단숨에 선점했습니다!";
                    OnShotFeedback?.Invoke(msg, false);
                }
            }
        }

        /// <summary>
        /// 태클 외 추가 탈취 수단 [F키]: 어깨 밀치기 / 피지컬 몸싸움 볼 탈취 (반칙 위험 없는 정당한 바디체크!)
        /// </summary>
        private void PerformShoulderBargeSteal()
        {
            if (ball == null) return;
            _shoulderBargeCooldown = 1.05f;

            SoccerPlayerController targetOpp = (CurrentAimTargetType == AimTargetType.Opponent && CurrentAimTargetPlayer != null)
                ? CurrentAimTargetPlayer
                : opponent;

            Vector3 pressDir = transform.forward;
            float sideSign = 1f;
            if (targetOpp != null)
            {
                Vector3 toOpp = targetOpp.transform.position - transform.position;
                toOpp.y = 0f;
                if (toOpp.magnitude <= 4.2f && toOpp.sqrMagnitude > 0.01f)
                {
                    pressDir = toOpp.normalized;
                    sideSign = Vector3.Dot(pressDir, transform.right) >= 0f ? 1f : -1f;
                    if (!isAI)
                    {
                        float yaw = Quaternion.LookRotation(pressDir, Vector3.up).eulerAngles.y;
                        FirstPersonYaw = Mathf.LerpAngle(FirstPersonYaw, yaw, 0.50f);
                        transform.rotation = Quaternion.Euler(0f, FirstPersonYaw, 0f);
                    }
                }
            }

            _planarVelocity = pressDir * 8.6f;
            if (visualRig != null)
                visualRig.TriggerShoulderBarge(sideSign, 0.30f);

            float planarDistToBall = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(ball.transform.position.x, ball.transform.position.z));

            SoccerPlayerController owner = ball.CurrentOwner;
            if (owner != null && owner != this && owner.attackingGoalSign != attackingGoalSign)
            {
                float distToCarrier = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(owner.transform.position.x, owner.transform.position.z));

                if (distToCarrier <= 2.25f)
                {
                    float myDefStat = Profile != null ? (Profile.defense * 0.60f + Profile.physical * 0.40f) : 76f;
                    float myStrength = myDefStat + (Profile != null ? (Profile.weightKg - 75f) * 0.45f : 0f);
                    float oppStrength = (owner.Profile != null ? (owner.Profile.physical * 0.65f + owner.Profile.agility * 0.35f) : 74f) + (owner.Profile != null ? (owner.Profile.weightKg - 75f) * 0.45f : 0f);
                    float evasivePenalty = owner.IsPerformingEvasiveDribble ? 0.24f : 0f;
                    float winProb = Mathf.Clamp(0.58f + (myStrength - oppStrength) * 0.010f - evasivePenalty, 0.26f, 0.90f);

                    if (UnityEngine.Random.value <= winProb)
                    {
                        string oppName = owner.Profile != null ? owner.Profile.playerName : "상대 선수";
                        owner.ApplyBriefStumble(pressDir * 3.4f, 0.48f);
                        ball.ReleaseFromOwner(owner, 0.55f);
                        ball.SetOwner(this);

                        if (ImpactVfxManager.Instance != null)
                            ImpactVfxManager.Instance.TriggerDashBurstVfx(transform.position + pressDir * 0.7f + Vector3.up * 0.8f, pressDir, isHeavyBlitz: false);

                        if (DynamicSoccerCamera.Instance != null)
                            DynamicSoccerCamera.Instance.TriggerImpactShake(0.20f, 0.18f);

                        if (!isAI)
                            OnShotFeedback?.Invoke($"🛡️ [어깨 몸싸움 탈취!] 정당한 피지컬 경합으로 {oppName}의 볼을 깔끔하게 뺏었습니다!", false);
                    }
                    else
                    {
                        owner.ApplyBriefStumble(pressDir * 1.4f, 0.14f);
                        if (!isAI)
                            OnShotFeedback?.Invoke("💢 몸싸움 경합! 상대 버티기가 강합니다 — [Space×2] 순간 돌진을 노려보세요!", false);
                    }
                    return;
                }
            }
            else if (owner == null && planarDistToBall <= 2.35f && ball.HeightAboveGround <= 1.5f)
            {
                ball.SetOwner(this);
                if (!isAI)
                    OnShotFeedback?.Invoke("🛡️ 몸을 던진 경합으로 루즈볼을 차지했습니다!", false);
            }
        }

        /// <summary>
        /// 포켓몬고 볼 스핀 / 드래그 제스처 기반 커브 바이어스 산출 (-1.55 좌측 감기 ~ +1.55 우측 감기, 0 = 직선 파워슛)
        /// </summary>
        public float ComputeCurrentDragCurveBias()
        {
            if (!_isDraggingCurveShot) return 0f;

            float absDispX = Mathf.Abs(_dragCursorOffset.x);
            float absVelX = Mathf.Abs(_peakSwipeVelX);
            float absSpin = Mathf.Abs(_circularSpinMomentum);

            // 1) 좌우로 빠르게 드래그/스와이프한 경우 또는 20px 이상 옆으로 당긴 경우 또는 포켓몬고처럼 원을 그리며 스핀을 준 경우!
            bool triggeredBySwipe = absDispX >= 18f || absVelX >= 280f;
            bool triggeredByCircleSpin = absSpin >= 850f;

            if (!triggeredBySwipe && !triggeredByCircleSpin)
                return 0f;

            float dirSign;
            if (absDispX >= 14f)
                dirSign = Mathf.Sign(_dragCursorOffset.x);
            else if (absVelX >= 200f)
                dirSign = Mathf.Sign(_peakSwipeVelX);
            else
                dirSign = Mathf.Sign(_circularSpinMomentum);

            float dispStrength = Mathf.InverseLerp(16f, 95f, absDispX);
            float velStrength = Mathf.InverseLerp(220f, 1350f, absVelX);
            float spinStrength = Mathf.InverseLerp(600f, 3200f, absSpin);

            float combined01 = Mathf.Clamp01(Mathf.Max(dispStrength, velStrength * 0.95f) + spinStrength * 0.35f);
            return dirSign * Mathf.Lerp(0.82f, 1.55f, combined01);
        }

        public void BeginExternalCurveDragTracking()
        {
            _isDraggingCurveShot = true;
            _dragCursorOffset = Vector2.zero;
            _peakSwipeVelX = 0f;
            _circularSpinMomentum = 0f;
            _dragDuration = 0f;
        }

        public void UpdateExternalCurveDragTracking(float dt)
        {
            if (!_isDraggingCurveShot) return;
            _dragDuration += dt;
            _peakSwipeVelX = Mathf.MoveTowards(_peakSwipeVelX, 0f, dt * 1600f);

            if (Mouse.current != null)
            {
                Vector2 md = Mouse.current.delta.ReadValue();
                Vector2 prev = _dragCursorOffset;
                _dragCursorOffset += md;
                if (_dragCursorOffset.magnitude > 170f)
                    _dragCursorOffset = _dragCursorOffset.normalized * 170f;

                float instVelX = md.x / Mathf.Max(0.004f, dt);
                if (Mathf.Abs(instVelX) > Mathf.Abs(_peakSwipeVelX))
                    _peakSwipeVelX = instVelX;

                // 포켓몬고 몬스터볼 원형 스핀 감지 (외적 누적)
                float crossZ = prev.x * _dragCursorOffset.y - prev.y * _dragCursorOffset.x;
                _circularSpinMomentum = Mathf.Clamp(_circularSpinMomentum * 0.96f + crossZ * 0.35f, -4500f, 4500f);
            }
        }

        public float EndExternalCurveDragTracking()
        {
            float bias = ComputeCurrentDragCurveBias();
            _isDraggingCurveShot = false;
            _dragCursorOffset = Vector2.zero;
            _peakSwipeVelX = 0f;
            _circularSpinMomentum = 0f;
            return bias;
        }

        private void UpdateHumanInput(float dt, Vector3 goalCenter)
        {
            // 순간 돌진 탈취 진행 중에는 초고속 대시 물리 우선 수행!
            if (_blitzDashTimer > 0f)
            {
                UpdateActiveBlitzDash(dt);
                return;
            }

            Vector2 moveInput = ReadMoveInput();
            bool sprinting = ReadSprint(); // SHIFT 키 = 달리기(질주)

            bool shootPressed = ReadShootPressedThisFrame();
            bool shootHeld = ReadShootHeld();
            bool shootReleased = ReadShootReleasedThisFrame();

            bool passPressed = ReadPassPressedThisFrame();
            bool throughWPressed = ReadThroughPassWPressedThisFrame();
            bool lobAPressed = ReadLobAPressedThisFrame();
            bool tackleRPressed = ReadTackleRPressedThisFrame();
            Keyboard kbCur = Keyboard.current;
            bool qHeld = kbCur != null && kbCur.qKey.isPressed;
            if (kbCur != null && kbCur.qKey.wasPressedThisFrame)
                _qUsedAsModifier = false;
            bool flickQReleasedUnused = kbCur != null && kbCur.qKey.wasReleasedThisFrame && !_qUsedAsModifier;
            bool knockShiftHeld = ReadKnockOnBurstHeld();            bool spacePressed = ReadJumpOrDashSpacePressedThisFrame(); // Space 단타 = 점프 / 2연타 = 순간 돌진 탈취!
            bool shoulderBargePressed = ReadShoulderBargePressedThisFrame(); // F키 = 어깨 몸싸움 볼 탈취!

            // 1. 1인칭 시점 회전(마우스 360도 프리룩 / 드래그 감아차기 중 시점 고정) + WASD 체감 이동!
            bool isBroadcastView = DynamicSoccerCamera.Instance != null && DynamicSoccerCamera.Instance.UseThirdPersonBroadcast;
            if (isBroadcastView)
                ApplyThirdPersonBroadcastMovement(moveInput, sprinting, dt);
            else
                ApplyFirstPersonMovementAndLook(moveInput, sprinting, dt);

            bool hasBall = ball != null && ball.CurrentOwner == this;
            float groundDistToBall = ball != null
                ? Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(ball.transform.position.x, ball.transform.position.z))
                : 999f;
            bool nearPlayableGroundBall = ball != null && ball.CurrentOwner == null && !ball.IsAirborne && groundDistToBall <= 2.05f;

            // 2. Space바: 일반 Space = 점프! / 빠르게 Space 두 번(Space×2) = 순식간에 돌진해서 공 뺏기!!
            bool offBallOrDefense = !hasBall && !nearPlayableGroundBall && !CanDoAcrobaticNow && !CanDoJumpHeaderNow();
            if (offBallOrDefense && _tackleCooldown <= 0f && (spacePressed || shootPressed || lobAPressed || tackleRPressed))
            {
                bool useSlide = lobAPressed || ((spacePressed || tackleRPressed) && sprinting);
                SoccerPlayerController targetOpp = (CurrentAimTargetType == AimTargetType.Opponent && CurrentAimTargetPlayer != null)
                    ? CurrentAimTargetPlayer
                    : opponent;
                PerformTackle(isSlide: useSlide, explicitTarget: targetOpp);
                return;
            }

            // 3. F키: 태클 외 추가 볼 탈취 수단 — 어깨 밀치기 / 피지컬 바디체크 탈취!
            if (shoulderBargePressed && !hasBall && _shoulderBargeCooldown <= 0f)
            {
                PerformShoulderBargeSteal();
                return;
            }

            // 4. Q키 (단타): 볼 띄우기 없이 전방 동료 침투(Infiltrate Run)만 지시! (Q+W 조합 시 로빙 스루패스 유지)
            if (flickQReleasedUnused && !passPressed && !throughWPressed && !lobAPressed && _flickCooldown <= 0f && (hasBall || nearPlayableGroundBall))
            {
                _flickCooldown = 0.40f;
                TriggerTeammateInfiltrationRun(moveInput);
            }
            if (knockShiftHeld && (hasBall || nearPlayableGroundBall) && _flickCooldown <= 0f)
            {
                _wasEPressedInPossession = true;
                _eHoldTime = Mathf.Min(_eHoldTime + dt, 0.65f);
            }
            else if (_wasEPressedInPossession)
            {
                if ((hasBall || nearPlayableGroundBall) && _flickCooldown <= 0f)
                {
                    float charge01 = Mathf.Clamp01(_eHoldTime / 0.65f);
                    _flickCooldown = 0.50f;
                    Vector3 burstDir;
                    if (isBroadcastView)
                    {
                        burstDir = moveInput.sqrMagnitude > 0.04f
                            ? new Vector3(moveInput.x, 0f, moveInput.y).normalized
                            : transform.forward;
                    }
                    else
                    {
                        burstDir = (Mathf.Abs(moveInput.y) > 0.1f || Mathf.Abs(moveInput.x) > 0.1f)
                            ? (transform.forward * Mathf.Max(0.25f, moveInput.y) + transform.right * (moveInput.x * 0.45f)).normalized
                            : transform.forward;
                    }
                    if (burstDir.sqrMagnitude < 0.01f) burstDir = transform.forward;

                    float knockBallSpeed = Mathf.Lerp(9.8f, 15.6f, charge01);
                    float lockoutDur = Mathf.Lerp(0.24f, 0.44f, charge01);
                    ball.ReleaseFromOwner(this, lockoutDur);
                    ball.Rb.linearVelocity = burstDir * knockBallSpeed + Vector3.up * Mathf.Lerp(0.35f, 0.72f, charge01);
                    _planarVelocity = burstDir * (Profile.EffectiveTopSpeed * Mathf.Lerp(1.06f, 1.24f, charge01));
                }
                _eHoldTime = 0f;
                _wasEPressedInPossession = false;
            }
            else
            {
                _eHoldTime = 0f;
            }

            if ((throughWPressed || lobAPressed || passPressed) && _passCooldown <= 0f)
            {
                if (qHeld && throughWPressed)
                {
                    _qUsedAsModifier = true;
                    if (hasBall || nearPlayableGroundBall)
                    {
                        SoccerPlayerController lobTarget = (CurrentAimTargetType == AimTargetType.Teammate && CurrentAimTargetPlayer != null)
                            ? CurrentAimTargetPlayer
                            : FindBestPassTarget(GetFirstPersonAimDirection(), preferForward: true);
                        ExecuteOpenPlayCross(lobTarget, forceBoxCross: true);
                        return;
                    }
                    else if (ball != null && ball.CurrentOwner != null && ball.CurrentOwner != this &&
                             ball.CurrentOwner.attackingGoalSign == attackingGoalSign && ball.CurrentOwner.isAI)
                    {
                        ball.CurrentOwner.ExecuteOpenPlayCross(this, forceBoxCross: true);
                        return;
                    }
                }
                else if (lobAPressed)
                {
                    if (hasBall || nearPlayableGroundBall)
                    {
                        SoccerPlayerController crossTarget = CurrentAimTargetType == AimTargetType.Teammate ? CurrentAimTargetPlayer : null;
                        ExecuteOpenPlayCross(crossTarget, forceBoxCross: true);
                        return;
                    }
                    else if (ball != null && ball.CurrentOwner != null && ball.CurrentOwner != this &&
                             ball.CurrentOwner.attackingGoalSign == attackingGoalSign && ball.CurrentOwner.isAI)
                    {
                        ball.CurrentOwner.ExecuteOpenPlayCross(this, forceBoxCross: true);
                        return;
                    }
                }
                else if (throughWPressed || passPressed)
                {
                    bool throughPass = throughWPressed || sprinting;
                    if (hasBall || nearPlayableGroundBall)
                    {
                        SoccerPlayerController passTarget = (CurrentAimTargetType == AimTargetType.Teammate && CurrentAimTargetPlayer != null)
                            ? CurrentAimTargetPlayer
                            : FindBestPassTarget(GetFirstPersonAimDirection(), preferForward: true);
                        if (passTarget != null)
                        {
                            ExecuteGroundPass(passTarget, isThroughPass: throughPass);
                            return;
                        }
                    }
                    else if (ball != null && ball.CurrentOwner != null && ball.CurrentOwner != this &&
                             ball.CurrentOwner.attackingGoalSign == attackingGoalSign && ball.CurrentOwner.isAI)
                    {
                        ball.CurrentOwner.ExecuteGroundPass(this, isThroughPass: throughPass);
                        return;
                    }
                }
            }

            if (shootPressed)
            {
                // [A] 팀에 대고 좌클 누르면 패스!
                if (CurrentAimTargetType == AimTargetType.Teammate && CurrentAimTargetPlayer != null && _passCooldown <= 0f)
                {
                    SoccerPlayerController mate = CurrentAimTargetPlayer;
                    if (hasBall || nearPlayableGroundBall)
                    {
                        string receiverName = mate.Profile != null ? mate.Profile.playerName : "동료";
                        ExecuteGroundPass(mate, isThroughPass: sprinting);
                        OnShotFeedback?.Invoke($"🎯 [에임 패스] {receiverName} 선수에게 패스! (볼이 닿는 순간 자동 전환)", false);
                        return;
                    }
                    else if (ball != null && ball.CurrentOwner != null && ball.CurrentOwner == mate)
                    {
                        mate.ExecuteGroundPass(this, isThroughPass: sprinting);
                        string passerName = mate.Profile != null ? mate.Profile.playerName : "동료";
                        OnShotFeedback?.Invoke($"📢 [패스 콜] {passerName} 선수가 내게 즉시 패스를 찔러줍니다!", false);
                        return;
                    }
                }

                // [B] 공중볼 찬스: 마우스 에임이 공에 가 있을 때만 오버헤드킥 / 시저스 발리 / 점프 헤딩 발동!
                bool wantAcroMod = ReadHoldAcrobaticPosture();
                if (!wantAcroMod && CanDoJumpHeaderNow())
                {
                    ExecuteHeaderShot();
                    return;
                }

                if (CanDoAcrobaticNow)
                {
                    _isChargingShot = false;
                    _shotCharge01 = 0f;
                    _isDraggingCurveShot = false;
                    StartCoroutine(ExecuteAcrobaticKickRoutine(AvailableAcrobaticType, moveInput));
                    return;
                }

                if (CanDoJumpHeaderNow())
                {
                    ExecuteHeaderShot();
                    return;
                }

                if (hasBall || nearPlayableGroundBall)
                {
                    _isChargingShot = true;
                    _shotCharge01 = 0.14f;
                    _finesseShotRequested = ReadHoldZ();
                    BeginExternalCurveDragTracking();
                }
            }

            // 지상 슛 게이지 차징 & 포켓몬고 드래그 감아차기 트래킹
            if (_isChargingShot)
            {
                UpdateExternalCurveDragTracking(dt);

                // 차징 도중에 공이 떠서 아크로바틱 조건이 되면 즉시 아크로바틱 전환!
                if (CanDoAcrobaticNow)
                {
                    _isChargingShot = false;
                    EndExternalCurveDragTracking();
                    StartCoroutine(ExecuteAcrobaticKickRoutine(AvailableAcrobaticType, moveInput));
                    return;
                }

                if (ReadHoldZ())
                    _finesseShotRequested = true;

                if (shootHeld)
                {
                    _shotCharge01 = Mathf.Clamp01(_shotCharge01 + dt / 0.56f);
                    if (_shotCharge01 >= 1.0f)
                    {
                        float curveBias = EndExternalCurveDragTracking();
                        bool isBroadcast = DynamicSoccerCamera.Instance != null && DynamicSoccerCamera.Instance.UseThirdPersonBroadcast;
                        float dragBias = (isBroadcast || _finesseShotRequested) ? 0f : curveBias;
                        if (_finesseShotRequested || Mathf.Abs(dragBias) >= 0.70f)
                        {
                            FireDragCurveShot(moveInput, Mathf.Abs(dragBias) >= 0.70f ? dragBias : 0f);
                        }
                        else
                        {
                            FireGroundShot(moveInput);
                        }
                        _finesseShotRequested = false;
                        return;
                    }
                }

                if (shootReleased && _isChargingShot)
                {
                    float curveBias = EndExternalCurveDragTracking();
                    bool isBroadcast = DynamicSoccerCamera.Instance != null && DynamicSoccerCamera.Instance.UseThirdPersonBroadcast;
                    float dragBias = (isBroadcast || _finesseShotRequested) ? 0f : curveBias;
                    if (_finesseShotRequested || Mathf.Abs(dragBias) >= 0.70f)
                    {
                        _shotCharge01 = Mathf.Max(_shotCharge01, _finesseShotRequested ? 0.58f : Mathf.Clamp(Mathf.Abs(dragBias) * 0.58f, 0.62f, 0.88f));
                        FireDragCurveShot(moveInput, Mathf.Abs(dragBias) >= 0.70f ? dragBias : 0f);
                    }
                    else
                    {
                        _shotCharge01 = Mathf.Max(_shotCharge01, 0.42f);
                        FireGroundShot(moveInput);
                    }
                    _finesseShotRequested = false;
                }
            }
        }
        private void ApplyThirdPersonBroadcastMovement(Vector2 moveInput, bool sprinting, float dt)
        {
            Vector3 wishDir = new Vector3(moveInput.x, 0f, moveInput.y);
            if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

            if (wishDir.sqrMagnitude > 0.02f)
            {
                float targetYaw = Quaternion.LookRotation(wishDir, Vector3.up).eulerAngles.y;
                FirstPersonYaw = Mathf.LerpAngle(FirstPersonYaw, targetYaw, 1f - Mathf.Exp(-14f * dt));
            }
            transform.rotation = Quaternion.Euler(0f, FirstPersonYaw, 0f);
            FirstPersonEulerPitch = 6f;

            float topSpeed = Profile != null ? Profile.EffectiveTopSpeed : 8.0f;
            bool activeSprint = sprinting && wishDir.sqrMagnitude > 0.04f;
            IsSprintingNow = activeSprint;

            float speedMult = activeSprint ? 1.0f : 0.76f;
            if (ball != null && ball.CurrentOwner == this) speedMult *= 0.93f;

            Vector3 targetVel = wishDir * (topSpeed * speedMult);
            float accel = Profile != null ? Profile.EffectiveAcceleration : 20f;
            float decel = accel * 1.18f;

            float rate = wishDir.sqrMagnitude > 0.02f ? accel : decel;
            _planarVelocity = Vector3.MoveTowards(_planarVelocity, targetVel, rate * dt);

            MovePlanarWithBounds(_planarVelocity * dt);

            if (visualRig != null)
                visualRig.UpdateProceduralAnimation(_planarVelocity, activeSprint, dt);
        }

        private void ApplyFirstPersonMovementAndLook(Vector2 moveInput, bool sprinting, float dt)
        {
            // 1. 마우스 델타 & 우측 스틱 1인칭 프리룩 (감아차기 드래그 중에는 카메라가 돌아가지 않도록 고정!)
            if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null && !_isDraggingCurveShot)
            {
                Vector2 md = Mouse.current.delta.ReadValue();
                if (md.sqrMagnitude > 0.0001f)
                {
                    FirstPersonYaw += md.x * 0.14f;
                    FirstPersonEulerPitch = Mathf.Clamp(FirstPersonEulerPitch - md.y * 0.14f, -48f, 56f);
                }
            }

            Gamepad gp = Gamepad.current;
            if (gp != null && !_isDraggingCurveShot)
            {
                Vector2 rs = gp.rightStick.ReadValue();
                if (rs.sqrMagnitude > 0.04f)
                {
                    FirstPersonYaw += rs.x * 145f * dt;
                    FirstPersonEulerPitch = Mathf.Clamp(FirstPersonEulerPitch - rs.y * 105f * dt, -48f, 56f);
                }
            }

            // 2. R키: 공 방향 즉시 바라보기 (볼 포커스 어시스트)
            if (ReadBallFocusHeld() && ball != null)
            {
                Vector3 eye = GetFirstPersonEyePosition();
                Vector3 toBall = ball.transform.position - eye;
                Vector3 flatToBall = new Vector3(toBall.x, 0f, toBall.z);
                if (flatToBall.sqrMagnitude > 0.01f)
                {
                    float wantYaw = Quaternion.LookRotation(flatToBall.normalized, Vector3.up).eulerAngles.y;
                    FirstPersonYaw = Mathf.LerpAngle(FirstPersonYaw, wantYaw, dt * 12f);
                }
                if (toBall.sqrMagnitude > 0.05f)
                {
                    Vector3 n = toBall.normalized;
                    float wantPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(n.y, -0.92f, 0.92f)) * Mathf.Rad2Deg, -42f, 45f);
                    FirstPersonEulerPitch = Mathf.Lerp(FirstPersonEulerPitch, wantPitch, dt * 11f);
                }
            }

            // 3. 1인칭 시점 기준 WASD 이동 (전후진 및 좌우 스트레이프)
            transform.rotation = Quaternion.Euler(0f, FirstPersonYaw, 0f);
            float fwdMult = moveInput.y < 0f ? 0.76f : 1.0f;
            Vector3 wishDir = transform.forward * (moveInput.y * fwdMult) + transform.right * (moveInput.x * 0.92f);

            if (wishDir.sqrMagnitude > 1f)
                wishDir.Normalize();

            float topSpeed = Profile != null ? Profile.EffectiveTopSpeed : 8.0f;
            bool activeSprint = sprinting && wishDir.sqrMagnitude > 0.08f && moveInput.y >= -0.15f;
            IsSprintingNow = activeSprint;

            float speedMult = activeSprint ? 1.0f : 0.74f;
            if (ball != null && ball.CurrentOwner == this) speedMult *= 0.93f;

            Vector3 targetVel = wishDir * (topSpeed * speedMult);
            float accel = Profile != null ? Profile.EffectiveAcceleration : 20f;
            float decel = accel * 1.18f;

            float rate = wishDir.sqrMagnitude > 0.02f ? accel : decel;
            _planarVelocity = Vector3.MoveTowards(_planarVelocity, targetVel, rate * dt);

            MovePlanarWithBounds(_planarVelocity * dt);

            if (visualRig != null)
                visualRig.UpdateProceduralAnimation(_planarVelocity, activeSprint, dt);
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

            // 골문 쪽으로 상체 회전 (1인칭 플레이어는 멀미 방지를 위해 부드럽게 보정만 수행)
            Vector3 goalCenter = new Vector3(attackingGoalSign * ShotCalculator.PitchHalfLength, 0f, 0f);
            Vector3 aimDir = (goalCenter - transform.position);
            aimDir.y = 0f;
            if (aimDir.sqrMagnitude > 0.01f)
            {
                if (isAI)
                {
                    transform.rotation = Quaternion.LookRotation(aimDir.normalized);
                }
                else
                {
                    float goalYaw = Quaternion.LookRotation(aimDir.normalized).eulerAngles.y;
                    if (Mathf.Abs(Mathf.DeltaAngle(FirstPersonYaw, goalYaw)) > 55f)
                    {
                        FirstPersonYaw = Mathf.LerpAngle(FirstPersonYaw, goalYaw, 0.45f);
                        transform.rotation = Quaternion.Euler(0f, FirstPersonYaw, 0f);
                    }
                }
            }

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

        private void FireDragCurveShot(Vector2 aimInput, float curveBias)
        {
            _isChargingShot = false;
            _isDraggingCurveShot = false;
            float charge = Mathf.Clamp(_shotCharge01, 0.22f, 1.0f);
            _shotCharge01 = 0f;

            if (ball == null) return;

            float d = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(ball.transform.position.x, ball.transform.position.z));
            if (ball.CurrentOwner != this && d > 2.2f)
                return;

            Vector3 goalCenter = new Vector3(attackingGoalSign * ShotCalculator.PitchHalfLength, 0f, 0f);
            Vector3 aimDir = (goalCenter - transform.position);
            aimDir.y = 0f;
            if (aimDir.sqrMagnitude > 0.01f && !isAI)
            {
                float goalYaw = Quaternion.LookRotation(aimDir.normalized).eulerAngles.y;
                if (Mathf.Abs(Mathf.DeltaAngle(FirstPersonYaw, goalYaw)) > 55f)
                {
                    FirstPersonYaw = Mathf.LerpAngle(FirstPersonYaw, goalYaw, 0.45f);
                    transform.rotation = Quaternion.Euler(0f, FirstPersonYaw, 0f);
                }
            }

            ComputedShotResult result = ShotCalculator.ComputeShot(
                this, ball, targetGoalkeeper, attackingGoalSign, ShotType.FinesseCurve, charge, aimInput, curveBias);

            if (visualRig != null)
                visualRig.TriggerNormalKick(result.metadata.footUsed, finesse: true);

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
                DynamicSoccerCamera.Instance.TriggerImpactShake(0.34f + charge * 0.18f, 0.24f);

            if (GoalCelebrationManager.Instance != null)
                GoalCelebrationManager.Instance.TriggerBriefImpactBloom(1.10f);

            string footCurveLabel = result.metadata.footUsed == PreferredFoot.Right
                ? "오른발 인사이드 감아차기 (우->좌)"
                : "왼발 인사이드 감아차기 (좌->우)";
            string dirStr = Mathf.Abs(curveBias) > 0.01f
                ? (curveBias > 0f ? "우측 감아차기 궤적" : "좌측 감아차기 궤적")
                : footCurveLabel;
            string toast = $"🌀 [감아차기] {dirStr}! {result.feedbackSummary}";
            OnShotFeedback?.Invoke(toast, result.metadata.usedWeakFoot);
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

            // AI는 3인칭 전신 백플립 각도 정렬, 1인칭 플레이어는 정면 공중의 공과 치솟는 축구화를 직관적으로 응시!
            Vector3 toGoal = (goalCenter - transform.position);
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude < 0.01f) toGoal = Vector3.right * attackingGoalSign;
            toGoal.Normalize();

            float startYaw = FirstPersonYaw;
            float goalYaw = Quaternion.LookRotation(toGoal, Vector3.up).eulerAngles.y;

            if (isAI)
            {
                if (acrobaticType == ShotType.OverheadKick)
                    transform.rotation = Quaternion.LookRotation(-toGoal, Vector3.up);
                else
                    transform.rotation = Quaternion.LookRotation(toGoal, Vector3.up);
            }

            // 공 타격 정점 위치 계산 (선수 앞쪽 상단 1.60~2.15m 지점)
            Vector3 startPlayerPos = transform.position;
            Vector3 ballStartPos = ball.transform.position;
            Vector3 strikeApexBallPos = isAI
                ? new Vector3(
                    Mathf.Lerp(startPlayerPos.x, ballStartPos.x, 0.65f),
                    Mathf.Clamp(ballStartPos.y, 1.55f, 2.25f),
                    Mathf.Lerp(startPlayerPos.z, ballStartPos.z, 0.65f))
                : new Vector3(
                    startPlayerPos.x + toGoal.x * 0.88f,
                    Mathf.Clamp(ballStartPos.y, 1.65f, 2.20f),
                    startPlayerPos.z + toGoal.z * 0.88f);

            Vector3 targetPlayerGroundPos = isAI
                ? (new Vector3(strikeApexBallPos.x, 0f, strikeApexBallPos.z) - (-toGoal * 0.25f))
                : startPlayerPos;

            // 카메라 줌인 + 화면 떨림 시작! (1인칭 도약 줌인 + 텐션 진동!)
            if (!isAI && DynamicSoccerCamera.Instance != null)
            {
                DynamicSoccerCamera.Instance.BeginAcrobaticWindupZoom(transform, 0.065f);
            }

            if (visualRig != null)
            {
                if (acrobaticType == ShotType.OverheadKick)
                    visualRig.TriggerOverheadKick(strikeFoot, 0.76f);
                else
                    visualRig.TriggerScissorsVolley(strikeFoot, 0.68f);
            }

            // 슬로모션 자세 잡기 구간 (실제 시간 약 0.34초 동안 극적인 공중 도약!)
            Time.timeScale = !isAI ? 0.32f : 1.0f;
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

                if (!isAI)
                {
                    FirstPersonYaw = Mathf.LerpAngle(startYaw, goalYaw, t);
                    FirstPersonEulerPitch = Mathf.Lerp(FirstPersonEulerPitch, -20f, Time.unscaledDeltaTime * 12f);
                    transform.rotation = Quaternion.Euler(0f, FirstPersonYaw, 0f);
                }

                yield return null;
            }

            // === 발이 공에 닿는 정점 임팩트 프레임!! ===
            Time.timeScale = !isAI ? 0.05f : 1.0f; // 짜릿한 0.045초 히트스톱!
            if (DynamicSoccerCamera.Instance != null)
            {
                DynamicSoccerCamera.Instance.EndAcrobaticWindupZoom();
                DynamicSoccerCamera.Instance.TriggerImpactShake(isAI ? 0.14f : 0.22f, 0.30f);
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

            if (!isAI)
            {
                // 발사 직후 골문으로 날아가는 불꽃/레이저 궤적을 1인칭 정면 시야로 감상!
                FirstPersonEulerPitch = 6.0f;
            }

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

        private void PerformTackle(bool isSlide, SoccerPlayerController explicitTarget = null)
        {
            _tackleCooldown = isSlide ? 0.88f : 0.58f;
            if (visualRig != null)
                visualRig.TriggerTackle();

            SoccerPlayerController victimCandidate = explicitTarget != null ? explicitTarget : opponent;

            Vector3 lungeDir = transform.forward;
            Vector3 desiredTargetPos = Vector3.zero;
            bool hasLungeTarget = false;

            if (explicitTarget != null)
            {
                desiredTargetPos = explicitTarget.transform.position;
                hasLungeTarget = true;
            }
            else if (ball != null)
            {
                desiredTargetPos = ball.transform.position;
                hasLungeTarget = true;
            }

            if (hasLungeTarget)
            {
                Vector3 toTarget = desiredTargetPos - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.005f && toTarget.magnitude <= 6.2f)
                {
                    lungeDir = toTarget.normalized;
                    if (isAI)
                    {
                        transform.rotation = Quaternion.LookRotation(lungeDir);
                    }
                    else
                    {
                        float lungeYaw = Quaternion.LookRotation(lungeDir).eulerAngles.y;
                        FirstPersonYaw = Mathf.LerpAngle(FirstPersonYaw, lungeYaw, 0.70f);
                        transform.rotation = Quaternion.Euler(0f, FirstPersonYaw, 0f);
                    }

                    // 타겟과의 거리가 1.6~4.8m 사이일 때 자연스러운 반보 스텝만 보정 (순간이동 느낌 방지!)
                    if (toTarget.magnitude > 1.25f && toTarget.magnitude <= 4.8f)
                    {
                        float closeStep = Mathf.Min(toTarget.magnitude - 1.05f, isSlide ? 0.92f : 0.66f);
                        MovePlanarWithBounds(lungeDir * closeStep);
                    }
                }
            }

            _planarVelocity = lungeDir * (isSlide ? 8.6f : 6.6f);

            if (ball == null) return;

            // 상대가 일반 Space 점프로 공중에 떠 있으면(>= 0.30m) 낮은 태클을 멋지게 뛰어넘어 회피!
            if (victimCandidate != null && victimCandidate.JumpHeightOffset >= 0.30f)
            {
                if (!victimCandidate.isAI)
                    victimCandidate.OnShotFeedback?.Invoke("🦘 점프로 상대의 태클을 가볍게 뛰어넘었습니다!!", false);
                return;
            }

            // === 위험한 태클(백태클 / 공보다 다리를 먼저 가격하는 거친 태클) 반칙 판정 ===
            if (victimCandidate != null)
            {
                Vector2 myXZ = new Vector2(transform.position.x, transform.position.z);
                Vector2 oppXZ = new Vector2(victimCandidate.transform.position.x, victimCandidate.transform.position.z);
                Vector2 ballXZ = new Vector2(ball.transform.position.x, ball.transform.position.z);

                float distToOpp = Vector2.Distance(myXZ, oppXZ);
                float distPlanarBall = Vector2.Distance(myXZ, ballXZ);
                float oppDistBall = Vector2.Distance(oppXZ, ballXZ);

                float bodyContactReach = isSlide ? 2.50f : 1.95f;
                if (distToOpp <= bodyContactReach)
                {
                    Vector3 oppToTackler = transform.position - victimCandidate.transform.position;
                    oppToTackler.y = 0f;
                    float approachDot = oppToTackler.sqrMagnitude > 0.001f
                        ? Vector3.Dot(victimCandidate.transform.forward, oppToTackler.normalized)
                        : 1f;

                    bool isBackTackle = approachDot < -0.38f;
                    bool hitBodyWithoutCleanBall = (distToOpp < distPlanarBall - 0.16f) || (distPlanarBall > (isSlide ? 2.25f : 1.80f));
                    bool oppInPlayWithBall = (ball.CurrentOwner == victimCandidate) || (oppDistBall < 2.4f);

                    float foulChance = 0f;
                    if (isSlide && isBackTackle)
                        foulChance = 0.72f; // 뒤에서 거는 슬라이딩 백태클
                    else if (isSlide && hitBodyWithoutCleanBall)
                        foulChance = 0.48f; // 공 대신 선수 다리를 먼저 쓸어버린 깊은 슬라이딩 태클
                    else if (!isSlide && isBackTackle && (hitBodyWithoutCleanBall && UnityEngine.Random.value < 0.45f))
                        foulChance = 0.35f; // 뒤에서 깊숙이 다리 걸기
                    else if (!isSlide && hitBodyWithoutCleanBall && oppInPlayWithBall && distToOpp < 1.28f)
                        foulChance = 0.20f; // 늦은 타이밍의 발 걸기

                    // 상대 AI가 백태클 반칙을 너무 자주 범하지 않도록 AI 반칙 확률 대폭 완화!
                    if (isAI)
                        foulChance *= isBackTackle ? 0.28f : 0.42f;

                    // 이미 슈팅이 발사되어 골문으로 날아가고 있거나 공이 골라인을 넘어가고 있을 때는 뒤늦은 태클 반칙으로 공 속도를 죽이거나 골을 취소시키지 않음 (어드밴티지)
                    bool ballHeadingToGoal = false;
                    if (ball.CurrentOwner == null)
                    {
                        Vector3 bp = ball.transform.position;
                        Vector3 bv = ball.Rb != null ? ball.Rb.linearVelocity : Vector3.zero;
                        bool nearOrInsideGoal = Mathf.Abs(bp.z) <= (ShotCalculator.GoalHalfWidth + 0.25f)
                                             && bp.y <= (ShotCalculator.GoalHeight + 0.25f)
                                             && Mathf.Abs(bp.x) >= (ShotCalculator.PitchHalfLength - 1.5f);
                        bool shotFlyingToGoal = (ball.IsSuperShotActive || bv.sqrMagnitude > 36f)
                                             && (bv.x * victimCandidate.attackingGoalSign > 4.0f);
                        if (nearOrInsideGoal || shotFlyingToGoal)
                            ballHeadingToGoal = true;
                    }

                    if (!ballHeadingToGoal && foulChance > 0f && UnityEngine.Random.value < foulChance)
                    {
                        if (ball.CurrentOwner != null)
                        {
                            ball.ReleaseFromOwner(ball.CurrentOwner, 0.5f);
                            ball.Rb.linearVelocity = lungeDir * 2.6f + Vector3.up * 0.85f;
                        }
                        else if (oppDistBall <= 1.8f && !ball.IsSuperShotActive)
                        {
                            ball.Rb.linearVelocity = lungeDir * 2.6f + Vector3.up * 0.85f;
                        }

                        victimCandidate.PlayFoulFall(lungeDir * 3.8f);
                        if (DynamicSoccerCamera.Instance != null)
                            DynamicSoccerCamera.Instance.TriggerImpactShake(0.44f, 0.28f);

                        OnFoulCommitted?.Invoke(new FoulEventArgs
                        {
                            offender = this,
                            victim = victimCandidate,
                            foulSpot = victimCandidate.transform.position,
                            isSlideTackle = isSlide,
                            isFromBehind = isBackTackle
                        });
                        return;
                    }
                }
            }

            float distToBall = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(ball.transform.position.x, ball.transform.position.z));
            float reach = isSlide ? 2.15f : 1.75f;

            if (distToBall <= reach && !ball.IsAirborne)
            {
                SoccerPlayerController prevOwner = ball.CurrentOwner;
                if (prevOwner != null && prevOwner != this)
                {
                    if (prevOwner.JumpHeightOffset >= 0.30f)
                        return;

                    // 포지션별 수비력(defense) & 몸싸움(physical) 스탯 및 상대의 탈압박 드리블 회피 반영
                    float myDefPower = Profile != null ? (Profile.defense * 0.65f + Profile.physical * 0.35f) : 75f;
                    float oppDribbleHold = prevOwner.Profile != null ? (prevOwner.Profile.agility * 0.55f + prevOwner.Profile.physical * 0.45f) : 75f;
                    float evasiveBonus = prevOwner.IsPerformingEvasiveDribble ? 0.26f : 0f;
                    float winProb = Mathf.Clamp(0.68f + (myDefPower - oppDribbleHold) * 0.0085f - evasiveBonus, 0.34f, 0.94f);

                    if (UnityEngine.Random.value <= winProb)
                    {
                        prevOwner.ApplyBriefStumble(lungeDir * 3.5f, 0.45f);
                        ball.ReleaseFromOwner(prevOwner, 0.50f);
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
                // 드리블 중 발 앞에 볼 위치 유지 (1인칭 플레이어는 정면 골대와 발밑 공이 한 화면에 동시에 들어오도록 최적 거리 배치!)
                float agilityNorm = Profile != null ? Mathf.InverseLerp(45f, 99f, Profile.agility) : 0.6f;
                float leadDist = isAI
                    ? Mathf.Lerp(0.78f, 0.58f, agilityNorm)
                    : Mathf.Lerp(1.16f, 0.96f, agilityNorm);

                // 주발 쪽 발 앞에 살짝 얹어 드리블 디테일 구현 (골대 안쪽은 골라인을 넘어 그대로 끌고 들어가 득점 가능, 일반 아웃라인은 이탈 방지)
                float footOffsetSide = (Profile == null || Profile.preferredFoot == PreferredFoot.Right) ? 0.14f : -0.14f;
                Vector3 targetBallPos = transform.position
                                      + transform.forward * leadDist
                                      + transform.right * footOffsetSide
                                      + Vector3.up * SoccerBall.BallRadius;
                bool inGoalMouthZ = Mathf.Abs(targetBallPos.z) < (ShotCalculator.GoalHalfWidth - 0.16f);
                float maxBallX = inGoalMouthZ
                    ? (ShotCalculator.PitchHalfLength + 2.05f)
                    : (ShotCalculator.PitchHalfLength - SoccerBall.BallRadius);
                targetBallPos.x = Mathf.Clamp(targetBallPos.x, -maxBallX, maxBallX);
                targetBallPos.z = Mathf.Clamp(targetBallPos.z, -ShotCalculator.PitchHalfWidth + SoccerBall.BallRadius, ShotCalculator.PitchHalfWidth - SoccerBall.BallRadius);

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

            // 패스 리시버 유도 보정: 방금 나에게 연결된 땅볼 패스가 굴러오는 중이면 내 발밑으로 매끄럽게 호밍 유도!
            if (_incomingPassAssistTimer > 0f && ball.CurrentOwner == null && !ball.IsAirborne &&
                (_incomingPassSender == null || ball.LastTouchPlayer == _incomingPassSender))
            {
                Vector3 toMe = transform.position - ball.transform.position;
                toMe.y = 0f;
                float dToMe = toMe.magnitude;
                Vector3 velPlanar = new Vector3(ball.Rb.linearVelocity.x, 0f, ball.Rb.linearVelocity.z);
                float curSpeed = velPlanar.magnitude;
                if (dToMe > 0.35f && dToMe < 36.0f && curSpeed > 2.5f)
                {
                    Vector3 wantDir = toMe / dToMe;
                    if (Vector3.Dot(velPlanar.normalized, wantDir) > -0.10f)
                    {
                        Vector3 steeredDir = Vector3.Slerp(velPlanar.normalized, wantDir, 1f - Mathf.Exp(-14f * dt)).normalized;
                        float maintainedSpeed = Mathf.Max(curSpeed, Mathf.Clamp(dToMe * 2.1f, 9.5f, 18.5f));
                        ball.Rb.linearVelocity = new Vector3(steeredDir.x * maintainedSpeed, ball.Rb.linearVelocity.y, steeredDir.z * maintainedSpeed);
                    }
                }
            }

            // 루즈볼 / 낙하볼 / 공중볼 신체 접촉 트래핑 체크 (공이 뜬 상태로 몸에 닿으면 트래핑 모션과 함께 소유권 획득!)
            if (ball.CurrentOwner == null && ball.CanBeClaimedBy(this))
            {
                float speed = ball.Rb.linearVelocity.magnitude;
                float maxTrapSpeed = ball.IsSuperShotActive ? 16.5f : (_incomingPassAssistTimer > 0f ? 33.0f : 27.5f);
                if (speed < maxTrapSpeed)
                {
                    float dist = Vector2.Distance(
                        new Vector2(transform.position.x, transform.position.z),
                        new Vector2(ball.transform.position.x, ball.transform.position.z));
                    float claimRadius = _incomingPassAssistTimer > 0f
                        ? 1.72f
                        : (isAI ? 0.98f : 1.28f);

                    float h = ball.HeightAboveGround;
                    float bodyTopHeight = (Profile != null ? Profile.HeightScale : 1.0f) * 1.92f;
                    float bodyTouchRadius = isAI ? 0.88f : 1.02f;

                    bool canTrapGround = h <= 0.52f && dist <= claimRadius;
                    bool canTrapDropping = h <= 1.52f && ball.Rb.linearVelocity.y <= 2.2f && dist <= (claimRadius + 0.18f);
                    bool canTrapAirborneBody = !IsHeadering && !_isChargingShot && _stunTimer <= 0f &&
                        h > 0.45f && h <= bodyTopHeight && dist <= bodyTouchRadius;

                    if (canTrapGround || canTrapDropping || canTrapAirborneBody)
                    {
                        float trappedHeight = h;
                        SoccerPlayerController prevLastTouch = ball.LastTouchPlayer;
                        bool wasDroppingOrFast = trappedHeight > 0.22f || speed > 2.8f;
                        bool shouldSwitchOnTouch = isAI && (
                            _autoSwitchToMeOnTouch ||
                            (prevLastTouch != null && !prevLastTouch.isAI && prevLastTouch != this && prevLastTouch.attackingGoalSign == attackingGoalSign));

                        _incomingPassAssistTimer = 0f;
                        _incomingPassSender = null;
                        _autoSwitchToMeOnTouch = false;
                        ball.SetOwner(this);
                        _aiBallHoldTimer = 0f;

                        // 공을 발 앞 0.72m 위치로 안정적으로 부드럽게 쿠셔닝 안착 (공중볼은 몸에서 발밑으로 부드럽게 낙하)
                        Vector3 trapSpot = transform.position + transform.forward * 0.72f + Vector3.up * SoccerBall.BallRadius;
                        trapSpot.x = Mathf.Clamp(trapSpot.x, -ShotCalculator.PitchHalfLength + SoccerBall.BallRadius, ShotCalculator.PitchHalfLength - SoccerBall.BallRadius);
                        trapSpot.z = Mathf.Clamp(trapSpot.z, -ShotCalculator.PitchHalfWidth + SoccerBall.BallRadius, ShotCalculator.PitchHalfWidth - SoccerBall.BallRadius);
                        ball.transform.position = Vector3.Lerp(ball.transform.position, trapSpot, trappedHeight >= 0.48f ? 0.42f : 0.70f);
                        ball.Rb.linearVelocity = Vector3.zero;
                        ball.Rb.angularVelocity = Vector3.zero;

                        PreferredFoot foot = Profile != null ? Profile.preferredFoot : PreferredFoot.Right;
                        if (visualRig != null)
                        {
                            float trapDur = trappedHeight >= 0.48f ? 0.46f : 0.38f;
                            visualRig.TriggerTrapping(foot, trapDur, trappedHeight);
                        }

                        // 공이 패스 받는 선수의 몸/발에 닿아 트래핑된 바로 이 순간에 조작 & 카메라 전환!
                        if (shouldSwitchOnTouch)
                        {
                            OnHumanPassExecuted?.Invoke(this);
                            string myName = Profile != null ? Profile.playerName : "동료 선수";
                            OnShotFeedback?.Invoke($"⚽ [패스 연결 완료!] {myName} 선수가 볼을 트래핑해 조작 전환!", false);
                        }
                        else if (!isAI && wasDroppingOrFast)
                        {
                            string trapLabel = trappedHeight >= 0.92f
                                ? "⚽ 가슴 트래핑! 공중볼을 안정적으로 컨트롤"
                                : (trappedHeight >= 0.48f ? "⚽ 허벅지 트래핑! 공중볼 컨트롤 완료" : "⚽ 퍼스트 터치 트래핑! 안정적으로 볼 컨트롤 완료");
                            OnShotFeedback?.Invoke(trapLabel, false);
                        }
                    }
                }
            }
        }

        private void RefreshNearestOpponent()
        {
            if (opponents == null || opponents.Count == 0)
                return;

            SoccerPlayerController best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < opponents.Count; i++)
            {
                SoccerPlayerController cand = opponents[i];
                if (cand == null) continue;
                float sqr = (cand.transform.position - transform.position).sqrMagnitude;
                // 상대 볼 소유자가 근처에 있으면 우선 타겟팅
                if (ball != null && ball.CurrentOwner == cand)
                    sqr *= 0.65f;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = cand;
                }
            }

            if (best != null)
                opponent = best;
        }

        public void UpdateAimTarget()
        {
            CurrentAimTargetType = AimTargetType.None;
            CurrentAimTargetPlayer = null;
            BlitzDashCandidateTarget = null;

            if (isAI) return;

            Vector3 eye = GetFirstPersonEyePosition();
            Vector3 aimDir = GetFirstPersonAimDirection();
            bool iHaveBall = ball != null && ball.CurrentOwner == this;

            SoccerPlayerController bestTarget = null;
            AimTargetType bestType = AimTargetType.None;
            float bestScore = -999f;

            SoccerPlayerController bestDashOpp = null;
            float bestDashScore = -999f;

            // 1. 상대 선수들 체크:
            // - 근거리(<= 5.6m) & 수비 상황일 때만 좌클릭 태클 락온! (원거리 상대 때문에 슛/오버헤드킥이 태클로 씹히는 버그 완벽 차단!)
            // - 중거리(~15.5m) 상대 볼 소유자는 Space×2 순간 돌진 탈취 후보로 표시!
            if (opponents != null && !iHaveBall && !CanDoAcrobaticNow)
            {
                for (int i = 0; i < opponents.Count; i++)
                {
                    SoccerPlayerController opp = opponents[i];
                    if (opp == null) continue;

                    Vector3 toOpp = (opp.transform.position + Vector3.up * 0.95f) - eye;
                    float dist = toOpp.magnitude;
                    if (dist < 0.05f || dist > 16.0f) continue;

                    float angle = Vector3.Angle(aimDir, toOpp);
                    bool oppNearOrHasBall = ball != null && (ball.CurrentOwner == opp || Vector3.Distance(opp.transform.position, ball.transform.position) <= 3.5f);

                    if (dist <= 8.2f && angle < 32f && oppNearOrHasBall)
                    {
                        float dScore = (34f - angle) * 2.2f - dist * 0.7f + (ball != null && ball.CurrentOwner == opp ? 18f : 0f);
                        if (dScore > bestDashScore)
                        {
                            bestDashScore = dScore;
                            bestDashOpp = opp;
                        }
                    }

                    // 좌클릭 태클 조준은 실제 태클 대시 타격 사거리(<= 5.6m) 이내일 때만 활성화!
                    if (dist <= 5.6f && angle < 25f)
                    {
                        float proj = Mathf.Max(0.1f, Vector3.Dot(toOpp, aimDir));
                        Vector3 perp = toOpp - aimDir * proj;
                        if (perp.magnitude <= 1.85f)
                        {
                            float score = (27f - angle) * 3.2f - dist * 1.4f + (oppNearOrHasBall ? 14f : 0f);
                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestTarget = opp;
                                bestType = AimTargetType.Opponent;
                            }
                        }
                    }
                }
            }

            // 2. 동료 선수들 체크 (조준 시 좌클릭 = 패스 / 패스 요청 대상)
            if (teammates != null && !_isDraggingCurveShot)
            {
                for (int i = 0; i < teammates.Count; i++)
                {
                    SoccerPlayerController mate = teammates[i];
                    if (mate == null || mate == this) continue;

                    Vector3 toMate = (mate.transform.position + Vector3.up * 0.95f) - eye;
                    float dist = toMate.magnitude;
                    if (dist < 0.8f || dist > 65f) continue;

                    float proj = Vector3.Dot(toMate, aimDir);
                    if (proj <= 0.2f) continue;

                    Vector3 perp = toMate - aimDir * proj;
                    float perpDist = perp.magnitude;
                    float maxAllowedPerp = Mathf.Clamp(1.30f + proj * 0.058f, 1.30f, 3.8f);

                    if (perpDist <= maxAllowedPerp)
                    {
                        float angle = Vector3.Angle(aimDir, toMate);
                        if (angle < 21f)
                        {
                            // 골문을 정면 조준해 슈팅하려는데 동료가 골문 근처 선상에 살짝 겹친 경우 슛 방해 최소화 (각도 11도 이내 정밀 조준 요구)
                            Vector3 goalCenter = new Vector3(attackingGoalSign * ShotCalculator.PitchHalfLength, 1.2f, 0f);
                            float angleToGoal = Vector3.Angle(aimDir, goalCenter - eye);
                            if (iHaveBall && angleToGoal < 10f && angle > 9.5f)
                                continue;

                            float score = (26f - angle) * 3.2f - dist * 0.32f;
                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestTarget = mate;
                                bestType = AimTargetType.Teammate;
                            }
                        }
                    }
                }
            }

            CurrentAimTargetPlayer = bestTarget;
            CurrentAimTargetType = bestType;
            BlitzDashCandidateTarget = bestDashOpp;
        }

        private void UpdateGroundBallArrowVisual()
        {
            if (isAI || _groundBallArrowRoot == null)
                return;

            bool isBroadcast = DynamicSoccerCamera.Instance != null && DynamicSoccerCamera.Instance.UseThirdPersonBroadcast;
            bool shouldShow = !isBroadcast && _controlsEnabled && ball != null && ball.CurrentOwner != this && !_isInAcrobaticRoutine;
            if (_groundBallArrowRoot.gameObject.activeSelf != shouldShow)
                _groundBallArrowRoot.gameObject.SetActive(shouldShow);

            if (!shouldShow)
                return;

            Vector3 myPos = transform.position;
            Vector3 toBall = ball.transform.position - myPos;
            toBall.y = 0f;
            float planarDist = toBall.magnitude;

            Vector3 dir = planarDist > 0.05f ? (toBall / planarDist) : transform.forward;
            float orbitRadius = Mathf.Clamp(planarDist * 0.45f, 0.95f, 1.52f);
            float pulse = 1.0f + Mathf.Sin(Time.unscaledTime * 8.0f) * 0.08f;

            _groundBallArrowRoot.position = new Vector3(
                myPos.x + dir.x * orbitRadius,
                0.07f,
                myPos.z + dir.z * orbitRadius);
            _groundBallArrowRoot.rotation = Quaternion.LookRotation(dir, Vector3.up);

            float distScale = Mathf.Lerp(0.92f, 1.35f, Mathf.InverseLerp(4f, 45f, planarDist)) * pulse;
            _groundBallArrowRoot.localScale = new Vector3(distScale, 1f, distScale);

            if (_groundBallArrowMat != null)
            {
                Color arrowTint;
                if (ball.CurrentOwner != null && ball.CurrentOwner.attackingGoalSign == attackingGoalSign)
                    arrowTint = new Color(0.18f, 1.0f, 0.68f); // 우리 팀 동료 소유: 에메랄드 시안
                else if (ball.CurrentOwner != null)
                    arrowTint = new Color(1.0f, 0.34f, 0.16f); // 상대 팀 소유: 경고 레드-오렌지
                else
                    arrowTint = new Color(1.0f, 0.88f, 0.18f); // 루즈볼: 하이비저빌리티 골드

                Color hdr = arrowTint * 2.4f;
                hdr.a = 1f;
                if (_groundBallArrowMat.HasProperty("_BaseColor")) _groundBallArrowMat.SetColor("_BaseColor", hdr);
                if (_groundBallArrowMat.HasProperty("_Color")) _groundBallArrowMat.SetColor("_Color", hdr);
            }
        }

        public SoccerPlayerController FindBestPassTarget(Vector3 lookDirPlanar, bool preferForward)
        {
            if (teammates == null || teammates.Count == 0)
                return null;

            lookDirPlanar.y = 0f;
            if (lookDirPlanar.sqrMagnitude < 0.001f)
                lookDirPlanar = Vector3.right * attackingGoalSign;
            lookDirPlanar.Normalize();

            SoccerPlayerController best = null;
            float bestScore = -999f;

            for (int i = 0; i < teammates.Count; i++)
            {
                SoccerPlayerController mate = teammates[i];
                if (mate == null || mate == this) continue;

                Vector3 diff = mate.transform.position - transform.position;
                diff.y = 0f;
                float dist = diff.magnitude;
                if (dist < 2.2f || dist > 64f) continue;

                Vector3 dir = diff / dist;
                float alignDot = Vector3.Dot(lookDirPlanar, dir);
                float forwardProgress = dir.x * attackingGoalSign;

                float score = alignDot * 2.2f - dist * 0.018f;
                if (preferForward)
                    score += forwardProgress * 0.65f;
                if (!mate.isAI)
                    score += 0.85f; // 블루 AI가 패스할 때 인간 플레이어(스트라이커) 최우선 선호!

                if (score > bestScore)
                {
                    bestScore = score;
                    best = mate;
                }
            }

            return best;
        }

        public void CommandInfiltrateRun(Vector3 targetSpot, float duration = 3.4f)
        {
            _qInfiltrateRunTimer = duration;
            _qInfiltrateTarget = targetSpot;
            _aiEvasiveDribbleTimer = 0f;
        }

        private void TriggerTeammateInfiltrationRun(Vector2 moveInput)
        {
            if (teammates == null || teammates.Count == 0) return;

            Vector3 aimDir = moveInput.sqrMagnitude > 0.04f
                ? new Vector3(moveInput.x, 0f, moveInput.y).normalized
                : transform.forward;

            SoccerPlayerController bestMate = null;
            float bestScore = -9999f;

            for (int i = 0; i < teammates.Count; i++)
            {
                SoccerPlayerController mate = teammates[i];
                if (mate == null || mate == this) continue;

                Vector3 diff = mate.transform.position - transform.position;
                diff.y = 0f;
                float dist = diff.magnitude;
                if (dist < 2.0f || dist > 42.0f) continue;

                float dirDot = Vector3.Dot(diff / dist, aimDir);
                float forwardProgress = (mate.transform.position.x - transform.position.x) * attackingGoalSign;
                float roleBonus = mate.tacticalRole == TacticalRole.Striker ? 1.8f : 0f;
                float score = dirDot * 3.0f + Mathf.Clamp(forwardProgress * 0.12f, -1.5f, 3.0f) - dist * 0.05f + roleBonus;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMate = mate;
                }
            }

            if (bestMate == null) return;

            float targetX = Mathf.Clamp(
                Mathf.Max(bestMate.transform.position.x * attackingGoalSign + 16.0f, transform.position.x * attackingGoalSign + 14.0f),
                -ShotCalculator.PitchHalfLength + 12f,
                ShotCalculator.PitchHalfLength - 3.2f) * attackingGoalSign;
            float targetZ = Mathf.Clamp(bestMate.transform.position.z * 0.85f, -ShotCalculator.PitchHalfWidth + 4.5f, ShotCalculator.PitchHalfWidth - 4.5f);

            bestMate.CommandInfiltrateRun(new Vector3(targetX, 0f, targetZ), 3.4f);
            string mateName = bestMate.Profile != null ? bestMate.Profile.playerName : "동료 선수";
            OnShotFeedback?.Invoke($"🏃 [Q 침투!] {mateName} 선수가 전방 빈 공간으로 침투합니다!", false);
        }

        public void NotifyIncomingGroundPass(SoccerPlayerController sender, float assistDuration, bool autoSwitchOnTouch = false)
        {
            _incomingPassSender = sender;
            _incomingPassAssistTimer = Mathf.Clamp(assistDuration, 0.35f, 1.85f);
            _autoSwitchToMeOnTouch = autoSwitchOnTouch;
        }

        public void ExecuteGroundPass(SoccerPlayerController receiver, bool isThroughPass = false)
        {
            if (ball == null) return;

            if (receiver == null || receiver == this)
                receiver = FindBestPassTarget(isAI ? transform.forward : GetFirstPersonAimDirection(), preferForward: true);
            if (receiver == null || receiver == this)
                return;

            _passCooldown = 0.38f;
            _isChargingShot = false;
            _shotCharge01 = 0f;
            _isDraggingCurveShot = false;
            _dragCursorOffset = Vector2.zero;
            _peakSwipeVelX = 0f;
            _circularSpinMomentum = 0f;
            _dragDuration = 0f;

            LastIntendedReceiver = receiver;
            LastPassOrCrossTime = Time.time;

            Vector3 startBallPos = new Vector3(ball.transform.position.x, SoccerBall.BallRadius, ball.transform.position.z);
            ball.transform.position = startBallPos;

            Vector3 toReceiver = receiver.transform.position - startBallPos;
            toReceiver.y = 0f;
            float dist = toReceiver.magnitude;

            float passStat01 = Profile != null ? Mathf.InverseLerp(45f, 99f, Profile.passing) : 0.65f;
            float passSpeed = Mathf.Clamp(17.2f + dist * 0.44f + passStat01 * 2.8f, 19.0f, 32.5f);
            float estTime = Mathf.Clamp(dist / passSpeed, 0.14f, 1.15f);

            // 패스 궤적이 엉뚱한 방향으로 튀지 않도록 리드 오프셋을 안전 범위 내로 제한!
            Vector3 receiverVel = new Vector3(receiver.PlanarVelocity.x, 0f, receiver.PlanarVelocity.z);
            Vector3 leadOffset = Vector3.ClampMagnitude(
                receiverVel * (estTime * (isThroughPass ? 0.48f : 0.22f)),
                isThroughPass ? 2.4f : 1.05f);
            if (isThroughPass && dist > 9.0f)
                leadOffset += new Vector3(attackingGoalSign * 1.65f, 0f, 0f);

            Vector3 targetSpot = receiver.transform.position + leadOffset;
            targetSpot.x = Mathf.Clamp(targetSpot.x, -ShotCalculator.PitchHalfLength + 2.5f, ShotCalculator.PitchHalfLength - 2.5f);
            targetSpot.z = Mathf.Clamp(targetSpot.z, -ShotCalculator.PitchHalfWidth + 2.0f, ShotCalculator.PitchHalfWidth - 2.0f);

            Vector3 passDir = targetSpot - startBallPos;
            passDir.y = 0f;
            if (passDir.sqrMagnitude < 0.01f)
                passDir = toReceiver.sqrMagnitude > 0.01f ? toReceiver.normalized : transform.forward;
            else
                passDir.Normalize();

            if (isAI)
                transform.rotation = Quaternion.LookRotation(passDir, Vector3.up);

            PreferredFoot foot = Profile != null ? Profile.preferredFoot : PreferredFoot.Right;
            if (visualRig != null)
                visualRig.TriggerNormalKick(foot, false);

            ball.ReleaseFromOwner(this, 0.34f);
            float liftY = dist > 35f ? Mathf.Lerp(0.28f, 0.95f, Mathf.InverseLerp(35f, 56f, dist)) : 0.02f;
            ball.Rb.linearVelocity = passDir * passSpeed + Vector3.up * liftY;
            ball.Rb.angularVelocity = Vector3.Cross(Vector3.up, passDir) * 18f;

            // 패스를 차는 즉시 카메라를 끊어 바꾸지 않고, 공이 패스 받는 선수의 발에 완전히 닿아 트래핑될 때 전환되도록 예약!
            bool switchWhenBallTouchesReceiver = !isAI && receiver.attackingGoalSign == attackingGoalSign;
            receiver.NotifyIncomingGroundPass(this, estTime + 0.85f, autoSwitchOnTouch: switchWhenBallTouchesReceiver);
        }

        /// <summary>
        /// 동료의 크로스가 올라왔을 때 AI 선수가 물리 이동으로 박스 안 낙하지점으로 전력 침투하여 실제 점프 헤딩/오버헤드킥을 연결하도록 지시!
        /// </summary>
        public void NotifyIncomingCross(Vector3 landingSpot, float flightDuration)
        {
            if (!isAI) return; // 인간 플레이어는 강제 위치 이동 없이 직접 자유롭게 뛰어가 헤딩/오버헤드킥 처리!
            _hasIncomingCross = true;
            _incomingCrossSpot = new Vector3(landingSpot.x, 0f, landingSpot.z);
            _incomingCrossTimer = flightDuration + 0.65f;
        }

        public void ExecuteOpenPlayCross(SoccerPlayerController receiver = null, bool forceBoxCross = false)
        {
            if (ball == null) return;

            float atkSign = attackingGoalSign;

            // 패스/크로스 키(우클릭)를 잘못 눌렀거나 크로스 지역 밖(자기 진영/미드필드) 또는 근거리 동료를 조준하고 누른 경우:
            // 공이 등 뒤나 엉뚱한 허공 궤적으로 날아가지 않도록 정밀 땅볼 패스(& 자동 선수 전환)로 스마트 처리!
            if (!isAI && !forceBoxCross)
            {
                bool inAttackingCrossZone = (transform.position.x * atkSign) >= 12.0f;
                if (receiver != null && receiver != this)
                {
                    float distToAimedMate = Vector2.Distance(
                        new Vector2(transform.position.x, transform.position.z),
                        new Vector2(receiver.transform.position.x, receiver.transform.position.z));
                    bool mateInPenaltyBox = (receiver.transform.position.x * atkSign) >= (ShotCalculator.PitchHalfLength - 18.5f);
                    if (!inAttackingCrossZone || !mateInPenaltyBox || distToAimedMate < 16.0f)
                    {
                        string rName = receiver.Profile != null ? receiver.Profile.playerName : "동료 선수";
                        ExecuteGroundPass(receiver, isThroughPass: IsSprintingNow);
                        OnShotFeedback?.Invoke($"🎯 [패스] {rName} 선수에게 패스! (볼이 발에 닿는 순간 전환)", false);
                        return;
                    }
                }
                else if (!inAttackingCrossZone)
                {
                    SoccerPlayerController bestPassMate = FindBestPassTarget(GetFirstPersonAimDirection(), preferForward: true);
                    if (bestPassMate != null)
                    {
                        string rName = bestPassMate.Profile != null ? bestPassMate.Profile.playerName : "동료 선수";
                        ExecuteGroundPass(bestPassMate, isThroughPass: IsSprintingNow);
                        OnShotFeedback?.Invoke($"🎯 [패스] {rName} 선수에게 패스! (볼이 발에 닿는 순간 전환)", false);
                        return;
                    }
                }
            }

            _passCooldown = 0.50f;
            _isChargingShot = false;
            _shotCharge01 = 0f;
            _isDraggingCurveShot = false;
            _dragCursorOffset = Vector2.zero;
            _peakSwipeVelX = 0f;
            _circularSpinMomentum = 0f;
            _dragDuration = 0f;

            Vector3 goalCenter = new Vector3(atkSign * ShotCalculator.PitchHalfLength, 0f, 0f);

            // 타겟 동료를 아직 안 골랐으면 가장 전방 박스 침투에 유리한 동료 선정!
            if (receiver == null && teammates != null && teammates.Count > 0)
            {
                float bestScore = -999f;
                for (int i = 0; i < teammates.Count; i++)
                {
                    SoccerPlayerController mate = teammates[i];
                    if (mate == null || mate == this) continue;
                    float forwardX = mate.transform.position.x * atkSign;
                    float distToBox = Vector2.Distance(
                        new Vector2(mate.transform.position.x, mate.transform.position.z),
                        new Vector2(goalCenter.x - atkSign * 10.5f, 0f));
                    float score = forwardX * 0.65f - distToBox * 0.85f + (mate.tacticalRole == TacticalRole.Striker ? 8f : 0f);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        receiver = mate;
                    }
                }
            }

            Vector3 crossSpot;
            if (receiver != null)
            {
                if (!receiver.isAI)
                {
                    // 인간 플레이어에게 올려주는 크로스: 플레이어 전방 진행 방향(또는 골문 앞)으로 정확히 배달해 오버헤드킥/점프헤딩 각 생성!
                    float targetX = Mathf.Clamp(
                        Mathf.Max(receiver.transform.position.x * atkSign + 5.5f, ShotCalculator.PitchHalfLength - 14.5f),
                        -ShotCalculator.PitchHalfLength + 6f,
                        ShotCalculator.PitchHalfLength - 6.5f) * atkSign;
                    float targetZ = Mathf.Clamp(receiver.transform.position.z * 0.65f, -7.5f, 7.5f);
                    crossSpot = new Vector3(targetX, 1.75f, targetZ);
                }
                else
                {
                    // AI 동료가 침투할 골문 앞 페널티 박스 헤더 스팟 선정 (절대로 선수 뒤쪽이나 수비 진영으로 끌려가지 않도록 보장!)
                    float boxX = goalCenter.x - atkSign * UnityEngine.Random.Range(8.6f, 11.8f);
                    float boxZ = Mathf.Clamp(receiver.transform.position.z * 0.38f + UnityEngine.Random.Range(-2.8f, 2.8f), -5.2f, 5.2f);
                    crossSpot = new Vector3(boxX, 1.75f, boxZ);

                    // 만약 AI 러너가 너무 멀리 뒤에 처져 있었다면 페널티 아크 부근으로 전진시켜 자연스럽게 크로스 낙하지점으로 쇄도하게 함!
                    float mateDistToSpot = Vector2.Distance(
                        new Vector2(receiver.transform.position.x, receiver.transform.position.z),
                        new Vector2(crossSpot.x, crossSpot.z));
                    if (mateDistToSpot > 13.5f)
                    {
                        Vector2 dirFromSpot = (new Vector2(receiver.transform.position.x, receiver.transform.position.z) - new Vector2(crossSpot.x, crossSpot.z)).normalized;
                        if (dirFromSpot.sqrMagnitude < 0.01f) dirFromSpot = new Vector2(-atkSign, 0f);
                        Vector2 boostStart = new Vector2(crossSpot.x, crossSpot.z) + dirFromSpot * 11.5f;
                        receiver.transform.position = new Vector3(
                            Mathf.Clamp(boostStart.x, -ShotCalculator.PitchHalfLength + 5f, ShotCalculator.PitchHalfLength - 5f),
                            0f,
                            Mathf.Clamp(boostStart.y, -ShotCalculator.PitchHalfWidth + 4f, ShotCalculator.PitchHalfWidth - 4f));
                    }
                }
            }
            else
            {
                crossSpot = new Vector3(goalCenter.x - atkSign * 10.0f, 1.75f, UnityEngine.Random.Range(-3.8f, 3.8f));
            }

            Vector3 startPos = new Vector3(ball.transform.position.x, Mathf.Max(ball.transform.position.y, SoccerBall.BallRadius), ball.transform.position.z);
            float planarCrossDist = Vector2.Distance(new Vector2(startPos.x, startPos.z), new Vector2(crossSpot.x, crossSpot.z));
            float flightTime = Mathf.Clamp(planarCrossDist / 21.5f, 0.72f, 1.24f);

            // 공기저항과 중력을 정확히 역산하여 crossSpot(높이 1.75m)에 정확히 안착하는 택배 크로스 궤적! (임의 Y속도 덮어쓰기 제거!)
            Vector3 launchVel = ShotCalculator.ComputeDampedBallisticVelocity(ball, startPos, crossSpot, flightTime);
            if (launchVel.magnitude > 27.5f)
                launchVel = launchVel.normalized * 27.5f;

            Vector3 toCrossPlanar = new Vector3(crossSpot.x - startPos.x, 0f, crossSpot.z - startPos.z);
            if (toCrossPlanar.sqrMagnitude > 0.01f && isAI)
                transform.rotation = Quaternion.LookRotation(toCrossPlanar.normalized, Vector3.up);

            PreferredFoot foot = Profile != null ? Profile.preferredFoot : PreferredFoot.Right;
            if (visualRig != null)
                visualRig.TriggerNormalKick(foot, finesse: true);

            ball.ReleaseFromOwner(this, 0.36f);
            ball.Rb.linearVelocity = launchVel;
            ball.Rb.angularVelocity = Vector3.Cross(Vector3.up, launchVel.normalized) * 22f;

            if (DynamicSoccerCamera.Instance != null && !isAI)
                DynamicSoccerCamera.Instance.TriggerImpactShake(0.22f, 0.18f);

            if (receiver != null)
            {
                LastIntendedReceiver = receiver;
                LastPassOrCrossTime = Time.time;
                receiver.NotifyIncomingCross(crossSpot, flightTime);

                if (!isAI)
                {
                    string mateName = receiver.Profile != null ? receiver.Profile.playerName : "동료 선수";
                    OnShotFeedback?.Invoke($"🚀 [우클릭 택배 크로스] {mateName} 박스 침투 헤딩 찬스!! ([C] 키로 직접 공중 타격 전환 가능!)", false);
                }
            }
            else if (!isAI)
            {
                OnShotFeedback?.Invoke("🚀 [우클릭 크로스] 페널티 박스로 날카로운 로빙 크로스 투하!", false);
            }
        }

        private void ResolveBodyJostleWithAllPlayers(float dt)
        {
            float myWeight = Profile != null ? Profile.weightKg : 75f;

            // 1. 상대 팀 선수들과의 몸싸움 물리 분리
            for (int i = 0; i < opponents.Count; i++)
            {
                SoccerPlayerController opp = opponents[i];
                if (opp == null) continue;
                Vector3 diff = transform.position - opp.transform.position;
                diff.y = 0f;
                float dist = diff.magnitude;
                const float minOppSep = 0.84f;
                if (dist < minOppSep && dist > 0.001f)
                {
                    float oppWeight = opp.Profile != null ? opp.Profile.weightKg : 75f;
                    float myShare = oppWeight / Mathf.Max(1f, myWeight + oppWeight);
                    MovePlanarWithBounds(diff.normalized * ((minOppSep - dist) * myShare));
                }
            }

            // 2. 같은 팀 동료들과의 겹침 방지 분리
            for (int i = 0; i < teammates.Count; i++)
            {
                SoccerPlayerController mate = teammates[i];
                if (mate == null || mate == this) continue;
                Vector3 diff = transform.position - mate.transform.position;
                diff.y = 0f;
                float dist = diff.magnitude;
                const float minMateSep = 1.05f;
                if (dist < minMateSep && dist > 0.001f)
                {
                    float share = isAI && !mate.isAI ? 0.85f : 0.5f;
                    MovePlanarWithBounds(diff.normalized * ((minMateSep - dist) * share));
                }
            }
        }

        private void UpdateAI(float dt, Vector3 aiTargetGoal)
        {
            if (ball == null) return;

            // 크로스 침투 명령이 활성화된 AI 동료: 낙하지점으로 전력 질주하여 타점 점프 헤딩 또는 아크로바틱 발리 연결!!
            if (_hasIncomingCross)
            {
                if (ball.CurrentOwner != null)
                {
                    _hasIncomingCross = false;
                }
                else
                {
                    float planarBallDist = Vector2.Distance(
                        new Vector2(transform.position.x, transform.position.z),
                        new Vector2(ball.transform.position.x, ball.transform.position.z));

                    // 공이 머리와 발 타점 반경 내로 들어오면 즉시 다이빙 헤딩 / 오버헤드킥 마무리!
                    if (planarBallDist <= 2.35f && ball.HeightAboveGround >= 0.65f && ball.HeightAboveGround <= 3.15f)
                    {
                        _hasIncomingCross = false;
                        if (Profile != null && Profile.acrobatics >= 88 && CanDoAcrobaticNow && UnityEngine.Random.value < 0.32f)
                        {
                            StartCoroutine(ExecuteAcrobaticKickRoutine(AvailableAcrobaticType, Vector2.zero));
                        }
                        else
                        {
                            ExecuteHeaderShot();
                        }
                        return;
                    }

                    // 공의 실시간 예상 낙하지점을 향해 전력 스프린트 침투
                    Vector3 dynamicLanding = new Vector3(
                        Mathf.Lerp(_incomingCrossSpot.x, ball.transform.position.x + ball.Rb.linearVelocity.x * 0.28f, 0.45f),
                        0f,
                        Mathf.Lerp(_incomingCrossSpot.z, ball.transform.position.z + ball.Rb.linearVelocity.z * 0.28f, 0.45f));

                    Vector3 toSpot = dynamicLanding - transform.position;
                    toSpot.y = 0f;
                    if (toSpot.magnitude > 0.45f)
                    {
                        Vector3 runDir = toSpot.normalized;
                        ApplyMovementFeel(new Vector2(runDir.x, runDir.z) * 1.12f, sprinting: true, holdBackToGoalC: false, aiTargetGoal, dt);
                    }
                    else
                    {
                        ApplyMovementFeel(Vector2.zero, sprinting: false, holdBackToGoalC: false, aiTargetGoal, dt);
                    }
                    return;
                }
            }

            // 동료(플레이어)가 나에게 보낸 땅볼 패스가 굴러오는 중인 경우:
            // 제자리를 이탈하지 않고 굴러오는 공 쪽으로 마중 나가 발로 확실히 트래핑한 뒤 조작권을 넘겨받음!
            if (_incomingPassAssistTimer > 0f)
            {
                if (ball.CurrentOwner != null)
                {
                    _incomingPassAssistTimer = 0f;
                    _incomingPassSender = null;
                    _autoSwitchToMeOnTouch = false;
                }
                else
                {
                    Vector3 toBall = ball.transform.position - transform.position;
                    toBall.y = 0f;
                    float distToBall = toBall.magnitude;
                    if (distToBall > 0.32f)
                    {
                        Vector3 meetDir = toBall / distToBall;
                        ApplyMovementFeel(new Vector2(meetDir.x, meetDir.z) * 0.92f, sprinting: distToBall > 3.5f, holdBackToGoalC: false, aiTargetGoal, dt);
                    }
                    else
                    {
                        ApplyMovementFeel(Vector2.zero, sprinting: false, holdBackToGoalC: false, aiTargetGoal, dt);
                    }
                    return;
                }
            }

            _aiDecisionTimer -= dt;
            if (_aiDecisionTimer <= 0f)
            {
                _aiDecisionTimer = UnityEngine.Random.Range(0.26f, 0.52f);
                _aiStrafeOffsetZ = UnityEngine.Random.Range(-7.5f, 7.5f);
            }

            if (tacticalRole == TacticalRole.Striker)
                UpdateStrikerAI(dt, aiTargetGoal);
            else
                UpdateAutoDefenderAI(dt, aiTargetGoal);
        }

        /// <summary>
        /// 레드 팀 에이스 공격수 AI (105m x 68m 5대5 대응 드리블 돌파 · 연계 · 슈팅 · 오버헤드킥)
        /// </summary>
        private void UpdateStrikerAI(float dt, Vector3 aiTargetGoal)
        {
            bool aiHasBall = ball.CurrentOwner == this;
            SoccerPlayerController owner = ball.CurrentOwner;
            bool myTeamHasBall = owner != null && owner.attackingGoalSign == attackingGoalSign;
            bool oppTeamHasBall = owner != null && owner.attackingGoalSign != attackingGoalSign;

            // 공격수 AI 공중볼 오버헤드킥 시도!
            if (CanDoAcrobaticNow && Vector3.Distance(transform.position, aiTargetGoal) < 23f && UnityEngine.Random.value < 0.14f)
            {
                StartCoroutine(ExecuteAcrobaticKickRoutine(AvailableAcrobaticType, Vector2.zero));
                return;
            }

            if (aiHasBall)
            {
                _aiBallHoldTimer += dt;
                float distToGoal = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(aiTargetGoal.x, aiTargetGoal.z));

                // 플레이어나 상대 수비수가 가까이 다가왔을 때: 순간 탈압박 방향 꺾기 드리블(헛다리·치고 달리기) 또는 빈 동료에게 빠른 패스!
                if (TryHandleAIPressureDribbleOrPass(dt, aiTargetGoal))
                    return;

                // 전방 수비수 회피 벡터 합성 (측면 우회 돌파 드리블!)
                Vector3 targetPoint = new Vector3(aiTargetGoal.x, 0f, _aiStrafeOffsetZ);
                Vector3 moveDir = (targetPoint - transform.position);
                moveDir.y = 0f;
                if (moveDir.sqrMagnitude > 0.01f) moveDir.Normalize();

                Vector3 evade = ComputeDefenderEvadeVector();
                Vector3 finalDir = (moveDir + evade * 1.15f);
                finalDir.y = 0f;
                if (finalDir.sqrMagnitude > 0.01f) finalDir.Normalize();

                bool nearOpp = opponent != null && Vector3.Distance(transform.position, opponent.transform.position) < 7.2f;
                ApplyMovementFeel(new Vector2(finalDir.x, finalDir.z), sprinting: distToGoal > 13f || nearOpp, holdBackToGoalC: false, aiTargetGoal, dt);

                // 페널티박스 근처 슈팅 찬스 판단
                if (distToGoal < 23.5f && UnityEngine.Random.value < dt * 1.55f)
                {
                    if (Profile != null && Profile.acrobatics >= 85 && distToGoal < 16.5f && _flickCooldown <= 0f && UnityEngine.Random.value < 0.26f)
                    {
                        _flickCooldown = 1.6f;
                        Vector3 popVel = finalDir * 1.3f + Vector3.up * 7.2f;
                        ball.PopUpForAcrobatic(this, popVel);
                        return;
                    }

                    _chargingFinesse = Profile != null && Profile.curve >= 80 && UnityEngine.Random.value < 0.58f;
                    _shotCharge01 = UnityEngine.Random.Range(0.56f, 0.86f);
                    FireGroundShot(new Vector2(finalDir.x, UnityEngine.Random.Range(-1f, 1f)));
                }
            }
            else if (myTeamHasBall)
            {
                if (_qInfiltrateRunTimer > 0f)
                {
                    float runX = Mathf.Clamp(
                        Mathf.Max(_qInfiltrateTarget.x * attackingGoalSign, ball.transform.position.x * attackingGoalSign + 14.0f),
                        -ShotCalculator.PitchHalfLength + 12f,
                        ShotCalculator.PitchHalfLength - 3.2f) * attackingGoalSign;
                    Vector3 liveTarget = new Vector3(runX, 0f, _qInfiltrateTarget.z);
                    MoveTowardsTacticalSpot(liveTarget, aiTargetGoal, sprintDistThreshold: 0.4f, speedScale: 1.0f, dt);
                    return;
                }

                // 우리 팀이 볼을 잡았을 때 본인의 포메이션 공격 레인(ST/LS/RS/LW/RW)을 따라 전방 침투 오프더볼 움직임!
                Vector3 runSpot = ComputeDefenderHomePosition(ball.transform.position, teamInPossession: true);
                runSpot.z = Mathf.Clamp(runSpot.z + _aiStrafeOffsetZ * 0.45f, -ShotCalculator.PitchHalfWidth + 5f, ShotCalculator.PitchHalfWidth - 5f);
                MoveTowardsTacticalSpot(runSpot, aiTargetGoal, sprintDistThreshold: 8f, speedScale: 0.92f, dt);
            }
            else if (oppTeamHasBall)
            {
                // 상대가 빌드업할 때 전방 압박 (가장 가까운 공격수 1명만 전방 압박하고 나머지는 역습 포메이션 라인 유지!)
                float maxDropBackX = -attackingGoalSign * 14.0f;
                bool inPressZone = attackingGoalSign < 0
                    ? owner.transform.position.x <= maxDropBackX
                    : owner.transform.position.x >= maxDropBackX;
                bool closestForward = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(owner.transform.position.x, owner.transform.position.z)) < 18.0f && IsClosestOnTeamToBall();

                if (inPressZone && closestForward)
                {
                    Vector3 defendSpot = owner.transform.position + new Vector3(-attackingGoalSign * 0.75f, 0f, 0f);
                    Vector3 dir = defendSpot - transform.position;
                    dir.y = 0f;
                    float distToCarrier = dir.magnitude;
                    if (dir.sqrMagnitude > 0.01f) dir.Normalize();

                    ApplyMovementFeel(new Vector2(dir.x, dir.z) * 0.90f, sprinting: distToCarrier > 5.5f, holdBackToGoalC: false, aiTargetGoal, dt);

                    Vector3 carrierToMe = transform.position - owner.transform.position;
                    carrierToMe.y = 0f;
                    bool isBehindCarrier = carrierToMe.sqrMagnitude > 0.001f &&
                        Vector3.Dot(owner.transform.forward, carrierToMe.normalized) < -0.18f;
                    bool canInstantFrontTackle = !isBehindCarrier && distToCarrier < 1.45f;
                    float tackleAttemptRate = isBehindCarrier ? 0.22f : 1.85f;

                    if (distToCarrier < 2.15f && _tackleCooldown <= 0f && (canInstantFrontTackle || UnityEngine.Random.value < dt * tackleAttemptRate))
                    {
                        bool recklessSlide = !isBehindCarrier && distToCarrier > 1.50f && UnityEngine.Random.value < 0.14f;
                        PerformTackle(isSlide: recklessSlide);
                    }
                }
                else
                {
                    // 포메이션 역습 대기 위치 유지
                    Vector3 counterSpot = ComputeDefenderHomePosition(ball.transform.position, teamInPossession: false);
                    MoveTowardsTacticalSpot(counterSpot, aiTargetGoal, sprintDistThreshold: 12f, speedScale: 0.82f, dt);
                }
            }
            else
            {
                // 루즈볼 추격 (팀 내 최단거리 선수일 때 적극 경합)
                if (IsClosestOnTeamToBall())
                {
                    bool humanJustPopped = ball.IsAirborne && ball.LastTouchPlayer != null && !ball.LastTouchPlayer.isAI;
                    Vector3 dir = ball.transform.position - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f) dir.Normalize();
                    float scale = humanJustPopped ? 0.50f : 0.96f;
                    ApplyMovementFeel(new Vector2(dir.x, dir.z) * scale, sprinting: !humanJustPopped, holdBackToGoalC: false, aiTargetGoal, dt);
                }
                else
                {
                    Vector3 supportSpot = ComputeDefenderHomePosition(ball.transform.position, teamInPossession: true);
                    MoveTowardsTacticalSpot(supportSpot, aiTargetGoal, sprintDistThreshold: 12f, speedScale: 0.84f, dt);
                }
            }
        }

        /// <summary>
        /// 팀당 3명의 자동 수비수 AI (LCB / CB·CDM / RCB):
        /// - 3백 지역 방어 라인 유지 + 가장 가까운 수비수 1명 대인 압박/태클 + 탈취 즉시 우리 공격수(플레이어/스트라이커)에게 전진 패스!
        /// </summary>
        private void UpdateAutoDefenderAI(float dt, Vector3 aiTargetGoal)
        {
            SoccerPlayerController owner = ball.CurrentOwner;
            bool iHaveBall = owner == this;
            bool myTeamHasBall = owner != null && owner.attackingGoalSign == attackingGoalSign;
            bool oppTeamHasBall = owner != null && owner.attackingGoalSign != attackingGoalSign;

            // 1. 자동 수비수가 공을 탈취/소유했을 때:
            // - 우리 팀(블루) 수비 AI는 플레이어에게 빠르게 패스 지원!
            // - 상대 팀(레드) 필드플레이어 AI는 볼만 돌리지 않고 직접 탈압박 드리블 돌파·치고 달리기를 구사하며 전진!
            if (iHaveBall)
            {
                _aiBallHoldTimer += dt;

                SoccerPlayerController humanMate = FindHumanTeammate();
                if (humanMate != null)
                {
                    // 우리 팀(블루) 수비 AI: 플레이어(1P)에게 신속하게 패스 연결!
                    Vector3 blueAdvance = new Vector3(attackingGoalSign, 0f, 0f) + ComputeDefenderEvadeVector() * 0.85f;
                    blueAdvance.y = 0f;
                    if (blueAdvance.sqrMagnitude > 0.01f) blueAdvance.Normalize();
                    ApplyMovementFeel(new Vector2(blueAdvance.x, blueAdvance.z) * 0.88f, sprinting: false, holdBackToGoalC: false, aiTargetGoal, dt);

                    bool blueUnderPressure = opponent != null && Vector3.Distance(transform.position, opponent.transform.position) < 3.4f;
                    if (_passCooldown <= 0f && (_aiBallHoldTimer >= 0.34f || blueUnderPressure))
                    {
                        bool through = humanMate.IsSprintingNow || Vector3.Distance(transform.position, humanMate.transform.position) > 20f;
                        ExecuteGroundPass(humanMate, isThroughPass: through);
                        string myName = Profile != null ? Profile.playerName : "수비 AI";
                        humanMate.OnShotFeedback?.Invoke($"🛡️ {myName} 수비 성공 후 내게 전방 패스 지원!", false);
                    }
                    return;
                }

                // 상대 팀(레드) 필드플레이어: 플레이어가 다가오면 패스만 돌리지 않고 직접 꺾기 드리블/치고 달리기로 제치며 돌파!
                if (TryHandleAIPressureDribbleOrPass(dt, aiTargetGoal))
                    return;

                float distToGoal = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(aiTargetGoal.x, aiTargetGoal.z));

                Vector3 targetPoint = new Vector3(aiTargetGoal.x, 0f, _aiStrafeOffsetZ * 0.8f);
                Vector3 moveDir = (targetPoint - transform.position);
                moveDir.y = 0f;
                if (moveDir.sqrMagnitude > 0.01f) moveDir.Normalize();

                Vector3 safeAdvance = (moveDir + ComputeDefenderEvadeVector() * 1.25f).normalized;
                ApplyMovementFeel(new Vector2(safeAdvance.x, safeAdvance.z), sprinting: true, holdBackToGoalC: false, aiTargetGoal, dt);

                // 직접 몰고 올라가서 슈팅 거리(22m 이내)에 들어오면 과감히 슈팅!
                if (distToGoal < 22.0f && UnityEngine.Random.value < dt * 1.35f)
                {
                    _chargingFinesse = Profile != null && Profile.curve >= 80 && UnityEngine.Random.value < 0.50f;
                    _shotCharge01 = UnityEngine.Random.Range(0.55f, 0.84f);
                    FireGroundShot(new Vector2(safeAdvance.x, UnityEngine.Random.Range(-1f, 1f)));
                    return;
                }

                // 충분히 오래 드리블(2.8초 이상)한 뒤 전방 스트라이커가 훨씬 좋은 위치에 비어 있을 때만 가끔 전진 패스
                if (_passCooldown <= 0f && _aiBallHoldTimer >= 2.85f && UnityEngine.Random.value < dt * 0.75f)
                {
                    SoccerPlayerController striker = FindTeamStriker();
                    if (striker != null && striker != this &&
                        (striker.transform.position.x * attackingGoalSign) > (transform.position.x * attackingGoalSign + 8.0f))
                    {
                        ExecuteGroundPass(striker, isThroughPass: true);
                        return;
                    }
                }
                return;
            }

            Vector3 tacticalHome = ComputeDefenderHomePosition(ball.transform.position, myTeamHasBall);

            // 2. 상대 팀이 볼을 소유 중일 때: 대인 압박(1순위 수비수) + 패스/슈팅 길목 커버(나머지 2명)
            if (oppTeamHasBall)
            {
                int myDefRank = GetDefenderDistanceRankTo(owner.transform.position);
                float ownGoalX = -attackingGoalSign * ShotCalculator.PitchHalfLength;
                float carrierDistToOurGoal = Mathf.Abs(owner.transform.position.x - ownGoalX);
                float myDistToCarrier = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(owner.transform.position.x, owner.transform.position.z));

                // 상대가 하프라인 넘어 우리 진영 근처로 들어왔을 때 가장 가까운 수비수(위험지역 내에서는 2번째 수비수까지 협력수비!)가 압박 출격!
                bool carrierInOurTerritory = (owner.transform.position.x * attackingGoalSign) < 12.0f;
                bool shouldPressCarrier = (myDefRank == 0 && (carrierInOurTerritory || myDistToCarrier < 19f))
                                       || (myDefRank == 1 && carrierDistToOurGoal < 21.0f && myDistToCarrier < 13.0f);

                if (shouldPressCarrier)
                {
                    // 골문과 볼 소유자 사이(Goal-side)를 막아서며 접근
                    Vector3 toOurGoal = (new Vector3(ownGoalX, 0f, 0f) - owner.transform.position).normalized;
                    Vector3 interceptSpot = owner.transform.position + toOurGoal * (myDistToCarrier > 3.2f ? 1.35f : 0.45f);
                    Vector3 dir = interceptSpot - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f) dir.Normalize();

                    ApplyMovementFeel(new Vector2(dir.x, dir.z) * 0.95f, sprinting: myDistToCarrier > 3.5f, holdBackToGoalC: false, aiTargetGoal, dt);

                    Vector3 carrierToMe = transform.position - owner.transform.position;
                    carrierToMe.y = 0f;
                    bool isBehindCarrier = carrierToMe.sqrMagnitude > 0.001f &&
                        Vector3.Dot(owner.transform.forward, carrierToMe.normalized) < -0.18f;
                    bool canInstantFrontTackle = !isBehindCarrier && myDistToCarrier < 1.55f;
                    float tackleAttemptRate = isBehindCarrier ? 0.22f : 2.10f;

                    if (myDistToCarrier < 2.20f && _tackleCooldown <= 0f && (canInstantFrontTackle || UnityEngine.Random.value < dt * tackleAttemptRate))
                    {
                        // 등 뒤에서 무리한 백태클을 남발하지 않고 정면/측면 위주로 깔끔한 스탠딩 태클 구사!
                        bool slide = !isBehindCarrier && myDistToCarrier > 1.55f && UnityEngine.Random.value < 0.12f;
                        PerformTackle(isSlide: slide);
                    }
                    return;
                }

                // 압박 담당이 아닌 나머지 2명의 수비수는 대형 유지 & 배후 공간 커버!
                MoveTowardsTacticalSpot(tacticalHome, aiTargetGoal, sprintDistThreshold: 6.5f, speedScale: 0.90f, dt);
                return;
            }

            // 3. 우리 팀 동료가 공을 가지고 있을 때: Q 침투 지시 우선 실행, 평상시에는 빌드업 지원 라인 유지
            if (myTeamHasBall)
            {
                if (_qInfiltrateRunTimer > 0f)
                {
                    float runX = Mathf.Clamp(
                        Mathf.Max(_qInfiltrateTarget.x * attackingGoalSign, ball.transform.position.x * attackingGoalSign + 14.0f),
                        -ShotCalculator.PitchHalfLength + 12f,
                        ShotCalculator.PitchHalfLength - 3.2f) * attackingGoalSign;
                    Vector3 liveTarget = new Vector3(runX, 0f, _qInfiltrateTarget.z);
                    MoveTowardsTacticalSpot(liveTarget, aiTargetGoal, sprintDistThreshold: 0.4f, speedScale: 1.0f, dt);
                    return;
                }

                MoveTowardsTacticalSpot(tacticalHome, aiTargetGoal, sprintDistThreshold: 10.0f, speedScale: 0.82f, dt);
                return;
            }

            // 4. 루즈볼 상황: 플레이어가 셀프 플릭(Q/A)한 공은 방해하지 않고, 팀 내 최단거리면 즉시 달려가 회수!
            bool humanPoppedForAcro = ball.IsAirborne && ball.LastTouchPlayer != null && !ball.LastTouchPlayer.isAI && attackingGoalSign == ball.LastTouchPlayer.attackingGoalSign;
            if (!humanPoppedForAcro && IsClosestOnTeamToBall())
            {
                Vector3 dir = ball.transform.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f) dir.Normalize();
                ApplyMovementFeel(new Vector2(dir.x, dir.z) * 0.96f, sprinting: true, holdBackToGoalC: false, aiTargetGoal, dt);
            }
            else
            {
                MoveTowardsTacticalSpot(tacticalHome, aiTargetGoal, sprintDistThreshold: 8.5f, speedScale: 0.86f, dt);
            }
        }

        private Vector3 ComputeDefenderHomePosition(Vector3 ballPos, bool teamInPossession)
        {
            // 11 vs 11 랜덤 포메이션 슬롯(AssignedSlot) 기반 유기적 전술 라인 산출!
            float ballProgressAlongAttack = ballPos.x * attackingGoalSign; // -52.5(우리 골문) ~ +52.5(상대 골문)
            float slotBaseDepth = AssignedSlot.normDepthX * (ShotCalculator.PitchHalfLength * 0.74f); // 약 -38.8m ~ +38.8m
            float ballShift = ballProgressAlongAttack * (teamInPossession ? 0.38f : 0.32f);
            float possessionBias = teamInPossession
                ? (AssignedSlot.line == PositionLine.Forward ? 7.5f : (AssignedSlot.line == PositionLine.Midfielder ? 5.5f : 2.5f))
                : (AssignedSlot.line == PositionLine.Defender ? -4.5f : -2.5f);

            float targetAttackProgress = Mathf.Clamp(
                slotBaseDepth + ballShift + possessionBias,
                -ShotCalculator.PitchHalfLength + 8.5f,
                ShotCalculator.PitchHalfLength - 9.0f);

            float homeX = attackingGoalSign * targetAttackProgress;
            float baseLaneZ = AssignedSlot.normLaneZ * (ShotCalculator.PitchHalfWidth * 0.72f) * attackingGoalSign;
            float homeZ = Mathf.Clamp(
                baseLaneZ + ballPos.z * 0.25f,
                -ShotCalculator.PitchHalfWidth + 5.0f,
                ShotCalculator.PitchHalfWidth - 5.0f);

            return new Vector3(homeX, 0f, homeZ);
        }

        private void MoveTowardsTacticalSpot(Vector3 targetSpot, Vector3 goalCenter, float sprintDistThreshold, float speedScale, float dt)
        {
            Vector3 diff = targetSpot - transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            if (dist < 0.65f)
            {
                ApplyMovementFeel(Vector2.zero, sprinting: false, holdBackToGoalC: false, goalCenter, dt);
                // 정지 대기 중에는 공 방향 주시
                if (ball != null)
                {
                    Vector3 lookBall = ball.transform.position - transform.position;
                    lookBall.y = 0f;
                    if (lookBall.sqrMagnitude > 0.04f)
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookBall.normalized), dt * 8f);
                }
                return;
            }

            Vector3 dir = diff / dist;
            float throttle = Mathf.Clamp01(dist / 2.5f) * speedScale;
            ApplyMovementFeel(new Vector2(dir.x, dir.z) * throttle, sprinting: dist > sprintDistThreshold, holdBackToGoalC: false, goalCenter, dt);
        }

        /// <summary>
        /// 상대 AI 볼 소유자가 수비수(특히 플레이어)가 가까이 다가왔을 때:
        /// 패스만 돌리지 않고 1순위로 현란한 방향 전환 꺾기 드리블(사이드스텝·바디페인트) 및 치고 달리기로 직접 수비수를 제치며 돌파!
        /// (패스는 2명 이상에게 완전히 막혔거나 드리블 쿨타임 중 코앞에 몰렸을 때만 간헐적으로 수행)
        /// </summary>
        private bool TryHandleAIPressureDribbleOrPass(float dt, Vector3 aiTargetGoal)
        {
            if (opponents == null || opponents.Count == 0)
                return false;

            SoccerPlayerController closestThreat = null;
            float closestDist = float.MaxValue;
            int nearbyPressCount = 0;

            for (int i = 0; i < opponents.Count; i++)
            {
                SoccerPlayerController opp = opponents[i];
                if (opp == null) continue;

                float d = Vector2.Distance(
                    new Vector2(transform.position.x, transform.position.z),
                    new Vector2(opp.transform.position.x, opp.transform.position.z));

                Vector3 oppToMe = transform.position - opp.transform.position;
                oppToMe.y = 0f;
                float closingSpeed = oppToMe.sqrMagnitude > 0.01f
                    ? Vector3.Dot(opp.PlanarVelocity, oppToMe.normalized)
                    : 0f;
                float effectiveDist = d - Mathf.Clamp(closingSpeed * 0.16f, 0f, 1.50f);

                if (effectiveDist < 5.2f)
                    nearbyPressCount++;

                if (effectiveDist < closestDist)
                {
                    closestDist = effectiveDist;
                    closestThreat = opp;
                }
            }

            // 현재 탈압박 방향 전환 드리블(Juke/Cut/Knock-On)이 진행 중인 경우:
            // 수비수의 실시간 움직임에 맞춰 옆 공간을 파고들며 1.18배 가속 드리블로 완전히 제침!
            if (_aiEvasiveDribbleTimer > 0f)
            {
                if (_aiEvasiveDribbleDir.sqrMagnitude < 0.01f)
                    _aiEvasiveDribbleDir = new Vector3(attackingGoalSign, 0f, 0f);

                // 다가오는 수비수가 내 진로를 다시 막으려 하면 실시간으로 반대 대각선 각도를 더 날카롭게 꺾음
                if (closestThreat != null && closestDist < 3.6f)
                {
                    Vector3 liveCut = ComputeBestEvasiveDribbleDirection(closestThreat, aiTargetGoal);
                    _aiEvasiveDribbleDir = Vector3.Slerp(_aiEvasiveDribbleDir, liveCut, dt * 8.5f).normalized;
                }

                float topSpeed = Profile != null ? Profile.EffectiveTopSpeed : 8.2f;
                Vector3 burstVel = _aiEvasiveDribbleDir * (topSpeed * 1.16f);
                _planarVelocity = Vector3.MoveTowards(_planarVelocity, burstVel, 32f * dt);
                MovePlanarWithBounds(_planarVelocity * dt);

                if (_aiEvasiveDribbleDir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(_aiEvasiveDribbleDir, Vector3.up), dt * 18f);

                if (visualRig != null)
                    visualRig.UpdateProceduralAnimation(_planarVelocity, true, dt);

                return true;
            }

            if (closestThreat == null || closestDist > 5.6f)
                return false;

            // [1순위] 순간 탈압박 드리블 돌파! (측면 급격한 방향 꺾기 바디페인트 & 치고 달리기 가속 돌파)
            if (_aiEvasiveDribbleCooldown <= 0f && closestDist <= 5.4f)
            {
                Vector3 jukeDir = ComputeBestEvasiveDribbleDirection(closestThreat, aiTargetGoal);
                _aiEvasiveDribbleDir = jukeDir;
                _aiEvasiveDribbleTimer = UnityEngine.Random.Range(0.55f, 0.82f);
                _aiEvasiveDribbleCooldown = UnityEngine.Random.Range(0.72f, 1.15f);

                float topSpeed = Profile != null ? Profile.EffectiveTopSpeed : 8.2f;

                // 전방 대각선 공간이 넓게 열려 있을 때는 플레이어의 E키처럼 공을 살짝 앞으로 치고 달리는(Knock-On) 고속 드리블 돌파도 구사!
                float openSpaceScore = EvaluateDribbleEscapeDirection(jukeDir);
                if (openSpaceScore > 5.2f && _flickCooldown <= 0f && UnityEngine.Random.value < 0.38f)
                {
                    _flickCooldown = 0.75f;
                    ball.ReleaseFromOwner(this, 0.06f);
                    ball.Rb.linearVelocity = jukeDir * (topSpeed * 1.18f) + Vector3.up * 0.30f;
                    _planarVelocity = jukeDir * (topSpeed * 1.22f);
                }
                else
                {
                    // 발밑에 공을 붙인 채 순간 옆으로 툭 꺾어 수비수를 제치는 팬텀/사이드스텝 드리블!
                    _planarVelocity = jukeDir * (topSpeed * 1.18f);
                }

                transform.rotation = Quaternion.LookRotation(jukeDir, Vector3.up);

                // 수비수가 코앞(2.8m 이내)에서 태클로 들어올 때 점프로 태클을 뛰어넘으며 돌파!
                if (closestDist < 2.85f && _jumpHeightOffset <= 0.02f && UnityEngine.Random.value < 0.30f)
                {
                    TriggerManualJump();
                }

                MovePlanarWithBounds(_planarVelocity * dt);
                if (visualRig != null)
                    visualRig.UpdateProceduralAnimation(_planarVelocity, true, dt);
                return true;
            }

            // [2순위] 이미 드리블 기술을 방금 썼는데도 수비수 2명에게 포위되었거나 코앞(2.6m 이내)에 완전히 막혔을 때만 간헐적으로 패스!
            if (_passCooldown <= 0f && _aiBallHoldTimer >= 1.45f && (nearbyPressCount >= 2 || closestDist < 2.65f))
            {
                SoccerPlayerController openMate = FindOpenTeammateUnderPressure(closestThreat);
                if (openMate != null && UnityEngine.Random.value < dt * 1.15f)
                {
                    bool through = (openMate.transform.position.x * attackingGoalSign) > (transform.position.x * attackingGoalSign + 5.5f);
                    ExecuteGroundPass(openMate, isThroughPass: through);
                    return true;
                }
            }

            return false;
        }

        private Vector3 ComputeBestEvasiveDribbleDirection(SoccerPlayerController threat, Vector3 aiTargetGoal)
        {
            Vector3 toGoal = aiTargetGoal - transform.position;
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude < 0.01f)
                toGoal = new Vector3(attackingGoalSign, 0f, 0f);
            toGoal.Normalize();

            Vector3 toThreat = threat.transform.position - transform.position;
            toThreat.y = 0f;
            if (toThreat.sqrMagnitude < 0.001f)
                toThreat = toGoal;
            toThreat.Normalize();

            // 수비수 진행선 기준 좌/우 58도 대각선 돌파 후보 생성
            Vector3 awayFromThreat = -toThreat;
            Vector3 perpLeft = new Vector3(-toThreat.z, 0f, toThreat.x);
            Vector3 perpRight = new Vector3(toThreat.z, 0f, -toThreat.x);

            Vector3 optionA = (toGoal * 0.55f + perpLeft * 0.95f + awayFromThreat * 0.30f).normalized;
            Vector3 optionB = (toGoal * 0.55f + perpRight * 0.95f + awayFromThreat * 0.30f).normalized;

            float scoreA = EvaluateDribbleEscapeDirection(optionA);
            float scoreB = EvaluateDribbleEscapeDirection(optionB);

            return scoreA >= scoreB ? optionA : optionB;
        }

        private float EvaluateDribbleEscapeDirection(Vector3 candidateDir)
        {
            Vector3 samplePos = transform.position + candidateDir * 4.2f;

            // 1) 터치라인/골라인 밖으로 몰리는 방향 페널티
            float marginZ = ShotCalculator.PitchHalfWidth - Mathf.Abs(samplePos.z);
            float marginX = ShotCalculator.PitchHalfLength - Mathf.Abs(samplePos.x);
            float score = Mathf.Min(marginZ, 12f) * 0.8f + Mathf.Min(marginX, 12f) * 0.4f;

            // 2) 공격 전진성 보너스
            score += (candidateDir.x * attackingGoalSign) * 3.5f;

            // 3) 주변 모든 상대 수비수들과의 이격 거리 점수
            for (int i = 0; i < opponents.Count; i++)
            {
                SoccerPlayerController opp = opponents[i];
                if (opp == null) continue;
                Vector3 oppFuture = opp.transform.position + opp.PlanarVelocity * 0.25f;
                float d = Vector2.Distance(new Vector2(samplePos.x, samplePos.z), new Vector2(oppFuture.x, oppFuture.z));
                if (d < 5.5f)
                    score -= (5.5f - d) * 3.2f;
            }

            return score;
        }

        private SoccerPlayerController FindOpenTeammateUnderPressure(SoccerPlayerController pressingOpp)
        {
            if (teammates == null || teammates.Count == 0)
                return null;

            SoccerPlayerController bestMate = null;
            float bestScore = -999f;

            Vector3 toPresserDir = Vector3.zero;
            float presserDist = 999f;
            if (pressingOpp != null)
            {
                Vector3 diffP = pressingOpp.transform.position - transform.position;
                diffP.y = 0f;
                presserDist = diffP.magnitude;
                if (presserDist > 0.01f)
                    toPresserDir = diffP / presserDist;
            }

            for (int i = 0; i < teammates.Count; i++)
            {
                SoccerPlayerController mate = teammates[i];
                if (mate == null || mate == this) continue;

                Vector3 toMate = mate.transform.position - transform.position;
                toMate.y = 0f;
                float dist = toMate.magnitude;
                if (dist < 4.5f || dist > 46.0f) continue;

                Vector3 dirToMate = toMate / dist;

                // 압박해 들어오는 수비수가 패스 길목을 정면으로 가로막고 있으면 감점/제외
                if (pressingOpp != null && dist > presserDist)
                {
                    float blockAngle = Vector3.Angle(toPresserDir, dirToMate);
                    if (blockAngle < 22f)
                        continue;
                }

                // 패스 받을 동료 주변의 상대 수비수 마킹 거리 확인
                float minOppDistToMate = 99f;
                for (int j = 0; j < opponents.Count; j++)
                {
                    SoccerPlayerController opp = opponents[j];
                    if (opp == null) continue;
                    float dOpp = Vector2.Distance(
                        new Vector2(mate.transform.position.x, mate.transform.position.z),
                        new Vector2(opp.transform.position.x, opp.transform.position.z));
                    if (dOpp < minOppDistToMate)
                        minOppDistToMate = dOpp;
                }

                if (minOppDistToMate < 2.6f)
                    continue;

                float forwardGain = (mate.transform.position.x - transform.position.x) * attackingGoalSign;
                float spaceScore = Mathf.Min(minOppDistToMate, 14f) * 1.35f;
                float distPenalty = Mathf.Abs(dist - 14.0f) * 0.18f;
                float strikerBonus = mate.tacticalRole == TacticalRole.Striker ? 2.8f : 0f;

                float totalScore = spaceScore + forwardGain * 0.32f + strikerBonus - distPenalty;
                if (totalScore > bestScore)
                {
                    bestScore = totalScore;
                    bestMate = mate;
                }
            }

            return bestMate;
        }

        private Vector3 ComputeDefenderEvadeVector()
        {
            Vector3 evade = Vector3.zero;
            for (int i = 0; i < opponents.Count; i++)
            {
                SoccerPlayerController opp = opponents[i];
                if (opp == null) continue;
                Vector3 away = transform.position - opp.transform.position;
                away.y = 0f;
                float d = away.magnitude;
                if (d < 6.8f && d > 0.01f)
                {
                    Vector3 awayNorm = away / d;
                    float weight = (6.8f - d) / 6.8f;

                    // 단순 후퇴가 아니라 수비수 옆 공간(측면 대각선)으로 부드럽게 흘려 제치는 회피 벡터 합성!
                    Vector3 lateral = new Vector3(-awayNorm.z, 0f, awayNorm.x);
                    if (transform.position.z > ShotCalculator.PitchHalfWidth - 8f)
                        lateral = new Vector3(lateral.x, 0f, -Mathf.Abs(lateral.z));
                    else if (transform.position.z < -ShotCalculator.PitchHalfWidth + 8f)
                        lateral = new Vector3(lateral.x, 0f, Mathf.Abs(lateral.z));
                    else if (Vector3.Dot(lateral, new Vector3(attackingGoalSign, 0f, 0f)) < 0f)
                        lateral = -lateral;

                    evade += (awayNorm * 0.55f + lateral * 0.90f) * weight;
                }
            }
            return evade;
        }

        private SoccerPlayerController FindHumanTeammate()
        {
            for (int i = 0; i < teammates.Count; i++)
            {
                SoccerPlayerController mate = teammates[i];
                if (mate != null && !mate.isAI)
                    return mate;
            }
            return null;
        }

        private SoccerPlayerController FindTeamStriker()
        {
            for (int i = 0; i < teammates.Count; i++)
            {
                SoccerPlayerController mate = teammates[i];
                if (mate != null && mate.tacticalRole == TacticalRole.Striker)
                    return mate;
            }
            return null;
        }

        private int GetDefenderDistanceRankTo(Vector3 worldPt)
        {
            float mySqr = (new Vector2(transform.position.x - worldPt.x, transform.position.z - worldPt.z)).sqrMagnitude;
            int rank = 0;
            for (int i = 0; i < teammates.Count; i++)
            {
                SoccerPlayerController mate = teammates[i];
                if (mate == null || mate == this || mate.tacticalRole == TacticalRole.Striker)
                    continue;
                float mateSqr = (new Vector2(mate.transform.position.x - worldPt.x, mate.transform.position.z - worldPt.z)).sqrMagnitude;
                if (mateSqr < mySqr - 0.05f)
                    rank++;
            }
            return rank;
        }

        private bool IsClosestOnTeamToBall()
        {
            if (ball == null) return false;
            Vector3 bp = ball.transform.position;
            float myDist = Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(bp.x, bp.z));

            for (int i = 0; i < teammates.Count; i++)
            {
                SoccerPlayerController mate = teammates[i];
                if (mate == null || mate == this) continue;
                float mateDist = Vector2.Distance(new Vector2(mate.transform.position.x, mate.transform.position.z), new Vector2(bp.x, bp.z));
                // 인간 플레이어가 공과 거의 비슷한 거리(2.2m 이내 차이)에 있으면 플레이어에게 양보!
                float bias = !mate.isAI ? -2.2f : 0f;
                if (mateDist + bias < myDist)
                    return false;
            }
            return true;
        }

        private void MovePlanarWithBounds(Vector3 delta)
        {
            Vector3 p = transform.position + delta;
            float goalInnerZ = ShotCalculator.GoalHalfWidth - 0.30f;
            float maxFieldX = ShotCalculator.PitchHalfLength - 0.16f;
            float maxFieldZ = ShotCalculator.PitchHalfWidth - 0.16f;
            float maxGoalX = ShotCalculator.PitchHalfLength + 1.85f;

            // 골대 정면 구간은 선수가 공을 몰고 골라인 안쪽 그물 앞까지 직접 들어갈 수 있도록 허용
            if (Mathf.Abs(p.z) < goalInnerZ)
            {
                if (p.x > maxGoalX)
                {
                    p.x = maxGoalX;
                    if (_planarVelocity.x > 0f) _planarVelocity.x = 0f;
                }
                else if (p.x < -maxGoalX)
                {
                    p.x = -maxGoalX;
                    if (_planarVelocity.x < 0f) _planarVelocity.x = 0f;
                }
            }
            else
            {
                // 이미 골대 내부에 있을 때는 옆 그물을 통과하지 않도록 Z축 우선 제한
                if (Mathf.Abs(transform.position.x) > maxFieldX && Mathf.Abs(transform.position.z) < goalInnerZ)
                {
                    if (p.z > goalInnerZ)
                    {
                        p.z = goalInnerZ;
                        if (_planarVelocity.z > 0f) _planarVelocity.z = 0f;
                    }
                    else if (p.z < -goalInnerZ)
                    {
                        p.z = -goalInnerZ;
                        if (_planarVelocity.z < 0f) _planarVelocity.z = 0f;
                    }
                }
                else
                {
                    // 일반 엔드라인(아웃라인): 라인 밖으로 지나치게 빠져 공과 분리되거나 제자리 달리기를 하지 않도록 경계 고정 + 바깥쪽 속도 제거
                    if (p.x > maxFieldX)
                    {
                        p.x = maxFieldX;
                        if (_planarVelocity.x > 0f) _planarVelocity.x = 0f;
                    }
                    else if (p.x < -maxFieldX)
                    {
                        p.x = -maxFieldX;
                        if (_planarVelocity.x < 0f) _planarVelocity.x = 0f;
                    }
                }
            }

            // 일반 사이드 터치라인(아웃라인): 경계 접촉 시 바깥쪽 속도 성분 제거해 라인을 따라 부드럽게 미끄러지도록 처리
            if (p.z > maxFieldZ)
            {
                p.z = maxFieldZ;
                if (_planarVelocity.z > 0f) _planarVelocity.z = 0f;
            }
            else if (p.z < -maxFieldZ)
            {
                p.z = -maxFieldZ;
                if (_planarVelocity.z < 0f) _planarVelocity.z = 0f;
            }

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
                if (kb.upArrowKey.isPressed) v.y += 1f;
                if (kb.downArrowKey.isPressed) v.y -= 1f;
                if (kb.leftArrowKey.isPressed) v.x -= 1f;
                if (kb.rightArrowKey.isPressed) v.x += 1f;
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

        public static bool ReadCrossPressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.aKey.wasPressedThisFrame)
                return true;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.buttonWest.wasPressedThisFrame;
        }

        public static bool ReadKnockOnBurstPressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame))
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.buttonNorth.wasPressedThisFrame;
        }

        public static bool ReadKnockOnBurstHeld()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.buttonNorth.isPressed;
        }

        public static bool ReadHoldZ()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.zKey.isPressed)
                return true;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.middleButton.isPressed)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.rightShoulder.isPressed;
        }

        public static bool ReadHoldAcrobaticPosture()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.leftAltKey.isPressed)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.leftTrigger.ReadValue() > 0.35f;
        }

        public static bool ReadPlayerSwitchPressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.cKey.wasPressedThisFrame)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.leftShoulder.wasPressedThisFrame;
        }

        public static bool ReadHoldC()
        {
            return ReadHoldAcrobaticPosture();
        }

        public static bool ReadShootPressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.dKey.wasPressedThisFrame)
                return true;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.buttonEast.wasPressedThisFrame;
        }

        public static bool ReadShootHeld()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.dKey.isPressed)
                return true;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.buttonEast.isPressed;
        }

        public static bool ReadShootReleasedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.dKey.wasReleasedThisFrame)
                return true;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasReleasedThisFrame)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.buttonEast.wasReleasedThisFrame;
        }

        public static bool ReadPassPressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.sKey.wasPressedThisFrame)
                return true;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.buttonSouth.wasPressedThisFrame;
        }

        public static bool ReadThroughPassWPressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.wKey.wasPressedThisFrame)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.buttonNorth.wasPressedThisFrame;
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
            return gp != null && gp.rightStickButton.wasPressedThisFrame;
        }

        public static bool ReadPassSPressedThisFrame()
        {
            return ReadPassPressedThisFrame();
        }

        public static bool ReadKnockOnShiftPressedThisFrame()
        {
            return ReadKnockOnBurstPressedThisFrame();
        }

        public static bool ReadJumpOrDashSpacePressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.buttonSouth.wasPressedThisFrame;
        }

        public static bool ReadStandingTackleSpacePressedThisFrame()
        {
            return ReadJumpOrDashSpacePressedThisFrame();
        }

        public static bool ReadTackleRPressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.buttonWest.wasPressedThisFrame;
        }

        public static bool ReadShoulderBargePressedThisFrame()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame)
                return true;
            return false;
        }

        public static bool ReadBallFocusHeld()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.vKey.isPressed)
                return true;
            Gamepad gp = Gamepad.current;
            return gp != null && gp.leftStickButton.isPressed;
        }

        #endregion
    }
}

