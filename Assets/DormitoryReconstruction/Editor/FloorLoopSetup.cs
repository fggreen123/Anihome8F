using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Anihome.Dormitory;

namespace Anihome.Dormitory.EditorTools
{
    /// <summary>
    /// 8번 출구식 층 반복 시스템을 씬에 붙인다.
    /// 계단 확인 구역·올라가기 차단벽·표지판 연결·관리자·시작 위치를 전부 여기서 만든다.
    /// 여러 번 눌러도 안전하다 (있으면 지우고 다시 만든다).
    /// 메뉴: Tools ▸ Dormitory ▸ Setup Floor Loop
    /// </summary>
    public static class FloorLoopSetup
    {
        static string MatRoot => DormMaterialFolder.Path;
        const string LoopName = "GameLoop";
        const string SpawnName = "PlayerSpawn";
        const string StairRoot = "Stairwells";
        const string BlockerName = "UpStairBlocker";
        const string GateName = "DownStairGate";
        const string DownWallName = "DownStairBlocker";

        // 계단실 로컬 좌표 (StairwellBuilder 와 같은 기준)
        const float FW = 1.00f, GAP = 0.60f, XO = 2.60f, LAND = 1.05f;
        const float XA0 = 0f, XA1 = FW;          // 올라가는 계단
        const float XB0 = FW + GAP, XB1 = XO;    // 내려가는 계단

        // 전자레인지 앞(근거리) 로비. 계단실 입구 옆, 복도를 바라보게.
        static readonly Vector2 SpawnXZ = new Vector2(0.30f, 1.60f);
        static readonly Vector3 SpawnEuler = new Vector3(0f, 180f, 0f);

        [MenuItem("Tools/Dormitory/Setup Floor Loop", false, 50)]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("층 반복 설정", "DormitoryFloor 장면을 먼저 열어 주세요.", "확인");
                return;
            }

            var walker = Object.FindFirstObjectByType<DormitoryWalkthrough>();
            if (walker == null)
            {
                EditorUtility.DisplayDialog("층 반복 설정",
                    "플레이어(DormitoryWalkthrough)를 찾지 못했습니다.", "확인");
                return;
            }

            GameObject stairs = FindRoot(StairRoot);
            if (stairs == null)
            {
                EditorUtility.DisplayDialog("층 반복 설정",
                    "'Stairwells' 를 찾지 못했습니다.\nTools ▸ Dormitory ▸ Rebuild Stairwells 를 먼저 실행하세요.", "확인");
                return;
            }

            // ---- 계단 확인 구역 / 차단벽 -------------------------------
            int gates = 0, blockers = 0;
            foreach (Transform m in stairs.transform)
            {
                if (Mathf.Abs(m.localPosition.y) > 0.01f) continue;      // 플레이 층만
                bool near = m.name.StartsWith("Stair Near");
                bool far = m.name.StartsWith("Stair Far");
                if (!near && !far) continue;
                float xs = near ? 1f : -1f;                               // 원거리는 폭이 뒤집혀 있다

                ClearChild(m, BlockerName);
                ClearChild(m, GateName);
                ClearChild(m, DownWallName);

                // 올라가는 계단 — 아예 못 올라가게
                MakeBox(m, BlockerName, xs, XA0 - 0.10f, XA1 + 0.06f, 0f, 2.30f, LAND - 0.12f, LAND + 0.16f, false);
                blockers++;

                // 내려가는 계단 — 걸어서는 못 내려가고, 확인 창으로만 내려간다
                MakeBox(m, DownWallName, xs, XB0 - 0.06f, XB1 + 0.10f, -1.20f, 2.30f, LAND + 0.10f, LAND + 0.34f, false);
                blockers++;

                var gate = MakeBox(m, GateName, xs, XB0 - 0.06f, XB1 + 0.10f, 0f, 2.30f, LAND - 0.75f, LAND + 0.12f, true);
                var sg = gate.AddComponent<StairGate>();
                sg.side = near ? StairSide.Microwave : StairSide.Opposite;
                sg.label = near ? "전자레인지 앞 계단" : "반대편 계단";
                gates++;
            }

            // ---- 층수 표지판 -------------------------------------------
            var signs = new List<FloorSign>();
            CollectSigns(stairs.transform, signs);

