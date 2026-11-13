using System;
using System.Collections;
using Soccer3D.CameraSystem;
using Soccer3D.Data;
using Soccer3D.Visuals;
using UnityEngine;

namespace Soccer3D.Gameplay
{
    public class GoalkeeperAI : MonoBehaviour
    {
        public int defendingGoalSign = 1; // +1 = 오른쪽(+X) 골문 수비(AI측 키퍼), -1 = 왼쪽(-X) 골문 수비(플레이어측 키퍼)
        public SoccerBall ball;
        public PlayerVisualRig visualRig;
        public PlayerProfile Profile { get; private set; }

        public event Action<GoalkeeperAI, bool> OnSaveMade; // (키퍼, 슈퍼다이빙세이브여부)

        private Vector3 _homeGoalCenter;
        private Vector3 _diveTargetPos;
        private float _diveTimer;
        private float _diveTotalDuration;
        private float _diveJumpHeight;
        private float _saveCooldown;
        private Vector3 _currentVelocity;
        private bool _fullGloveStretchOnCurrentShot = true;
        private bool _hasJudgedCurrentSuperShot;

        public void Initialize(int goalSign, SoccerBall targetBall, Color kitAccent)
        {
            defendingGoalSign = goalSign;
            ball = targetBall;
            _homeGoalCenter = new Vector3(goalSign * ShotCalculator.PitchHalfLength, 0f, 0f);

            PlayerProfile gkProfile = PlayerProfileGenerator.GenerateGoalkeeperProfile(goalSign < 0);
            gkProfile.jerseySecondaryColor = kitAccent;
            ApplyProfile(gkProfile);

            ResetPosition();

            if (ball != null)
            {
                ball.OnShotLaunched -= HandleShotLaunched;
                ball.OnShotLaunched += HandleShotLaunched;
            }
        }

        public void ApplyProfile(PlayerProfile profile)
        {
            if (profile == null) return;
            Profile = profile;
            visualRig = gameObject.GetComponent<PlayerVisualRig>();
            if (visualRig == null)
                visualRig = gameObject.AddComponent<PlayerVisualRig>();
            visualRig.BuildFromProfile(profile, isGoalkeeper: true);
        }

        private void OnDestroy()
        {
            if (ball != null)
                ball.OnShotLaunched -= HandleShotLaunched;
        }

        public void ResetPosition()
        {
            _homeGoalCenter = new Vector3(defendingGoalSign * ShotCalculator.PitchHalfLength, 0f, 0f);
            _diveTimer = 0f;
            _diveTotalDuration = 0f;
            _diveJumpHeight = 0f;
            _saveCooldown = 0f;
            _currentVelocity = Vector3.zero;
            _fullGloveStretchOnCurrentShot = true;
            _hasJudgedCurrentSuperShot = false;
            transform.position = new Vector3(defendingGoalSign * (ShotCalculator.PitchHalfLength - 1.25f), 0f, 0f);
            transform.rotation = Quaternion.LookRotation(new Vector3(-defendingGoalSign, 0f, 0f));
        }

        private void HandleShotLaunched(ShotMetadata meta)
        {
            if (meta.targetTeamGoal != defendingGoalSign)
                return;

            _hasJudgedCurrentSuperShot = true;
            StopAllCoroutines();
            StartCoroutine(ReactToShotRoutine(meta));
        }

