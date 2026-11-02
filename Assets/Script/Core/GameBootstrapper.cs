using UnityEngine;

namespace Soccer3D.Core
{
    public static class GameBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapOnPlay()
        {
            // 씬에 이미 SoccerMatchManager가 배치되어 있다면 중복 생성하지 않음
            if (Object.FindAnyObjectByType<SoccerMatchManager>() != null)
                return;

            GameObject managerObj = new GameObject("SoccerMatchManager_AutoBoot");
            managerObj.AddComponent<SoccerMatchManager>();
        }
    }
}
