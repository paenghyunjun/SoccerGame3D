using System.Collections;
using Soccer3D.Visuals;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Soccer3D.Effects
{
    public class GoalCelebrationManager : MonoBehaviour
    {
        public static GoalCelebrationManager Instance { get; private set; }

        private Bloom _sceneBloom;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            Volume vol = FindAnyObjectByType<Volume>();
            if (vol != null && vol.profile != null)
            {
                vol.profile.TryGet(out _sceneBloom);
            }
        }

        public void PlayGoalSpectacle(Vector3 goalLineCenter, Color teamPrimaryColor, bool isAcrobaticGoal)
        {
            StartCoroutine(GoalSpectacleRoutine(goalLineCenter, teamPrimaryColor, isAcrobaticGoal));
        }

        public void TriggerBriefImpactBloom(float peakIntensity = 1.1f)
        {
            if (_sceneBloom != null)
                StartCoroutine(BloomPulseRoutine(peakIntensity, 0.22f));
        }

        private IEnumerator BloomPulseRoutine(float peak, float duration)
        {
            if (_sceneBloom == null) yield break;
            float baseVal = 0.30f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(t / duration);
                _sceneBloom.intensity.value = Mathf.Lerp(baseVal, peak, k * k);
                yield return null;
            }
            _sceneBloom.intensity.value = baseVal;
        }

        private IEnumerator GoalSpectacleRoutine(Vector3 goalCenter, Color teamCol, bool isAcrobatic)
        {
            if (_sceneBloom != null)
                StartCoroutine(BloomPulseRoutine(isAcrobatic ? 1.85f : 1.35f, 0.9f));

            // 1. 양쪽 골포스트 황금 화염 분수!
            SpawnGoalpostFountain(goalCenter + new Vector3(0f, 0.1f, -3.8f), new Color(1f, 0.78f, 0.15f), teamCol);
            SpawnGoalpostFountain(goalCenter + new Vector3(0f, 0.1f, 3.8f), new Color(1f, 0.78f, 0.15f), teamCol);

            // 2. 경기장 상공 화려한 골 컨페티 폭죽 샤워!
            SpawnConfettiShower(goalCenter + new Vector3(-Mathf.Sign(goalCenter.x) * 6f, 9.5f, 0f));

            // 3. 연속 스타버스트 불꽃놀이 7연발!
            Color[] fireworkColors =
            {
                new Color(1.0f, 0.30f, 0.20f),
                new Color(0.18f, 0.85f, 1.0f),
                new Color(1.0f, 0.88f, 0.15f),
                new Color(0.85f, 0.22f, 1.0f),
                new Color(0.20f, 1.0f, 0.48f),
                teamCol
            };

            int bursts = isAcrobatic ? 9 : 6;
            for (int i = 0; i < bursts; i++)
            {
                Vector3 burstPos = goalCenter + new Vector3(
                    Mathf.Sign(goalCenter.x) * Random.Range(1.5f, 6.5f),
                    Random.Range(4.8f, 10.5f),
                    Random.Range(-11f, 11f)
                );
                Color c1 = fireworkColors[i % fireworkColors.Length];
                Color c2 = Color.white;
                SpawnFireworkStarburst(burstPos, c1, c2, isAcrobatic ? 110 : 75);
                yield return new WaitForSecondsRealtime(0.16f);
            }
        }

        private void SpawnGoalpostFountain(Vector3 pos, Color gold, Color accent)
        {
            GameObject go = new GameObject("GoalpostPyro");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 2.0f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(9f, 17f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            main.startColor = new ParticleSystem.MinMaxGradient(gold, accent);
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 85f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.15f;

            var sizeLife = ps.sizeOverLifetime;
            sizeLife.enabled = true;
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = MaterialHelper.CreateAdditiveVfxMaterial(gold, 3.8f);
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.lengthScale = 2.4f;

            ps.Play();
            Destroy(go, 3.4f);
        }

        private void SpawnFireworkStarburst(Vector3 pos, Color primary, Color secondary, int count)
        {
            GameObject go = new GameObject("GoalFireworkBurst");
            go.transform.position = pos;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.65f, 1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 16f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.48f);
            main.startColor = new ParticleSystem.MinMaxGradient(primary, secondary);
            main.gravityModifier = 0.65f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            var sizeLife = ps.sizeOverLifetime;
            sizeLife.enabled = true;
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = MaterialHelper.CreateAdditiveVfxMaterial(primary, 4.4f);
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.lengthScale = 2.6f;

            // 순간 플래시 라이트
            Light flash = go.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = primary;
            flash.range = 18f;
            flash.intensity = 10f;
            StartCoroutine(FadeLightOut(flash, 0.55f));

            ps.Play();
            Destroy(go, 2.2f);
        }

        private IEnumerator FadeLightOut(Light l, float dur)
        {
            float start = l.intensity;
            float t = 0f;
            while (t < dur && l != null)
            {
                t += Time.unscaledDeltaTime;
                l.intensity = Mathf.Lerp(start, 0f, t / dur);
                yield return null;
            }
        }

        private void SpawnConfettiShower(Vector3 centerTop)
        {
            GameObject go = new GameObject("GoalConfettiShower");
            go.transform.position = centerTop;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 2.4f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.28f);
            main.startRotation3D = true;
            main.gravityModifier = 0.45f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            Gradient rainbow = new Gradient();
            rainbow.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.85f, 0.15f), 0f),
                    new GradientColorKey(new Color(0.15f, 0.85f, 1f), 0.33f),
                    new GradientColorKey(new Color(1f, 0.25f, 0.65f), 0.66f),
                    new GradientColorKey(new Color(0.25f, 1f, 0.45f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            var colGradient = new ParticleSystem.MinMaxGradient(rainbow)
            {
                mode = ParticleSystemGradientMode.RandomColor
            };
            main.startColor = colGradient;

            var emission = ps.emission;
            emission.rateOverTime = 95f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(16f, 1f, 18f);

            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = MaterialHelper.CreateAdditiveVfxMaterial(Color.white, 2.2f);

            ps.Play();
            Destroy(go, 5.2f);
        }
    }
}
