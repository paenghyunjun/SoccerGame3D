using Soccer3D.Gameplay;
using Soccer3D.Visuals;
using UnityEngine;

namespace Soccer3D.CameraSystem
{
    [RequireComponent(typeof(Camera))]
    public class DynamicSoccerCamera : MonoBehaviour
    {
        public static DynamicSoccerCamera Instance { get; private set; }

        public Transform playerTarget;
        public Transform aiTarget;
        public Transform ballTarget;
        public SoccerPlayerController humanController;

        private Camera _cam;
        private Quaternion _smoothedRot = Quaternion.identity;
        private SoccerPlayerController _lastFirstPersonController;

        // 연출 상태
        private bool _preMatchMode;
        private Transform _preMatchSpotlightTarget;
        private float _preMatchSpotlightHeightScale = 1f;
        private int _preMatchSpotlightAttackSign = 1;
        private float _preMatchSpotlightHoldTime;
        private GameObject _preMatchSpotlightRig;
        private Transform _preMatchGroundRing;
        private Light _preMatchKeyLight;
        private bool _freeKickMode;
        private bool _freeKickHumanIsKicker;
        private Vector3 _freeKickBallPos;
        private Vector3 _freeKickLookTarget;

        private bool _acrobaticZoomActive;
        private Transform _focusActor;
        private float _acrobaticShakeIntensity;

        private float _blitzDashFovTimer;

        private float _impactShakeTimer;
        private float _impactShakeDuration;
        private float _impactShakeMagnitude;

        private float _goalFocusTimer;
        private Transform _goalCelebrator;

        // 선수 전환 시 카메라가 "띡" 하고 끊기지 않고 부드럽게 이동하도록 보간 상태 유지
        private float _switchBlendTimer;
        private float _switchBlendDuration = 0.22f;
        private Vector3 _switchStartPos;
        private Quaternion _switchStartRot = Quaternion.identity;

        public bool UseThirdPersonBroadcast { get; set; } = true;
        private Vector3 _smoothedCamPos;
        private bool _hasSmoothedCamPos;
        private SoccerBall _cachedBall;
        private Transform _cachedBallTransform;

        private float _currentFov = ThirdPersonBroadcastFov;
        private const float ThirdPersonBroadcastFov = 36.5f;
        private const float ThirdPersonSprintFov = 38.2f;
        private const float ThirdPersonCelebrationFov = 42.0f;
        private const float BaseFirstPersonFov = 78.0f;
        private const float SprintFirstPersonFov = 84.5f;
        private const float BlitzDashFirstPersonFov = 86.0f;
        private const float AcrobaticFirstPersonFov = 56.0f;
        private const float FreeKickFirstPersonFov = 70.0f;
        private const float PreMatchShowcaseFov = 41.0f;

        private void Awake()
        {
            Instance = this;
            _cam = GetComponent<Camera>();
            _currentFov = UseThirdPersonBroadcast ? ThirdPersonBroadcastFov : BaseFirstPersonFov;
            if (_cam != null)
            {
                _cam.nearClipPlane = 0.08f; // 1인칭 팔·축구화 클리핑 방지
                _cam.farClipPlane = 185f;   // 경기장 전체가 안정적으로 들어오도록 설정
                _cam.useOcclusionCulling = false; // 마우스 회전 시 오클루전 재계산 스파이크 방지
                _cam.fieldOfView = _currentFov;
                ApplyOmniDirectionalCullingMatrix();
            }
        }

        public void SetPreMatchPreview(bool active)
        {
            _preMatchMode = active;
            if (active) _freeKickMode = false;
            UpdatePlayerFirstPersonMeshState(!active);
            if (_preMatchSpotlightRig != null && _preMatchSpotlightRig.activeSelf != active)
                _preMatchSpotlightRig.SetActive(active);
        }

