using System.Collections;
using Soccer3D.Data;
using Soccer3D.Gameplay;
using Soccer3D.Visuals;
using UnityEngine;

namespace Soccer3D.Effects
{
    public class ImpactVfxManager : MonoBehaviour
    {
        public static ImpactVfxManager Instance { get; private set; }

        private SoccerBall _trackedBall;
        private GameObject _ballAuraRoot;
        private TrailRenderer _mainTrail;
        private TrailRenderer _spiralTrail;
        private Transform _spiralPivot;
        private ParticleSystem _flightParticles;
        private Light _ballLight;
        private Renderer _coreRenderer;
        private float _auraTimer;

        private void Awake()
        {
            Instance = this;
        }

        public static void GetVfxPalette(ImpactVfxType type, out Color coreColor, out Color trailColor, out Color accentColor)
        {
            switch (type)
            {
                case ImpactVfxType.BlazingFire:
                    coreColor = new Color(1.0f, 0.52f, 0.08f, 0.9f);
                    trailColor = new Color(1.0f, 0.26f, 0.04f, 0.95f);
                    accentColor = new Color(1.0f, 0.90f, 0.25f, 1.0f);
                    break;

                case ImpactVfxType.NeonLaser:
                    coreColor = new Color(0.10f, 0.92f, 1.0f, 0.9f);
                    trailColor = new Color(0.05f, 0.65f, 1.0f, 0.95f);
                    accentColor = new Color(1.0f, 0.18f, 0.85f, 1.0f);
                    break;

                case ImpactVfxType.CosmicPlasma:
                    coreColor = new Color(0.68f, 0.20f, 1.0f, 0.9f);
                    trailColor = new Color(0.95f, 0.15f, 0.72f, 0.95f);
                    accentColor = new Color(0.38f, 0.85f, 1.0f, 1.0f);
                    break;

                case ImpactVfxType.GoldenLightning:
                    coreColor = new Color(1.0f, 0.88f, 0.18f, 0.9f);
                    trailColor = new Color(1.0f, 0.65f, 0.08f, 0.95f);
                    accentColor = new Color(0.75f, 0.95f, 1.0f, 1.0f);
                    break;

                default: // EmeraldSonic
                    coreColor = new Color(0.12f, 1.0f, 0.55f, 0.9f);
                    trailColor = new Color(0.05f, 0.88f, 0.75f, 0.95f);
                    accentColor = new Color(0.82f, 1.0f, 0.35f, 1.0f);
                    break;
            }
        }

        private void Update()
        {
            if (_ballAuraRoot == null || !_ballAuraRoot.activeSelf || _trackedBall == null)
                return;

            _ballAuraRoot.transform.position = _trackedBall.transform.position;
            if (_spiralPivot != null)
            {
                _spiralPivot.Rotate(0f, 0f, 1080f * Time.deltaTime, Space.Self);
                if (_trackedBall.Rb != null && _trackedBall.Rb.linearVelocity.sqrMagnitude > 0.5f)
                    _ballAuraRoot.transform.rotation = Quaternion.LookRotation(_trackedBall.Rb.linearVelocity.normalized);
            }

            _auraTimer -= Time.deltaTime;
            bool shouldStop = _auraTimer <= 0f || !_trackedBall.IsSuperShotActive || _trackedBall.CurrentOwner != null;
            if (shouldStop)
            {
                StopBallAura();
            }
        }