            // ---- 시작 위치 ----------------------------------------------
            GameObject spawn = FindRoot(SpawnName);
            if (spawn == null)
            {
                spawn = new GameObject(SpawnName);
                Undo.RegisterCreatedObjectUndo(spawn, "Setup Floor Loop");
            }
            // ---- 걸음걸이 (공포 게임용) --------------------------------
            Undo.RecordObject(walker, "Setup Floor Loop");
            walker.moveSpeed = 1.55f;
            walker.sprintSpeed = 2.95f;
            walker.acceleration = 9f;
            walker.headBob = true;
            walker.stridesPerMeter = 0.70f;
            walker.bobUp = 0.018f;
            walker.bobSide = 0.008f;
            walker.bobRoll = 0.20f;
            walker.bobSettle = 4.5f;
            walker.breathe = 0.005f;
            walker.landDip = 0.05f;
            EditorUtility.SetDirty(walker);

            var cc = walker.GetComponent<CharacterController>();
            float y = cc != null ? cc.height * 0.5f + 0.05f : 0.95f;
            Undo.RecordObject(spawn.transform, "Setup Floor Loop");
            spawn.transform.position = new Vector3(SpawnXZ.x, y, SpawnXZ.y);
            spawn.transform.rotation = Quaternion.Euler(SpawnEuler);

            // ---- 관리자 --------------------------------------------------
            GameObject loop = FindRoot(LoopName);
            if (loop == null)
            {
                loop = new GameObject(LoopName);
                Undo.RegisterCreatedObjectUndo(loop, "Setup Floor Loop");
            }
            var mgr = loop.GetComponent<FloorLoopManager>();
            if (mgr == null) mgr = Undo.AddComponent<FloorLoopManager>(loop);

            Undo.RecordObject(mgr, "Setup Floor Loop");
            mgr.startFloor = 8;
            mgr.goalFloor = 1;
            mgr.player = walker.transform;
            mgr.spawnPoint = spawn.transform;
            mgr.signs = signs.ToArray();

            var mats = new Material[13];
            int found = 0;
            for (int n = 2; n <= 12; n++)
            {
                var mm = AssetDatabase.LoadAssetAtPath<Material>($"{MatRoot}/Stair_Sign_{n:00}_{n - 1:00}.mat");
                mats[n] = mm;
                if (mm) found++;
            }
            mgr.signByUpperFloor = mats;

            EditorUtility.SetDirty(mgr);
            EditorUtility.SetDirty(spawn);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = loop;

            Debug.Log($"[FloorLoop] 확인 구역 {gates}개, 차단벽 {blockers}개, 표지판 {signs.Count}개, " +
                      $"층수 재질 {found}개. 시작 위치 {spawn.transform.position}. Ctrl+S 로 저장하세요.");

            if (gates < 2)
                Debug.LogWarning("[FloorLoop] 확인 구역이 2개 미만입니다. " +
                                 "'Stairwells' 아래에 y=0 인 'Stair Near …' / 'Stair Far …' 가 있는지 확인하세요.");
        }

        [MenuItem("Tools/Dormitory/Remove Floor Loop", false, 51)]
        public static void Remove()
        {
            GameObject stairs = FindRoot(StairRoot);
            if (stairs != null)
                foreach (Transform m in stairs.transform)
                {
                    ClearChild(m, BlockerName);
                    ClearChild(m, GateName);
                    ClearChild(m, DownWallName);
                }
            foreach (string n in new[] { LoopName, SpawnName })
            {
                GameObject g = FindRoot(n);
                if (g != null) Undo.DestroyObjectImmediate(g);
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[FloorLoop] 확인 구역·차단벽·관리자·시작 위치를 지웠습니다.");
        }

        // ---------------------------------------------------------------- 도우미

        static void CollectSigns(Transform t, List<FloorSign> list)
        {
            if (t.name == "Floor_Number_Plate")
            {
                var fs = t.GetComponent<FloorSign>();
                if (fs == null) fs = Undo.AddComponent<FloorSign>(t.gameObject);
                if (fs.target == null) fs.target = t.GetComponent<Renderer>();
                EditorUtility.SetDirty(fs);
                list.Add(fs);
            }
            for (int i = 0; i < t.childCount; i++) CollectSigns(t.GetChild(i), list);
        }

        static void ClearChild(Transform parent, string name)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform c = parent.GetChild(i);
                if (c.name == name) Undo.DestroyObjectImmediate(c.gameObject);
            }
        }

        static GameObject MakeBox(Transform parent, string name, float xs,
                                  float x0, float x1, float y0, float y1, float z0, float z1, bool trigger)
        {
            var g = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(g, "Setup Floor Loop");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = new Vector3(xs * (x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            g.transform.localRotation = Quaternion.identity;
            var bc = g.AddComponent<BoxCollider>();
            bc.size = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
            bc.isTrigger = trigger;
            return g;
        }

        static GameObject FindRoot(string name)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;
            foreach (var r in scene.GetRootGameObjects()) if (r.name == name) return r;
            return null;
        }
    }
}
