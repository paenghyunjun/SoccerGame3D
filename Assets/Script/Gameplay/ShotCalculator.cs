using Soccer3D.CameraSystem;
using Soccer3D.Data;
using UnityEngine;

namespace Soccer3D.Gameplay
{
    public struct ComputedShotResult
    {
        public ShotMetadata metadata;
        public Vector3 launchVelocity;
        public Vector3 magnusAcceleration;
        public float curveDuration;
        public Vector3 spinAngularVelocity;
        public string feedbackSummary;
    }

    public static class ShotCalculator
    {
        public const float PitchHalfLength = 52.5f; // FIFA 정규 규격 105m (반장 52.5m)
        public const float PitchHalfWidth = 34.0f;  // FIFA 정규 규격 68m  (반폭 34.0m)
        public const float GoalHalfWidth = 3.66f;   // FIFA 정규 골문 너비 7.32m (반폭 3.66m)
        public const float GoalHeight = 2.44f;      // FIFA 정규 골문 높이 2.44m

        /// <summary>
        /// 현재 선수 자세와 공 높이/거리를 바탕으로 오버헤드킥 또는 아크로바틱 발리 가능 여부 판정
        /// </summary>
        public static bool CheckAcrobaticOpportunity(
            SoccerPlayerController player,
            SoccerBall ball,
            Vector3 goalCenterPos,
            bool holdingAcrobaticKey,
            out ShotType acrobaticType,
            bool requireMouseAimOnBall = true)
        {
            acrobaticType = ShotType.Normal;
            if (player == null || ball == null) return false;

            // 드리블 완전 소유 상태(지상)가 아니고 공이 떠 있어야 함
            float h = ball.HeightAboveGround;
            if (h < 0.48f || h > 2.95f)
                return false;

            PlayerProfile p = player.Profile;
            float acroNorm = p != null ? Mathf.InverseLerp(45f, 99f, p.acrobatics) : 0.5f;
            float reachRadius = 1.55f + (p != null ? p.AerialReachBonus : 0f) * 0.35f + acroNorm * 0.40f;

            Vector3 playerPlanar = new Vector3(player.transform.position.x, 0f, player.transform.position.z);
            Vector3 ballPlanar = new Vector3(ball.transform.position.x, 0f, ball.transform.position.z);
            float planarDist = Vector3.Distance(playerPlanar, ballPlanar);

            if (planarDist > reachRadius)
                return false;

            // 1인칭 플레이어는 마우스 에임(+)이 공에 올라가 있을 때만 아크로바틱 슛 발동! (에임이 공에 안 들어가 있으면 발동 안 됨)
            if (!player.isAI && requireMouseAimOnBall && !player.CheckMouseAimOnBall())
                return false;

            Vector3 toGoal = (goalCenterPos - player.transform.position);
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude < 0.01f) toGoal = Vector3.right;
            toGoal.Normalize();

            Vector3 fwd = player.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = toGoal;
            fwd.Normalize();

            float facingGoalDot = Vector3.Dot(fwd, toGoal);

            // 오버헤드킥 조건 (아크로바틱 골이 많이 나오도록 판정을 넉넉하고 직관적으로 설계!):
            // 1) 골문을 등지거나 옆으로 선 자세 (각도 약 52도 이상, dot < 0.62)
            // 2) 공이 가슴/어깨 높이 이상(>= 1.02m) 떠 있을 때 (셀프 플릭 Q / 로빙 A 직후 포함!)
            // 3) C키(아크로바틱 자세 키)를 누른 상태
            if (facingGoalDot < 0.62f || h >= 0.90f || holdingAcrobaticKey)
            {
                acrobaticType = ShotType.OverheadKick;
                return true;
            }

            // 골문을 정면으로 보며 낮게 뜬 공(0.48~1.02m)은 시저스 아크로바틱 발리!
            acrobaticType = ShotType.AcrobaticVolley;
            return true;
        }

        /// <summary>
        /// 선수의 몸 기준 공 위치·각도·감아차기 방향을 분석해 실제 타격 발(오른발/왼발) 결정
        /// </summary>
        public static PreferredFoot DetermineStrikingFoot(
            SoccerPlayerController shooter,
            Vector3 ballPos,
            Vector3 goalCenter,
            ShotType shotType,
            Vector2 moveAimInput)
        {
            PlayerProfile p = shooter.Profile;
            PreferredFoot pref = p != null ? p.preferredFoot : PreferredFoot.Right;

            Vector3 toGoal = (goalCenter - shooter.transform.position);
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude < 0.01f) toGoal = Vector3.right;
            toGoal.Normalize();

            // 공격 방향 기준 오른쪽 단위벡터 (수평면)
            Vector3 attackRight = Vector3.Cross(Vector3.up, toGoal).normalized;
            float ballSideOffset = Vector3.Dot(ballPos - shooter.transform.position, attackRight);