        public void TriggerShotImpactVfx(Vector3 footPos, SoccerBall ball, Vector3 shotDir, ImpactVfxType vfxType, bool isAcrobatic)
        {
            GetVfxPalette(vfxType, out Color coreCol, out Color trailCol, out Color accentCol);

            float scaleMult = isAcrobatic ? 1.45f : 1.0f;

            // 1. 발 타격 지점 쇼크웨이브 링 & 스파크 폭발
            StartCoroutine(SpawnShockwaveRing(footPos, shotDir, coreCol, accentCol, scaleMult));
            SpawnImpactBurstParticles(footPos, shotDir, coreCol, accentCol, isAcrobatic ? 75 : 45);

            // 2. 레이저 / 번개 계열(또는 아크로바틱 슛)은 전광석화 라이트닝/레이저 줄기 추가!
            if (vfxType == ImpactVfxType.NeonLaser || vfxType == ImpactVfxType.GoldenLightning || isAcrobatic)
            {
                StartCoroutine(SpawnLaserArcBolts(footPos, shotDir, accentCol, coreCol, vfxType == ImpactVfxType.NeonLaser));
            }

            // 3. 날아가는 공에 불꽃/레이저 오라 + 트레일 부착!
            if (ball != null)
            {
                AttachFlightAura(ball, vfxType, coreCol, trailCol, accentCol, isAcrobatic);
            }
        }

        /// <summary>
        /// 스페이스 2연타 순간 돌진 탈취(Blitz Dash) 및 어깨 밀치기 발동 시 소닉 쇼크웨이브 연출
        /// </summary>
        public void TriggerDashBurstVfx(Vector3 worldPos, Vector3 dashDir, bool isHeavyBlitz = true)
        {
            Vector3 dir = dashDir.sqrMagnitude > 0.001f ? dashDir.normalized : Vector3.forward;
            Color coreCol = isHeavyBlitz
                ? new Color(0.15f, 0.96f, 1.0f, 0.95f)
                : new Color(0.30f, 1.0f, 0.65f, 0.90f);
            Color accentCol = isHeavyBlitz
                ? new Color(0.85f, 1.0f, 0.40f, 1.0f)
                : new Color(1.0f, 0.92f, 0.35f, 1.0f);

            StartCoroutine(SpawnShockwaveRing(worldPos, dir, coreCol, accentCol, isHeavyBlitz ? 1.20f : 0.85f));
            SpawnImpactBurstParticles(worldPos, -dir, coreCol, accentCol, isHeavyBlitz ? 42 : 24);
        }

        public void StopBallAura()
        {
            if (_ballAuraRoot == null) return;
            if (_flightParticles != null) _flightParticles.Stop();
            if (_mainTrail != null) _mainTrail.emitting = false;
            if (_spiralTrail != null) _spiralTrail.emitting = false;
            _ballAuraRoot.SetActive(false);
        }

