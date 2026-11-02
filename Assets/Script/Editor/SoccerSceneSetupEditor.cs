using Soccer3D.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Soccer3D.EditorTools
{
    public static class SoccerSceneSetupEditor
    {
        [MenuItem("Soccer3D/1vs1 슈팅 매니저 씬에 배치 (Setup Scene)")]
        public static void SetupSceneManager()
        {
            SoccerMatchManager existing = Object.FindAnyObjectByType<SoccerMatchManager>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("[Soccer3D] 이미 씬에 SoccerMatchManager가 배치되어 있습니다! 바로 Play 버튼을 눌러 게임을 실행하세요.");
                return;
            }

            GameObject go = new GameObject("SoccerMatchManager");
            Undo.RegisterCreatedObjectUndo(go, "Create SoccerMatchManager");
            go.AddComponent<SoccerMatchManager>();
            Selection.activeGameObject = go;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Soccer3D] SoccerMatchManager가 씬에 배치되었습니다! (배치 없이 바로 Play를 눌러도 자동 실행됩니다.)");
        }
    }
}
