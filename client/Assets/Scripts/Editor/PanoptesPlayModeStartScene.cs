using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Panoptes.Editor
{
    [InitializeOnLoad]
    internal static class PanoptesPlayModeStartScene
    {
        private const string LoginScenePath = "Assets/Scenes/Login.unity";

        static PanoptesPlayModeStartScene()
        {
            EditorApplication.delayCall += EnsureLoginStartScene;
        }

        [MenuItem("Panoptes/Diagnostics/Use Login As Play Mode Start Scene")]
        private static void EnsureLoginStartSceneFromMenu()
        {
            EnsureLoginStartScene();
            Debug.Log("[Panoptes] Play Mode start scene is Login.");
        }

        private static void EnsureLoginStartScene()
        {
            var loginScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(LoginScenePath);
            if (loginScene == null)
            {
                Debug.LogWarning($"[Panoptes] Cannot set Play Mode start scene because {LoginScenePath} is missing.");
                return;
            }

            if (EditorSceneManager.playModeStartScene != loginScene)
            {
                EditorSceneManager.playModeStartScene = loginScene;
            }
        }
    }
}
