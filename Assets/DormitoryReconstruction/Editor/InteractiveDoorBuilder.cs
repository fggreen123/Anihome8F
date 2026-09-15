using System.Collections.Generic;
using Anihome.Dormitory;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anihome.Dormitory.EditorTools
{
    /// <summary>
    /// 열려 있는 장면에 화장실·독서실 유리문을 만들어 넣고, 화장실 칸막이 문과
    /// 플레이어 쪽 상호작용 스크립트까지 한 번에 연결한다.
    /// 메뉴: Tools ▸ Dormitory ▸ Add Interactive Doors
    /// </summary>
    public static class InteractiveDoorBuilder
    {
        const string DormRoot = "Assets/DormitoryReconstruction";
        const string MatRoot = DormRoot + "/Materials";
        const string TexRoot = DormRoot + "/Textures";
        const string WashroomFbx = "Assets/WashroomAddition/Models/Washroom_Laundry.fbx";
        const string ContainerName = "Interactive Doors";

        // ------------------------------------------------------------------ 메뉴

        [MenuItem("Tools/Dormitory/Add Interactive Doors", false, 30)]
        public static void Build()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("문 만들기",
                    "DormitoryFloor 장면을 먼저 연 다음 실행해 주세요.", "확인");
                return;
            }

            // 화장실 모델은 충돌체 없이 들어와 있어서, 문을 달아도 안으로 들어갈 수 없다.
            bool reimported = EnsureWashroomColliders();

            RemoveContainer(false);

            Materials m = Materials.Load();
            if (m.frame == null || m.glass == null)
            {
                Debug.LogError("[Doors] DormitoryReconstruction/Materials 에서 재질을 찾지 못했습니다.");
                return;
            }

            var container = new GameObject(ContainerName);
            Undo.RegisterCreatedObjectUndo(container, "Add Interactive Doors");

            // 독서실 문 — 복도(-X)에서 들어가 독서실(+X) 안쪽으로 열린다.
            BuildGlassDoor(container.transform, m, new DoorPlan
            {
                name = "Study Room Glass Door",
                labelKo = "독서실 문",
                labelEn = "Reading Room Door",
                hinge = new Vector3(1.4225f, 0.012f, -15.1475f),
                yaw = -90f,
                width = 1.00f,
                height = 2.11f,
                thickness = 0.045f,
                openAngle = 90f,
                sign = m.signStudy
            });

            // 화장실 문 — 안쪽은 세탁기가 막고 있어 복도(+X) 쪽으로 열린다.
            BuildGlassDoor(container.transform, m, new DoorPlan
            {
                name = "Washroom Glass Door",
                labelKo = "화장실 문",
                labelEn = "Restroom Door",
                hinge = new Vector3(-0.8475f, 0.028f, -16.1415f),
                yaw = -90f,
                width = 0.99f,
                height = 2.05f,
                thickness = 0.045f,
                openAngle = 90f,
                sign = m.signWashroom
            });

            int stalls = 0;
            stalls += HookCubicleDoor("WR_Cubicle_Door", "첫째 칸 문", "Toilet Stall 1", -40f) ? 1 : 0;
            stalls += HookCubicleDoor("WR_Cubicle_Door.001", "둘째 칸 문", "Toilet Stall 2", -80f) ? 1 : 0;

            bool player = AttachInteractor();

            EditorSceneManager_MarkDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = container;

            Debug.Log($"[Doors] 유리문 2개, 화장실 칸막이 문 {stalls}개를 넣었습니다." +
                      (player ? " 플레이어에 DoorInteractor를 붙였습니다." : " (플레이어를 못 찾아 DoorInteractor는 수동으로 붙여 주세요.)") +
                      (reimported ? " 화장실 FBX에 충돌체를 새로 생성했습니다." : "") +
                      " Ctrl+S 로 장면을 저장하세요.");
        }

        [MenuItem("Tools/Dormitory/Remove Interactive Doors", false, 31)]
        public static void Remove()
        {
            RemoveContainer(true);
            UnhookCubicleDoor("WR_Cubicle_Door");
            UnhookCubicleDoor("WR_Cubicle_Door.001");
            EditorSceneManager_MarkDirty(SceneManager.GetActiveScene());
            Debug.Log("[Doors] 추가했던 문을 제거했습니다.");
        }

        // ------------------------------------------------------------------ 자료구조

        class DoorPlan
        {
            public string name;
            public string labelKo;
            public string labelEn;
            public Vector3 hinge;
            public float yaw;
            public float width;
            public float height;
            public float thickness;
            public float openAngle;
            public Material sign;
        }

        class Materials
        {
            public Material frame;
            public Material glass;
            public Material chrome;
            public Material gasket;
            public Material signWashroom;
            public Material signStudy;

            public static Materials Load()
            {
                var m = new Materials
                {
                    frame = Mat(MatRoot + "/SilverPaint.mat"),
                    glass = Mat(MatRoot + "/Glass.mat"),
                    chrome = Mat(MatRoot + "/Chrome.mat"),
                    gasket = Mat(MatRoot + "/DarkGrey.mat")
                };
                if (m.chrome == null) m.chrome = m.frame;
                if (m.gasket == null) m.gasket = m.frame;

                Material shaderSource = m.frame != null ? m.frame : Mat(MatRoot + "/WhiteWall.mat");
                m.signWashroom = EnsureSign(MatRoot + "/Sign_Washroom.mat", TexRoot + "/Sign_Washroom.png", shaderSource);
                m.signStudy = EnsureSign(MatRoot + "/Sign_StudyRoom.mat", TexRoot + "/Sign_StudyRoom.png", shaderSource);
                return m;
            }

            static Material Mat(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);

            static Material EnsureSign(string matPath, string texPath, Material shaderSource)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    Shader shader = shaderSource != null ? shaderSource.shader : Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                if (tex != null)
                {
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                }
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.30f);
                EditorUtility.SetDirty(mat);
                return mat;
            }
        }

        // ------------------------------------------------------------------ 유리문 만들기

        static void BuildGlassDoor(Transform parent, Materials m, DoorPlan p)
        {
            var leaf = new GameObject(p.name);
            leaf.transform.SetParent(parent, false);
            leaf.transform.position = p.hinge;
            leaf.transform.rotation = Quaternion.Euler(0f, p.yaw, 0f);

            float w = p.width, h = p.height, t = p.thickness;
            const float stile = 0.055f;   // 세로 프레임 폭
            const float railB = 0.165f;   // 아래 가로 프레임
            const float railT = 0.090f;   // 위 가로 프레임
            float mid = t * 0.5f;

            Prim(leaf.transform, PrimitiveType.Cube, "Frame_Stile_Hinge",
                new Vector3(stile * 0.5f, h * 0.5f, mid), new Vector3(stile, h, t), Vector3.zero, m.frame);
            Prim(leaf.transform, PrimitiveType.Cube, "Frame_Stile_Lock",
                new Vector3(w - stile * 0.5f, h * 0.5f, mid), new Vector3(stile, h, t), Vector3.zero, m.frame);
            Prim(leaf.transform, PrimitiveType.Cube, "Frame_Rail_Bottom",
                new Vector3(w * 0.5f, railB * 0.5f, mid), new Vector3(w, railB, t), Vector3.zero, m.frame);
            Prim(leaf.transform, PrimitiveType.Cube, "Frame_Rail_Top",
                new Vector3(w * 0.5f, h - railT * 0.5f, mid), new Vector3(w, railT, t), Vector3.zero, m.frame);

            // 유리 — 프레임 안쪽을 조금 물고 들어간다
            float gx0 = stile - 0.007f, gx1 = w - stile + 0.007f;
            float gy0 = railB - 0.007f, gy1 = h - railT + 0.007f;
            var glass = Prim(leaf.transform, PrimitiveType.Cube, "Glass_Pane",
                new Vector3((gx0 + gx1) * 0.5f, (gy0 + gy1) * 0.5f, mid),
                new Vector3(gx1 - gx0, gy1 - gy0, 0.012f), Vector3.zero, m.glass);
            var gr = glass.GetComponent<Renderer>();
            if (gr) gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 유리 물림 고무
            float bead = 0.012f;
            Prim(leaf.transform, PrimitiveType.Cube, "Glazing_Bead_Bottom",
                new Vector3((gx0 + gx1) * 0.5f, gy0 + bead * 0.5f, mid), new Vector3(gx1 - gx0, bead, t - 0.004f), Vector3.zero, m.gasket);
            Prim(leaf.transform, PrimitiveType.Cube, "Glazing_Bead_Top",
                new Vector3((gx0 + gx1) * 0.5f, gy1 - bead * 0.5f, mid), new Vector3(gx1 - gx0, bead, t - 0.004f), Vector3.zero, m.gasket);
            Prim(leaf.transform, PrimitiveType.Cube, "Glazing_Bead_Hinge",
                new Vector3(gx0 + bead * 0.5f, (gy0 + gy1) * 0.5f, mid), new Vector3(bead, gy1 - gy0, t - 0.004f), Vector3.zero, m.gasket);
            Prim(leaf.transform, PrimitiveType.Cube, "Glazing_Bead_Lock",
                new Vector3(gx1 - bead * 0.5f, (gy0 + gy1) * 0.5f, mid), new Vector3(bead, gy1 - gy0, t - 0.004f), Vector3.zero, m.gasket);

            // 경첩 3개
            for (int i = 0; i < 3; i++)
            {
                float y = Mathf.Lerp(0.34f, h - 0.34f, i * 0.5f);
                Prim(leaf.transform, PrimitiveType.Cylinder, "Hinge_" + (i + 1),
                    new Vector3(-0.004f, y, mid), new Vector3(0.034f, 0.055f, 0.034f), Vector3.zero, m.chrome);
            }

            // 세로 손잡이 바 (앞뒤 양면)
            float barX = w - 0.095f;
            float barY0 = 0.92f, barY1 = 1.46f;
            float barMid = (barY0 + barY1) * 0.5f;
            float barLen = barY1 - barY0;
            BuildPull(leaf.transform, m.chrome, barX, barMid, barLen, t + 0.048f, t, "Front");
            BuildPull(leaf.transform, m.chrome, barX, barMid, barLen, -0.048f, 0f, "Back");

            // 표지판 — 유리 양면에 붙인 시트
            if (p.sign != null)
            {
                Prim(leaf.transform, PrimitiveType.Quad, "Sign_Front",
                    new Vector3(w * 0.5f, 1.70f, mid + 0.010f), new Vector3(0.30f, 0.12f, 1f), Vector3.zero, p.sign);
                Prim(leaf.transform, PrimitiveType.Quad, "Sign_Back",
                    new Vector3(w * 0.5f, 1.70f, mid - 0.010f), new Vector3(0.30f, 0.12f, 1f), new Vector3(0f, 180f, 0f), p.sign);
            }

            // 충돌체 한 장
            var box = leaf.AddComponent<BoxCollider>();
            box.center = new Vector3(w * 0.5f, h * 0.5f, mid);
            box.size = new Vector3(w, h, t + 0.02f);

            var door = leaf.AddComponent<DoorInteractable>();
            door.hingeAnchor = Vector3.zero;
            door.hingeAxis = Vector3.up;
            door.openAngle = p.openAngle;
            door.swingSpeed = 185f;
            door.labelKo = p.labelKo;
            door.labelEn = p.labelEn;
        }

        static void BuildPull(Transform leaf, Material chrome, float x, float yMid, float len, float z, float faceZ, string suffix)
        {
            Prim(leaf, PrimitiveType.Cylinder, "Pull_Bar_" + suffix,
                new Vector3(x, yMid, z), new Vector3(0.028f, len * 0.5f, 0.028f), Vector3.zero, chrome);

            float standoffZ = (z + faceZ) * 0.5f;
            float standoffLen = Mathf.Abs(z - faceZ);
            for (int i = 0; i < 2; i++)
            {
                float y = yMid + (i == 0 ? -1f : 1f) * (len * 0.5f - 0.035f);
                Prim(leaf, PrimitiveType.Cylinder, "Pull_Standoff_" + suffix + "_" + (i + 1),
                    new Vector3(x, y, standoffZ), new Vector3(0.018f, standoffLen * 0.5f, 0.018f),
                    new Vector3(90f, 0f, 0f), chrome);
            }
        }

        static GameObject Prim(Transform parent, PrimitiveType type, string name,
                               Vector3 localPos, Vector3 localScale, Vector3 localEuler, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col) Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = localEuler;
            go.transform.localScale = localScale;
            var rend = go.GetComponent<Renderer>();
            if (rend && mat) rend.sharedMaterial = mat;
            return go;
        }

        // ------------------------------------------------------------------ 화장실 칸막이 문

        static bool HookCubicleDoor(string objectName, string labelKo, string labelEn, float openAngle)
        {
            GameObject go = FindInScene(objectName);
            if (go == null)
            {
                Debug.LogWarning($"[Doors] 장면에서 '{objectName}' 을(를) 찾지 못했습니다.");
                return false;
            }

            var rend = go.GetComponent<Renderer>();
            if (rend == null) return false;
            Bounds b = rend.bounds;

            if (go.GetComponent<Collider>() == null)
            {
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    var bc = Undo.AddComponent<BoxCollider>(go);
                    bc.center = mf.sharedMesh.bounds.center;
                    bc.size = mf.sharedMesh.bounds.size;
                }
            }

            var existing = go.GetComponent<DoorInteractable>();
            if (existing != null) Undo.DestroyObjectImmediate(existing);

            var door = Undo.AddComponent<DoorInteractable>(go);
            // 경첩: 문짝의 +X 쪽 끝, 열리는 방향(-Z) 면
            Vector3 hingeWorld = new Vector3(b.max.x, b.center.y, b.min.z);
            door.hingeAnchor = go.transform.InverseTransformPoint(hingeWorld);
            door.hingeAxis = go.transform.InverseTransformDirection(Vector3.up);
            door.openAngle = openAngle;
            door.swingSpeed = 170f;
            door.labelKo = labelKo;
            door.labelEn = labelEn;
            EditorUtility.SetDirty(go);
            return true;
        }

        static void UnhookCubicleDoor(string objectName)
        {
            GameObject go = FindInScene(objectName);
            if (go == null) return;
            var d = go.GetComponent<DoorInteractable>();
            if (d != null) Undo.DestroyObjectImmediate(d);
        }

        // ------------------------------------------------------------------ 보조

        static bool AttachInteractor()
        {
            var walker = Object.FindFirstObjectByType<DormitoryWalkthrough>();
            if (walker == null) return false;
            var interactor = walker.GetComponent<DoorInteractor>();
            if (interactor == null) interactor = Undo.AddComponent<DoorInteractor>(walker.gameObject);
            interactor.viewCamera = walker.viewCamera ? walker.viewCamera : walker.GetComponentInChildren<Camera>();
            EditorUtility.SetDirty(walker.gameObject);
            return true;
        }

        static bool EnsureWashroomColliders()
        {
            var importer = AssetImporter.GetAtPath(WashroomFbx) as ModelImporter;
            if (importer == null) return false;
            if (importer.addCollider) return false;
            importer.addCollider = true;
            importer.SaveAndReimport();
            return true;
        }

        static void RemoveContainer(bool undoable)
        {
            var existing = FindRootByName(ContainerName);
            while (existing != null)
            {
                if (undoable) Undo.DestroyObjectImmediate(existing);
                else Object.DestroyImmediate(existing);
                existing = FindRootByName(ContainerName);
            }
        }

        static GameObject FindRootByName(string name)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        static GameObject FindInScene(string name)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;
            var stack = new Stack<Transform>();
            foreach (var root in scene.GetRootGameObjects()) stack.Push(root.transform);
            while (stack.Count > 0)
            {
                Transform t = stack.Pop();
                if (t.name == name) return t.gameObject;
                for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));
            }
            return null;
        }

        static void EditorSceneManager_MarkDirty(Scene scene)
        {
            if (scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
