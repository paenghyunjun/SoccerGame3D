using System;
using UnityEngine;

namespace Soccer3D.Gameplay
{
    public class GoalTrigger : MonoBehaviour
    {
        /// <summary>
        /// +1 이면 오른쪽 골대(AI 수비 골대 -> 플레이어 득점!), -1 이면 왼쪽 골대(플레이어 수비 골대 -> AI 득점!)
        /// </summary>
        public int goalSideSign = 1;
        public Transform netVisualRoot;

        public event Action<int> OnGoalScored;

        private float _netShakeTimer;
        private Vector3 _netBaseScale = Vector3.one;

        private void Start()
        {
            if (netVisualRoot != null)
                _netBaseScale = netVisualRoot.localScale;
        }

        private void Update()
        {
            if (_netShakeTimer > 0f && netVisualRoot != null)
            {
                _netShakeTimer -= Time.unscaledDeltaTime;
                float amp = Mathf.Clamp01(_netShakeTimer / 0.7f) * 0.08f;
                float wave = Mathf.Sin(Time.unscaledTime * 42f) * amp;
                netVisualRoot.localScale = _netBaseScale + new Vector3(wave, -wave * 0.5f, wave);
                if (_netShakeTimer <= 0f)
                    netVisualRoot.localScale = _netBaseScale;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            SoccerBall ball = other.GetComponent<SoccerBall>();
            if (ball == null && other.attachedRigidbody != null)
                ball = other.attachedRigidbody.GetComponent<SoccerBall>();

            if (ball != null)
            {
                _netShakeTimer = 0.7f;
                OnGoalScored?.Invoke(goalSideSign);
            }
        }
    }
}