            // 감아차기(Z+D)는 인사이드 컬 특성상 경기장 좌측(측면)에서 우측 골문으로 감을 때 오른발,
            // 우측 측면에서 감을 때 왼발이 자연스럽게 나감 (단, 공이 확실히 한쪽 발 앞에 있으면 그 발 사용)
            if (shotType == ShotType.FinesseCurve)
            {
                if (Mathf.Abs(ballSideOffset) > 0.22f)
                    return ballSideOffset >= 0f ? PreferredFoot.Right : PreferredFoot.Left;

                // 공격 방향 기준 선수가 좌측 하프스페이스에 있으면 오른발 감아차기, 우측이면 왼발 감아차기 유리
                float fieldSide = Vector3.Dot(shooter.transform.position - goalCenter, attackRight);
                if (Mathf.Abs(fieldSide) > 2.4f)
                    return fieldSide < 0f ? PreferredFoot.Right : PreferredFoot.Left;

                return pref;
            }

            // 일반 슛 / 오버헤드킥: 공이 몸 중심에서 어느 발 쪽으로 치우쳐 있는지 + 진행 방향 기반 판정
            if (ballSideOffset > 0.14f)
                return PreferredFoot.Right;
            if (ballSideOffset < -0.14f)
                return PreferredFoot.Left;

            // 애매한 중앙에 있을 때는 이동 방향이 약발 쪽으로 급격히 꺾여 있으면 약발이 걸릴 수 있음
            float lateralMove = Vector3.Dot(new Vector3(moveAimInput.x, 0f, moveAimInput.y), attackRight);
            if (pref == PreferredFoot.Right && lateralMove < -0.65f && Random.value < 0.55f)
                return PreferredFoot.Left;
            if (pref == PreferredFoot.Left && lateralMove > 0.65f && Random.value < 0.55f)
                return PreferredFoot.Right;

