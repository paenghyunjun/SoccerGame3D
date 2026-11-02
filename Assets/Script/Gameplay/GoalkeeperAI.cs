using System.Collections;
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

        private Vector3 _homeGoalCenter;
        private Vector3 _diveTargetPos;
        private float _diveTimer;
        private float _saveCooldown;
        private Vector3 _currentVelocity;
        private bool _canAttemptSaveOnCurrentShot = true;

        public void Initialize(int goalSign, SoccerBall targetBall, Color kitAccent)
        {
            defendingGoalSign = goalSign;
            ball = targetBall;
            _homeGoalCenter = new Vector3(goalSign * ShotCalculator.PitchHalfLength, 0f, 0f);

            PlayerProfile gkProfile = new PlayerProfile
            {
                playerName = goalSign > 0 ? "AI GK" : "MY GK",
                heightCm = 191,
                weightKg = 84,
                hairStyle = (HairStyleType)Random.Range(0, 8),
                hairColor = new Color(0.18f, 0.12f, 0.08f),
                skinColor = new Color(0.88f, 0.70f, 0.56f),
                jerseyPrimaryColor = goalSign > 0 ? new Color(0.95f, 0.55f, 0.12f) : new Color(0.15f, 0.85f, 0.45f),
                jerseySecondaryColor = kitAccent,
                shortsColor = new Color(0.12f, 0.14f, 0.16f),
                bootColor = Color.white,
                preferredFoot = PreferredFoot.Right
            };

            visualRig = gameObject.GetComponent<PlayerVisualRig>();
            if (visualRig == null)
                visualRig = gameObject.AddComponent<PlayerVisualRig>();
            visualRig.BuildFromProfile(gkProfile, isGoalkeeper: true);

            ResetPosition();

            if (ball != null)
            {
                ball.OnShotLaunched -= HandleShotLaunched;
                ball.OnShotLaunched += HandleShotLaunched;
            }
        }

        private void OnDestroy()
        {
            if (ball != null)
                ball.OnShotLaunched -= HandleShotLaunched;
        }

        public void ResetPosition()
        {
            _diveTimer = 0f;
            _saveCooldown = 0f;
            _currentVelocity = Vector3.zero;
            transform.position = new Vector3(defendingGoalSign * (ShotCalculator.PitchHalfLength - 1.15f), 0f, 0f);
            transform.rotation = Quaternion.LookRotation(new Vector3(-defendingGoalSign, 0f, 0f));
        }

        private void HandleShotLaunched(ShotMetadata meta)
        {
            if (meta.targetTeamGoal != defendingGoalSign)
                return;

            StopAllCoroutines();
            StartCoroutine(ReactToShotRoutine(meta));
        }

        private IEnumerator ReactToShotRoutine(ShotMetadata meta)
        {
            // 스탯이 높거나 아크로바틱 원더골일수록 키퍼가 역동작에 걸리거나 손끝이 살짝 못 미칠 확률 상승!
            bool isAcrobatic = meta.shotType == ShotType.OverheadKick || meta.shotType == ShotType.AcrobaticVolley;
            float quality = meta.shotQuality01;

            // 적당한 난이도의 키퍼 반응 딜레이
            float reactionDelay = Mathf.Lerp(0.11f, 0.28f, Mathf.Clamp01(quality));
            if (isAcrobatic) reactionDelay += 0.05f;

            yield return new WaitForSeconds(reactionDelay);

            if (ball == null) yield break;

            // 다이빙 방향 계산
            float targetZ = Mathf.Clamp(meta.targetPoint.z, -3.55f, 3.55f);
            float diveSign = Mathf.Sign(targetZ - transform.position.z);

            // 슛 퀄리티가 매우 높으면(고스탯 정타/아크로바틱/감아차기) 멋지게 다이빙은 하되 손끝을 스치며 골이 들어가도록 판정 완화!
            float saveChance = Mathf.Lerp(0.82f, 0.14f, Mathf.InverseLerp(0.32f, 1.02f, quality));
            if (meta.usedWeakFoot) saveChance += 0.22f;
            if (isAcrobatic && !meta.usedWeakFoot) saveChance *= 0.62f;

            _canAttemptSaveOnCurrentShot = Random.value < saveChance;

            float reachFactor = _canAttemptSaveOnCurrentShot ? 0.96f : 0.58f;
            float destZ = Mathf.Lerp(transform.position.z, targetZ, reachFactor);
            _diveTargetPos = new Vector3(
                defendingGoalSign * (ShotCalculator.PitchHalfLength - 0.95f),
                0f,
                Mathf.Clamp(destZ, -3.45f, 3.45f)
            );
            _diveTimer = 0.55f;

            if (visualRig != null)
            {
                // 키퍼가 필드를 바라보는 기준 로컬 좌/우 부호 변환
                float localRightDot = Vector3.Dot(Vector3.forward * diveSign, transform.right);
                visualRig.TriggerKeeperDive(localRightDot >= 0f ? 1f : -1f, 0.58f);
            }
        }

        private void Update()
        {
            if (ball == null) return;

            float dt = Time.deltaTime;
            if (_saveCooldown > 0f) _saveCooldown -= dt;

            Vector3 desiredPos;
            float moveSpeed;

            if (_diveTimer > 0f)
            {
                _diveTimer -= dt;
                desiredPos = _diveTargetPos;
                moveSpeed = 8.2f;
            }
            else
            {
                // 평소 각 좁히기 포지셔닝 ("적당하게" 골문을 지키는 자연스러운 AI)
                float ballZ = ball.transform.position.z;
                float guardZ = Mathf.Clamp(ballZ * 0.42f, -2.55f, 2.55f);
                float distFromGoal = Mathf.Abs(ball.transform.position.x - _homeGoalCenter.x);
                float stepOut = distFromGoal < 14f ? 1.55f : 1.05f;

                desiredPos = new Vector3(
                    defendingGoalSign * (ShotCalculator.PitchHalfLength - stepOut),
                    0f,
                    guardZ
                );
                moveSpeed = 4.6f;
            }

            Vector3 prev = transform.position;
            transform.position = Vector3.MoveTowards(prev, desiredPos, moveSpeed * dt);
            _currentVelocity = dt > 0.0001f ? (transform.position - prev) / dt : Vector3.zero;

            // 항상 경기장 안쪽(공 방향) 주시
            Vector3 lookDir = ball.transform.position - transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir.normalized), dt * 10f);
            }

            if (visualRig != null)
                visualRig.UpdateProceduralAnimation(_currentVelocity, false, dt);

            CheckParryOrClear();
        }

        private void CheckParryOrClear()
        {
            if (_saveCooldown > 0f || ball == null || ball.CurrentOwner != null)
                return;

            Vector3 gkChest = transform.position + Vector3.up * 1.15f;
            float dist = Vector3.Distance(gkChest, ball.transform.position);

            // 1. 슈팅 선방 판정
            if (ball.IsSuperShotActive || ball.Rb.linearVelocity.magnitude > 9f)
            {
                // 골문을 향하는 슛인지 확인
                if (Mathf.Sign(ball.Rb.linearVelocity.x) != defendingGoalSign)
                    return;

                float reach = _canAttemptSaveOnCurrentShot ? 1.52f : 0.68f;
                if (dist <= reach)
                {
                    _saveCooldown = 0.65f;
                    // 공중으로 높게 쳐내서 세컨볼 오버헤드킥 찬스까지 자연스럽게 연출!!
                    Vector3 outDir = new Vector3(-defendingGoalSign * Random.Range(7.5f, 12.5f), Random.Range(6.2f, 9.2f), Random.Range(-5.5f, 5.5f));
                    ball.Rb.linearVelocity = outDir;
                    ball.Rb.angularVelocity = Random.insideUnitSphere * 18f;
                }
            }
            else if (dist <= 1.35f)
            {
                // 2. 골 에어리어 앞에 굴러온 루즈볼 롱킥 클리어링 (하프라인 부근 공중볼 경합 유도!)
                _saveCooldown = 0.9f;
                Vector3 clearVel = new Vector3(-defendingGoalSign * Random.Range(11f, 15f), Random.Range(6.5f, 9.0f), Random.Range(-5f, 5f));
                ball.Rb.linearVelocity = clearVel;
                if (visualRig != null)
                    visualRig.TriggerNormalKick(PreferredFoot.Right, false);
            }
        }
    }
}

