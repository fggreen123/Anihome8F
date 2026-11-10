using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Anihome.Dormitory.EditorTools
{
    public static class CorridorPrefabSetup
    {
        const string GameScene = "Assets/DormitoryReconstruction/Scenes/Game.unity";
        const string PrefabFolder = "Assets/DormitoryReconstruction/Prefabs/Corridors";
        const string NormalPath = PrefabFolder + "/Corridor_Normal.prefab";
        const string MeshPath = "Assets/DormitoryReconstruction/Meshes/Corridor_Normal_Generated.asset";

        [MenuItem("Tools/Dormitory/Setup Corridor Prefabs", false, 58)]
        public static void ConfigureGameScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before configuring corridor prefabs.");

            if (Application.isBatchMode) EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != GameScene)
                throw new InvalidOperationException("Open Game.unity before running Setup Corridor Prefabs.");

            var manager = Object.FindFirstObjectByType<FloorLoopManager>();
            if (!manager || !manager.player || !manager.spawnPoint)
                throw new InvalidOperationException("Game scene needs a floor manager, player and spawn point.");
            if (manager.normalCorridorPrefab)
            {
                Debug.Log("[CorridorPrefabs] Already configured; existing prefabs were preserved.");
                return;
            }
            if (AssetDatabase.LoadMainAssetAtPath(NormalPath) || AssetDatabase.LoadMainAssetAtPath(MeshPath))
                throw new InvalidOperationException("Corridor assets already exist. Assign them to GameLoop instead of overwriting them.");

            EnsureFolder(PrefabFolder);
            EnsureFolder("Assets/DormitoryReconstruction/Meshes");

            // Player and game state survive every corridor replacement.
            Undo.SetTransformParent(manager.player, null, "Setup Corridor Prefabs");
            Undo.SetTransformParent(manager.transform, null, "Setup Corridor Prefabs");
            var anchor = new GameObject("CorridorRoot");
            Undo.RegisterCreatedObjectUndo(anchor, "Setup Corridor Prefabs");
            var normal = new GameObject("Corridor_Normal");
            Undo.RegisterCreatedObjectUndo(normal, "Setup Corridor Prefabs");
            normal.transform.SetParent(anchor.transform, false);

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == anchor || root == manager.gameObject || root == manager.player.gameObject) continue;
                Undo.SetTransformParent(root.transform, normal.transform, "Setup Corridor Prefabs");
            }

            var layout = Undo.AddComponent<CorridorLayout>(normal);
            layout.spawnPoint = manager.spawnPoint;
            foreach (var walker in normal.GetComponentsInChildren<CorridorWalker>(true))
            {
                Undo.RecordObject(walker, "Setup Corridor Prefabs");
                walker.player = null;
            }

            // Scene-embedded procedural meshes must become assets before prefab export.
            PersistSceneMeshes(normal);
            EditorUtility.SetDirty(layout);
            GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(normal, NormalPath, InteractionMode.AutomatedAction);
            if (!prefab) throw new InvalidOperationException("Could not save the normal corridor prefab.");

            Undo.RecordObject(manager, "Setup Corridor Prefabs");
            manager.normalCorridorPrefab = prefab.GetComponent<CorridorLayout>();
            manager.corridorParent = anchor.transform;
            manager.SetCorridorPreview(layout);
            EditorUtility.SetDirty(manager);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save Game.unity.");
            Debug.Log("[CorridorPrefabs] Saved Corridor_Normal.prefab and connected Game.unity.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            string parent = path.Substring(0, split);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
        }

        static void PersistSceneMeshes(GameObject root)
        {
            var saved = new Dictionary<Mesh, Mesh>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                filter.sharedMesh = Persist(filter.sharedMesh, saved);
            foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true))
                collider.sharedMesh = Persist(collider.sharedMesh, saved);
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                renderer.sharedMesh = Persist(renderer.sharedMesh, saved);
            Debug.Log($"[CorridorPrefabs] Persisted {saved.Count} procedural meshes.");
        }

        static Mesh Persist(Mesh source, Dictionary<Mesh, Mesh> saved)
        {
            if (!source) return null;
            string path = AssetDatabase.GetAssetPath(source);
            if (EditorUtility.IsPersistent(source) && !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                return source;
            if (saved.TryGetValue(source, out Mesh existing)) return existing;

            Mesh copy = Object.Instantiate(source);
            copy.name = $"{saved.Count:000}_{source.name}";
            if (saved.Count == 0) AssetDatabase.CreateAsset(copy, MeshPath);
            else AssetDatabase.AddObjectToAsset(copy, MeshPath);
            saved.Add(source, copy);
            return copy;
        }
    }
}
