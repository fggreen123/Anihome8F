using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Anihome.Dormitory;

namespace Anihome.Dormitory.Editor
{
    /// <summary>Imports the photo reconstruction into its own scene. Never saves or edits existing scenes.</summary>
    [InitializeOnLoad]
    public static class DormitoryImporter
    {
        const string Root = "Assets/DormitoryReconstruction";
        const string ModelPath = Root + "/Models/DormitoryFloor.fbx";
        const string ScenePath = Root + "/Scenes/DormitoryFloor.unity";
        const string PrefabPath = Root + "/Prefabs/DormitoryFloor.prefab";
        const string ReportPath = Root + "/integration_report.json";
        const string OutputDirectory = @"C:\Users\jiwoo\Documents\Codex\2026-09-09\new-chat-2\outputs";
        static readonly string[] ViewNames = { "Corridor_A", "Corridor_B", "Lobby_A", "Lobby_B", "Study", "Stairs" };
        static readonly string[] ColliderTokens = { "Floor", "Wall", "DoorLeaf", "Step", "Landing", "Desk", "Counter", "Elevator" };
        static bool running;
        static bool attemptedAutoImport;
        static double nextAttempt;
        static readonly List<string> CapturedErrors = new List<string>();

        // Fields are populated by JsonUtility from the Blender manifest.
#pragma warning disable CS0649
        [Serializable] sealed class MaterialList { public MaterialEntry[] materials; }
        [Serializable] sealed class MaterialEntry
        {
            public string name;
            public float[] color;
            public float metallic;
            public float roughness = 0.65f;
            public float[] emission;
            public float emission_strength;
            public string base_texture;
            public string normal_texture;
            public string metallic_texture;
            public float[] scale;
        }
#pragma warning restore CS0649
        [Serializable] sealed class Report
        {
            public string status;
            public string generatedUtc;
            public string unityVersion;
            public string model;
            public string scene;
            public string prefab;
            public string renderPipeline;
            public string estimatedDimensionsNotice = "Photo-based approximation: room count, dimensions and unseen connections are inferred; not surveyed.";
            public Vector3 modelBoundsCenter;
            public Vector3 modelBoundsSize;
            public int rendererCount;
            public int meshCount;
            public long triangles;
            public int materialCount;
            public int colliderCount;
            public int pointLights;
            public int directionalLights;
            public int cameras;
            public string[] missingMaterials;
            public string[] missingMarkers;
            public string[] floorChecks;
            public string[] passageChecks;
            public int reflectionProbes;
            public string[] screenshots;
            public string[] errors;
            public string walkthrough = "Open Scenes/DormitoryFloor.unity alone. Play: WASD or arrows, Shift to move faster, left click for mouse look, Escape to release pointer.";
        }

        static DormitoryImporter()
        {
            nextAttempt = EditorApplication.timeSinceStartup + 3;
            EditorApplication.update += AutoImportWhenReady;
        }