        private void AttachFlightAura(SoccerBall ball, ImpactVfxType vfxType, Color coreCol, Color trailCol, Color accentCol, bool isAcrobatic)
        {
            _trackedBall = ball;
            EnsureBallAuraCreated();

            _ballAuraRoot.transform.position = ball.transform.position;
            _ballAuraRoot.SetActive(true);
            _auraTimer = 2.4f;

            // 코어 에너지 구체
            _coreRenderer.sharedMaterial = MaterialHelper.CreateAdditiveVfxMaterial(coreCol, isAcrobatic ? 4.2f : 3.0f);
            _coreRenderer.transform.localScale = Vector3.one * (isAcrobatic ? 0.82f : 0.64f);

            // 메인 리본 트레일 (레이저는 날카롭고 쨍하게, 불꽃은 풍성하게!)
            bool isLaser = vfxType == ImpactVfxType.NeonLaser;
            _mainTrail.Clear();
            _mainTrail.time = isAcrobatic ? 0.42f : 0.30f;
            _mainTrail.startWidth = isLaser ? 0.36f : (isAcrobatic ? 0.62f : 0.48f);
            _mainTrail.endWidth = 0.02f;
            _mainTrail.sharedMaterial = MaterialHelper.CreateAdditiveVfxMaterial(trailCol, isLaser ? 5.0f : 3.6f);
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(accentCol, 0f), new GradientColorKey(trailCol, 0.45f), new GradientColorKey(coreCol, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            _mainTrail.colorGradient = g;
            _mainTrail.emitting = true;

            // 나선형 보조 레이저/화염 궤적
            _spiralTrail.Clear();
            _spiralTrail.time = 0.28f;
            _spiralTrail.startWidth = isAcrobatic ? 0.18f : 0.12f;
            _spiralTrail.endWidth = 0.0f;
            _spiralTrail.sharedMaterial = MaterialHelper.CreateAdditiveVfxMaterial(accentCol, 4.2f);
            _spiralTrail.emitting = true;

            // 비행 중 뿜어져 나오는 불티/플라즈마 파티클
            var main = _flightParticles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(accentCol, coreCol);
            main.startSize = isLaser ? new ParticleSystem.MinMaxCurve(0.08f, 0.22f) : new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
            _flightParticles.GetComponent<ParticleSystemRenderer>().sharedMaterial =
                MaterialHelper.CreateAdditiveVfxMaterial(coreCol, 3.2f);
            _flightParticles.Clear();
            _flightParticles.Play();

            // 잔디 위를 실시간으로 비추는 다이나믹 라이트
            _ballLight.color = Color.Lerp(coreCol, accentCol, 0.35f);
            _ballLight.intensity = isAcrobatic ? 7.5f : 5.0f;
            _ballLight.range = isAcrobatic ? 9.5f : 6.5f;
        }

        private void EnsureBallAuraCreated()
        {
            if (_ballAuraRoot != null) return;

            _ballAuraRoot = new GameObject("BallSuperAura");
            _ballAuraRoot.transform.SetParent(transform, false);

            GameObject coreSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coreSphere.name = "EnergyCore";
            Destroy(coreSphere.GetComponent<Collider>());
            coreSphere.transform.SetParent(_ballAuraRoot.transform, false);
            _coreRenderer = coreSphere.GetComponent<Renderer>();

            GameObject trailObj = new GameObject("MainTrail");
            trailObj.transform.SetParent(_ballAuraRoot.transform, false);
            _mainTrail = trailObj.AddComponent<TrailRenderer>();
            _mainTrail.minVertexDistance = 0.05f;
            _mainTrail.numCornerVertices = 4;
            _mainTrail.numCapVertices = 4;
            _mainTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _spiralPivot = new GameObject("SpiralPivot").transform;
            _spiralPivot.SetParent(_ballAuraRoot.transform, false);
            GameObject spiralTip = new GameObject("SpiralTip");
            spiralTip.transform.SetParent(_spiralPivot, false);
            spiralTip.transform.localPosition = new Vector3(0.28f, 0f, 0f);
            _spiralTrail = spiralTip.AddComponent<TrailRenderer>();
            _spiralTrail.minVertexDistance = 0.04f;
            _spiralTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject psObj = new GameObject("FlightEmbers");
            psObj.transform.SetParent(_ballAuraRoot.transform, false);
            _flightParticles = psObj.AddComponent<ParticleSystem>();
            var main = _flightParticles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 2.8f);
            main.maxParticles = 200;

            var emission = _flightParticles.emission;
            emission.rateOverTime = 95f;

            var shape = _flightParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.22f;

            var sizeOverLifetime = _flightParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            GameObject lightObj = new GameObject("BallGlowLight");
            lightObj.transform.SetParent(_ballAuraRoot.transform, false);
            _ballLight = lightObj.AddComponent<Light>();
            _ballLight.type = LightType.Point;
            _ballLight.shadows = LightShadows.None;
        }

        private IEnumerator SpawnShockwaveRing(Vector3 pos, Vector3 dir, Color c1, Color c2, float scaleMult)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ring.name = "ImpactShockwave";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.position = pos;
            if (dir.sqrMagnitude > 0.001f)
                ring.transform.rotation = Quaternion.LookRotation(dir.normalized);

