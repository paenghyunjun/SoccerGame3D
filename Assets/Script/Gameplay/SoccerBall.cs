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
        private Vector3 _lastKickOrigin;

        // 공 위치 & 공중 낙하지점을 바닥에 선명히 표시해주는 링 마커 + 원거리 1인칭 식별용 상단 3D 화살표 비콘
        private Transform _groundDropMarker;
        private Material _groundDropMat;
        private Transform _possessionAuraOuter;
        private Material _possessionAuraMat;
        private Material _ballMaterial;
        private Transform _overheadBeaconRoot;

        public Vector3 ActiveCurveAcceleration => _magnusTimer > 0f ? _magnusLateralAccel : Vector3.zero;
        public float RemainingCurveTime => Mathf.Max(0f, _magnusTimer);
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
            _ballMaterial = mat;

            // 바닥 공 위치 & 낙하지점 하이라이트 링 생성
            if (_groundDropMarker == null)
            {
                GameObject markerObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                markerObj.name = "BallGroundRingMarker";
                Collider mc = markerObj.GetComponent<Collider>();
                if (Application.isPlaying) Destroy(mc); else DestroyImmediate(mc);
                markerObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Renderer mr = markerObj.GetComponent<Renderer>();
                _groundDropMat = MaterialHelper.CreateAdditiveVfxMaterial(
                    new Color(1.0f, 0.88f, 0.18f, 0.92f),
                    hdrMultiplier: 2.2f,
                    useRing: true
                );
                mr.sharedMaterial = _groundDropMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _groundDropMarker = markerObj.transform;
            }

            // 공을 잡고 있을 때(볼 소유 중) 선수 발밑과 공 주변에 선명하게 퍼지는 이중 소유 오라 링 생성!
            if (_possessionAuraOuter == null)
            {
                GameObject auraObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                auraObj.name = "BallPossessionAuraRing";
                Collider ac = auraObj.GetComponent<Collider>();
                if (Application.isPlaying) Destroy(ac); else DestroyImmediate(ac);
                auraObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Renderer ar = auraObj.GetComponent<Renderer>();
                _possessionAuraMat = MaterialHelper.CreateAdditiveVfxMaterial(
                    new Color(0.15f, 1.0f, 0.86f, 0.95f),
                    hdrMultiplier: 3.1f,
                    useRing: true
                );
                ar.sharedMaterial = _possessionAuraMat;
                ar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _possessionAuraOuter = auraObj.transform;
                _possessionAuraOuter.gameObject.SetActive(false);
            }

            // 105m 대형 구장 1인칭 시점에서도 멀리 있는 공 위치를 한눈에 알려주는 공중 3D 화살표 비콘 (▼)
            if (_overheadBeaconRoot == null)
            {
                GameObject beaconObj = new GameObject("BallOverheadBeacon3D");
                _overheadBeaconRoot = beaconObj.transform;

                Material beaconGlow = MaterialHelper.CreateHdrUnlitMaterial(new Color(1.0f, 0.86f, 0.16f), 2.8f);
                Material stemGlow = MaterialHelper.CreateHdrUnlitMaterial(new Color(0.35f, 0.92f, 1.0f), 2.2f);

                CreateBeaconBar("ChevronLeft", _overheadBeaconRoot,
                    new Vector3(-0.17f, 0.22f, 0f), new Vector3(0.11f, 0.52f, 0.08f), Quaternion.Euler(0f, 0f, 36f), beaconGlow);
                CreateBeaconBar("ChevronRight", _overheadBeaconRoot,
                    new Vector3(0.17f, 0.22f, 0f), new Vector3(0.11f, 0.52f, 0.08f), Quaternion.Euler(0f, 0f, -36f), beaconGlow);
                CreateBeaconBar("VerticalStem", _overheadBeaconRoot,
                    new Vector3(0f, -0.35f, 0f), new Vector3(0.045f, 0.55f, 0.045f), Quaternion.identity, stemGlow);
            }
        }

        private static void CreateBeaconBar(string name, Transform parent, Vector3 localPos, Vector3 localScale, Quaternion localRot, Material mat)
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
        }

        private void OnDestroy()
        {
            if (_groundDropMarker != null)
            {
                if (Application.isPlaying) Destroy(_groundDropMarker.gameObject);
                else DestroyImmediate(_groundDropMarker.gameObject);
            }
            if (_possessionAuraOuter != null)
            {
                if (Application.isPlaying) Destroy(_possessionAuraOuter.gameObject);
                else DestroyImmediate(_possessionAuraOuter.gameObject);
            }
            if (_overheadBeaconRoot != null)
            {
                if (Application.isPlaying) Destroy(_overheadBeaconRoot.gameObject);
                else DestroyImmediate(_overheadBeaconRoot.gameObject);
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

            Vector3 p = transform.position;
            Camera cam = Camera.main;
            float camDist = cam != null ? Vector3.Distance(cam.transform.position, p) : 15f;
            bool isPossessed = CurrentOwner != null;
            bool isHumanPossessing = isPossessed && !CurrentOwner.isAI;

            if (_ballMaterial != null)
            {
                Color targetEmission = isHumanPossessing
                    ? new Color(0.22f, 0.78f, 0.62f) * (1.0f + 0.25f * Mathf.Sin(Time.unscaledTime * 10f))
                    : new Color(0.32f, 0.24f, 0.02f);
                if (_ballMaterial.HasProperty("_EmissionColor"))
                    _ballMaterial.SetColor("_EmissionColor", targetEmission);
            }

            if (_groundDropMarker != null)
            {
                _groundDropMarker.position = new Vector3(p.x, 0.025f, p.z);
                float h = HeightAboveGround;
                float distScale = Mathf.Lerp(1.0f, 2.2f, Mathf.InverseLerp(10f, 65f, camDist));
                float pulse = Mathf.Sin(Time.unscaledTime * 11f) * 0.09f;
                float ringSize = isHumanPossessing
                    ? (1.42f + pulse)
                    : (h > 0.45f
                        ? Mathf.Lerp(1.15f, 1.95f, Mathf.Clamp01(h / 3.5f)) + Mathf.Sin(Time.unscaledTime * 14f) * 0.08f
                        : 1.08f) * distScale;
                _groundDropMarker.localScale = new Vector3(ringSize, ringSize, 1f);

                if (_groundDropMat != null)
                {
                    Color ringCol = isHumanPossessing
                        ? new Color(0.18f, 1.0f, 0.85f, 0.96f) * 2.6f
                        : new Color(1.0f, 0.88f, 0.18f, 0.92f) * 2.2f;
                    _groundDropMat.color = ringCol;
                    if (_groundDropMat.HasProperty("_BaseColor"))
                        _groundDropMat.SetColor("_BaseColor", ringCol);
                }
            }

            if (_possessionAuraOuter != null)
            {
                if (_possessionAuraOuter.gameObject.activeSelf != isPossessed)
                    _possessionAuraOuter.gameObject.SetActive(isPossessed);

                if (isPossessed)
                {
                    Vector3 ownerPos = CurrentOwner.transform.position;
                    Vector3 centerXZ = Vector3.Lerp(new Vector3(ownerPos.x, 0.032f, ownerPos.z), new Vector3(p.x, 0.032f, p.z), 0.55f);
                    _possessionAuraOuter.position = centerXZ;
                    float auraPulse = 1.0f + Mathf.Sin(Time.unscaledTime * 9.5f) * 0.11f;
                    float baseRadius = isHumanPossessing ? 2.35f : 1.95f;
                    _possessionAuraOuter.localScale = new Vector3(baseRadius * auraPulse, baseRadius * auraPulse, 1f);
                    _possessionAuraOuter.rotation = Quaternion.Euler(90f, Time.unscaledTime * 95f, 0f);

                    if (_possessionAuraMat != null)
                    {
                        bool isBlueTeam = CurrentOwner.attackingGoalSign > 0;
                        Color auraCol = isHumanPossessing
                            ? new Color(0.15f, 1.0f, 0.86f, 0.96f) * 3.1f
                            : (isBlueTeam
                                ? new Color(0.25f, 0.78f, 1.0f, 0.88f) * 2.4f
                                : new Color(1.0f, 0.34f, 0.22f, 0.90f) * 2.5f);
                        _possessionAuraMat.color = auraCol;
                        if (_possessionAuraMat.HasProperty("_BaseColor"))
                            _possessionAuraMat.SetColor("_BaseColor", auraCol);
                    }
                }
            }

            if (_overheadBeaconRoot != null)
            {
                // 내 발밑(6.5m 이내)에 있을 때는 드리블/오버헤드킥 시야를 가리지 않도록 감추고, 멀어질수록 크게 떠서 위치 안내!
                bool showBeacon = camDist > 6.5f;
                if (_overheadBeaconRoot.gameObject.activeSelf != showBeacon)
                    _overheadBeaconRoot.gameObject.SetActive(showBeacon);

                if (showBeacon)
                {
                    float distFactor = Mathf.InverseLerp(6.5f, 68f, camDist);
                    float bob = Mathf.Sin(Time.unscaledTime * 5.2f) * 0.18f;
                    float hoverY = Mathf.Lerp(2.15f, 4.4f, distFactor) + bob;
                    _overheadBeaconRoot.position = p + Vector3.up * hoverY;

                    float s = Mathf.Lerp(0.75f, 2.85f, distFactor);
                    _overheadBeaconRoot.localScale = new Vector3(s, s, s);

                    if (cam != null)
                    {
                        Vector3 toCam = cam.transform.position - _overheadBeaconRoot.position;
                        toCam.y = 0f;
                        if (toCam.sqrMagnitude > 0.01f)
                            _overheadBeaconRoot.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
                    }
                }
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

            // 공이 킥 지점에서 멀리 날아갈수록(특히 장거리·하프라인 뒤 슈팅) 공기저항으로 속도가 점점 줄어들도록 감속 적용!
            float travelDist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(_lastKickOrigin.x, _lastKickOrigin.z));

            if (travelDist > 18.0f && Rb.linearVelocity.sqrMagnitude > 2.25f)
            {
                float distRatio = Mathf.InverseLerp(18.0f, 56.0f, travelDist);
                float progressiveDecel = Mathf.Lerp(0.16f, 1.38f, distRatio * distRatio);
                float decay = Mathf.Exp(-progressiveDecel * Time.fixedDeltaTime);
                Vector3 v = Rb.linearVelocity;
                v.x *= decay;
                v.z *= decay;
                Rb.linearVelocity = v;
            }

            // 바닥 위에서 자연스러운 잔디 구름 감속 (장거리 이동 후에는 더 빠르게 감속)
            if (HeightAboveGround < 0.06f && Rb.linearVelocity.sqrMagnitude > 0.05f)
            {
                float rollFriction = travelDist > 28.0f ? 0.976f : 0.988f;
                Vector3 v = Rb.linearVelocity;
                v.x *= rollFriction;
                v.z *= rollFriction;
                Rb.linearVelocity = v;
            }

            // 경기장 이탈 방지 안전망 (바닥/천장 및 터치라인·골라인 밖 끼임 완벽 차단!)
            Vector3 p = transform.position;
            Vector3 vel = Rb.linearVelocity;
            bool posAdjusted = false;

            if (p.y < BallRadius * 0.85f)
            {
                p.y = BallRadius;
                posAdjusted = true;
                if (vel.y < 0f)
                    vel.y = -vel.y * 0.55f;
            }
            else if (p.y > 33.0f)
            {
                p.y = 33.0f;
                posAdjusted = true;
                if (vel.y > 0f)
                    vel.y = -vel.y * 0.45f;
            }

            // 1) 좌우 터치라인(Z축) 이탈 방지: 라인 밖으로 나가서 못 잡는 현상이 절대 발생하지 않도록 구장 안쪽으로 즉시 바운스 복귀!
            float maxZ = ShotCalculator.PitchHalfWidth - BallRadius * 0.5f;
            if (Mathf.Abs(p.z) > maxZ)
            {
                float signZ = Mathf.Sign(p.z);
                p.z = signZ * maxZ;
                posAdjusted = true;
                if (vel.z * signZ >= -0.5f)
                {
                    vel.z = -signZ * Mathf.Max(Mathf.Abs(vel.z) * 0.62f, 2.6f);
                }
            }

            // 2) 양쪽 골라인(X축) 이탈 방지: 골대 안(골문 폭 & 크로스바 높이 이내)으로 들어가는 슛만 통과를 허용하고,
            //    그 외 골라인 바깥이나 옆그물 바깥으로 나간 공은 즉시 구장 안쪽으로 튕겨 복귀!
            bool inGoalMouth = Mathf.Abs(p.z) < (ShotCalculator.GoalHalfWidth - 0.08f)
                            && p.y < (ShotCalculator.GoalHeight + 0.12f);
            float maxX = ShotCalculator.PitchHalfLength - BallRadius * 0.5f;
            if (!inGoalMouth && Mathf.Abs(p.x) > maxX)
            {
                float signX = Mathf.Sign(p.x);
                p.x = signX * maxX;
                posAdjusted = true;
                if (vel.x * signX >= -0.5f)
                {
                    vel.x = -signX * Mathf.Max(Mathf.Abs(vel.x) * 0.62f, 2.6f);
                }
            }

            if (posAdjusted)
            {
                transform.position = p;
                Rb.linearVelocity = vel;
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

            LastTouchPlayer = previousOwner;
            IsSuperShotActive = false;
            _magnusTimer = 0f;
            _magnusLateralAccel = Vector3.zero;
            _lockedOutPlayer = previousOwner;
            _pickupLockoutTimer = lockoutSeconds;
            _lastKickOrigin = transform.position;
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
            _lastKickOrigin = transform.position;

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
            _lastKickOrigin = worldPos;

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