        private IEnumerator ReactToShotRoutine(ShotMetadata meta)
        {
            bool isAcrobatic = meta.shotType == ShotType.OverheadKick || meta.shotType == ShotType.AcrobaticVolley;
            float quality = meta.shotQuality01;

            Vector3 shooterPos = meta.shooter != null ? meta.shooter.transform.position : Vector3.zero;
            float shotDist = meta.shooter != null
                ? Vector2.Distance(
                    new Vector2(shooterPos.x, shooterPos.z),
                    new Vector2(_homeGoalCenter.x, _homeGoalCenter.z))
                : 20f;
            bool fromBehindHalfway = (meta.shooter != null && (shooterPos.x * defendingGoalSign) <= 0f) || shotDist >= 44.0f;

            // 민첩하고 자연스러운 골키퍼 반사신경 (이전보다 조금 더 기민하게 반응!)
            float baseReflex = Mathf.Lerp(0.095f, 0.165f, Mathf.Clamp01(quality));
            if (shotDist < 12.5f) baseReflex += 0.018f;
            if (isAcrobatic) baseReflex += 0.032f;

            yield return new WaitForSeconds(baseReflex);

            if (ball == null) yield break;

            // 장거리 / 하프라인 뒤에서 날아오는 슛은 공이 골문 앞 20m 안쪽으로 들어올 때까지 미리 스텝을 밟아 코스를 따라간 뒤 정확한 타이밍에 다이빙!
            float waitGuardTimer = 0f;
            while (ball != null && ball.CurrentOwner == null && waitGuardTimer < 2.8f)
            {
                float currentDistToGoalX = Mathf.Abs(ball.transform.position.x - _homeGoalCenter.x);
                if (currentDistToGoalX <= 20.5f || (ball.Rb != null && ball.Rb.linearVelocity.x * defendingGoalSign <= 2.0f))
                    break;

                waitGuardTimer += Time.deltaTime;
                yield return null;
            }

            if (ball == null) yield break;

            // 커브 예측률 68% (하프라인 뒤 초장거리 슛은 궤적을 95% 완벽히 읽음!)
            float curveRead = fromBehindHalfway ? 0.97f : 0.90f;
            Vector3 predictedCross = meta.targetPoint;
            if (PredictGoalPlaneCrossing(out Vector3 simCross, out float _, curveReadFactor: curveRead))
            {
                predictedCross = simCross;
            }

            float targetZ = Mathf.Clamp(predictedCross.z, -ShotCalculator.GoalHalfWidth - 0.15f, ShotCalculator.GoalHalfWidth + 0.15f);
            float targetY = Mathf.Clamp(predictedCross.y, 0.25f, ShotCalculator.GoalHeight + 0.20f);
            float cornerDepth01 = Mathf.InverseLerp(1.05f, ShotCalculator.GoalHalfWidth - 0.22f, Mathf.Abs(targetZ));

            // 골키퍼 난이도 소폭 상향 조정 ("좀 더 아주 좀만 더 잘 막게" + 하프라인 뒤 이상한 슛은 확실히 선방!):
            float attackEdge01 = Mathf.Clamp01(Mathf.InverseLerp(0.35f, 0.98f, quality) * 0.60f + cornerDepth01 * 0.52f);
            float saveChance = Mathf.Lerp(0.88f, 0.38f, attackEdge01);

            if (isAcrobatic)
                saveChance -= 0.11f;

            if (shotDist > 25f)
                saveChance += Mathf.InverseLerp(25f, 44f, shotDist) * 0.28f;
            if (meta.usedWeakFoot)
                saveChance += 0.18f;

            if (fromBehindHalfway)
                saveChance = Mathf.Max(saveChance, 0.97f); // 하프라인 뒤에서 찬 장거리 슛은 골키퍼가 안정적으로 캐치/선방!
            else if (Mathf.Abs(targetZ - transform.position.z) < 0.96f && shotDist > 8.5f)
                saveChance = Mathf.Max(saveChance, 0.85f); // 정면 근처 코스는 든든하게 선방!

            saveChance = Mathf.Clamp(saveChance, 0.20f, 0.97f);
            _fullGloveStretchOnCurrentShot = UnityEngine.Random.value < saveChance;

            float destZ;
            if (_fullGloveStretchOnCurrentShot)
            {
                float readErrorZ = fromBehindHalfway ? UnityEngine.Random.Range(-0.06f, 0.06f) : UnityEngine.Random.Range(-0.12f, 0.12f);
                destZ = Mathf.Clamp(targetZ + readErrorZ, -ShotCalculator.GoalHalfWidth + 0.14f, ShotCalculator.GoalHalfWidth - 0.14f);
            }
            else
            {
                // 키퍼가 뚫리는 슛일 때: 공 쪽으로 몸을 날리되 손끝이 0.92~1.26m 아슬아슬하게 못 미쳐 골문 구석으로 빨려 들어가도록 연출!
                float shortSign = Mathf.Abs(transform.position.z - targetZ) > 0.15f
                    ? Mathf.Sign(transform.position.z - targetZ)
                    : (targetZ >= 0f ? -1f : 1f);
                float missGap = UnityEngine.Random.Range(0.92f, 1.26f);
                destZ = Mathf.Clamp(targetZ + shortSign * missGap, -ShotCalculator.GoalHalfWidth + 0.20f, ShotCalculator.GoalHalfWidth - 0.20f);
            }

            float divePlaneX = defendingGoalSign * (ShotCalculator.PitchHalfLength - 0.92f);

            _diveTargetPos = new Vector3(divePlaneX, 0f, destZ);
            _diveTotalDuration = 0.78f;
            _diveTimer = _diveTotalDuration;
            _diveJumpHeight = targetY > 1.32f ? Mathf.Clamp((targetY - 1.18f) * 0.48f, 0.16f, 0.64f) : 0.06f;

            if (visualRig != null)
            {
                float diveSign = Mathf.Sign(destZ - transform.position.z);
                if (Mathf.Abs(destZ - transform.position.z) > 0.35f)
                {
                    float localRightDot = Vector3.Dot(Vector3.forward * diveSign, transform.right);
                    visualRig.TriggerKeeperDive(localRightDot >= 0f ? 1f : -1f, 0.68f);
                }
            }
        }

