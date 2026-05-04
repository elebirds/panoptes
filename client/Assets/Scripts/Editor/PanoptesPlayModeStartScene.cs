using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Panoptes.Editor
{
    [InitializeOnLoad]
    internal static class PanoptesPlayModeStartScene
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        static PanoptesPlayModeStartScene()
        {
            EditorApplication.delayCall += EnsureMainMenuStartScene;
        }

        [MenuItem("Panoptes/Diagnostics/Use Main Menu As Play Mode Start Scene")]
        private static void EnsureMainMenuStartSceneFromMenu()
        {
            EnsureMainMenuStartScene();
            Debug.Log("[Panoptes] Play Mode start scene is MainMenu.");
        }

        private static void EnsureMainMenuStartScene()
        {
            var mainMenuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath);
            if (mainMenuScene == null)
            {
                Debug.LogWarning($"[Panoptes] Cannot set Play Mode start scene because {MainMenuScenePath} is missing.");
                return;
            }

            if (EditorSceneManager.playModeStartScene != mainMenuScene)
            {
                EditorSceneManager.playModeStartScene = mainMenuScene;
            }
        }
    }
}