        /// <summary>
        /// 축구 TV 중계 라인업 소개 연출:
        /// GK(#1)부터 수비수·미드필더·공격수까지 1명씩 차례대로 3D 캐릭터를 화면 우측에 세워놓고 클로즈업 스포트라이트!
        /// </summary>
        public void SetPreMatchSpotlightTarget(Transform targetTransform, float heightScale = 1f, int teamAttackSign = 1)
        {
            _preMatchSpotlightTarget = targetTransform;
            _preMatchSpotlightHeightScale = Mathf.Clamp(heightScale, 0.88f, 1.15f);
            _preMatchSpotlightAttackSign = teamAttackSign >= 0 ? 1 : -1;
            _preMatchSpotlightHoldTime = 0f;
            EnsurePreMatchSpotlightRig();
        }

        private void EnsurePreMatchSpotlightRig()
        {
            if (_preMatchSpotlightRig != null) return;

            _preMatchSpotlightRig = new GameObject("PreMatchBroadcastSpotlightRig");
            _preMatchSpotlightRig.transform.SetParent(transform.parent, false);

            GameObject ringObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringObj.name = "BroadcastSpotlightPodiumRing";
            ringObj.transform.SetParent(_preMatchSpotlightRig.transform, false);
            ringObj.transform.localScale = new Vector3(1.55f, 0.014f, 1.55f);
            Collider rc = ringObj.GetComponent<Collider>();
            if (rc != null) Destroy(rc);
            Renderer rr = ringObj.GetComponent<Renderer>();
            if (rr != null)
            {
                rr.sharedMaterial = MaterialHelper.CreateAdditiveVfxMaterial(new Color(0.20f, 0.92f, 1.0f, 0.65f), 2.8f, useRing: true);
                rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _preMatchGroundRing = ringObj.transform;

            GameObject lightObj = new GameObject("BroadcastKeyLight");
            lightObj.transform.SetParent(_preMatchSpotlightRig.transform, false);
            _preMatchKeyLight = lightObj.AddComponent<Light>();
            _preMatchKeyLight.type = LightType.Spot;
            _preMatchKeyLight.spotAngle = 58f;
            _preMatchKeyLight.range = 8.5f;
            _preMatchKeyLight.intensity = 3.2f;
            _preMatchKeyLight.color = new Color(1.0f, 0.97f, 0.92f);
            _preMatchKeyLight.shadows = LightShadows.None;
        }

        public void SetFreeKickCamera(bool active, Vector3 ballPos = default, Vector3 lookTargetPos = default, bool humanIsKicker = false)
        {
            _freeKickMode = active;
            _freeKickHumanIsKicker = active && humanIsKicker;
            _freeKickBallPos = ballPos;
            _freeKickLookTarget = lookTargetPos;
        }

        public void SetFreeKickLookPoint(Vector3 lookTargetPos)
        {
            _freeKickLookTarget = lookTargetPos;
        }

        public void TriggerBlitzDashFov(float duration = 0.24f)
        {
            _blitzDashFovTimer = Mathf.Max(_blitzDashFovTimer, duration);
        }

        /// <summary>
        /// 선수 전환(패스 리시브 전환 또는 수동 전환) 시 카메라가 순간이동처럼 끊기지 않고 자연스럽게 슬라이딩 전환되도록 시작점 기록
        /// </summary>
        public void NotifyPlayerSwitched(float blendDuration = 0.22f)
        {
            _switchStartPos = transform.position;
            _switchStartRot = transform.rotation;
            _switchBlendDuration = Mathf.Max(0.06f, blendDuration);
            _switchBlendTimer = _switchBlendDuration;
        }

        /// <summary>
        /// 오버헤드킥 / 아크로바틱 자세 잡을 때 1인칭 시야 줌 인 + 긴장감 있는 화면 떨림 시작!
        /// </summary>
        public void BeginAcrobaticWindupZoom(Transform striker, float tremorIntensity = 0.065f)
        {
            if (striker == null || (playerTarget != null && striker != playerTarget)) return;
            _acrobaticZoomActive = true;
            _focusActor = striker;
            _acrobaticShakeIntensity = tremorIntensity;
        }
        public void EndAcrobaticWindupZoom()
        {
            _acrobaticZoomActive = false;
            _acrobaticShakeIntensity = 0f;
        }

        /// <summary>
        /// 발에 공이 닿는 순간 묵직한 1인칭 타격 쉐이크!
        /// </summary>
        public void TriggerImpactShake(float magnitude = 0.22f, float duration = 0.26f)
        {
            _impactShakeMagnitude = Mathf.Max(_impactShakeMagnitude, Mathf.Min(magnitude, 0.28f));
            _impactShakeDuration = Mathf.Max(0.05f, duration);
            _impactShakeTimer = _impactShakeDuration;
        }

        public void TriggerGoalCelebrationFocus(Transform scorer, float duration = 3.0f)
        {
            _goalCelebrator = scorer;
            _goalFocusTimer = duration;
            _acrobaticZoomActive = false;
            _freeKickMode = false;
        }

        private void EnsureHumanController()
        {
            if (humanController == null && playerTarget != null)
                humanController = playerTarget.GetComponent<SoccerPlayerController>();
        }

        private void UpdatePlayerFirstPersonMeshState(bool isFirstPerson)
        {
            EnsureHumanController();

            if (_lastFirstPersonController != null && _lastFirstPersonController != humanController)
            {
                if (_lastFirstPersonController.visualRig != null && _lastFirstPersonController.visualRig.IsFirstPersonMode)
                    _lastFirstPersonController.visualRig.SetFirstPersonViewMode(false);
                _lastFirstPersonController = null;
            }

            if (humanController != null && humanController.visualRig != null)
            {
                if (humanController.visualRig.IsFirstPersonMode != isFirstPerson)
                    humanController.visualRig.SetFirstPersonViewMode(isFirstPerson);
                if (isFirstPerson)
                    _lastFirstPersonController = humanController;
            }
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) dt = 0.016f;

            EnsureHumanController();

            // 1. 경기 시작 전 라인업 소개 방송 중계 뷰: 현재 소개 중인 선수를 화면 우측에 3D로 세워놓고 클로즈업!
            if (_preMatchMode)
            {
                _hasSmoothedCamPos = false;
                UpdatePlayerFirstPersonMeshState(false);
                _preMatchSpotlightHoldTime += dt;

                if (_preMatchSpotlightTarget != null)
                {
                    EnsurePreMatchSpotlightRig();
                    if (_preMatchSpotlightRig != null && !_preMatchSpotlightRig.activeSelf)
                        _preMatchSpotlightRig.SetActive(true);

                    Vector3 pPos = _preMatchSpotlightTarget.position;
                    float hScale = _preMatchSpotlightHeightScale;

                    // 선수 정면 방향 (블루팀은 +X, 레드팀은 -X 방향을 바라보며 서 있음)
                    Vector3 baseFwd = new Vector3(_preMatchSpotlightAttackSign, 0f, 0f);
                    // 카메라가 선수를 정면(-baseFwd)에서 바라볼 때 카메라의 오른쪽 방향 벡터
                    Vector3 camRight = Vector3.Cross(Vector3.up, -baseFwd).normalized;

                    // TV 중계 카메라 돌리 인 효과 (2.92m -> 2.60m로 여유 있는 상반신 클로즈업)
                    float dollyDist = Mathf.Lerp(2.92f, 2.60f, 1f - Mathf.Exp(-3.2f * _preMatchSpotlightHoldTime));
                    float subtleSway = Mathf.Sin(Time.unscaledTime * 1.15f) * 0.035f;

                    // 선수가 화면 우측(Viewport X ≈ 0.75, SBS Sports 중계 화면 구도!)에 서 있도록 카메라 시선을 좌측(-camRight)으로 오프셋
                    float rightSideFramingShift = 0.76f;
                    Vector3 showcasePos = pPos + baseFwd * dollyDist - camRight * (rightSideFramingShift - subtleSway) + Vector3.up * (1.28f * hScale);
                    Vector3 showcaseLook = pPos - camRight * rightSideFramingShift + Vector3.up * (1.24f * hScale);

                    // 소개 중인 3D 선수가 카메라 렌즈를 정면으로 당당하게 응시하도록 회전 정렬!
                    Vector3 toLens = new Vector3(showcasePos.x - pPos.x, 0f, showcasePos.z - pPos.z);
                    if (toLens.sqrMagnitude > 0.01f)
                    {
                        _preMatchSpotlightTarget.rotation = Quaternion.LookRotation(toLens.normalized, Vector3.up);
                    }

                    transform.position = showcasePos;
                    transform.rotation = Quaternion.LookRotation((showcaseLook - showcasePos).normalized, Vector3.up);
                    _smoothedRot = transform.rotation;

                    if (_preMatchGroundRing != null)
                    {
                        float ringPulse = 1.48f + Mathf.Sin(Time.unscaledTime * 5.5f) * 0.08f;
                        _preMatchGroundRing.position = new Vector3(pPos.x, 0.018f, pPos.z);
                        _preMatchGroundRing.localScale = new Vector3(ringPulse, 0.014f, ringPulse);
                    }

                    if (_preMatchKeyLight != null)
                    {
                        Vector3 lightPos = pPos + baseFwd * 2.8f + camRight * 0.7f + Vector3.up * 2.5f;
                        _preMatchKeyLight.transform.position = lightPos;
                        _preMatchKeyLight.transform.rotation = Quaternion.LookRotation((pPos + Vector3.up * 1.25f - lightPos).normalized, Vector3.up);
                    }

                    _currentFov = Mathf.Lerp(_currentFov, 38.0f, 1f - Mathf.Exp(-10f * dt));
                    if (_cam != null) _cam.fieldOfView = _currentFov;
                    return;
                }

                float orbit = Mathf.Sin(Time.unscaledTime * 0.45f) * 2.2f;
                Vector3 fallbackPos = new Vector3(orbit, 3.1f, -7.8f);
                Vector3 fallbackLook = new Vector3(0f, 1.30f, 0f);

                transform.position = fallbackPos;
                transform.rotation = Quaternion.LookRotation((fallbackLook - fallbackPos).normalized, Vector3.up);
                _smoothedRot = transform.rotation;

                _currentFov = Mathf.Lerp(_currentFov, PreMatchShowcaseFov, 1f - Mathf.Exp(-8f * dt));
                if (_cam != null) _cam.fieldOfView = _currentFov;
                return;
            }

            // 2. 인게임 전 구간 1인칭 선수 시점(First-Person POV)!
            if (UseThirdPersonBroadcast)
            {
                UpdateThirdPersonBroadcastCamera(dt);
                return;
            }

            UpdatePlayerFirstPersonMeshState(true);

            Vector3 eyePos = humanController != null
                ? humanController.GetFirstPersonEyePosition()
                : (playerTarget != null ? playerTarget.position + Vector3.up * 1.54f : new Vector3(0f, 1.6f, 0f));

            // 달리기·질주 시 생동감 있는 1인칭 애슬레틱 헤드밥(Head-bob)
            float strideRoll = 0f;
            if (humanController != null && humanController.visualRig != null && !_freeKickMode && !_acrobaticZoomActive)
            {
                float speed = humanController.PlanarVelocity.magnitude;
                if (speed > 0.35f)
                {
                    float speed01 = Mathf.Clamp01(speed / 9.5f);
                    float cycle = humanController.visualRig.WalkCycle;
                    float bobUp = Mathf.Abs(Mathf.Cos(cycle)) * 0.028f * speed01;
                    float bobSide = Mathf.Sin(cycle) * 0.018f * speed01;
                    eyePos += Vector3.up * bobUp + transform.right * bobSide;
                    strideRoll = -Mathf.Sin(cycle) * 0.65f * speed01;
                }
            }

            if (_blitzDashFovTimer > 0f)
                _blitzDashFovTimer -= dt;

            Quaternion desiredRot;
            float desiredFov = BaseFirstPersonFov;
            bool instantLook = false;

            if (_freeKickMode)
            {
                if (_freeKickHumanIsKicker && humanController != null)
                {
                    // 내가 키커일 때: 마우스로 자유롭게 360도 시야를 돌려 골문 구석 조준 및 동료 확인(좌클릭 패스 / 우클릭 크로스) 가능!
                    desiredRot = humanController.GetFirstPersonLookRotation();
                    desiredFov = FreeKickFirstPersonFov;
                    instantLook = true;
                }
                else
                {
                    // 내가 수비벽일 때: 정면 키커와 공을 1인칭으로 응시!
                    Vector3 dir = _freeKickLookTarget - eyePos;
                    if (dir.sqrMagnitude < 0.001f)
                        dir = (_freeKickBallPos - eyePos);
                    if (dir.sqrMagnitude < 0.001f)
                        dir = Vector3.right;

                    desiredRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    desiredFov = FreeKickFirstPersonFov;
                }
            }
            else if (_acrobaticZoomActive && _focusActor == playerTarget && ballTarget != null)
            {
                // 1인칭 오버헤드킥 / 시저스 발리 도약! 공중의 공과 솟구치는 내 축구화로 줌인 + 텐션 화면 떨림!
                Vector3 toBall = (ballTarget.position - eyePos);
                if (toBall.sqrMagnitude < 0.001f)
                    toBall = humanController != null ? humanController.GetFirstPersonAimDirection() : Vector3.right;

                desiredRot = Quaternion.LookRotation(toBall.normalized, Vector3.up);
                desiredFov = AcrobaticFirstPersonFov;
            }
            else if (_goalFocusTimer > 0f)
            {
                // 1인칭 골 세리머니 시점: 내 눈높이에서 터지는 폭죽과 골문을 바라보며 환호!
                _goalFocusTimer -= dt;
                Vector3 lookPoint = ballTarget != null
                    ? new Vector3(ballTarget.position.x, 1.85f, ballTarget.position.z * 0.5f)
                    : eyePos + Vector3.right * 10f;
                Vector3 dir = lookPoint - eyePos;
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.right;
                desiredRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                desiredFov = 74.0f;
            }
            else
            {
                // 일반 인플레이 1인칭 시점: 마우스/키보드 입력 지연 없이 즉각 반응!
                desiredRot = humanController != null
                    ? humanController.GetFirstPersonLookRotation()
                    : Quaternion.identity;
                if (_blitzDashFovTimer > 0f)
                    desiredFov = BlitzDashFirstPersonFov;
                else if (humanController != null && humanController.IsSprintingNow)
                    desiredFov = SprintFirstPersonFov;
                else
                    desiredFov = BaseFirstPersonFov;
                instantLook = true;
            }

            if (instantLook)
            {
                _smoothedRot = desiredRot;
            }
            else
            {
                _smoothedRot = Quaternion.Slerp(_smoothedRot, desiredRot, 1f - Mathf.Exp(-12f * dt));
                if (humanController != null)
                {
                    Vector3 e = _smoothedRot.eulerAngles;
                    float pitchSigned = e.x > 180f ? e.x - 360f : e.x;
                    humanController.AlignFirstPersonToDirection(
                        Quaternion.Euler(0f, e.y, 0f) * Vector3.forward,
                        pitchSigned);
                }
            }

            if (_switchBlendTimer > 0f)
            {
                _switchBlendTimer = Mathf.Max(0f, _switchBlendTimer - dt);
                float t = 1f - (_switchBlendTimer / Mathf.Max(0.01f, _switchBlendDuration));
                float smoothT = t * t * (3f - 2f * t);
                eyePos = Vector3.Lerp(_switchStartPos, eyePos, smoothT);
                _smoothedRot = Quaternion.Slerp(_switchStartRot, _smoothedRot, smoothT);
            }

            float fovSpeed = _acrobaticZoomActive ? 15f : 8.5f;
            _currentFov = Mathf.Lerp(_currentFov, desiredFov, 1f - Mathf.Exp(-fovSpeed * dt));
            if (_cam != null)
                _cam.fieldOfView = _currentFov;

            // 오버헤드킥 도약 텐션 떨림 & 슈팅 발 임팩트 쉐이크 합성
            Vector3 localShake = Vector3.zero;
            float rollShake = strideRoll;

            if (_acrobaticZoomActive && _acrobaticShakeIntensity > 0.001f)
            {
                float t = Time.unscaledTime * 48f;
                localShake += new Vector3(
                    Mathf.Sin(t * 1.13f) * _acrobaticShakeIntensity,
                    Mathf.Cos(t * 1.37f) * _acrobaticShakeIntensity * 0.85f,
                    0f
                );
                rollShake += Mathf.Sin(t * 0.95f) * 1.35f;
            }

            if (_impactShakeTimer > 0f)
            {
                _impactShakeTimer -= dt;
                float decay = Mathf.Clamp01(_impactShakeTimer / _impactShakeDuration);
                decay *= decay;
                localShake += Random.insideUnitSphere * (_impactShakeMagnitude * 0.35f * decay);
                rollShake += Random.Range(-2.4f, 2.4f) * decay;
            }

            transform.position = eyePos + _smoothedRot * localShake;
            transform.rotation = _smoothedRot * Quaternion.Euler(0f, 0f, rollShake);

            // 카메라 뒤쪽(360도 전 방향) 오브젝트도 프러스텀 컬링에서 제외되지 않고 GPU 파이프라인에 상주하도록 전방위 컬링 매트릭스 유지!
            // 마우스를 급격히 돌릴 때 뒤쪽 선수·관중석·골대가 갑자기 렌더링 큐에 대량 진입하며 생기는 프레임 드랍(렉)을 원천 차단합니다.
            ApplyOmniDirectionalCullingMatrix();
        }