            Material mat = MaterialHelper.CreateAdditiveVfxMaterial(Color.Lerp(c1, c2, 0.5f), 4.5f, useRing: true);
            Renderer rend = ring.GetComponent<Renderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 양면 보이도록 반대면 쿼드 추가
            GameObject backQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(backQuad.GetComponent<Collider>());
            backQuad.transform.SetParent(ring.transform, false);
            backQuad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            backQuad.GetComponent<Renderer>().sharedMaterial = mat;

            float duration = 0.28f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float s = Mathf.Lerp(0.35f, 3.3f * scaleMult, 1f - Mathf.Pow(1f - t, 3f));
                ring.transform.localScale = new Vector3(s, s, 1f);

                Color fade = Color.Lerp(c2, c1, t) * Mathf.Lerp(4.5f, 0f, t);
                fade.a = 1f - t;
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", fade);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", fade);
                yield return null;
            }

            Destroy(ring);
            Destroy(mat);
        }

        private void SpawnImpactBurstParticles(Vector3 pos, Vector3 shotDir, Color c1, Color c2, int count)
        {
            GameObject burstObj = new GameObject("FootImpactBurst");
            burstObj.transform.position = pos;
            if (shotDir.sqrMagnitude > 0.01f)
                burstObj.transform.rotation = Quaternion.LookRotation(shotDir.normalized);

            ParticleSystem ps = burstObj.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.48f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 16f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.36f);
            main.startColor = new ParticleSystem.MinMaxGradient(c1, c2);
            main.gravityModifier = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 42f;
            shape.radius = 0.15f;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ParticleSystemRenderer psr = burstObj.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = MaterialHelper.CreateAdditiveVfxMaterial(c2, 4.0f);
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.velocityScale = 0.05f;
            psr.lengthScale = 2.2f;

            ps.Play();
            Destroy(burstObj, 1.0f);
        }

        private IEnumerator SpawnLaserArcBolts(Vector3 origin, Vector3 shotDir, Color boltColor, Color coreColor, bool straightLaserBeam)
        {
            int boltCount = straightLaserBeam ? 3 : 4;
            GameObject container = new GameObject("ImpactLaserBolts");
            LineRenderer[] lines = new LineRenderer[boltCount];
            Material mat = MaterialHelper.CreateAdditiveVfxMaterial(boltColor, 5.2f);

            Vector3 dir = shotDir.sqrMagnitude > 0.01f ? shotDir.normalized : Vector3.right;

            for (int i = 0; i < boltCount; i++)
            {
                GameObject child = new GameObject("Bolt" + i);
                child.transform.SetParent(container.transform, false);
                LineRenderer lr = child.AddComponent<LineRenderer>();
                lr.sharedMaterial = mat;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.startWidth = straightLaserBeam && i == 0 ? 0.24f : 0.09f;
                lr.endWidth = 0.01f;

                int segs = straightLaserBeam && i == 0 ? 2 : 7;
                lr.positionCount = segs;

                if (segs == 2)
                {
                    // 발끝에서 뻗어나가는 일직선 섬광 레이저 빔!
                    lr.SetPosition(0, origin - dir * 0.4f);
                    lr.SetPosition(1, origin + dir * 7.5f);
                }
                else
                {
                    Vector3 end = origin + Quaternion.Euler(Random.Range(-35f, 35f), Random.Range(-35f, 35f), 0f) * (dir * Random.Range(1.8f, 3.6f));
                    for (int s = 0; s < segs; s++)
                    {
                        float t = s / (float)(segs - 1);
                        Vector3 pt = Vector3.Lerp(origin, end, t);
                        if (s > 0 && s < segs - 1)
                            pt += Random.insideUnitSphere * 0.26f;
                        lr.SetPosition(s, pt);
                    }
                }
                lines[i] = lr;
            }

            float dur = 0.22f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / dur);
                Color c = Color.Lerp(coreColor, boltColor, alpha) * (4.8f * alpha);
                c.a = alpha;
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                yield return null;
            }

            Destroy(container);
            Destroy(mat);
        }
    }
}

