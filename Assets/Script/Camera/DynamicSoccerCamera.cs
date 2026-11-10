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

        private float _currentFov = BaseFirstPersonFov;
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
            _currentFov = BaseFirstPersonFov;
            if (_cam != null)
            {
                _cam.nearClipPlane = 0.08f; // 1인칭 팔·축구화 클리핑 방지
                _cam.fieldOfView = _currentFov;
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
        }
    }
}
