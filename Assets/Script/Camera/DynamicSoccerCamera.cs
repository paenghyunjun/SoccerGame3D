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

        private Camera _cam;
        private Vector3 _baseDesiredPos;
        private Quaternion _baseDesiredRot;

        // 연출 상태
        private bool _preMatchMode;
        private bool _freeKickMode;
        private Vector3 _freeKickBallPos;
        private Vector3 _freeKickGoalCenter;

        private bool _acrobaticZoomActive;
        private Transform _focusActor;
        private float _acrobaticShakeIntensity;

        private float _impactShakeTimer;
        private float _impactShakeDuration;
        private float _impactShakeMagnitude;

        private float _goalFocusTimer;
        private Transform _goalCelebrator;

        private float _currentFov = 50f;
        private const float BaseBroadcastFov = 50f;
        private const float AcrobaticZoomFov = 36.0f;
        private const float FreeKickFov = 43.0f;

        private void Awake()
        {
            Instance = this;
            _cam = GetComponent<Camera>();
            _currentFov = BaseBroadcastFov;
            if (_cam != null)
                _cam.fieldOfView = _currentFov;
        }

        public void SetPreMatchPreview(bool active)
        {
            _preMatchMode = active;
            if (active) _freeKickMode = false;
        }

        public void SetFreeKickCamera(bool active, Vector3 ballPos = default, Vector3 targetGoalPos = default)
        {
            _freeKickMode = active;
            _freeKickBallPos = ballPos;
            _freeKickGoalCenter = targetGoalPos;
        }

        /// <summary>
        /// 오버헤드킥 / 아크로바틱 자세 잡을 때 줌 인 + 긴장감 있는 화면 떨림 시작!
        /// </summary>
        public void BeginAcrobaticWindupZoom(Transform striker, float tremorIntensity = 0.20f)
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
        /// 발에 공이 닿는 순간 묵직한 타격 쉐이크!
        /// </summary>
        public void TriggerImpactShake(float magnitude = 0.42f, float duration = 0.26f)
        {
            _impactShakeMagnitude = Mathf.Max(_impactShakeMagnitude, magnitude);
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

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) dt = 0.016f;

            Vector3 targetPos;
            Vector3 lookAtPoint;
            float desiredFov = BaseBroadcastFov;

            if (_preMatchMode)
            {
                float orbit = Mathf.Sin(Time.unscaledTime * 0.45f) * 2.2f;
                targetPos = new Vector3(orbit, 3.1f, -7.8f);
                lookAtPoint = new Vector3(0f, 1.30f, 0f);
                desiredFov = 41f;
            }
            else if (_goalFocusTimer > 0f && _goalCelebrator != null)
            {
                _goalFocusTimer -= dt;
                Vector3 p = _goalCelebrator.position;
                float angle = Time.unscaledTime * 32f;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 3.0f, -6.5f);
                targetPos = p + offset;
                targetPos.y = Mathf.Max(2.3f, targetPos.y);
                lookAtPoint = p + Vector3.up * 1.35f;
                desiredFov = 40f;
            }
            else if (_freeKickMode)
            {
                // 피파 3인칭 프리킥 전용 카메라: 키커와 공 등 뒤에서 수비벽과 골문을 정면으로 응시!
                Vector3 toGoal = (_freeKickGoalCenter - _freeKickBallPos);
                toGoal.y = 0f;
                if (toGoal.sqrMagnitude < 0.01f) toGoal = Vector3.right;
                toGoal.Normalize();

                targetPos = _freeKickBallPos - toGoal * 5.4f + Vector3.up * 2.05f;
                lookAtPoint = _freeKickGoalCenter + Vector3.up * 1.35f;
                desiredFov = FreeKickFov;
            }
            else if (_acrobaticZoomActive && _focusActor != null)
            {
                Vector3 actorPos = _focusActor.position;
                Vector3 ballPos = ballTarget != null ? ballTarget.position : actorPos + Vector3.up * 1.5f;
                Vector3 center = Vector3.Lerp(actorPos, ballPos, 0.4f);

                targetPos = new Vector3(
                    Mathf.Clamp(center.x * 0.88f, -24f, 24f),
                    6.6f,
                    center.z * 0.55f - 11.5f
                );
                lookAtPoint = center + Vector3.up * 0.65f;
                desiredFov = AcrobaticZoomFov;
            }
            else
            {
                // 피파 브로드캐스트 중계 카메라
                Vector3 pPos = playerTarget != null ? playerTarget.position : Vector3.zero;
                Vector3 bPos = ballTarget != null ? ballTarget.position : Vector3.zero;

                Vector3 focus = Vector3.Lerp(bPos, pPos, 0.32f);
                float leadX = Mathf.Clamp(focus.x * 0.82f, -21.5f, 21.5f);
                float followZ = Mathf.Clamp(focus.z * 0.42f, -6.5f, 6.5f);

                targetPos = new Vector3(leadX, 12.0f, followZ - 19.2f);
                lookAtPoint = new Vector3(Mathf.Clamp(focus.x * 0.92f, -24.5f, 24.5f), 0.85f, focus.z * 0.55f);
                desiredFov = BaseBroadcastFov;
            }

            float posLerpSpeed = (_acrobaticZoomActive || _freeKickMode) ? 10.5f : 6.8f;
            _baseDesiredPos = Vector3.Lerp(
                _baseDesiredPos == Vector3.zero ? targetPos : _baseDesiredPos,
                targetPos,
                1f - Mathf.Exp(-posLerpSpeed * dt)
            );

            Vector3 dirToLook = (lookAtPoint - _baseDesiredPos);
            if (dirToLook.sqrMagnitude > 0.001f)
            {
                Quaternion wantRot = Quaternion.LookRotation(dirToLook.normalized, Vector3.up);
                _baseDesiredRot = _baseDesiredRot == Quaternion.identity
                    ? wantRot
                    : Quaternion.Slerp(_baseDesiredRot, wantRot, 1f - Mathf.Exp(-10f * dt));
            }

            float fovSpeed = _acrobaticZoomActive ? 14f : 7.5f;
            _currentFov = Mathf.Lerp(_currentFov, desiredFov, 1f - Mathf.Exp(-fovSpeed * dt));
            if (_cam != null)
                _cam.fieldOfView = _currentFov;

            Vector3 shakeOffset = Vector3.zero;
            float rollShake = 0f;

            if (_acrobaticZoomActive && _acrobaticShakeIntensity > 0.001f)
            {
                float freq = 46f;
                float t = Time.unscaledTime * freq;
                shakeOffset += new Vector3(
                    Mathf.Sin(t * 1.13f) * _acrobaticShakeIntensity,
                    Mathf.Cos(t * 1.37f) * _acrobaticShakeIntensity * 0.85f,
                    0f
                );
                rollShake += Mathf.Sin(t * 0.95f) * 1.15f;
            }

            if (_impactShakeTimer > 0f)
            {
                _impactShakeTimer -= dt;
                float decay = Mathf.Clamp01(_impactShakeTimer / _impactShakeDuration);
                decay = decay * decay;
                shakeOffset += Random.insideUnitSphere * (_impactShakeMagnitude * decay);
                rollShake += Random.Range(-2.2f, 2.2f) * decay;
            }

            transform.position = _baseDesiredPos + transform.TransformVector(shakeOffset);
            transform.rotation = _baseDesiredRot * Quaternion.Euler(0f, 0f, rollShake);
        }
    }
}
