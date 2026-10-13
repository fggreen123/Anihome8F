using System.Collections.Generic;
using Anihome.Dormitory;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anihome.Dormitory.EditorTools
{
    /// <summary>
    /// 복도를 지나가는 사람(NPC)을 장면에 넣는다.
    ///
    /// 경로는 계단 수치에서 자동으로 뽑는다.
    ///   반대편 아래층 중간참 (안 보이는 곳에서 등장)
    ///   → 반대편 계단으로 올라와 복도로
    ///   → 복도를 쭉 지나 전자레인지 앞 계단으로
    ///   → 중간참에서 돌아 윗층 층참에서 사라짐
    ///
    /// 사람 모양은 임시 마네킹이다. 진짜 모델은 NPC/Visual 아래에 넣으면 된다.
    /// 메뉴: Tools ▸ Dormitory ▸ Setup Corridor Walker
    /// </summary>
    public static class WalkerSetup
    {
        const string RootName = "Corridor Walker";
        const string StairRoot = "Stairwells";

        // 계단 수치 (StairwellBuilder 와 같아야 한다)
        const float H = 2.72f, RISE = 0.17f, GO = 0.27f, HALF = H * 0.5f;
        const int STEPS = 7;
        const float LAND = 1.05f;
        const float HZ0 = LAND + STEPS * GO;              // 2.94
        const float SKEW = 0.55f;
        const float GO_A = GO - SKEW / (2f * STEPS);       // 부채꼴 계단 가운데 줄의 디딤 폭 ≈ 0.231
        const float XA = 0.50f;                            // 올라가는 계단(A) 가운데
        const float XB = 2.10f;                            // B 계단 가운데
        const float TURN_X = 1.30f, TURN_Z = 3.60f;        // 중간참에서 도는 자리

        static string MatRoot => DormMaterialFolder.Path;

        [MenuItem("Tools/Dormitory/Setup Corridor Walker", false, 56)]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("복도 NPC", "DormitoryFloor 장면을 먼저 열어 주세요.", "확인");
                return;
            }

            GameObject stairs = FindRoot(StairRoot);
            if (stairs == null)
            {
                EditorUtility.DisplayDialog("복도 NPC", "'Stairwells' 를 찾지 못했습니다. 계단을 먼저 만들어 주세요.", "확인");
                return;
            }

            Transform farLow = FindModule(stairs.transform, "Stair Far", -H);
            Transform far0 = FindModule(stairs.transform, "Stair Far", 0f);
            Transform near0 = FindModule(stairs.transform, "Stair Near", 0f);
            if (farLow == null || far0 == null || near0 == null)
            {
                EditorUtility.DisplayDialog("복도 NPC",
                    "계단 모듈을 찾지 못했습니다.\n(필요: 반대편 아래층, 반대편 현재층, 전자레인지 쪽 현재층)", "확인");
                return;
            }

            var pts = new List<Vector3>();
            var stairFlags = new List<bool>();
            BuildRoute(farLow, far0, near0, pts, stairFlags);

            // 다시 만들기
            GameObject old = FindRoot(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Setup Corridor Walker");

            var routeGo = new GameObject("Route");
            routeGo.transform.SetParent(root.transform, false);
            var route = routeGo.AddComponent<WalkerRoute>();
            route.points = pts.ToArray();
            route.stairs = stairFlags.ToArray();

            var npc = new GameObject("NPC");
            npc.transform.SetParent(root.transform, false);
            npc.transform.position = pts[0];

            var cap = npc.AddComponent<CapsuleCollider>();
            cap.radius = 0.25f;
            cap.height = 1.74f;
            cap.center = new Vector3(0f, 0.87f, 0f);

            var rb = npc.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var visual = BuildMannequin(npc.transform, out MannequinParts parts);

            var walker = npc.AddComponent<CorridorWalker>();
            walker.route = route;
            walker.visual = visual;
            walker.radius = cap.radius;
            var player = Object.FindFirstObjectByType<DormitoryWalkthrough>();
            if (player) walker.player = player.transform;

            var anim = visual.AddComponent<MannequinAnimator>();
            anim.walker = walker;
            anim.leftLeg = parts.leftLeg;
            anim.rightLeg = parts.rightLeg;
            anim.leftArm = parts.leftArm;
            anim.rightArm = parts.rightArm;
            anim.upper = parts.upper;

            if (pts.Count > 1)
            {
                Vector3 f = pts[1] - pts[0]; f.y = 0f;
                if (f.sqrMagnitude > 1e-6f) npc.transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
            }

            float len = 0f;
            for (int i = 1; i < pts.Count; i++) len += Vector3.Distance(pts[i - 1], pts[i]);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = npc;
            Debug.Log($"[Walker v1] 경로 {pts.Count}점, 길이 {len:0.0} m, 걸어서 약 {len / walker.walkSpeed:0}초. " +
                      "씬 화면에서 하늘색(평지)·주황색(계단) 선으로 경로가 보입니다. Ctrl+S 로 저장하세요.");
        }

        [MenuItem("Tools/Dormitory/Remove Corridor Walker", false, 57)]
        public static void Remove()
        {
            GameObject old = FindRoot(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Walker] 복도 NPC 를 지웠습니다.");
        }

        // ---------------------------------------------------------------- 경로

        static void BuildRoute(Transform farLow, Transform far0, Transform near0, List<Vector3> pts, List<bool> st)
        {
            const float FAR = -1f, NEAR = 1f;

            // 복도 걷는 줄 — 전자레인지 쪽 계단실 입구에서 1 m 안쪽
            float cx = near0.position.x - 1.0f;

            // 반대편 아래층 중간참에서 등장 → B 계단으로 올라온다
            Add(pts, st, W(farLow, FAR, TURN_X, HALF, TURN_Z), false);
            Add(pts, st, W(farLow, FAR, XB, HALF, HZ0 + GO), true);      // 계단 시작 (코 끝 줄)
            Add(pts, st, W(farLow, FAR, XB, H, LAND), false);            // 플레이 층 도착

            // 층참 → 복도. 입구 옆 기둥을 피해 계단 안쪽 줄로 나온다
            Vector3 exit = W(far0, FAR, 1.85f, 0f, 0.45f);
            Add(pts, st, exit, false);
            Add(pts, st, new Vector3(cx + 0.15f, 0f, exit.z), false);
            Add(pts, st, new Vector3(cx, 0f, exit.z + 0.55f), false);

            // 복도를 쭉 지나 전자레인지 앞 계단으로
            Vector3 entry = W(near0, NEAR, XA, 0f, 0.30f);
            Add(pts, st, new Vector3(cx, 0f, entry.z - 0.95f), false);
            Add(pts, st, entry, false);

            // A 계단 (부채꼴) — 코 끝 줄을 따라 중간참까지
            Add(pts, st, W(near0, NEAR, XA, 0f, LAND - GO_A), true);
            Add(pts, st, W(near0, NEAR, XA, HALF, LAND + STEPS * GO_A), false);

            // 중간참에서 돌아 B 계단으로 윗층
            Add(pts, st, W(near0, NEAR, TURN_X, HALF, TURN_Z), false);
            Add(pts, st, W(near0, NEAR, XB, HALF, HZ0 + GO), true);
            Add(pts, st, W(near0, NEAR, XB, H, LAND), false);

            // 윗층 층참 안쪽 (아래에서 안 보이는 곳)에서 사라진다
            Add(pts, st, W(near0, NEAR, 1.60f, H, 0.60f), false);

            // 마지막 구간 표시는 쓰지 않는다
            if (st.Count > 0) st.RemoveAt(st.Count - 1);
        }

        /// 점을 넣고, 그 점에서 다음 점까지가 계단인지 함께 기록한다.
        static void Add(List<Vector3> pts, List<bool> st, Vector3 p, bool stairToNext)
        {
            pts.Add(p);
            st.Add(stairToNext);
        }

        /// 계단실 모듈 좌표 → 월드. xs 는 반대편 계단실의 좌우 뒤집힘.
        static Vector3 W(Transform module, float xs, float x, float y, float z)
            => module.TransformPoint(new Vector3(xs * x, y, z));

        static Transform FindModule(Transform stairs, string prefix, float y)
        {
            foreach (Transform m in stairs)
                if (m.name.StartsWith(prefix) && Mathf.Abs(m.position.y - y) < 0.1f) return m;
            return null;
        }

        // ---------------------------------------------------------------- 임시 마네킹

        struct MannequinParts
        {
            public Transform leftLeg, rightLeg, leftArm, rightArm, upper;
        }

        static GameObject BuildMannequin(Transform parent, out MannequinParts parts)
        {
            Material suit = Mat("Walker_Suit", new Color(0.11f, 0.11f, 0.13f), 0.22f);
            Material skin = Mat("Walker_Skin", new Color(0.78f, 0.64f, 0.54f), 0.30f);
            Material shoe = Mat("Walker_Shoe", new Color(0.05f, 0.05f, 0.05f), 0.45f);
            Material shirt = Mat("Walker_Shirt", new Color(0.86f, 0.86f, 0.84f), 0.20f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);

            var upper = new GameObject("Upper").transform;
            upper.SetParent(visual.transform, false);

            Part(PrimitiveType.Capsule, "Torso", upper, new Vector3(0f, 1.20f, 0f), new Vector3(0.40f, 0.34f, 0.24f), suit);
            Part(PrimitiveType.Cube, "Collar", upper, new Vector3(0f, 1.50f, 0.055f), new Vector3(0.10f, 0.08f, 0.10f), shirt);
            Part(PrimitiveType.Sphere, "Head", upper, new Vector3(0f, 1.63f, 0f), new Vector3(0.20f, 0.24f, 0.21f), skin);

            Transform lArm = Pivot("Shoulder_L", upper, new Vector3(-0.235f, 1.46f, 0f));
            Transform rArm = Pivot("Shoulder_R", upper, new Vector3(0.235f, 1.46f, 0f));
            foreach (Transform a in new[] { lArm, rArm })
            {
                Part(PrimitiveType.Capsule, "Arm", a, new Vector3(0f, -0.31f, 0f), new Vector3(0.10f, 0.31f, 0.10f), suit);
                Part(PrimitiveType.Sphere, "Hand", a, new Vector3(0f, -0.64f, 0f), new Vector3(0.085f, 0.10f, 0.085f), skin);
            }

            Transform lLeg = Pivot("Hip_L", visual.transform, new Vector3(-0.095f, 0.90f, 0f));
            Transform rLeg = Pivot("Hip_R", visual.transform, new Vector3(0.095f, 0.90f, 0f));
            foreach (Transform l in new[] { lLeg, rLeg })
            {
                Part(PrimitiveType.Capsule, "Leg", l, new Vector3(0f, -0.45f, 0f), new Vector3(0.14f, 0.45f, 0.14f), suit);
                Part(PrimitiveType.Cube, "Shoe", l, new Vector3(0f, -0.865f, 0.04f), new Vector3(0.11f, 0.07f, 0.25f), shoe);
            }

            parts = new MannequinParts
            {
                leftLeg = lLeg, rightLeg = rLeg, leftArm = lArm, rightArm = rArm, upper = upper
            };
            return visual;
        }

        static Transform Pivot(string name, Transform parent, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }

        static void Part(PrimitiveType type, string name, Transform parent, Vector3 local, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            var c = g.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = local;
            g.transform.localScale = scale;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
        }

        static Material Mat(string name, Color c, float smooth)
        {
            string path = MatRoot + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject FindRoot(string name)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;
            foreach (GameObject r in scene.GetRootGameObjects()) if (r.name == name) return r;
            return null;
        }
    }
}
