using System;
using Soccer3D.Data;
using Soccer3D.Visuals;
using UnityEngine;

namespace Soccer3D.Gameplay
{
    public struct ShotMetadata
    {
        public SoccerPlayerController shooter;
        public ShotType shotType;
        public ImpactVfxType vfxType;
        public PreferredFoot footUsed;
        public bool usedWeakFoot;
        public float shotQuality01;   // 스탯·주발 반영 종합 슛 퀄리티 (0~1.25)
        public float initialSpeedMps;
        public Vector3 targetPoint;
        public int targetTeamGoal;    // 1 = AI 골문(+X), -1 = 플레이어 골문(-X)
    }

    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class SoccerBall : MonoBehaviour
    {
        // 시인성을 대폭 높인 매치볼 반지름 (직경 0.70m)
        public const float BallRadius = 0.35f;

        public Rigidbody Rb { get; private set; }
        public SphereCollider Col { get; private set; }
        public SoccerPlayerController CurrentOwner { get; private set; }
        public SoccerPlayerController LastTouchPlayer { get; private set; }
        public ShotMetadata LastShot { get; private set; }
        public bool IsSuperShotActive { get; private set; }

        public event Action<ShotMetadata> OnShotLaunched;
        public event Action<Vector3> OnBallBounced;

        private Vector3 _magnusLateralAccel;
        private float _magnusTimer;
        private float _pickupLockoutTimer;
        private SoccerPlayerController _lockedOutPlayer;

        // 공 위치 & 공중 낙하지점을 바닥에 선명히 표시해주는 링 마커
        private Transform _groundDropMarker;

        public float HeightAboveGround => Mathf.Max(0f, transform.position.y - BallRadius);
        public bool IsAirborne => HeightAboveGround >= 0.48f;

        private void Awake()
        {
            Rb = GetComponent<Rigidbody>();
            Col = GetComponent<SphereCollider>();

            Col.radius = BallRadius;

            PhysicsMaterial ballMat = new PhysicsMaterial("SoccerBallMat")
            {
                bounciness = 0.68f,
                dynamicFriction = 0.32f,
                staticFriction = 0.35f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
            Col.material = ballMat;

            Rb.mass = 0.43f;
            Rb.linearDamping = 0.22f;
            Rb.angularDamping = 0.45f;
            Rb.interpolation = RigidbodyInterpolation.Interpolate;
            Rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Rb.maxAngularVelocity = 45f;
        }

        public void BuildVisuals()
        {
            Renderer r = GetComponent<Renderer>();
            if (r == null)
            {
                GameObject meshObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                meshObj.name = "BallVisual";
                Collider c = meshObj.GetComponent<Collider>();
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
                meshObj.transform.SetParent(transform, false);
                meshObj.transform.localPosition = Vector3.zero;
                meshObj.transform.localScale = Vector3.one * (BallRadius * 2f);
                r = meshObj.GetComponent<Renderer>();
            }

            // 형광 옐로우-오렌지 매치볼 + 은은한 자체 발광으로 그늘에서도 100% 선명하게 보이도록 설정
            Material mat = MaterialHelper.CreateLitMaterial(
                Color.white,
                smoothness: 0.72f,
                metallic: 0.05f,
                emissionHdr: new Color(0.32f, 0.24f, 0.02f)
            );
            Texture2D ballTex = MaterialHelper.GetSoccerBallTexture();
            mat.mainTexture = ballTex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", ballTex);
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // 바닥 공 위치 & 낙하지점 하이라이트 링 생성
            if (_groundDropMarker == null)
            {
                GameObject markerObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                markerObj.name = "BallGroundRingMarker";
                Collider mc = markerObj.GetComponent<Collider>();
                if (Application.isPlaying) Destroy(mc); else DestroyImmediate(mc);
                markerObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Renderer mr = markerObj.GetComponent<Renderer>();
                mr.sharedMaterial = MaterialHelper.CreateAdditiveVfxMaterial(
                    new Color(1.0f, 0.88f, 0.18f, 0.92f),
                    hdrMultiplier: 2.2f,
                    useRing: true
                );
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _groundDropMarker = markerObj.transform;
            }
        }

        private void OnDestroy()
        {
            if (_groundDropMarker != null)
            {
                if (Application.isPlaying) Destroy(_groundDropMarker.gameObject);
                else DestroyImmediate(_groundDropMarker.gameObject);
            }
        }

        private void Update()
        {
            if (_pickupLockoutTimer > 0f)
            {
                _pickupLockoutTimer -= Time.deltaTime;
                if (_pickupLockoutTimer <= 0f)
                    _lockedOutPlayer = null;
            }

            if (_groundDropMarker != null)
            {
                Vector3 p = transform.position;
                _groundDropMarker.position = new Vector3(p.x, 0.025f, p.z);
                float h = HeightAboveGround;
                float ringSize = h > 0.45f
                    ? Mathf.Lerp(1.15f, 1.95f, Mathf.Clamp01(h / 3.5f)) + Mathf.Sin(Time.unscaledTime * 14f) * 0.08f
                    : 1.05f;
                _groundDropMarker.localScale = new Vector3(ringSize, ringSize, 1f);
            }
        }

        private void FixedUpdate()
        {
            if (CurrentOwner != null)
                return;

            // 감아차기 / 무회전 등 마그누스 커브 가속도 적용
            if (_magnusTimer > 0f)
            {
                _magnusTimer -= Time.fixedDeltaTime;
                Rb.AddForce(_magnusLateralAccel, ForceMode.Acceleration);
            }

            // 바닥 위에서 자연스러운 구름 감속
            if (HeightAboveGround < 0.06f && Rb.linearVelocity.sqrMagnitude > 0.05f)
            {
                Vector3 v = Rb.linearVelocity;
                v.x *= 0.992f;
                v.z *= 0.992f;
                Rb.linearVelocity = v;
            }

            // 경기장 이탈 방지 안전망
            Vector3 p = transform.position;
            if (p.y < BallRadius * 0.85f)
            {
                p.y = BallRadius;
                transform.position = p;
                if (Rb.linearVelocity.y < 0f)
                {
                    Vector3 v = Rb.linearVelocity;
                    v.y = -v.y * 0.55f;
                    Rb.linearVelocity = v;
                }
            }
        }

        public bool CanBeClaimedBy(SoccerPlayerController player)
        {
            if (player == null) return false;
            if (_pickupLockoutTimer > 0f && _lockedOutPlayer == player)
                return false;
            if (IsSuperShotActive && Rb.linearVelocity.magnitude > 15f && _magnusTimer > 0.15f && player == LastTouchPlayer)
                return false;
            return true;
        }

        public void SetOwner(SoccerPlayerController newOwner)
        {
            CurrentOwner = newOwner;
            if (newOwner != null)
            {
                LastTouchPlayer = newOwner;
                IsSuperShotActive = false;
                _magnusTimer = 0f;
                _magnusLateralAccel = Vector3.zero;
                Rb.linearVelocity = Vector3.zero;
                Rb.angularVelocity = Vector3.zero;
            }
        }

        public void ReleaseFromOwner(SoccerPlayerController previousOwner, float lockoutSeconds = 0.25f)
        {
            if (CurrentOwner == previousOwner)
                CurrentOwner = null;

            _lockedOutPlayer = previousOwner;
            _pickupLockoutTimer = lockoutSeconds;
        }

        /// <summary>
        /// 셀프 로빙 / 볼 띄우기 (오버헤드킥 & 아크로바틱 셋업용!)
        /// </summary>
        public void PopUpForAcrobatic(SoccerPlayerController player, Vector3 popVelocity)
        {
            ReleaseFromOwner(player, 0.22f);
            LastTouchPlayer = player;
            IsSuperShotActive = false;
            _magnusTimer = 0f;
            transform.position = new Vector3(transform.position.x, Mathf.Max(transform.position.y, BallRadius + 0.22f), transform.position.z);
            Rb.linearVelocity = popVelocity;
            Rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 10f;
        }

        /// <summary>
        /// 스탯 계산이 완료된 슈팅 발사!
        /// </summary>
        public void LaunchShot(ShotMetadata meta, Vector3 launchVelocity, Vector3 magnusAcceleration, float curveDuration, Vector3 spinAngularVel)
        {
            if (CurrentOwner != null)
                CurrentOwner = null;

            LastTouchPlayer = meta.shooter;
            _lockedOutPlayer = meta.shooter;
            _pickupLockoutTimer = 0.36f;

            LastShot = meta;
            IsSuperShotActive = true;
            _magnusLateralAccel = magnusAcceleration;
            _magnusTimer = curveDuration;

            Rb.linearVelocity = launchVelocity;
            Rb.angularVelocity = spinAngularVel;

            OnShotLaunched?.Invoke(meta);
        }

        public void ResetAtPosition(Vector3 worldPos)
        {
            CurrentOwner = null;
            _lockedOutPlayer = null;
            _pickupLockoutTimer = 0f;
            _magnusTimer = 0f;
            _magnusLateralAccel = Vector3.zero;
            IsSuperShotActive = false;

            transform.position = worldPos;
            transform.rotation = Quaternion.identity;
            if (Rb != null)
            {
                Rb.linearVelocity = Vector3.zero;
                Rb.angularVelocity = Vector3.zero;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("MainCamera")) return;

            if (collision.relativeVelocity.magnitude > 4f)
            {
                OnBallBounced?.Invoke(collision.GetContact(0).point);
            }

            if (transform.position.y <= BallRadius + 0.08f && _magnusTimer <= 0.05f)
            {
                IsSuperShotActive = false;
            }
        }
    }
}