        /// <summary>
        /// 현재 공의 속도·마그누스 커브·중력·댐핑을 수치 적분하여 골라인 평면 통과 위치(Y, Z) 계산
        /// </summary>
        private bool PredictGoalPlaneCrossing(out Vector3 crossingPoint, out float timeToCross, float curveReadFactor = 0.68f)
        {
            crossingPoint = Vector3.zero;
            timeToCross = 99f;

            if (ball == null || ball.Rb == null)
                return false;

            Vector3 pos = ball.transform.position;
            Vector3 vel = ball.Rb.linearVelocity;
            if (vel.x * defendingGoalSign <= 3.0f)
                return false;

            float goalPlaneX = defendingGoalSign * (ShotCalculator.PitchHalfLength - 0.90f);
            float k = Mathf.Max(0.01f, ball.Rb.linearDamping);
            Vector3 curveAcc = ball.ActiveCurveAcceleration * curveReadFactor;
            float remCurve = ball.RemainingCurveTime;

            const float dtStep = 0.04f;
            float simTime = 0f;

            for (int step = 0; step < 80; step++)
            {
                Vector3 prevPos = pos;
                Vector3 acc = Physics.gravity + (simTime < remCurve ? curveAcc : Vector3.zero);
                vel += acc * dtStep;
                vel *= Mathf.Exp(-k * dtStep);
                pos += vel * dtStep;
                simTime += dtStep;

                if (pos.y < SoccerBall.BallRadius)
                {
                    pos.y = SoccerBall.BallRadius;
                    if (vel.y < 0f) vel.y = -vel.y * 0.55f;
                }

                bool crossed = defendingGoalSign > 0
                    ? (prevPos.x <= goalPlaneX && pos.x >= goalPlaneX)
                    : (prevPos.x >= goalPlaneX && pos.x <= goalPlaneX);

                if (crossed)
                {
                    float frac = Mathf.InverseLerp(prevPos.x, pos.x, goalPlaneX);
                    crossingPoint = Vector3.Lerp(prevPos, pos, frac);
                    timeToCross = simTime;
                    return true;
                }
            }

            return false;
        }