        static void AutoImportWhenReady()
        {
            if (running || EditorApplication.timeSinceStartup < nextAttempt) return;
            nextAttempt = EditorApplication.timeSinceStartup + 3;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(Root + "/capture.request"))
            {
                File.Delete(Root + "/capture.request");
                CaptureExistingScene();
                return;
            }
            if (File.Exists(Root + "/open.request"))
            {
                File.Delete(Root + "/open.request");
                OpenPreviewScene();
                return;
            }
            bool force = File.Exists(Root + "/rebuild.request");
            if (!force && (File.Exists(ReportPath) || attemptedAutoImport)) return;
            if (!File.Exists(ModelPath) || !File.Exists(Root + "/material_manifest.json")) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null) return;
            if (force) File.Delete(Root + "/rebuild.request");
            attemptedAutoImport = true;
            Rebuild();
        }

        [MenuItem("Tools/Dormitory/Rebuild + Capture")]
        public static void Rebuild()
        {
            if (running) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Dormitory: leave Play mode before rebuilding.");
                return;
            }
            running = true;
            CapturedErrors.Clear();
            Application.logMessageReceived += RecordError;
            Scene previouslyActive = SceneManager.GetActiveScene();
            Scene scene = default;
            Report report = new Report
            {
                status = "building", generatedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion, model = ModelPath, scene = ScenePath, prefab = PrefabPath,
                renderPipeline = GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "Built-in"
            };
            try
            {
                EnsureFolders();
                Scene old = SceneManager.GetSceneByPath(ScenePath);
                if (old.IsValid() && old.isLoaded)
                {
                    if (old.isDirty) throw new InvalidOperationException("Generated DormitoryFloor scene has unsaved changes. Save it before rebuilding.");
                }
                var materials = CreateMaterials();
                ConfigureModelImporter(materials);
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                if (!source) throw new FileNotFoundException("FBX is not imported: " + ModelPath);

                bool onlyGeneratedScenes = true;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene loaded = SceneManager.GetSceneAt(i);
                    bool owned = loaded.path == ScenePath || (string.IsNullOrEmpty(loaded.path) &&
                        loaded.GetRootGameObjects().Any(g => g.name == "DormitoryFloor" &&
                        g.GetComponentInChildren<DormitoryWalkthrough>(true) != null));
                    if (!owned) onlyGeneratedScenes = false;
                }
                StageUtility.GoToMainStage();
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    onlyGeneratedScenes ? NewSceneMode.Single : NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                // Unity cannot close its only loaded scene. Create the replacement first.
                if (old.IsValid() && old.isLoaded && old != scene && !EditorSceneManager.CloseScene(old, true))
                    throw new InvalidOperationException("Could not close the previous generated scene.");
                // Clean up only task-owned unsaved results from an interrupted generation.
                for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
                {
                    Scene candidate = SceneManager.GetSceneAt(i);
                    if (candidate == scene || !string.IsNullOrEmpty(candidate.path) || candidate.name != "DormitoryFloor") continue;
                    bool generated = candidate.GetRootGameObjects().Any(g => g.name == "DormitoryFloor" &&
                        g.GetComponentInChildren<DormitoryWalkthrough>(true) != null);
                    if (generated) EditorSceneManager.CloseScene(candidate, true);
                }
                scene.name = "DormitoryFloor";
                var environment = new GameObject("DormitoryFloor");
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                model.name = "DormitoryFloor_Model";
                model.transform.SetParent(environment.transform, false);
                var missingMaterials = new HashSet<string>();
                var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var renderer in renderers)
                {
                    Material[] mapped = renderer.sharedMaterials;
                    for (int i = 0; i < mapped.Length; i++)
                    {
                        string key = mapped[i] ? NormalizeMaterialName(mapped[i].name) : "<null>";
                        if (materials.TryGetValue(key, out Material material)) mapped[i] = material;
                        else missingMaterials.Add(key);
                    }
                    renderer.sharedMaterials = mapped;
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,
                        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                        StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
                    if (ShouldCollide(renderer.name))
                    {
                        MeshFilter mesh = renderer.GetComponent<MeshFilter>();
                        if (mesh && mesh.sharedMesh && !renderer.GetComponent<Collider>())
                        {
                            var collider = renderer.gameObject.AddComponent<MeshCollider>();
                            collider.sharedMesh = mesh.sharedMesh;
                            collider.convex = false;
                        }
                    }
                }
                if (renderers.Length == 0) throw new InvalidOperationException("No mesh renderers were imported.");
                if (missingMaterials.Count > 0)
                    throw new InvalidOperationException("Unmapped FBX materials: " + string.Join(", ", missingMaterials));

                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                ConfigureEnvironment();
                CreateLights(environment, model, renderers, bounds);
                Dictionary<string, Transform> markers = model.GetComponentsInChildren<Transform>(true)
                    .GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                var missingMarkers = new List<string>();
                var cameras = CreateCameras(environment, markers, missingMarkers);
                if (missingMarkers.Count > 0)
                    throw new InvalidOperationException("Missing camera markers: " + string.Join(", ", missingMarkers));
                CreateVolume(environment);
                CreateReflectionProbes(environment, markers, scene);

                PrefabUtility.SaveAsPrefabAsset(environment, PrefabPath);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("Failed to save the generated scene.");
                AssetDatabase.SaveAssets();

                Physics.SyncTransforms();
                var checks = new List<string>();
                foreach (string name in ViewNames)
                {
                    if (!markers.TryGetValue("View_" + name, out Transform marker)) continue;
                    bool floor = Physics.RaycastAll(marker.position, Vector3.down, 5f)
                        .Any(hit => hit.collider.gameObject.scene == scene && ShouldCollide(hit.collider.name));
                    checks.Add(name + (floor ? ": floor collider below camera" : ": NO floor collider below camera"));
                }
                var images = CaptureCameras(cameras, scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                report.modelBoundsCenter = bounds.center;
                report.modelBoundsSize = bounds.size;
                report.rendererCount = renderers.Length;
                var meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
                report.meshCount = meshFilters.Length;
                report.triangles = meshFilters.Where(m => m.sharedMesh).Sum(m => (long)m.sharedMesh.triangles.Length / 3);
                report.materialCount = materials.Count;
                report.colliderCount = model.GetComponentsInChildren<Collider>(true).Length;
                report.pointLights = environment.GetComponentsInChildren<Light>(true).Count(l => l.type == LightType.Point);
                report.directionalLights = environment.GetComponentsInChildren<Light>(true).Count(l => l.type == LightType.Directional);
                report.cameras = cameras.Count;
                report.missingMaterials = missingMaterials.ToArray();
                report.missingMarkers = missingMarkers.ToArray();
                report.floorChecks = checks.ToArray();
                report.passageChecks = CheckPassages(markers, scene);
                report.reflectionProbes = environment.GetComponentsInChildren<ReflectionProbe>(true).Length;
                report.screenshots = images.ToArray();
                report.errors = CapturedErrors.ToArray();
                report.status = CapturedErrors.Count == 0 ? "success" : "completed_with_console_errors";
                SaveReport(report);
                Selection.activeGameObject = environment;
                SceneView.lastActiveSceneView?.LookAt(cameras[0].transform.position + cameras[0].transform.forward * 4f,
                    cameras[0].transform.rotation, 4f, false, true);
                Debug.Log("Dormitory reconstruction ready: " + ScenePath + "; " + report.rendererCount + " meshes, " + report.materialCount + " materials, " + report.colliderCount + " colliders. Captures: " + OutputDirectory);
            }
            catch (Exception exception)
            {
                report.status = "failed";
                report.errors = CapturedErrors.Concat(new[] { exception.ToString() }).ToArray();
                Directory.CreateDirectory(OutputDirectory);
                File.WriteAllText(Path.Combine(OutputDirectory, "UnityIntegrationReport_FAILED.json"), JsonUtility.ToJson(report, true));
                Debug.LogException(exception);
                if (previouslyActive.IsValid() && previouslyActive.isLoaded) SceneManager.SetActiveScene(previouslyActive);
            }
            finally
            {
                Application.logMessageReceived -= RecordError;
                running = false;
                EditorUtility.ClearProgressBar();
            }
        }

        static void RecordError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                CapturedErrors.Add(message + "\n" + stack);
        }

        static void EnsureFolders()
        {
            foreach (string suffix in new[] { "Materials", "Prefabs", "Scenes", "Settings" })
            {
                string path = Root + "/" + suffix;
                if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Root, suffix);
            }
            Directory.CreateDirectory(OutputDirectory);
        }

        static Dictionary<string, Material> CreateMaterials()
        {
            string json = File.ReadAllText(Root + "/material_manifest.json").Trim();
            if (json.StartsWith("[")) json = "{\"materials\":" + json + "}";
            MaterialList manifest = JsonUtility.FromJson<MaterialList>(json);
            if (manifest?.materials == null || manifest.materials.Length == 0)
                throw new InvalidOperationException("Material manifest is empty or invalid.");
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader is unavailable.");
            var result = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            foreach (MaterialEntry entry in manifest.materials)
            {
                string path = Root + "/Materials/" + SafeFilename(entry.name) + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.shader = shader;
                material.name = entry.name;
                Color color = ReadColor(entry.color, Color.white);
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Metallic", Mathf.Clamp01(entry.metallic));
                material.SetFloat("_Smoothness", Mathf.Clamp01(1 - entry.roughness));
                material.SetFloat("_SpecularHighlights", entry.name == "Glass" || entry.name == "SkyOutside" ? 0 : 1);
                if (entry.name == "Glass" || entry.name == "SkyOutside") material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                else material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.SetFloat("_Surface", 0);
                material.SetFloat("_Cull", 2);
                material.SetFloat("_AlphaClip", 0);
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_ZWrite", 1);
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Geometry;
                if (color.a < 0.99f || entry.name.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    material.SetFloat("_Surface", 1);
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0);
                    material.SetFloat("_Cull", 0);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.renderQueue = (int)RenderQueue.Transparent;
                    material.SetShaderPassEnabled("ShadowCaster", false);
                }
                else
                {
                    material.SetOverrideTag("RenderType", "Opaque");
                    material.SetShaderPassEnabled("ShadowCaster", true);
                }
                Color emission = ReadColor(entry.emission, Color.black) * Mathf.Min(entry.emission_strength, 4f);
                material.SetColor("_EmissionColor", emission);
                if (entry.emission_strength > 0)
                {
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else material.DisableKeyword("_EMISSION");
                ApplyTexture(material, "_BaseMap", entry.base_texture, false);
                ApplyTexture(material, "_BumpMap", entry.normal_texture, true);
                ApplyTexture(material, "_MetallicGlossMap", entry.metallic_texture, false);
                if (!string.IsNullOrEmpty(entry.normal_texture)) material.EnableKeyword("_NORMALMAP");
                if (!string.IsNullOrEmpty(entry.metallic_texture)) material.EnableKeyword("_METALLICSPECGLOSSMAP");
                if (entry.scale != null && entry.scale.Length >= 2)
                    material.SetTextureScale("_BaseMap", new Vector2(entry.scale[0], entry.scale[1]));
                EditorUtility.SetDirty(material);
                result[NormalizeMaterialName(entry.name)] = material;
            }
            return result;
        }

        static void ApplyTexture(Material material, string property, string relativePath, bool normal)
        {
            if (string.IsNullOrEmpty(relativePath)) return;
            string path = relativePath.StartsWith("Assets/") ? relativePath : Root + "/" + relativePath.Replace('\\', '/');
            if (normal && AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture) throw new FileNotFoundException("Missing material texture: " + path);
            material.SetTexture(property, texture);
        }

        static Color ReadColor(float[] values, Color fallback)
        {
            return values == null || values.Length < 3 ? fallback :
                new Color(values[0], values[1], values[2], values.Length > 3 ? values[3] : 1f);
        }
        static string SafeFilename(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value;
        }
        static string NormalizeMaterialName(string value)
        {
            return value.Replace(" (Instance)", "").Trim();
        }

        static void ConfigureModelImporter(Dictionary<string, Material> materials)
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (!importer) throw new InvalidOperationException("ModelImporter is not ready.");
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.isReadable = true;
            importer.addCollider = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.generateSecondaryUV = false;
            foreach (var pair in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Value.name), pair.Value);
            importer.SaveAndReimport();
        }

        static bool ShouldCollide(string name)
        {
            if (name.IndexOf("Ceiling", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return ColliderTokens.Any(token => name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static void ConfigureEnvironment()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.38f, 0.40f, 0.41f);
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.34f, 0.36f);
            RenderSettings.ambientEquatorColor = new Color(0.27f, 0.28f, 0.29f);
            RenderSettings.ambientGroundColor = new Color(0.17f, 0.175f, 0.18f);
            RenderSettings.ambientIntensity = 1;
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(new Color(0.24f, 0.25f, 0.26f));
            RenderSettings.ambientProbe = ambient;
            RenderSettings.reflectionIntensity = 0.3f;
            RenderSettings.fog = false;
            RenderSettings.sun = null;
        }

        static void CreateLights(GameObject environment, GameObject model, MeshRenderer[] renderers, Bounds bounds)
        {
            var root = new GameObject("Lighting");
            root.transform.SetParent(environment.transform, false);
            Light mainFill = AddLight(root.transform, "Main_Fill_Light", LightType.Directional,
                Vector3.up * 5, new Color(0.94f, 0.97f, 1f), 0.055f, 100f);
            mainFill.transform.rotation = Quaternion.Euler(60, 20, 0);
            mainFill.shadows = LightShadows.None;
            int index = 0;
            foreach (MeshRenderer renderer in renderers.Where(r => r.name.StartsWith("LED_Diffuser", StringComparison.OrdinalIgnoreCase)))
            {
                Light lamp = AddLight(root.transform, "Fluorescent_" + index.ToString("00"), LightType.Point,
                    renderer.bounds.center + Vector3.down * 0.23f, new Color(0.91f, 0.96f, 1f), 2.2f, 5.2f);
                lamp.shadows = index % 3 == 0 ? LightShadows.Soft : LightShadows.None;
                lamp.shadowStrength = 0.65f;
                lamp.shadowBias = 0.025f;
                lamp.shadowNormalBias = 0.08f;
                index++;
            }
            foreach (Transform marker in model.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Light_Window", StringComparison.OrdinalIgnoreCase)))
            {
                Light light = AddLight(root.transform, "Daylight_" + marker.name, LightType.Point,
                    marker.position, new Color(0.78f, 0.88f, 1f), 4.5f, 7f);
                light.shadows = LightShadows.Soft;
            }
            if (!root.GetComponentsInChildren<Light>().Any(l => l.name.StartsWith("Daylight_")))
            {
                AddLight(root.transform, "Daylight_Window_A", LightType.Point,
                    new Vector3(bounds.center.x, 1.95f, bounds.min.z + 1.0f), new Color(0.78f, 0.88f, 1f), 3f, 6f);
                AddLight(root.transform, "Daylight_Window_B", LightType.Point,
                    new Vector3(bounds.center.x, 1.95f, bounds.max.z - 1.0f), new Color(0.78f, 0.88f, 1f), 3f, 6f);
            }
        }

        static Light AddLight(Transform root, string name, LightType type, Vector3 position, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = position;
            Light light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.renderMode = LightRenderMode.ForcePixel;
            return light;
        }

        static void CreateReflectionProbes(GameObject environment, Dictionary<string, Transform> markers, Scene scene)
        {
            if (!markers.TryGetValue("View_Corridor_A", out Transform a) ||
                !markers.TryGetValue("View_Corridor_B", out Transform b)) return;
            var points = new List<Vector3> { Vector3.Lerp(a.position, b.position, .2f), Vector3.Lerp(a.position, b.position, .8f) };
            if (markers.TryGetValue("View_Study", out Transform study)) points.Add(study.position);
            var otherLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(l => l.enabled && l.gameObject.scene != scene).ToArray();
            try
            {
                foreach (Light light in otherLights) light.enabled = false;
                for (int i = 0; i < points.Count; i++)
                {
                    var go = new GameObject("Baked_Reflection_" + i);
                    go.transform.SetParent(environment.transform, false);
                    go.transform.position = points[i];
                    ReflectionProbe probe = go.AddComponent<ReflectionProbe>();
                    probe.mode = ReflectionProbeMode.Baked;
                    probe.resolution = 128;
                    probe.hdr = true;
                    probe.boxProjection = true;
                    probe.size = i < 2 ? new Vector3(3.2f, 3f, 15f) : new Vector3(7f, 3.5f, 6f);
                    probe.blendDistance = 1.2f;
                    probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
                    probe.backgroundColor = new Color(.3f,.35f,.4f);
                    string path = Root + "/Settings/Reflection_" + i + ".exr";
                    if (!Lightmapping.BakeReflectionProbe(probe, path))
                        throw new InvalidOperationException("Reflection probe bake failed: " + path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    probe.bakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                }
            }
            finally { foreach (Light light in otherLights) if (light) light.enabled = true; }
        }

        static string[] CheckPassages(Dictionary<string, Transform> markers, Scene scene)
        {
            var results = new List<string>();
            if (markers.TryGetValue("View_Corridor_A", out Transform a) &&
                markers.TryGetValue("View_Corridor_B", out Transform b))
            {
                Vector3 from = a.position; from.y = .35f;
                Vector3 to = b.position; to.y = .35f;
                Vector3 direction = (to - from).normalized;
                var hits = Physics.CapsuleCastAll(from, from + Vector3.up * 1.15f, .22f,
                    direction, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => h.collider.gameObject.scene == scene && !(h.collider is CharacterController)).ToArray();
                results.Add(hits.Length == 0 ? "Main corridor: 0.44 m capsule has clear passage between viewpoints" :
                    "Main corridor blocked by: " + string.Join(", ", hits.Select(h => h.collider.name).Distinct()));
            }
            return results.ToArray();
        }

        static List<Camera> CreateCameras(GameObject environment, Dictionary<string, Transform> markers, List<string> missing)
        {
            var result = new List<Camera>();
            var root = new GameObject("Photo_Viewpoints");
            root.transform.SetParent(environment.transform, false);
            foreach (string name in ViewNames)
            {
                if (!markers.TryGetValue("View_" + name, out Transform view)) { missing.Add("View_" + name); continue; }
                if (!markers.TryGetValue("Target_" + name, out Transform target)) { missing.Add("Target_" + name); continue; }
                var go = new GameObject("Photo_" + name);
                go.transform.SetParent(root.transform, false);
                go.transform.position = view.position;
                go.transform.rotation = Quaternion.LookRotation(target.position - view.position, Vector3.up);
                Camera camera = go.AddComponent<Camera>();
                camera.fieldOfView = 64f;
                camera.nearClipPlane = 0.04f;
                camera.farClipPlane = 100f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.65f, 0.75f, 0.82f);
                camera.allowHDR = true;
                camera.allowMSAA = true;
                camera.enabled = false;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                result.Add(camera);
            }
            if (result.Count > 0)
            {
                Camera view = result[0];
                var player = new GameObject("Walkthrough_Player");
                player.transform.SetParent(environment.transform, false);
                player.transform.position = view.transform.position - Vector3.up * 1.65f;
                player.transform.rotation = Quaternion.Euler(0, view.transform.eulerAngles.y, 0);
                CharacterController character = player.AddComponent<CharacterController>();
                character.height = 1.8f;
                character.radius = 0.22f;
                character.center = new Vector3(0, 0.9f, 0);
                character.skinWidth = 0.025f;
                character.stepOffset = 0.26f;
                character.slopeLimit = 50;
                var main = UnityEngine.Object.Instantiate(view.gameObject, player.transform);
                main.name = "Main Camera";
                main.tag = "MainCamera";
                main.transform.position = view.transform.position;
                main.transform.rotation = view.transform.rotation;
                Camera mainCamera = main.GetComponent<Camera>();
                mainCamera.enabled = true;
                main.AddComponent<AudioListener>();
                player.AddComponent<DormitoryWalkthrough>().viewCamera = mainCamera;
            }
            return result;
        }

        static void CreateVolume(GameObject environment)
        {
            string path = Root + "/Settings/DormitoryLighting.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
            if (!profile.TryGet(out Tonemapping tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>(true);
                AssetDatabase.AddObjectToAsset(tonemapping, profile);
            }
            tonemapping.mode.Override(TonemappingMode.ACES);
            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>(true);
                AssetDatabase.AddObjectToAsset(bloom, profile);
            }
            bloom.intensity.Override(0.12f);
            bloom.threshold.Override(1f);
            bloom.scatter.Override(0.55f);
            if (!profile.TryGet(out ColorAdjustments colors))
            {
                colors = profile.Add<ColorAdjustments>(true);
                AssetDatabase.AddObjectToAsset(colors, profile);
            }
            colors.postExposure.Override(0.25f);
            colors.saturation.Override(-5f);
            colors.contrast.Override(7f);
            EditorUtility.SetDirty(profile);
            var go = new GameObject("Dormitory_Lighting_Volume");
            go.transform.SetParent(environment.transform, false);
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            volume.sharedProfile = profile;
        }

        [MenuItem("Tools/Dormitory/Capture Existing Scene")]
        public static void CaptureExistingScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var cameras = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true))
                .Where(c => c.name.StartsWith("Photo_")).OrderBy(c => Array.IndexOf(ViewNames, c.name.Substring(6))).ToList();
            CaptureCameras(cameras, scene);
        }

        [MenuItem("Tools/Dormitory/Open Walkthrough Scene")]
        public static void OpenPreviewScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loaded = SceneManager.GetSceneAt(i);
                if (loaded.isDirty && loaded.path != ScenePath)
                {
                    Debug.LogWarning("Dormitory: another scene has unsaved changes; keeping it open. Open DormitoryFloor.unity alone after saving it.");
                    return;
                }
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        static List<string> CaptureCameras(List<Camera> cameras, Scene scene)
        {
            Directory.CreateDirectory(OutputDirectory);
            var images = new List<string>();
            var outsideLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(l => l.enabled && l.gameObject.scene != scene).ToArray();
            var outsideVolumes = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(v => v.enabled && v.gameObject.scene != scene).ToArray();
            var outsideRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => r.enabled && r.gameObject.scene != scene).ToArray();
            Scene active = SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(scene);
                foreach (Light light in outsideLights) light.enabled = false;
                foreach (Volume volume in outsideVolumes) volume.enabled = false;
                foreach (Renderer renderer in outsideRenderers) renderer.enabled = false;
                foreach (Camera camera in cameras)
                {
                    EditorUtility.DisplayProgressBar("Dormitory", "Rendering " + camera.name, (float)images.Count / Math.Max(1, cameras.Count));
                    var target = new RenderTexture(1080, 1440, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    target.antiAliasing = 1;
                    target.Create();
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture oldTarget = camera.targetTexture;
                    Texture2D readback = null;
                    float oldAspect = camera.aspect;
                    try
                    {
                        camera.aspect = 0.75f;
                        camera.targetTexture = target;
                        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                        RenderPipeline.SubmitRenderRequest(camera, request);
                        RenderTexture.active = target;
                        readback = new Texture2D(target.width, target.height, TextureFormat.RGBAFloat, false, true);
                        readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                        readback.Apply();
                        // HDR render targets store linear values; PNG pixels need sRGB encoding.
                        Color[] pixels = readback.GetPixels();
                        if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                        {
                            for (int pixel = 0; pixel < pixels.Length; pixel++) pixels[pixel] = pixels[pixel].gamma;
                        }
                        UnityEngine.Object.DestroyImmediate(readback);
                        readback = new Texture2D(target.width, target.height, TextureFormat.RGB24, false, false);
                        readback.SetPixels(pixels);
                        readback.Apply();
                        string filename = Path.Combine(OutputDirectory, "Unity_" + camera.name.Substring(6) + ".png");
                        File.WriteAllBytes(filename, readback.EncodeToPNG());
                        images.Add(filename);
                    }
                    finally
                    {
                        camera.targetTexture = oldTarget;
                        camera.aspect = oldAspect;
                        RenderTexture.active = previous;
                        target.Release();
                        UnityEngine.Object.DestroyImmediate(target);
                        if (readback) UnityEngine.Object.DestroyImmediate(readback);
                    }
                }
            }
            finally
            {
                foreach (Light light in outsideLights) if (light) light.enabled = true;
                foreach (Volume volume in outsideVolumes) if (volume) volume.enabled = true;
                foreach (Renderer renderer in outsideRenderers) if (renderer) renderer.enabled = true;
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                EditorUtility.ClearProgressBar();
            }
            return images;
        }

        static void SaveReport(Report report)
        {
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(ReportPath, json);
            File.WriteAllText(Path.Combine(OutputDirectory, "UnityIntegrationReport.json"), json);
            AssetDatabase.ImportAsset(ReportPath);
        }
    }
}