        /// <summary>
        /// 경기장 전체 반경(360도 전 방향)을 포괄하는 직교 컬링 행렬을 적용하여
        /// 카메라 등 뒤에 있는 오브젝트들도 컬링 해제/재진입으로 인한 버퍼 스파이크 없이 항상 GPU 배칭 상태를 유지하도록 합니다.
        /// </summary>
        private void UpdateThirdPersonBroadcastCamera(float dt)
        {
            UpdatePlayerFirstPersonMeshState(false);

            if (_blitzDashFovTimer > 0f)
                _blitzDashFovTimer -= dt;

            Vector3 pPos = playerTarget != null ? playerTarget.position : Vector3.zero;
            Vector3 bPos = ballTarget != null ? ballTarget.position : pPos;

            if (ballTarget != null && ballTarget != _cachedBallTransform)
            {
                _cachedBallTransform = ballTarget;
                _cachedBall = ballTarget.GetComponent<SoccerBall>();
            }

            bool isAttackingPhase = false;
            int myGoalSign = humanController != null ? humanController.attackingGoalSign : 1;
            if (_cachedBall != null)
            {
                if (_cachedBall.CurrentOwner != null)
                    isAttackingPhase = _cachedBall.CurrentOwner.attackingGoalSign == myGoalSign;
                else if (_cachedBall.LastTouchPlayer != null)
                    isAttackingPhase = _cachedBall.LastTouchPlayer.attackingGoalSign == myGoalSign;
            }

            Vector3 desiredCamPos;
            Vector3 desiredLookAt;
            float desiredFov = ThirdPersonBroadcastFov;
            float posLerpSpeed = 7.2f;
            float rotLerpSpeed = 9.5f;

            if (_goalFocusTimer > 0f && _goalCelebrator != null)
            {
                _goalFocusTimer -= dt;
                Vector3 celebPos = _goalCelebrator.position;
                Vector3 fwd = _goalCelebrator.forward;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.right;
                fwd.y = 0f;
                fwd.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                desiredCamPos = celebPos + fwd * 4.5f + right * 1.15f + Vector3.up * 1.72f;
                desiredLookAt = celebPos + Vector3.up * 1.18f;
                desiredFov = ThirdPersonCelebrationFov;
                posLerpSpeed = 6.0f;
                rotLerpSpeed = 8.0f;
            }
            else if (_goalFocusTimer > 0f)
            {
                _goalFocusTimer -= dt;
                Vector3 target = new Vector3(bPos.x, 1.3f, bPos.z * 0.5f);
                desiredCamPos = new Vector3(target.x * 0.85f, 11.5f, -18.5f + target.z * 0.4f);
                desiredLookAt = target;
                desiredFov = 35.0f;
            }
            else if (_acrobaticZoomActive && _focusActor != null && _focusActor == playerTarget)
            {
                Vector3 actorPos = _focusActor.position;
                Vector3 mid = Vector3.Lerp(actorPos, bPos, 0.5f);
                desiredCamPos = new Vector3(mid.x * 0.90f, 12.2f, -20.5f + mid.z * 0.42f);
                desiredLookAt = mid + Vector3.up * 1.0f;
                desiredFov = 32.5f;
                posLerpSpeed = 9.0f;
            }
            else if (_freeKickMode)
            {
                Vector3 focusPt = _freeKickHumanIsKicker ? pPos : Vector3.Lerp(pPos, _freeKickBallPos, 0.35f);
                desiredCamPos = new Vector3(focusPt.x, 16.4f, Mathf.Clamp(focusPt.z - 22.0f, -38.5f, -6.0f));
                desiredLookAt = new Vector3(focusPt.x, 0.85f, focusPt.z);
                desiredFov = 35.5f;
            }
            else
            {
                Vector3 camFocus = isAttackingPhase ? bPos : pPos;
                desiredCamPos = new Vector3(
                    camFocus.x,
                    17.8f,
                    Mathf.Clamp(camFocus.z - 23.5f, -38.5f, -6.0f)
                );
                desiredLookAt = new Vector3(
                    camFocus.x,
                    0.65f,
                    camFocus.z
                );

                if (_blitzDashFovTimer > 0f)
                    desiredFov = ThirdPersonSprintFov + 1.0f;
                else if (humanController != null && humanController.IsSprintingNow)
                    desiredFov = ThirdPersonSprintFov;
            }

            if (!_hasSmoothedCamPos)
            {
                _smoothedCamPos = desiredCamPos;
                _hasSmoothedCamPos = true;
            }
            else
            {
                _smoothedCamPos = Vector3.Lerp(_smoothedCamPos, desiredCamPos, 1f - Mathf.Exp(-posLerpSpeed * dt));
            }

            Vector3 lookDir = desiredLookAt - _smoothedCamPos;
            if (lookDir.sqrMagnitude < 0.001f) lookDir = Vector3.forward;
            Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            _smoothedRot = Quaternion.Slerp(_smoothedRot, targetRot, 1f - Mathf.Exp(-rotLerpSpeed * dt));

            _currentFov = Mathf.Lerp(_currentFov, desiredFov, 1f - Mathf.Exp(-8.5f * dt));
            if (_cam != null)
                _cam.fieldOfView = _currentFov;

            Vector3 shakeOffset = Vector3.zero;
            if (_impactShakeTimer > 0f)
            {
                _impactShakeTimer -= dt;
                float decay = Mathf.Clamp01(_impactShakeTimer / _impactShakeDuration);
                decay *= decay;
                shakeOffset = Random.insideUnitSphere * (_impactShakeMagnitude * 0.25f * decay);
            }

            transform.position = _smoothedCamPos + shakeOffset;
            transform.rotation = _smoothedRot;
            ApplyOmniDirectionalCullingMatrix();
        }

        private void ApplyOmniDirectionalCullingMatrix()
        {
            if (_cam == null) return;
            _cam.cullingMatrix = Matrix4x4.Ortho(-160f, 160f, -80f, 80f, -160f, 160f) *
                                 Matrix4x4.Translate(-transform.position);
        }
    }
}