        private void Update()
        {
            if (ball == null) return;

            float dt = Time.deltaTime;
            if (_saveCooldown > 0f) _saveCooldown -= dt;

            if (!ball.IsSuperShotActive)
                _hasJudgedCurrentSuperShot = false;

            // 정식 슈팅이 아닌 굴절 루즈볼이나 감속된 장거리 볼이 골문 안쪽으로 흘러들 때도 보조 반응 다이빙!
            if (_diveTimer <= 0f && !_hasJudgedCurrentSuperShot && !ball.IsSuperShotActive && ball.CurrentOwner == null && ball.Rb != null)
            {
                if (ball.Rb.linearVelocity.x * defendingGoalSign > 7.5f &&
                    PredictGoalPlaneCrossing(out Vector3 fastCross, out float tCross, 0.72f) &&
                    tCross < 0.85f &&
                    Mathf.Abs(fastCross.z) <= ShotCalculator.GoalHalfWidth + 0.50f)
                {
                    _fullGloveStretchOnCurrentShot = UnityEngine.Random.value < 0.76f;
                    float offset = _fullGloveStretchOnCurrentShot ? 0f : (fastCross.z >= 0f ? -1.05f : 1.05f);
                    _diveTargetPos = new Vector3(
                        defendingGoalSign * (ShotCalculator.PitchHalfLength - 0.92f),
                        0f,
                        Mathf.Clamp(fastCross.z + offset, -ShotCalculator.GoalHalfWidth + 0.20f, ShotCalculator.GoalHalfWidth - 0.20f));
                    _diveTotalDuration = 0.58f;
                    _diveTimer = _diveTotalDuration;
                    _diveJumpHeight = fastCross.y > 1.35f ? 0.44f : 0.06f;
                }
            }

            Vector3 desiredPlanarPos;
            float moveSpeed;
            float currentY = 0f;

            if (_diveTimer > 0f)
            {
                _diveTimer -= dt;
                desiredPlanarPos = _diveTargetPos;
                moveSpeed = 11.2f; // 이전(9.6)보다 조금 더 탄력 있고 날렵하게 다이빙하도록 상향!

                float phase = 1f - Mathf.Clamp01(_diveTimer / Mathf.Max(0.05f, _diveTotalDuration));
                currentY = Mathf.Sin(phase * Mathf.PI) * _diveJumpHeight;
            }
            else
            {
                // 골키퍼 기본 포지셔닝 (공의 예상 골라인 궤적까지 함께 고려하여 장거리 슛을 미리 따라감!)
                Vector3 ballPos = ball.transform.position;
                Vector3 fromGoalToBall = ballPos - _homeGoalCenter;
                fromGoalToBall.y = 0f;
                float distFromGoal = fromGoalToBall.magnitude;
                Vector3 bisectorDir = fromGoalToBall.sqrMagnitude > 0.01f
                    ? fromGoalToBall.normalized
                    : new Vector3(-defendingGoalSign, 0f, 0f);

                float stepOut;
                if (distFromGoal < 10.5f)
                    stepOut = Mathf.Lerp(3.25f, 2.20f, distFromGoal / 10.5f);
                else if (distFromGoal < 26.0f)
                    stepOut = Mathf.Lerp(2.20f, 1.35f, (distFromGoal - 10.5f) / 15.5f);
                else
                    stepOut = 1.20f;

                Vector3 guardPos = _homeGoalCenter + bisectorDir * stepOut;

                // 장거리 슛이 날아오는 중이면 골라인 통과 예상 Z위치 쪽으로 미리 사이드 스텝 이동!
                if (ball.CurrentOwner == null && ball.Rb != null && ball.Rb.linearVelocity.x * defendingGoalSign > 6.0f &&
                    PredictGoalPlaneCrossing(out Vector3 trackCross, out float _, 0.70f))
                {
                    guardPos.z = Mathf.Lerp(guardPos.z, trackCross.z, 0.65f);
                }

                float maxGuardZ = ShotCalculator.GoalHalfWidth - 0.65f;
                guardPos.z = Mathf.Clamp(guardPos.z, -maxGuardZ, maxGuardZ);
                guardPos.x = defendingGoalSign > 0
                    ? Mathf.Clamp(guardPos.x, ShotCalculator.PitchHalfLength - 3.6f, ShotCalculator.PitchHalfLength - 0.85f)
                    : Mathf.Clamp(guardPos.x, -ShotCalculator.PitchHalfLength + 0.85f, -ShotCalculator.PitchHalfLength + 3.6f);

                desiredPlanarPos = new Vector3(guardPos.x, 0f, guardPos.z);
                moveSpeed = 6.2f;
            }

            Vector3 prevPlanar = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 nextPlanar = Vector3.MoveTowards(prevPlanar, desiredPlanarPos, moveSpeed * dt);
            transform.position = new Vector3(nextPlanar.x, currentY, nextPlanar.z);
            _currentVelocity = dt > 0.0001f ? (nextPlanar - prevPlanar) / dt : Vector3.zero;

            // 항상 공 방향 주시
            Vector3 lookDir = ball.transform.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir.normalized), dt * 13f);
            }

            if (visualRig != null)
                visualRig.UpdateProceduralAnimation(_currentVelocity, false, dt);

            CheckParryOrClear();
        }

        private void CheckParryOrClear()
        {
            if (_saveCooldown > 0f || ball == null || ball.CurrentOwner != null || ball.Rb == null)
                return;

            Vector3 ballPos = ball.transform.position;
            float speed = ball.Rb.linearVelocity.magnitude;

            // 키퍼 몸통~양손 글러브 3D 세이브 캡슐 범위 계산
            float keeperCenterY = transform.position.y + 1.16f;
            float dy = Mathf.Abs(ballPos.y - keeperCenterY);
            float planarDist = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.z),
                new Vector2(ballPos.x, ballPos.z));

            // 1. 슈팅 선방 판정
            if (ball.IsSuperShotActive || speed > 7.2f)
            {
                if (Mathf.Sign(ball.Rb.linearVelocity.x) != defendingGoalSign)
                    return;

                float maxPlanarReach = _fullGloveStretchOnCurrentShot
                    ? (_diveTimer > 0f ? 1.52f : 1.28f)
                    : 0.62f;
                float maxVerticalHalfSpan = _fullGloveStretchOnCurrentShot ? 1.24f : 0.72f;

                if (planarDist <= maxPlanarReach && dy <= maxVerticalHalfSpan)
                {
                    _saveCooldown = 0.55f;
                    bool isDivingSave = _diveTimer > 0f || speed > 21f;

                    // 골문 정면 위험지역 대신 측면/전방 멀리 강력하게 펀칭 클리어링! (초강력 아크로바틱 슛일 때만 세컨볼 높이 굴절)
                    float sideClearSign = Mathf.Abs(ballPos.z) > 0.4f ? Mathf.Sign(ballPos.z) : (UnityEngine.Random.value < 0.5f ? 1f : -1f);
                    Vector3 parryVel;
                    if (speed > 31f && ball.LastShot.shotType == ShotType.OverheadKick)
                    {
                        parryVel = new Vector3(
                            -defendingGoalSign * UnityEngine.Random.Range(9.5f, 14.5f),
                            UnityEngine.Random.Range(7.5f, 10.5f),
                            sideClearSign * UnityEngine.Random.Range(5.0f, 9.5f));
                    }
                    else
                    {
                        parryVel = new Vector3(
                            -defendingGoalSign * UnityEngine.Random.Range(14.5f, 21.5f),
                            UnityEngine.Random.Range(4.8f, 8.2f),
                            sideClearSign * UnityEngine.Random.Range(7.5f, 13.5f));
                    }

                    ball.Rb.linearVelocity = parryVel;
                    ball.Rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 20f;

                    if (DynamicSoccerCamera.Instance != null)
                        DynamicSoccerCamera.Instance.TriggerImpactShake(0.24f, 0.18f);

                    OnSaveMade?.Invoke(this, isDivingSave);
                }
            }
            else if (planarDist <= 1.65f && dy <= 1.35f)
            {
                // 2. 골 에어리어 근처 루즈볼 즉시 롱킥 클리어링!
                _saveCooldown = 0.75f;
                float clearZ = UnityEngine.Random.Range(-12f, 12f);
                Vector3 clearVel = new Vector3(-defendingGoalSign * UnityEngine.Random.Range(18f, 25f), UnityEngine.Random.Range(6.5f, 9.8f), clearZ);
                ball.Rb.linearVelocity = clearVel;
                if (visualRig != null)
                    visualRig.TriggerNormalKick(PreferredFoot.Right, false);
            }
        }
    }
}