            return pref;
        }

        /// <summary>
        /// 스탯(골결정력·슛파워·중거리·커브·아크로바틱), 거리, 주발/약발, 게이지 차징을 모두 반영하여 최종 슈팅 물리 산출
        /// </summary>
        public static ComputedShotResult ComputeShot(
            SoccerPlayerController shooter,
            SoccerBall ball,
            GoalkeeperAI opposingKeeper,
            int targetGoalSign, // +1 = 오른쪽(+X) 골문, -1 = 왼쪽(-X) 골문
            ShotType shotType,
            float charge01,
            Vector2 aimInput,
            float customCurveBias = 0f)
        {
            PlayerProfile prof = shooter.Profile;
            Vector3 startPos = ball.transform.position;
            Vector3 goalCenter = new Vector3(targetGoalSign * PitchHalfLength, 0f, 0f);

            float distToGoal = Vector2.Distance(
                new Vector2(startPos.x, startPos.z),
                new Vector2(goalCenter.x, goalCenter.z));

            // 1. 타격 발 & 주발/약발 판정
            PreferredFoot footUsed = DetermineStrikingFoot(shooter, startPos, goalCenter, shotType, aimInput);
            bool usedWeakFoot = (prof != null) && (footUsed != prof.preferredFoot);

            // 2. 슛 종류 및 거리에 따른 핵심 스탯 가중치 계산
            float finNorm = prof != null ? Mathf.InverseLerp(45f, 99f, prof.finishing) : 0.6f;
            float powNorm = prof != null ? Mathf.InverseLerp(45f, 99f, prof.shotPower) : 0.6f;
            float longNorm = prof != null ? Mathf.InverseLerp(45f, 99f, prof.longShots) : 0.6f;
            float curveNorm = prof != null ? Mathf.InverseLerp(45f, 99f, prof.curve) : 0.6f;
            float acroNorm = prof != null ? Mathf.InverseLerp(45f, 99f, prof.acrobatics) : 0.6f;

            // 중거리 비중 (14m 이내 0 -> 30m 이상 1) 및 초장거리 페널티 (105m 정규 구장 밸런스)
            float longRangeWeight = Mathf.InverseLerp(14.0f, 30.0f, distToGoal);
            float ultraLongPenalty = Mathf.InverseLerp(31.0f, 56.0f, distToGoal) * 0.42f;

            float statAccuracy01;
            switch (shotType)
            {
                case ShotType.FinesseCurve:
                    // 감아차기: 커브 + 중거리(원거리일수록 크게 작용) + 골결정력 반영!
                    statAccuracy01 = curveNorm * 0.50f
                                   + Mathf.Lerp(finNorm, longNorm, Mathf.Max(0.45f, longRangeWeight)) * 0.50f;
                    break;

                case ShotType.OverheadKick:
                case ShotType.AcrobaticVolley:
                    statAccuracy01 = acroNorm * 0.72f + finNorm * 0.20f + powNorm * 0.08f;
                    break;
                default:
                    // 일반 파워 슛: 근거리면 골결정력, 원거리면 중거리 슛 스탯이 정확도 좌우
                    statAccuracy01 = Mathf.Lerp(finNorm, longNorm, longRangeWeight) * 0.75f + powNorm * 0.25f;
                    break;
            }

            // 게이지 스위트 스팟 (0.35 ~ 0.88 구간이 이상적, 풀차징 오버히트면 살짝 들뜸) + 초장거리 오차 반영
            float gaugePenalty = charge01 > 0.94f ? 0.14f : 0f;
            statAccuracy01 = Mathf.Clamp(statAccuracy01 - gaugePenalty - ultraLongPenalty, 0.08f, 1.12f);

            // 약발 페널티! (주발이 아닌 발로 차면 정확도 및 구석 조준력 대폭 하락)
            float weakFootPenalty = 0f;
            if (usedWeakFoot)
            {
                int stars = prof != null ? prof.weakFootStars : 3;
                // 2성 -> -0.38, 3성 -> -0.28, 4성 -> -0.19
                weakFootPenalty = Mathf.Lerp(0.42f, 0.18f, Mathf.InverseLerp(1f, 4.5f, stars));
                statAccuracy01 = Mathf.Max(0.08f, statAccuracy01 - weakFootPenalty);
            }

            // 3. 골문 내 목표 지점 선정 (1인칭 시점 에임 + 포켓몬고 드래그 커브 + 스탯 기반 사각지대 보정!)
            Vector3 toGoalPlanar = goalCenter - new Vector3(startPos.x, 0f, startPos.z);
            if (toGoalPlanar.sqrMagnitude < 0.01f) toGoalPlanar = Vector3.right * targetGoalSign;
            toGoalPlanar.Normalize();
            Vector3 attackRight = Vector3.Cross(Vector3.up, toGoalPlanar).normalized; // 골문을 바라볼 때 화면 우측 방향 벡터
            float screenRightZSign = Mathf.Abs(attackRight.z) > 0.01f ? Mathf.Sign(attackRight.z) : -targetGoalSign;
            float keeperZ = opposingKeeper != null ? opposingKeeper.transform.position.z : 0f;

            float desiredSideZ;
            bool usedFirstPersonCrosshairZ = false;
            float crosshairLookedZ = 0f;
            bool hasCrosshairGoalIntersect = false;

            if (!shooter.isAI)
            {
                // 1인칭 시점: 플레이어 눈(크로스헤어)이 바라보는 골라인 교차 지점 계산!
                Vector3 eyePos = shooter.GetFirstPersonEyePosition();
                Vector3 lookDir = shooter.GetFirstPersonAimDirection();
                if (lookDir.x * targetGoalSign > 0.18f)
                {
                    float tGoal = (goalCenter.x - eyePos.x) / lookDir.x;
                    float lookedZ = eyePos.z + lookDir.z * tGoal;
                    crosshairLookedZ = lookedZ;
                    hasCrosshairGoalIntersect = Mathf.Abs(lookedZ) <= 11.0f;

                    if (Mathf.Abs(lookedZ) <= 9.0f && Mathf.Abs(lookedZ) > 0.75f)
                    {
                        desiredSideZ = Mathf.Clamp(lookedZ, -3.28f, 3.28f);
                        usedFirstPersonCrosshairZ = true;
                    }
                    else
                    {
                        desiredSideZ = 0f;
                    }
                }
                else
                {
                    desiredSideZ = 0f;
                }

                // 좌/우 화살표(←/→)로 코스를 꺾어 찼을 때 화면 좌측(+Z)/우측(-Z) 포스트 조준 반영!
                bool isBroadcastView = DynamicSoccerCamera.Instance != null && DynamicSoccerCamera.Instance.UseThirdPersonBroadcast;
                if (isBroadcastView && Mathf.Abs(aimInput.y) > 0.25f)
                {
                    desiredSideZ = Mathf.Sign(aimInput.y) * Random.Range(2.25f, 3.22f);
                    usedFirstPersonCrosshairZ = true;
                }
                else if (!usedFirstPersonCrosshairZ && Mathf.Abs(aimInput.x) > 0.25f)
                {
                    float screenSideToWorldZ = Mathf.Sign(aimInput.x) * screenRightZSign;
                    desiredSideZ = screenSideToWorldZ * Random.Range(2.30f, 3.25f);
                    usedFirstPersonCrosshairZ = true;
                }
            }            else
            {
                desiredSideZ = 0f;
            }

            // 포켓몬고 스타일 드래그 & 드롭 감아차기: 드래그한 화면 방향(좌/우) 구석 사각지대로 확실하게 감겨 꽂히도록 최우선 반영!
            if (shotType == ShotType.FinesseCurve && Mathf.Abs(customCurveBias) > 0.01f)
            {
                float dragDirSign = Mathf.Sign(customCurveBias); // +1 = 우측 스와이프, -1 = 좌측 스와이프
                float cornerZ = dragDirSign * screenRightZSign * Random.Range(2.58f, 3.26f);
                if (hasCrosshairGoalIntersect && Mathf.Sign(crosshairLookedZ) == Mathf.Sign(cornerZ) && Mathf.Abs(crosshairLookedZ) > 1.85f)
                {
                    desiredSideZ = Mathf.Clamp(Mathf.Lerp(cornerZ, crosshairLookedZ, 0.35f), -3.28f, 3.28f);
                }
                else
                {
                    desiredSideZ = cornerZ;
                }
            }
            else if (!usedFirstPersonCrosshairZ)
            {
                if (shooter.isAI && Mathf.Abs(aimInput.y) > 0.25f)
                {
                    desiredSideZ = Mathf.Sign(aimInput.y) * Random.Range(2.25f, 3.22f);
                }
                else if (shotType == ShotType.FinesseCurve)
                {
                    // AI/기본 감아차기: 주발 인사이드 컬 방향으로 파 포스트 안쪽 코너 조준!
                    float curlDirSign = (footUsed == PreferredFoot.Right ? -1f : 1f) * screenRightZSign;
                    if (Mathf.Abs(curlDirSign) < 0.01f) curlDirSign = keeperZ >= 0f ? -1f : 1f;
                    desiredSideZ = curlDirSign * Random.Range(2.15f, 3.05f);
                }
                else
                {
                    // 정면/일반 슛은 조준을 따로 안 하면 골문 안쪽으로 들어가되 키퍼 세이브 반경과 정면 승부!
                    float openSideSign = keeperZ >= 0f ? -1f : 1f;
                    desiredSideZ = openSideSign * Random.Range(1.45f, 2.95f);
                }
            }

            // 스탯이 낮으면 정중앙(키퍼 정면)으로 몰리거나 바깥으로 빗나갈 오차가 커짐
            float cornerPrecision = Mathf.Clamp01(statAccuracy01);
            float baseTargetZ = Mathf.Lerp(desiredSideZ * 0.42f, desiredSideZ, cornerPrecision);

            float idealHeight;
            if (shotType == ShotType.OverheadKick)
                idealHeight = Random.Range(1.48f, 2.12f); // 상단 사각 꽂히기!
            else if (shotType == ShotType.FinesseCurve)
                idealHeight = Random.Range(1.38f, 2.08f);
            else
                idealHeight = Mathf.Lerp(0.50f, 2.02f, Mathf.Clamp01(charge01 * 0.85f + 0.15f));

            // 오차 산포 (스탯 낮거나 약발이거나 초장거리일수록 좌우/상하 빗나감 증가)
            float maxErrorZ = Mathf.Lerp(3.05f, 0.20f, cornerPrecision) + ultraLongPenalty * 2.4f;
            float maxErrorY = Mathf.Lerp(1.42f, 0.14f, cornerPrecision) + ultraLongPenalty * 0.9f;

            if (usedWeakFoot)
            {
                maxErrorZ += 1.35f;
                maxErrorY += 0.62f;
            }

            // 아크로바틱 슛은 유저 요청대로 골이 시원시원하게 많이 들어가도록 오차 상한 완화!
            bool isAcrobaticShot = (shotType == ShotType.OverheadKick || shotType == ShotType.AcrobaticVolley);
            if (isAcrobaticShot)
            {
                float acroErrScale = Mathf.Lerp(1.85f, 0.52f, acroNorm);
                if (usedWeakFoot) acroErrScale *= 1.25f;
                maxErrorZ *= acroErrScale;
                maxErrorY *= acroErrScale;
            }

            float finalZ = baseTargetZ + Random.Range(-maxErrorZ, maxErrorZ);
            float finalY = Mathf.Max(0.28f, idealHeight + Random.Range(-maxErrorY * 0.55f, maxErrorY));

            if (isAcrobaticShot)
            {
                float mishitChance = Mathf.Clamp01((0.72f - acroNorm) * 0.85f) + (usedWeakFoot ? 0.12f : 0f);
                if (Random.value < mishitChance)
                {
                    float shankSide = Random.value < 0.5f ? -1f : 1f;
                    finalZ += shankSide * Random.Range(2.4f, 5.2f);
                    finalY += Random.Range(0.75f, 2.55f);
                    statAccuracy01 *= 0.45f;
                }
            }

            Vector3 finalTarget = new Vector3(
                goalCenter.x + targetGoalSign * 0.35f, // 골라인 안쪽 깊숙이 목표
                finalY,
                finalZ
            );

            // 4. 슛 스피드 산출 (슛 파워 스탯 + 체중 파워 보너스 + 슛 타입 배율)
            float baseSpeed = Mathf.Lerp(20.5f, 34.5f, powNorm);
            if (prof != null)
                baseSpeed *= (1f + prof.HeavyShotBonus);

            float chargeBoost = Mathf.Lerp(0.82f, 1.14f, charge01);
            float shotSpeed = baseSpeed * chargeBoost;

            if (shotType == ShotType.OverheadKick)
                shotSpeed *= Mathf.Lerp(0.84f, 1.12f, acroNorm);
            else if (shotType == ShotType.AcrobaticVolley)
                shotSpeed *= Mathf.Lerp(0.84f, 1.07f, acroNorm);
            else if (shotType == ShotType.FinesseCurve)
                shotSpeed *= 0.92f;

            if (usedWeakFoot)
                shotSpeed *= 0.89f;

            shotSpeed = Mathf.Clamp(shotSpeed, 17.5f, 39.5f);

            // 5. 마그누스 커브 가속도 계산 (특히 감아차기 Z+D!) — 105m 구장 거리에 맞춰 자연스러운 체공시간 산출
            float flightTime = Mathf.Clamp(distToGoal / Mathf.Max(13f, shotSpeed * 0.92f), 0.30f, 2.15f);
            Vector3 magnusAccel = Vector3.zero;
            Vector3 spinVel = Random.insideUnitSphere * 8f;

            if (shotType == ShotType.FinesseCurve)
            {
                // 감아차기(Z+D): 차는 발 기준으로 바깥에서 안쪽으로 자연스럽게 휘어 감기는 마그누스 커브
                float curlSign = Mathf.Abs(customCurveBias) > 0.01f
                    ? Mathf.Sign(customCurveBias)
                    : ((footUsed == PreferredFoot.Right) ? -1f : 1f);

                float curveMultiplier = Mathf.Abs(customCurveBias) > 0.01f
                    ? Mathf.Clamp(Mathf.Abs(customCurveBias), 0.85f, 1.35f)
                    : 1.0f;
                float distCurveScale = Mathf.Lerp(1.05f, 0.56f, Mathf.InverseLerp(18f, 38f, distToGoal));
                float curveStrength = Mathf.Lerp(5.8f, 12.2f, curveNorm) * curveMultiplier * distCurveScale;
                if (usedWeakFoot) curveStrength *= 0.68f;

                // 부메랑처럼 과도하게 꺾이지 않는 실제 축구 인사이드 궤적 + 자연스러운 탑스핀 드롭
                magnusAccel = attackRight * (curlSign * curveStrength) + Vector3.down * (curveStrength * 0.14f);
                spinVel = Vector3.up * (curlSign * 38f);
            }
            else if (shotType == ShotType.OverheadKick || shotType == ShotType.AcrobaticVolley)
            {
                // 강렬한 드롭 탑스핀으로 크로스바 아래로 꽂히는 궤적!
                magnusAccel = Vector3.down * Mathf.Lerp(2.5f, 6.5f, acroNorm);
                spinVel = attackRight * 32f;
            }
            else
            {
                // 일반 슛도 커브 스탯에 비례한 약간의 자연스러운 아웃프론트/인프론트 휨 부여
                float subtleCurl = (footUsed == PreferredFoot.Right ? 1f : -1f) * Mathf.Lerp(1.0f, 5.0f, curveNorm);
                magnusAccel = attackRight * subtleCurl;
                spinVel = Vector3.up * (-subtleCurl * 4f);
            }

            // 6. 선형 공기저항(linearDamping = 0.22)과 중력+마그누스 가속도를 역산한 초기 발사 속도 산출
            // 단, 하프라인 뒤나 초장거리에서 찼을 때 비현실적인 초고속 대포알로 증폭되지 않도록 물리 상한 및 감쇠 적용!
            bool fromBehindHalfway = (startPos.x * targetGoalSign) <= 0f || distToGoal >= 48.0f;
            float k = ball.Rb != null ? Mathf.Max(0.001f, ball.Rb.linearDamping) : 0.22f;
            float alpha = (1f - Mathf.Exp(-k * flightTime)) / k;
            Vector3 totalAccel = Physics.gravity + magnusAccel;
            Vector3 launchVel = ((finalTarget - startPos) - totalAccel * ((flightTime - alpha) / k)) / Mathf.Max(0.05f, alpha);

            if (fromBehindHalfway || distToGoal > 34.0f)
            {
                // 장거리 및 하프라인 뒤 슈팅은 초기 발사 속도가 비정상적으로 뻥튀기되지 않게 제한하고, 날아갈수록 점점 감속되도록 유도!
                float longScale = fromBehindHalfway
                    ? Mathf.Lerp(0.84f, 0.72f, Mathf.InverseLerp(48f, 85f, distToGoal))
                    : Mathf.Lerp(1.04f, 0.88f, Mathf.InverseLerp(34f, 48f, distToGoal));
                float maxAllowedSpeed = shotSpeed * longScale;
                if (launchVel.magnitude > maxAllowedSpeed)
                    launchVel = launchVel.normalized * maxAllowedSpeed;

                float maxAllowedLiftY = fromBehindHalfway ? 7.4f : 9.6f;
                if (launchVel.y > maxAllowedLiftY)
                    launchVel.y = maxAllowedLiftY;

                if (fromBehindHalfway)
                    statAccuracy01 *= 0.38f;
            }
            else
            {
                float maxVelMult = shotType == ShotType.FinesseCurve ? 1.24f : 1.12f;
                if (launchVel.magnitude > shotSpeed * maxVelMult)
                    launchVel = launchVel.normalized * (shotSpeed * maxVelMult);
            }

            // 랜덤 임팩트 VFX 선정
            ImpactVfxType[] vfxPool = (ImpactVfxType[])System.Enum.GetValues(typeof(ImpactVfxType));
            ImpactVfxType chosenVfx = vfxPool[Random.Range(0, vfxPool.Length)];

            ShotMetadata meta = new ShotMetadata
            {
                shooter = shooter,
                shotType = shotType,
                vfxType = chosenVfx,
                footUsed = footUsed,
                usedWeakFoot = usedWeakFoot,
                shotQuality01 = statAccuracy01,
                initialSpeedMps = launchVel.magnitude,
                targetPoint = finalTarget,
                targetTeamGoal = targetGoalSign
            };

            // 실시간 타격 피드백 문구 생성
            string footLabel = footUsed == PreferredFoot.Right ? "오른발" : "왼발";
            string footStatus = usedWeakFoot
                ? $"⚠️ 약발({footLabel}) 정확도 하락!"
                : $"✅ 주발({footLabel}) 임팩트!";

            int kmh = Mathf.RoundToInt(launchVel.magnitude * 3.6f);
            string shotName = GetShotKoreanName(shotType);
            string vfxName = GetVfxKoreanName(chosenVfx);

            string statNote = "";
            if (shotType == ShotType.FinesseCurve && prof != null)
                statNote = $" [커브 {prof.curve} · 중거리 {prof.longShots}]";
            else if ((shotType == ShotType.OverheadKick || shotType == ShotType.AcrobaticVolley) && prof != null)
                statNote = $" [아크로바틱 {prof.acrobatics} · 결정력 {prof.finishing}]";
            else if (prof != null)
                statNote = distToGoal >= 15f ? $" [중거리 {prof.longShots} · 파워 {prof.shotPower}]" : $" [결정력 {prof.finishing} · 파워 {prof.shotPower}]";

            string summary = $"{vfxName} {shotName}! ({kmh} km/h) | {footStatus}{statNote}";

            return new ComputedShotResult
            {
                metadata = meta,
                launchVelocity = launchVel,
                magnusAcceleration = magnusAccel,
                curveDuration = flightTime * 1.08f,
                spinAngularVelocity = spinVel,
                feedbackSummary = summary
            };
        }

        /// <summary>
        /// 크로스 침투 다이빙 헤딩 & 1인칭 점프 헤딩 슈팅 물리 산출 (신장·몸싸움·골결정력 반영!)
        /// </summary>
        public static ComputedShotResult ComputeHeaderShot(
            SoccerPlayerController shooter,
            SoccerBall ball,
            GoalkeeperAI opposingKeeper,
            int targetGoalSign)
        {
            PlayerProfile prof = shooter.Profile;
            Vector3 startPos = ball.transform.position;
            Vector3 goalCenter = new Vector3(targetGoalSign * PitchHalfLength, 0f, 0f);
            float distToGoal = Vector2.Distance(
                new Vector2(startPos.x, startPos.z),
                new Vector2(goalCenter.x, goalCenter.z));

            float finNorm = prof != null ? Mathf.InverseLerp(45f, 99f, prof.finishing) : 0.65f;
            float physNorm = prof != null ? Mathf.InverseLerp(45f, 99f, prof.physical) : 0.65f;
            float heightBonus = prof != null ? Mathf.InverseLerp(168f, 198f, prof.heightCm) * 0.16f : 0.08f;

            float statAccuracy01 = Mathf.Clamp(finNorm * 0.52f + physNorm * 0.36f + heightBonus + 0.08f, 0.22f, 1.10f);
            float keeperZ = opposingKeeper != null ? opposingKeeper.transform.position.z : 0f;

            float desiredSideZ;
            if (!shooter.isAI)
            {
                Vector3 eyePos = shooter.GetFirstPersonEyePosition();
                Vector3 lookDir = shooter.GetFirstPersonAimDirection();
                if (lookDir.x * targetGoalSign > 0.18f)
                {
                    float tGoal = (goalCenter.x - eyePos.x) / lookDir.x;
                    float lookedZ = eyePos.z + lookDir.z * tGoal;
                    desiredSideZ = Mathf.Abs(lookedZ) <= 9.0f
                        ? Mathf.Clamp(lookedZ, -3.15f, 3.15f)
                        : ((keeperZ >= 0f ? -1f : 1f) * Random.Range(2.10f, 3.10f));
                }
                else
                {
                    desiredSideZ = (keeperZ >= 0f ? -1f : 1f) * Random.Range(2.10f, 3.10f);
                }
            }
            else
            {
                float openSide = keeperZ >= 0f ? -1f : 1f;
                desiredSideZ = openSide * Random.Range(2.05f, 3.15f);
            }

            float maxErrZ = Mathf.Lerp(1.85f, 0.22f, Mathf.Clamp01(statAccuracy01));
            float finalZ = desiredSideZ + Random.Range(-maxErrZ, maxErrZ);
            float finalY = Random.Range(0.55f, 1.95f); // 바운드/구석 내리꽂는 헤딩 탄도

            Vector3 finalTarget = new Vector3(
                goalCenter.x + targetGoalSign * 0.35f,
                finalY,
                finalZ);

            float speed = Mathf.Lerp(21.5f, 30.5f, physNorm * 0.6f + finNorm * 0.4f);
            float flightTime = Mathf.Clamp(distToGoal / Mathf.Max(13f, speed * 0.92f), 0.26f, 1.45f);

            Vector3 magnusAccel = Vector3.down * 3.8f; // 내리꽂히는 파워 헤더 드롭
            float k = ball.Rb != null ? Mathf.Max(0.001f, ball.Rb.linearDamping) : 0.22f;
            float alpha = (1f - Mathf.Exp(-k * flightTime)) / k;
            Vector3 totalAccel = Physics.gravity + magnusAccel;
            Vector3 launchVel = ((finalTarget - startPos) - totalAccel * ((flightTime - alpha) / k)) / Mathf.Max(0.05f, alpha);

            ImpactVfxType[] vfxPool = (ImpactVfxType[])System.Enum.GetValues(typeof(ImpactVfxType));
            ImpactVfxType chosenVfx = vfxPool[Random.Range(0, vfxPool.Length)];

            ShotMetadata meta = new ShotMetadata
            {
                shooter = shooter,
                shotType = ShotType.AcrobaticVolley,
                vfxType = chosenVfx,
                footUsed = prof != null ? prof.preferredFoot : PreferredFoot.Right,
                usedWeakFoot = false,
                shotQuality01 = statAccuracy01,
                initialSpeedMps = launchVel.magnitude,
                targetPoint = finalTarget,
                targetTeamGoal = targetGoalSign
            };

            int kmh = Mathf.RoundToInt(launchVel.magnitude * 3.6f);
            string vfxName = GetVfxKoreanName(chosenVfx);
            string pName = prof != null ? prof.playerName : "선수";
            string statInfo = prof != null ? $" [신장 {prof.heightCm}cm · 피지컬 {prof.physical}]" : "";
            string summary = $"💥 {vfxName} 타점 점프 헤딩슛! ({kmh} km/h) | {pName}{statInfo}";

            return new ComputedShotResult
            {
                metadata = meta,
                launchVelocity = launchVel,
                magnusAcceleration = magnusAccel,
                curveDuration = flightTime,
                spinAngularVelocity = Random.insideUnitSphere * 14f,
                feedbackSummary = summary
            };
        }

        /// <summary>
        /// 선형 공기저항(linearDamping)과 중력을 정확히 보정하여 목표 지점(targetPos)에 flightTime 후 정확히 도달하는 패스/크로스 초기 속도 산출
        /// </summary>
        public static Vector3 ComputeDampedBallisticVelocity(SoccerBall ball, Vector3 startPos, Vector3 targetPos, float flightTime)
        {
            float t = Mathf.Max(0.12f, flightTime);
            float k = (ball != null && ball.Rb != null) ? Mathf.Max(0.001f, ball.Rb.linearDamping) : 0.22f;
            float alpha = (1f - Mathf.Exp(-k * t)) / k;
            return ((targetPos - startPos) - Physics.gravity * ((t - alpha) / k)) / Mathf.Max(0.04f, alpha);
        }

        /// <summary>
        /// 반칙 선언 후 직접 프리킥(Direct Free Kick) 물리 산출:
        /// 수비벽을 돌아 휘어 감기는 포켓몬고 드래그 감아차기 프리킥과 무회전 파워 프리킥 지원!
        /// </summary>
        public static ComputedShotResult ComputeFreeKickShot(
            SoccerPlayerController shooter,
            SoccerBall ball,
            int targetGoalSign,
            Vector3 aimedGoalPoint,
            bool isCurveShot,
            float charge01,
            float customCurveBias = 0f)
        {
            PlayerProfile prof = shooter.Profile;
            Vector3 startPos = ball.transform.position;
            Vector3 goalCenter = new Vector3(targetGoalSign * PitchHalfLength, 0f, 0f);

            float distToGoal = Vector2.Distance(
                new Vector2(startPos.x, startPos.z),
                new Vector2(goalCenter.x, goalCenter.z));

            // 세트피스 프리킥은 선수가 주발 각도로 도움닫기를 잡으므로 주발 타격!
            PreferredFoot footUsed = prof != null ? prof.preferredFoot : PreferredFoot.Right;

            float finNorm = prof != null ? Mathf.InverseLerp(45f, 99f, prof.finishing) : 0.65f;
            float powNorm = prof != null ? Mathf.InverseLerp(45f, 99f, prof.shotPower) : 0.65f;
            float longNorm = prof != null ? Mathf.InverseLerp(45f, 99f, prof.longShots) : 0.65f;
            float curveNorm = prof != null ? Mathf.InverseLerp(45f, 99f, prof.curve) : 0.65f;

            bool isPenalty = distToGoal <= 13.0f;
            ShotType shotType = isCurveShot ? ShotType.FinesseCurve : ShotType.Normal;
            float statAccuracy01 = isPenalty
                ? (finNorm * 0.65f + powNorm * 0.35f + 0.08f)
                : (isCurveShot
                    ? (curveNorm * 0.58f + longNorm * 0.27f + finNorm * 0.15f)
                    : (longNorm * 0.42f + powNorm * 0.33f + finNorm * 0.25f));

            bool overcharged = charge01 > 0.94f;
            if (overcharged)
                statAccuracy01 -= 0.16f;

            Vector3 toGoalPlanar = (goalCenter - startPos);
            toGoalPlanar.y = 0f;
            if (toGoalPlanar.sqrMagnitude < 0.01f) toGoalPlanar = Vector3.right * targetGoalSign;
            toGoalPlanar.Normalize();
            Vector3 attackRight = Vector3.Cross(Vector3.up, toGoalPlanar).normalized;
            float screenRightZSign = Mathf.Abs(attackRight.z) > 0.01f ? Mathf.Sign(attackRight.z) : -targetGoalSign;

            float targetAimZ = aimedGoalPoint.z;
            if (isCurveShot && Mathf.Abs(customCurveBias) > 0.01f && Mathf.Abs(targetAimZ) < 1.8f)
            {
                // 중앙 근처에서 포켓몬고 드래그 스핀을 넣으면 드래그한 방향의 구석 포스트 쪽으로 예리하게 감김!
                targetAimZ = Mathf.Sign(customCurveBias) * screenRightZSign * Random.Range(2.45f, 3.18f);
            }

            float precision = Mathf.Clamp01(statAccuracy01 + 0.08f);
            float maxErrZ = Mathf.Lerp(1.55f, 0.09f, precision);
            float maxErrY = Mathf.Lerp(0.78f, 0.07f, precision);

            float finalZ = targetAimZ + Random.Range(-maxErrZ, maxErrZ);
            float finalY = Mathf.Max(0.28f, aimedGoalPoint.y + Random.Range(-maxErrY * 0.5f, maxErrY) + (overcharged ? 0.52f : 0f));

            Vector3 finalTarget = new Vector3(
                goalCenter.x + targetGoalSign * 0.38f,
                finalY,
                finalZ
            );

            float baseSpeed = Mathf.Lerp(21.5f, 35.0f, powNorm) * Mathf.Lerp(0.84f, 1.15f, charge01);
            if (prof != null)
                baseSpeed *= (1f + prof.HeavyShotBonus);
            if (isCurveShot)
                baseSpeed *= 0.87f;

            float shotSpeed = Mathf.Clamp(baseSpeed, 18.5f, 38.5f);
            float flightTime = Mathf.Clamp(distToGoal / Mathf.Max(13f, shotSpeed * 0.90f), 0.36f, 1.95f);

            Vector3 magnusAccel;
            Vector3 spinVel;
            if (isCurveShot)
            {
                // 수비벽 바깥으로 휘어 돌아 골문 구석으로 감겨 꽂히는 프리킥 궤적!
                float curlSign = Mathf.Abs(customCurveBias) > 0.01f
                    ? Mathf.Sign(customCurveBias)
                    : ((footUsed == PreferredFoot.Right) ? -1f : 1f);
                float mult = Mathf.Abs(customCurveBias) > 0.01f
                    ? Mathf.Clamp(Mathf.Abs(customCurveBias), 0.85f, 1.35f)
                    : 1.0f;
                float distCurveScale = Mathf.Lerp(1.05f, 0.58f, Mathf.InverseLerp(18f, 38f, distToGoal));
                float curveStrength = Mathf.Lerp(6.2f, 12.8f, curveNorm) * mult * distCurveScale;
                magnusAccel = attackRight * (curlSign * curveStrength) + Vector3.down * (curveStrength * 0.15f);
                spinVel = Vector3.up * (curlSign * 38f);
            }
            else
            {
                // 무회전/드롭 파워 프리킥 (수비벽 위를 넘겨 뚝 떨어지는 궤적)
                float dip = Mathf.Lerp(2.5f, 6.8f, longNorm);
                float subtleSide = Random.Range(-2.2f, 2.2f);
                magnusAccel = Vector3.down * dip + attackRight * subtleSide;
                spinVel = attackRight * 18f;
            }

            float k = ball.Rb != null ? Mathf.Max(0.001f, ball.Rb.linearDamping) : 0.22f;
            float alpha = (1f - Mathf.Exp(-k * flightTime)) / k;
            Vector3 totalAccel = Physics.gravity + magnusAccel;
            Vector3 launchVel = ((finalTarget - startPos) - totalAccel * ((flightTime - alpha) / k)) / Mathf.Max(0.05f, alpha);

            ImpactVfxType[] vfxPool = (ImpactVfxType[])System.Enum.GetValues(typeof(ImpactVfxType));
            ImpactVfxType chosenVfx = vfxPool[Random.Range(0, vfxPool.Length)];

            ShotMetadata meta = new ShotMetadata
            {
                shooter = shooter,
                shotType = shotType,
                vfxType = chosenVfx,
                footUsed = footUsed,
                usedWeakFoot = false,
                shotQuality01 = Mathf.Clamp(statAccuracy01 + 0.06f, 0.15f, 1.15f),
                initialSpeedMps = launchVel.magnitude,
                targetPoint = finalTarget,
                targetTeamGoal = targetGoalSign
            };

            int kmh = Mathf.RoundToInt(launchVel.magnitude * 3.6f);
            string vfxName = GetVfxKoreanName(chosenVfx);
            string fkLabel = isPenalty
                ? (isCurveShot ? "감아차기 페널티킥(PK)" : "파워 페널티킥(PK)")
                : (isCurveShot ? "감아차기 프리킥" : "파워 프리킥");
            string footLabel = footUsed == PreferredFoot.Right ? "오른발" : "왼발";
            string statNote = isPenalty && prof != null
                ? $" [결정력 {prof.finishing} · 파워 {prof.shotPower}]"
                : (isCurveShot && prof != null
                    ? $" [커브 {prof.curve} · 중거리 {prof.longShots}]"
                    : (prof != null ? $" [중거리 {prof.longShots} · 파워 {prof.shotPower}]" : ""));

            string summary = $"🎯 {vfxName} {fkLabel}! ({kmh} km/h) | ✅ 주발({footLabel}) 세트피스!{statNote}";

            return new ComputedShotResult
            {
                metadata = meta,
                launchVelocity = launchVel,
                magnusAcceleration = magnusAccel,
                curveDuration = flightTime * 1.08f,
                spinAngularVelocity = spinVel,
                feedbackSummary = summary
            };
        }

        public static string GetShotKoreanName(ShotType type)
        {
            switch (type)
            {
                case ShotType.OverheadKick: return "오버헤드킥";
                case ShotType.AcrobaticVolley: return "아크로바틱 시저스 발리";
                case ShotType.FinesseCurve: return "감아차기 슛";
                default: return "파워 슈팅";
            }
        }

        public static string GetVfxKoreanName(ImpactVfxType vfx)
        {
            switch (vfx)
            {
                case ImpactVfxType.BlazingFire: return "🔥 인페르노 불꽃";
                case ImpactVfxType.NeonLaser: return "⚡ 네온 레이저";
                case ImpactVfxType.CosmicPlasma: return "💜 코즈믹 플라즈마";
                case ImpactVfxType.GoldenLightning: return "⚡ 골든 썬더";
                case ImpactVfxType.EmeraldSonic: return "🐉 소닉 드래곤";
                default: return "✨ 슈퍼";
            }
        }
    }
}
