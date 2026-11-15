using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anihome.Dormitory.EditorTools
{
    [InitializeOnLoad]
    internal static class StairwellRebuildOnce
    {
        private const string MarkerPath = "Assets/_Game/Environment/Dormitory/rebuild-stair-treads.request";
        private const string ScenePath = "Assets/_Game/Scenes/Game.unity";

        static StairwellRebuildOnce()
        {
            EditorApplication.update += TryRebuild;
        }

        private static void TryRebuild()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EditorApplication.update -= TryRebuild;
            if (!File.Exists(MarkerPath))
                return;

            File.Delete(MarkerPath);
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
            {
                Debug.LogError($"[StairTreadFix] Open {ScenePath} before rebuilding. Active scene: {scene.path}");
                return;
            }

            StairwellBuilder.Build();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[StairTreadFix] Rebuilt and saved full-depth rubber stair treads.");
        }
    }
}
