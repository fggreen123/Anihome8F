using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Anihome.Dormitory;

namespace Anihome.Dormitory.EditorTools
{
    /// <summary>
    /// 계단실은 복도에서 직각으로 뻗어 나간다. 복도 안쪽을 향한 쪽이 직선 계단,
    /// 중간참에서 아래층으로 내려오는 쪽은 부채꼴로 비스듬히 돌아 내려온다.
    /// 그래서 가운데 우물이 사다리꼴(세모 느낌)이 되고, 맨 위층부터 맨 아래층까지 뚫려 있다.
    /// 층고 2.72 m 단위로 반복되므로 y += 2.72 로 복제하면 그대로 이어진다.
    /// 메뉴: Tools ▸ Dormitory ▸ Rebuild Stairwells
    /// </summary>
    public static class StairwellBuilder
    {
        // ------------------------------------------------------------- 치수
        // 로컬 좌표 : x = 복도와 나란한 폭,  z = 복도에서 멀어지는 깊이

        const float H = 2.72f;        // 층고 = 16 × 0.17
        const float RISE = 0.17f;
        const float GO = 0.27f;
        const int STEPS = 7;
        const float HALF = H * 0.5f;

        const float XO = 2.60f;       // 계단실 폭
        const float WD = 4.30f;       // 계단실 깊이 (복도 → 끝벽)
        const float CR = 1.30f;       // 중간참 안쪽 모서리 곡률
        const float LAND = 1.05f;     // 층참 깊이
        const float HZ0 = LAND + STEPS * GO;   // 2.94
        const float FW = 1.00f;
        const float GAP = XO - FW * 2f;        // 0.60
        const float XA0 = 0f, XA1 = FW;        // A = 부채꼴 (중간참 → 아래층)
        const float XB0 = FW + GAP, XB1 = XO;  // B = 직선 (층참 → 중간참)

        /// A 계단이 우물쪽으로 당겨지는 총량. 이만큼 비스듬해지고 우물이 세모가 된다.
        const float SKEW = 0.55f;
        static float ZoutA(int j) => LAND + j * GO;                 // 벽쪽 모서리
        static float ZinA(int j) => LAND + j * GO - SKEW * j / STEPS; // 우물쪽 모서리
        const float GO_IN = GO - SKEW / STEPS;                       // 우물쪽 디딤 ≈ 0.191

        /// 옆벽·걸레받이가 시작하는 깊이. 이보다 앞은 복도 영역이라 아무것도 두지 않는다.
        const float ZW = 0.20f;

        const float RAIL_A_X = XA1 + 0.02f;
        const float RAIL_B_X = XB0 - 0.02f;

        const float SLAB = 0.16f;
        const float SOF = 0.42f;      // 계단 몸통 두께 — 밑면이 이 두께로 계단모양을 이룬다
        const float WALL = 0.18f;
        const float RAIL_H = 0.90f;
        const float SKIRT = 0.11f;
        const float NOSE = 0.10f;
        const float UV = 2.78f;

        static float PitchB(float z) => HALF + (HZ0 - z) * (RISE / GO);

        const string MatRoot = "Assets/DormitoryReconstruction/Materials";
        const string TexRoot = "Assets/DormitoryReconstruction/Textures";
        const string ContainerName = "Stairwells";

        // ------------------------------------------------------------- 평면

        static float XMin(float z)
        {
            if (z <= WD - CR) return 0f;
            float d = z - (WD - CR);
            return CR - Mathf.Sqrt(Mathf.Max(0f, CR * CR - d * d));
        }

        static Vector2[] Shell()
        {
            var p = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(XO, 0f), new Vector2(XO, WD), new Vector2(CR, WD)
            };
            AddArc(p, new Vector2(CR, WD - CR), CR, 90f, 180f, 14);
            return Dedup(p);
        }

        static Vector2[] ShellSlice(float z0, float z1)
        {
            var o = new List<Vector2> { new Vector2(XMin(z0), z0), new Vector2(XO, z0), new Vector2(XO, z1) };
            int n = 16;
            for (int i = n; i >= 1; i--)
            {
                float z = Mathf.Lerp(z0, z1, i / (float)n);
                o.Add(new Vector2(XMin(z), z));
            }
            return Dedup(o);
        }

        static Vector2[] RoundWallPath()
        {
            var o = new List<Vector2>();
            AddArc(o, new Vector2(CR, WD - CR), CR, 180f, 90f, 14);
            return Dedup(o);
        }

        static void AddArc(List<Vector2> list, Vector2 c, float r, float a0, float a1, int segs)
        {
            for (int i = 0; i <= segs; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)segs) * Mathf.Deg2Rad;
                list.Add(new Vector2(c.x + r * Mathf.Cos(a), c.y + r * Mathf.Sin(a)));
            }
        }

        static Vector2[] Dedup(List<Vector2> pts)
        {
            var o = new List<Vector2>();
            foreach (var p in pts)
                if (o.Count == 0 || (o[o.Count - 1] - p).sqrMagnitude > 1e-6f) o.Add(p);
            return o.ToArray();
        }

        static Vector2[] Rect(float x0, float x1, float z0, float z1) => new[]
        {
            new Vector2(x0, z0), new Vector2(x1, z0), new Vector2(x1, z1), new Vector2(x0, z1)
        };

        /// A 계단의 한 칸 (비스듬한 사다리꼴).
        static Vector2[] QuadA(int j0, int j1, float inset) => new[]
        {
            new Vector2(XA0 + inset, ZoutA(j0)), new Vector2(XA1 - inset, ZinA(j0)),
            new Vector2(XA1 - inset, ZinA(j1)), new Vector2(XA0 + inset, ZoutA(j1))
        };

        /// A 계단 모서리를 따라가는 얇은 띠 (챌판·코고무용).
        static Vector2[] StripA(int j, float w, float inset) => new[]
        {
            new Vector2(XA0 + inset, ZoutA(j)), new Vector2(XA1 - inset, ZinA(j)),
            new Vector2(XA1 - inset, ZinA(j) + w), new Vector2(XA0 + inset, ZoutA(j) + w)
        };

        // ------------------------------------------------------------- 메뉴

        [MenuItem("Tools/Dormitory/Rebuild Stairwells", false, 40)]
        public static void Build()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("계단 만들기", "DormitoryFloor 장면을 먼저 열어 주세요.", "확인");
                return;
            }

            Pal p = Pal.Load();
            RemoveContainer(false);
            int hidden = SetOldStairsActive(false);

            var container = new GameObject(ContainerName);
            Undo.RegisterCreatedObjectUndo(container, "Rebuild Stairwells");

            int baseFloor = 8;
            for (int level = -1; level <= 1; level++)
            {
                // 근거리 계단실 = 전자레인지 앞 계단
                BuildModule(container.transform, "Stair Near F" + (baseFloor + level),
                    new Vector3(1.41f, level * H, 2.90f), 90f, +1f, p,
                    level == 0, level == 1, baseFloor + level + 1, true);

                // 원거리 계단실 = 반대편 계단
                BuildModule(container.transform, "Stair Far F" + (baseFloor + level),
                    new Vector3(1.41f, level * H, -20.70f), 90f, -1f, p,
                    level == 0, level == 1, baseFloor + level + 1, false);
            }

            TunePlayer();
            AssetDatabase.SaveAssets();
            EditorSceneManager_MarkDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = container;
            Debug.Log($"[Stairs v7 게임 루프 연동] 계단실 6개 모듈(양끝 × 3개층). 기존 계단 {hidden}개를 껐습니다. Ctrl+S 로 저장하세요.");
        }

        [MenuItem("Tools/Dormitory/Restore Original Stairs", false, 41)]
        public static void Restore()
        {
            RemoveContainer(true);
            int shown = SetOldStairsActive(true);
            EditorSceneManager_MarkDirty(SceneManager.GetActiveScene());
            Debug.Log($"[Stairs] 새 계단을 지우고 기존 계단 {shown}개를 다시 켰습니다.");
        }

        // ------------------------------------------------------------- 모듈

        static void BuildModule(Transform parent, string name, Vector3 origin, float yaw, float xs, Pal p,
                                bool isExistingFloor, bool capTop, int upperFloor, bool microwaveSide)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = origin;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var r = new Rig { root = go.transform, xs = xs };

            Landings(r, p, capTop);
            FlightA(r, p);
            FlightB(r, p);
            Balustrade(r, p);
            WallRails(r, p);
            Walls(r, p, isExistingFloor, capTop);
            Skirting(r, p);
            Lighting(r, p, capTop);
            Signage(r, p, upperFloor);
            Extinguisher(r, p);
            GameGates(r, isExistingFloor, microwaveSide);
        }

        /// 게임 진행용 — 플레이 층에만 놓는다.
        /// 올라가는 계단은 막고, 내려가는 계단 앞에는 확인 구역을 둔다.
        static void GameGates(Rig r, bool isPlayLevel, bool microwaveSide)
        {
            if (!isPlayLevel) return;

            r.Solid("UpStairBlocker", XA0 - 0.08f, XA1 + 0.04f, 0f, 2.30f, LAND - 0.08f, LAND + 0.12f);

            var gate = r.Solid("DownStairGate", XB0 - 0.04f, XB1 + 0.08f, 0f, 2.30f, LAND - 0.50f, LAND + 0.06f, true);
            var sg = gate.AddComponent<StairGate>();
            sg.side = microwaveSide ? StairSide.Microwave : StairSide.Opposite;
            sg.label = microwaveSide ? "전자레인지 앞 계단" : "반대편 계단";
        }

        static void Landings(Rig r, Pal p, bool capTop)
        {
            Vector2[] floorPoly = ShellSlice(0f, LAND);
            Vector2[] halfPoly = ShellSlice(HZ0, WD);
            // A 계단이 비스듬해지며 생긴 중간참 쪽 쐐기
            Vector2[] wedge = { new Vector2(XA0, HZ0), new Vector2(XA1, ZinA(STEPS)), new Vector2(XA1, HZ0) };

            r.Prism("Landing_Floor_Slab", floorPoly, -SLAB, -0.016f, p.concrete, 1f, true);
            r.Prism("Landing_Floor_Rubber", floorPoly, -0.016f, 0f, p.rubber, UV, true);
            r.Prism("Landing_Floor_Nose", Rect(XA0, XB1, LAND - NOSE, LAND), -0.02f, 0.004f, p.rubber, UV, false);

            r.Prism("Landing_Half_Slab", halfPoly, HALF - SLAB, HALF - 0.016f, p.concrete, 1f, true);
            r.Prism("Landing_Half_Rubber", halfPoly, HALF - 0.016f, HALF, p.rubber, UV, true);
            r.Prism("Landing_Half_Wedge_Slab", wedge, HALF - SLAB, HALF - 0.016f, p.concrete, 1f, true);
            r.Prism("Landing_Half_Wedge_Rubber", wedge, HALF - 0.016f, HALF, p.rubber, UV, true);
            r.Prism("Landing_Half_Nose", StripA(STEPS, NOSE, 0f), HALF - 0.02f, HALF + 0.004f, p.rubber, UV, false);
            r.Prism("Landing_Half_Nose_B", Rect(XB0, XB1, HZ0, HZ0 + NOSE), HALF - 0.02f, HALF + 0.004f, p.rubber, UV, false);

            if (capTop)
            {
                r.Prism("Landing_Top_Slab", floorPoly, H - SLAB, H - 0.016f, p.concrete, 1f, true);
                r.Prism("Landing_Top_Rubber", floorPoly, H - 0.016f, H, p.rubber, UV, true);
                r.Prism("Ceiling_Cap", ShellSlice(ZW, WD), H, H + SLAB, p.concrete, 1f, true);
            }
        }

        /// 중간참에서 아래층으로 — 부채꼴로 비스듬히.
        /// 한 칸을 두 덩어리로 나눈다. 위 17 cm 는 파랑(디딤판+챌판), 그 아래는 흰색.
        static void FlightA(Rig r, Pal p)
        {
            for (int i = 1; i <= STEPS; i++)
            {
                float top = i * RISE;
                r.Prism($"Step_A_{i:00}_Upper", QuadA(i - 1, i, 0f), top - RISE, top, p.tread, 1f, true);
                r.Prism($"Step_A_{i:00}_Lower", QuadA(i - 1, i, 0f), top - SOF, top - RISE, p.white, 1f, true);
                r.Prism($"Step_A_{i:00}_Nosing", StripA(i - 1, NOSE, 0.006f), top - 0.02f, top + 0.004f, p.rubber, UV, false);
            }
            // 중간참으로 올라서는 마지막 챌판
            r.Prism("Step_A_08_Riser", StripA(STEPS, -0.03f, 0.006f), HALF - RISE, HALF, p.tread, 1f, false);
        }

        /// 층참에서 중간참으로 — 직선. 위 17 cm 파랑 / 아래 흰색.
        static void FlightB(Rig r, Pal p)
        {
            for (int i = 1; i <= STEPS; i++)
            {
                float top = HALF + i * RISE;
                float zF = HZ0 - (i - 1) * GO;
                float za = zF - GO, zb = zF;

                r.Box($"Step_B_{i:00}_Upper", XB0, XB1, top - RISE, top, za, zb, p.tread, true);
                r.Box($"Step_B_{i:00}_Lower", XB0, XB1, top - SOF, top - RISE, za, zb, p.white, true);
                r.Prism($"Step_B_{i:00}_Nosing", Rect(XB0 + 0.006f, XB1 - 0.006f, zF - NOSE, zF),
                        top - 0.02f, top + 0.004f, p.rubber, UV, false);
            }
            r.Box("Step_B_08_Riser", XB0, XB1, H - RISE, H, LAND, LAND + 0.03f, p.tread);
        }

        // ------------------------------------------------------------- 난간

        static void Balustrade(Rig r, Pal p)
        {
            // A 쪽 — 비스듬한 우물 모서리를 따라간다
            Vector2 a0 = new Vector2(ZinA(0) - GO_IN, 0f);  // (z, y) — 코선을 바닥까지 연장
            Vector2 a1 = new Vector2(ZinA(STEPS), HALF);
            Run(r, p, "A", RAIL_A_X, a0, a1, GO_IN);

            // B 쪽 — 직선
            Vector2 b0 = new Vector2(HZ0, HALF);
            Vector2 b1 = new Vector2(LAND - GO, H);
            Run(r, p, "B", RAIL_B_X, b0, b1, GO);

            // 참에서의 되돌림
            float yH = HALF + RAIL_H;
            r.Tube("Rail_Return_Half_A", new Vector3(RAIL_A_X, yH, ZinA(STEPS)), new Vector3(RAIL_A_X, yH, ZinA(STEPS) + 0.30f), 0.055f, p.red);
            r.Tube("Rail_Return_Half_B", new Vector3(RAIL_B_X, yH, HZ0), new Vector3(RAIL_B_X, yH, HZ0 + 0.30f), 0.055f, p.red);
            r.Tube("Rail_Return_Half_Link", new Vector3(RAIL_A_X, yH, ZinA(STEPS) + 0.30f), new Vector3(RAIL_B_X, yH, HZ0 + 0.30f), 0.055f, p.red);

            float zf = LAND - 0.30f;
            r.Tube("Rail_Return_Floor_A", new Vector3(RAIL_A_X, RAIL_H, ZinA(0) - GO_IN), new Vector3(RAIL_A_X, RAIL_H, zf), 0.055f, p.red);
            r.Tube("Rail_Return_Floor_B", new Vector3(RAIL_B_X, RAIL_H, LAND), new Vector3(RAIL_B_X, RAIL_H, zf), 0.055f, p.red);
            r.Tube("Rail_Return_Floor_Link", new Vector3(RAIL_A_X, RAIL_H, zf), new Vector3(RAIL_B_X, RAIL_H, zf), 0.055f, p.red);

            // 우물 끝막이 — 비스듬한 모서리를 따라
            r.Prism("Well_Guard_Half", new[]
            {
                new Vector2(RAIL_A_X, ZinA(STEPS) - 0.03f), new Vector2(RAIL_B_X, HZ0 - 0.03f),
                new Vector2(RAIL_B_X, HZ0 + 0.03f), new Vector2(RAIL_A_X, ZinA(STEPS) + 0.03f)
            }, HALF + 0.10f, HALF + RAIL_H, p.mesh, 1f, false);
            r.Box("Well_Guard_Floor", RAIL_A_X, RAIL_B_X, 0.10f, RAIL_H, LAND - 0.03f, LAND + 0.03f, p.mesh);
        }

        /// (z, y) 두 점을 잇는 난간 한 벌. goStep 은 그 쪽 디딤 폭(법선 계산용).
        static void Run(Rig r, Pal p, string tag, float x, Vector2 a, Vector2 b, float goStep)
        {
            r.Tube($"Rail_{tag}_Top", new Vector3(x, a.y + RAIL_H, a.x), new Vector3(x, b.y + RAIL_H, b.x), 0.055f, p.red, true);
            r.Tube($"Rail_{tag}_Mid", new Vector3(x, a.y + 0.58f, a.x), new Vector3(x, b.y + 0.58f, b.x), 0.028f, p.red);
            r.Tube($"Rail_{tag}_Low", new Vector3(x, a.y + 0.30f, a.x), new Vector3(x, b.y + 0.30f, b.x), 0.028f, p.red);

            r.Slab($"Stringer_{tag}", x, 0.05f, Off(a, -0.17f, goStep), Off(b, -0.17f, goStep), 0.30f, p.brown, true);

            float panelH = 0.26f;
            r.Slab($"Mesh_{tag}", x, 0.008f,
                   Off(a, 0.02f + panelH * 0.5f, goStep), Off(b, 0.02f + panelH * 0.5f, goStep), panelH, p.mesh);

            for (int i = 0; i <= 5; i++)
            {
                float t = i / 5f;
                float z = Mathf.Lerp(a.x, b.x, t);
                float y = Mathf.Lerp(a.y, b.y, t);
                r.Box($"Post_{tag}_{i:00}", x - 0.024f, x + 0.024f, y + 0.02f, y + RAIL_H, z - 0.024f, z + 0.024f, p.red, true);
            }
        }

        /// 경사면 법선 방향으로 d 만큼 (양수 = 위쪽).
        static Vector2 Off(Vector2 pt, float d, float goStep)
        {
            float len = Mathf.Sqrt(goStep * goStep + RISE * RISE);
            Vector2 n = new Vector2(-RISE / len, goStep / len);
            if (n.y < 0f) n = -n;
            return pt + n * d;
        }

        static void WallRails(Rig r, Pal p)
        {
            r.Tube("WallRail_A", new Vector3(0.07f, RAIL_H, ZoutA(0)), new Vector3(0.07f, HALF + RAIL_H, ZoutA(STEPS)), 0.042f, p.chrome);
            for (int i = 0; i <= 3; i++)
            {
                float t = i / 3f;
                float z = Mathf.Lerp(ZoutA(0), ZoutA(STEPS), t), y = Mathf.Lerp(RAIL_H, HALF + RAIL_H, t);
                r.Tube($"WallRail_A_Bracket{i}", new Vector3(0f, y, z), new Vector3(0.07f, y, z), 0.022f, p.chrome);
            }

            float bx = XO - 0.07f;
            r.Tube("WallRail_B", new Vector3(bx, HALF + RAIL_H, HZ0), new Vector3(bx, H + RAIL_H, LAND - GO), 0.042f, p.chrome);
            for (int i = 0; i <= 3; i++)
            {
                float t = i / 3f;
                float z = Mathf.Lerp(HZ0, LAND - GO, t), y = Mathf.Lerp(HALF + RAIL_H, H + RAIL_H, t);
                r.Tube($"WallRail_B_Bracket{i}", new Vector3(XO, y, z), new Vector3(bx, y, z), 0.022f, p.chrome);
            }
        }

        static void Walls(Rig r, Pal p, bool isExistingFloor, bool capTop)
        {
            r.Box("Wall_Side_Straight", XO, XO + WALL, 0f, H, ZW, WD + WALL, p.white, true);
            r.Box("Wall_Side_Round", -WALL, 0f, 0f, H, ZW, WD - CR, p.white, true);
            r.WallRun("Wall_Round", RoundWallPath(), 0f, H, -WALL, p.white, true);
            r.Box("Wall_End", CR, XO, 0f, H, WD, WD + WALL, p.white, true);

            if (!isExistingFloor)
            {
                r.Box("Wall_Entry_A", -WALL, 0.75f, 0f, H, 0f, WALL, p.white, true);
                r.Box("Wall_Entry_B", 1.85f, XO + WALL, 0f, H, 0f, WALL, p.white, true);
                r.Box("Wall_Entry_Head", 0.75f, 1.85f, 2.10f, H, 0f, WALL, p.white, true);
                r.Box("Door_Frame_Jamb_A", 0.75f, 0.80f, 0f, 2.10f, 0f, WALL + 0.012f, p.brown);
                r.Box("Door_Frame_Jamb_B", 1.80f, 1.85f, 0f, 2.10f, 0f, WALL + 0.012f, p.brown);
                r.Box("Door_Frame_Head", 0.75f, 1.85f, 2.05f, 2.10f, 0f, WALL + 0.012f, p.brown);
            }
        }

        static void Skirting(Rig r, Pal p)
        {
            r.Box("Skirt_Floor_Round", 0f, 0.026f, 0f, SKIRT, ZW, LAND, p.brown);
            r.Box("Skirt_Floor_Straight", XO - 0.026f, XO, 0f, SKIRT, ZW, LAND, p.brown);

            r.Box("Skirt_Half_Straight", XO - 0.026f, XO, HALF, HALF + SKIRT, HZ0, WD, p.brown);
            r.Box("Skirt_Half_End", CR, XO, HALF, HALF + SKIRT, WD - 0.026f, WD, p.brown);
            r.WallRun("Skirt_Half_Round", RoundWallPath(), HALF, HALF + SKIRT, 0.026f, p.brown);

            // 경사 구간 — A 는 벽쪽 모서리를 따른다
            r.Slab("Skirt_A", 0.013f, 0.026f,
                   Off(new Vector2(ZoutA(0) - GO, 0f), 0.02f + SKIRT * 0.5f, GO),
                   Off(new Vector2(ZoutA(STEPS), HALF), 0.02f + SKIRT * 0.5f, GO), SKIRT, p.brown);
            r.Slab("Skirt_B", XO - 0.013f, 0.026f,
                   Off(new Vector2(HZ0, HALF), 0.02f + SKIRT * 0.5f, GO),
                   Off(new Vector2(LAND - GO, H), 0.02f + SKIRT * 0.5f, GO), SKIRT, p.brown);
        }

        // ------------------------------------------------------------- 조명

        static void Lighting(Rig r, Pal p, bool capTop)
        {
            // 층참 천장등 (윗층 바닥판 바로 아래)
            Lamp(r, p, "Lamp_Floor", XO * 0.5f, H - SLAB - 0.04f, LAND * 0.5f, 0.46f, 0.24f);
            // 중간참 벽등 (끝벽 위쪽)
            float cx = (CR + XO) * 0.5f;
            r.Box("Lamp_Half_Housing", cx - 0.24f, cx + 0.24f, HALF + 2.02f, HALF + 2.22f, WD - 0.16f, WD - 0.01f, p.white);
            r.Box("Lamp_Half_Lens", cx - 0.21f, cx + 0.21f, HALF + 2.05f, HALF + 2.19f, WD - 0.17f, WD - 0.155f, p.lamp);
            r.PointLight("Lamp_Half_Light", new Vector3(cx, HALF + 2.05f, WD - 0.45f), 6.5f, 2.6f, false);
            // 우물을 타고 내려가는 빛
            r.PointLight("Well_Light", new Vector3((XA1 + XB0) * 0.5f, HALF + 0.9f, (LAND + HZ0) * 0.5f), 5.5f, 1.4f, false);
        }

        static void Lamp(Rig r, Pal p, string n, float cx, float cy, float cz, float w, float d)
        {
            r.Box(n + "_Housing", cx - w * 0.5f, cx + w * 0.5f, cy, cy + 0.07f, cz - d * 0.5f, cz + d * 0.5f, p.white);
            r.Box(n + "_Lens", cx - w * 0.5f + 0.02f, cx + w * 0.5f - 0.02f, cy - 0.025f, cy,
                  cz - d * 0.5f + 0.02f, cz + d * 0.5f - 0.02f, p.lamp);
            r.PointLight(n + "_Light", new Vector3(cx, cy - 0.15f, cz), 6.5f, 2.8f, true);
        }

        static void Signage(Rig r, Pal p, int upperFloor)
        {
            Material sign = p.Sign(upperFloor);
            if (sign == null) return;
            float cx = (CR + XO) * 0.5f;
            var plate = r.Plate("Floor_Number_Plate", new Vector3(cx, HALF + 1.30f, WD - 0.013f), 0.30f, 0.34f, 180f, sign);
            if (plate) plate.AddComponent<FloorSign>().target = plate.GetComponent<Renderer>();
            r.Box("Floor_Number_Backing", cx - 0.155f, cx + 0.155f,
                  HALF + 1.12f, HALF + 1.48f, WD - 0.011f, WD - 0.003f, p.white);
        }

        static void Extinguisher(Rig r, Pal p)
        {
            float x = XO - 0.26f, z = 0.30f;
            r.Tube("Extinguisher_Body", new Vector3(x, 0.02f, z), new Vector3(x, 0.44f, z), 0.16f, p.red);
            r.Tube("Extinguisher_Neck", new Vector3(x, 0.44f, z), new Vector3(x, 0.55f, z), 0.05f, p.chrome);
        }

        // ------------------------------------------------------------- 배치

        class Rig
        {
            public Transform root;
            public float xs = 1f;

            Vector3 P(float x, float y, float z) => new Vector3(xs * x, y, z);

            GameObject Spawn(PrimitiveType t, string n, Material m, bool collide)
            {
                var g = GameObject.CreatePrimitive(t);
                g.name = n;
                var c = g.GetComponent<Collider>();
                if (c != null && (!collide || t != PrimitiveType.Cube)) Object.DestroyImmediate(c);
                g.transform.SetParent(root, false);
                var rd = g.GetComponent<Renderer>();
                if (rd && m) rd.sharedMaterial = m;
                return g;
            }

            public GameObject Box(string n, float x0, float x1, float y0, float y1, float z0, float z1,
                                  Material m, bool collide = false)
            {
                var g = Spawn(PrimitiveType.Cube, n, m, collide);
                g.transform.localPosition = P((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
                g.transform.localScale = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
                return g;
            }

            public GameObject Slab(string n, float xc, float xw, Vector2 a, Vector2 b,
                                   float thick, Material m, bool collide = false)
            {
                Vector3 wa = P(xc, a.y, a.x), wb = P(xc, b.y, b.x);
                Vector3 d = wb - wa;
                var g = Spawn(PrimitiveType.Cube, n, m, collide);
                g.transform.localPosition = (wa + wb) * 0.5f;
                g.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(d.y, d.z) * Mathf.Rad2Deg, 0f, 0f);
                g.transform.localScale = new Vector3(xw, thick, d.magnitude);
                return g;
            }

            public GameObject Tube(string n, Vector3 a, Vector3 b, float dia, Material m, bool collide = false)
            {
                Vector3 wa = P(a.x, a.y, a.z), wb = P(b.x, b.y, b.z);
                Vector3 d = wb - wa;
                if (d.sqrMagnitude < 1e-8f) return null;
                var g = Spawn(PrimitiveType.Cylinder, n, m, false);
                g.transform.localPosition = (wa + wb) * 0.5f;
                g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
                g.transform.localScale = new Vector3(dia, d.magnitude * 0.5f, dia);
                if (collide) g.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 1f);
                return g;
            }

            /// 눈에 보이지 않는 상자. isTrigger 면 통과 가능한 감지 구역.
            public GameObject Solid(string n, float x0, float x1, float y0, float y1, float z0, float z1,
                                    bool isTrigger = false)
            {
                var g = new GameObject(n);
                g.transform.SetParent(root, false);
                g.transform.localPosition = P((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
                var bc = g.AddComponent<BoxCollider>();
                bc.size = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
                bc.isTrigger = isTrigger;
                return g;
            }

            public GameObject Plate(string n, Vector3 c, float w, float h, float yaw, Material m)
            {
                var g = Spawn(PrimitiveType.Quad, n, m, false);
                g.transform.localPosition = P(c.x, c.y, c.z);
                g.transform.localRotation = Quaternion.Euler(0f, xs > 0f ? yaw : -yaw, 0f);
                g.transform.localScale = new Vector3(w, h, 1f);
                return g;
            }

            public GameObject PointLight(string n, Vector3 pos, float range, float intensity, bool shadows)
            {
                var g = new GameObject(n);
                g.transform.SetParent(root, false);
                g.transform.localPosition = P(pos.x, pos.y, pos.z);
                var l = g.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = range;
                l.intensity = intensity;
                l.color = new Color(1f, 0.96f, 0.89f);
                l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
                l.renderMode = LightRenderMode.Auto;
                return g;
            }

            public GameObject Prism(string n, Vector2[] poly, float y0, float y1,
                                    Material m, float uvScale, bool collide)
            {
                var g = new GameObject(n);
                g.transform.SetParent(root, false);
                var mf = g.AddComponent<MeshFilter>();
                var mr = g.AddComponent<MeshRenderer>();
                if (m) mr.sharedMaterial = m;
                Mesh mesh = BuildPrism(poly, y0, y1, uvScale, xs);
                mesh.name = n;
                mf.sharedMesh = mesh;
                if (collide) g.AddComponent<MeshCollider>().sharedMesh = mesh;
                return g;
            }

            public void WallRun(string n, Vector2[] path, float y0, float y1, float t,
                                Material m, bool collide = false)
            {
                for (int i = 0; i + 1 < path.Length; i++)
                {
                    Vector2 a = path[i], b = path[i + 1];
                    Vector2 d = b - a;
                    float len = d.magnitude;
                    if (len < 1e-4f) continue;
                    Vector2 dir = d / len;
                    Vector2 nor = new Vector2(dir.y, -dir.x);
                    Vector2 mid = (a + b) * 0.5f + nor * (t * 0.5f);

                    Vector3 wa = P(a.x, 0f, a.y), wb = P(b.x, 0f, b.y);
                    Vector3 wd = wb - wa;

                    var g = Spawn(PrimitiveType.Cube, $"{n}_{i:00}", m, collide);
                    g.transform.localPosition = P(mid.x, (y0 + y1) * 0.5f, mid.y);
                    g.transform.localRotation = Quaternion.Euler(0f, Mathf.Atan2(wd.x, wd.z) * Mathf.Rad2Deg, 0f);
                    g.transform.localScale = new Vector3(Mathf.Abs(t), y1 - y0, len + 0.02f);
                }
            }
        }

        // ------------------------------------------------------------- 메시

        static Mesh BuildPrism(Vector2[] poly, float y0, float y1, float uvScale, float xs)
        {
            int n = poly.Length;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();

            Vector3 V(Vector2 q, float y) => new Vector3(xs * q.x, y, q.y);

            Vector2 c2 = Vector2.zero;
            for (int i = 0; i < n; i++) c2 += poly[i];
            c2 /= n;

            int topC = v.Count; v.Add(V(c2, y1)); uv.Add(c2 * uvScale);
            int topS = v.Count;
            for (int i = 0; i < n; i++) { v.Add(V(poly[i], y1)); uv.Add(poly[i] * uvScale); }
            for (int i = 0; i < n; i++) Tri(tri, v, topC, topS + i, topS + (i + 1) % n, Vector3.up);

            int botC = v.Count; v.Add(V(c2, y0)); uv.Add(c2 * uvScale);
            int botS = v.Count;
            for (int i = 0; i < n; i++) { v.Add(V(poly[i], y0)); uv.Add(poly[i] * uvScale); }
            for (int i = 0; i < n; i++) Tri(tri, v, botC, botS + i, botS + (i + 1) % n, Vector3.down);

            float run = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % n];
                float seg = (b - a).magnitude;
                if (seg < 1e-5f) continue;
                Vector2 outward = new Vector2((b - a).y, -(b - a).x).normalized;
                Vector3 want = new Vector3(xs * outward.x, 0f, outward.y);

                int s = v.Count;
                v.Add(V(a, y0)); uv.Add(new Vector2(run, y0) * uvScale);
                v.Add(V(b, y0)); uv.Add(new Vector2(run + seg, y0) * uvScale);
                v.Add(V(b, y1)); uv.Add(new Vector2(run + seg, y1) * uvScale);
                v.Add(V(a, y1)); uv.Add(new Vector2(run, y1) * uvScale);
                Tri(tri, v, s, s + 1, s + 2, want);
                Tri(tri, v, s, s + 2, s + 3, want);
                run += seg;
            }

            var mesh = new Mesh();
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Tri(List<int> t, List<Vector3> v, int a, int b, int c, Vector3 want)
        {
            Vector3 nrm = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(nrm, want) < 0f) { t.Add(a); t.Add(c); t.Add(b); }
            else { t.Add(a); t.Add(b); t.Add(c); }
        }

        // ------------------------------------------------------------- 재질

        class Pal
        {
            public Material concrete, brown, white, red, chrome, tread, rubber, mesh, lamp;
            readonly Dictionary<int, Material> signs = new Dictionary<int, Material>();

            public Material Sign(int upper) => signs.TryGetValue(upper, out var m) ? m : null;

            public static Pal Load()
            {
                Material src = AssetDatabase.LoadAssetAtPath<Material>(MatRoot + "/WhiteWall.mat");
                Shader sh = src != null ? src.shader : Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");

                var coin = Tex(TexRoot + "/Rubber_Coin_Base.png", false);
                var coinN = Tex(TexRoot + "/Rubber_Coin_Normal.png", true);
                var grid = Tex(TexRoot + "/Stair_Rail_Mesh.png", false);

                var p = new Pal();
                p.concrete = Make(sh, "Stair_Concrete", new Color(0.72f, 0.71f, 0.68f), 0f, 0.18f);
                p.brown = Make(sh, "Stair_Skirt_Brown", new Color(0.478f, 0.372f, 0.196f), 0f, 0.42f);
                p.white = Make(sh, "Stair_Wall_White", new Color(0.905f, 0.894f, 0.858f), 0f, 0.20f);
                p.red = Make(sh, "Stair_Rail_Red", new Color(0.545f, 0.208f, 0.161f), 0.25f, 0.46f);
                p.tread = Make(sh, "Stair_Tread_Blue", new Color(0.40f, 0.55f, 0.70f), 0f, 0.60f);
                p.chrome = AssetDatabase.LoadAssetAtPath<Material>(MatRoot + "/Chrome.mat")
                             ?? Make(sh, "Stair_Chrome", new Color(0.72f, 0.73f, 0.74f), 0.55f, 0.72f);
                p.rubber = Make(sh, "Stair_Rubber_Blue", new Color(0.38f, 0.54f, 0.69f), 0f, 0.66f,
                                coin, coinN, Vector2.one);
                p.mesh = MakeCutout(sh, "Stair_Mesh_Red", grid, new Vector2(7f, 2f));
                p.lamp = MakeEmissive(sh, "Stair_Lamp_Lens", new Color(1f, 0.97f, 0.90f), 3.2f);

                for (int n = 2; n <= 12; n++)
                {
                    var t = Tex($"{TexRoot}/Sign_Stair_{n:00}_{n - 1:00}.png", false);
                    if (t == null) continue;
                    p.signs[n] = Make(sh, $"Stair_Sign_{n:00}_{n - 1:00}", Color.white, 0f, 0.25f, t, null, Vector2.one);
                }
                return p;
            }

            static Texture2D Tex(string path, bool normalMap)
            {
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp != null)
                {
                    var want = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    if (imp.textureType != want || imp.wrapMode != TextureWrapMode.Repeat)
                    {
                        imp.textureType = want;
                        imp.wrapMode = TextureWrapMode.Repeat;
                        if (!normalMap) imp.alphaIsTransparency = true;
                        imp.SaveAndReimport();
                    }
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            static Material Make(Shader sh, string name, Color c, float metallic, float smooth,
                                 Texture baseMap = null, Texture normal = null, Vector2 tiling = default)
            {
                string path = $"{MatRoot}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                if (baseMap != null)
                {
                    Vector2 ti = tiling == default ? Vector2.one : tiling;
                    if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", baseMap); m.SetTextureScale("_BaseMap", ti); }
                    if (m.HasProperty("_MainTex")) { m.SetTexture("_MainTex", baseMap); m.SetTextureScale("_MainTex", ti); }
                }
                if (normal != null && m.HasProperty("_BumpMap"))
                {
                    m.SetTexture("_BumpMap", normal);
                    m.EnableKeyword("_NORMALMAP");
                    if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 1f);
                }
                EditorUtility.SetDirty(m);
                return m;
            }

            static Material MakeCutout(Shader sh, string name, Texture grid, Vector2 tiling)
            {
                var m = Make(sh, name, Color.white, 0.3f, 0.45f, grid, null, tiling);
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
                if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
                if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
                if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 1f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = 2450;
                EditorUtility.SetDirty(m);
                return m;
            }

            static Material MakeEmissive(Shader sh, string name, Color c, float power)
            {
                var m = Make(sh, name, c, 0f, 0.35f);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * power);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                EditorUtility.SetDirty(m);
                return m;
            }
        }

        // ------------------------------------------------------------- 기타

        static readonly string[] OldStairPrefixes =
        {
            "Step_Near_", "Step_Far_", "Step_Rubber_", "Step_Nosing_", "Step_Return_",
            "Stair_Handrail_", "Stair_Midrail_", "Stair_RailPost_", "Stair_Return_Handrail_",
            "Stair_Number_Plate_", "Landing_StairHead_", "Landing_HalfLevel_",
            "Wall_StairEnd_", "Wall_StairOuter_",
            "Stainless_Safety_Rod_", "Safety_Rod_Foot_"
        };

        static int SetOldStairsActive(bool active)
        {
            int n = 0;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return 0;
            var stack = new Stack<Transform>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == ContainerName) continue;
                stack.Push(root.transform);
            }
            while (stack.Count > 0)
            {
                Transform t = stack.Pop();
                for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));
                foreach (string pre in OldStairPrefixes)
                {
                    if (!t.name.StartsWith(pre)) continue;
                    if (t.gameObject.activeSelf != active)
                    {
                        Undo.RecordObject(t.gameObject, "Toggle Old Stairs");
                        t.gameObject.SetActive(active);
                        EditorUtility.SetDirty(t.gameObject);
                        n++;
                    }
                    break;
                }
            }
            return n;
        }

        static void TunePlayer()
        {
            var walker = Object.FindFirstObjectByType<DormitoryWalkthrough>();
            if (walker == null) return;
            var cc = walker.GetComponent<CharacterController>();
            if (cc == null) return;
            if (cc.stepOffset < 0.32f || cc.slopeLimit < 50f)
            {
                Undo.RecordObject(cc, "Tune CharacterController");
                cc.stepOffset = Mathf.Max(cc.stepOffset, 0.32f);
                cc.slopeLimit = Mathf.Max(cc.slopeLimit, 50f);
                EditorUtility.SetDirty(cc);
            }
        }

        static void RemoveContainer(bool undoable)
        {
            GameObject g = FindRoot(ContainerName);
            while (g != null)
            {
                if (undoable) Undo.DestroyObjectImmediate(g); else Object.DestroyImmediate(g);
                g = FindRoot(ContainerName);
            }
        }

        static GameObject FindRoot(string name)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;
            foreach (var r in scene.GetRootGameObjects()) if (r.name == name) return r;
            return null;
        }

        static void EditorSceneManager_MarkDirty(Scene s)
        {
            if (s.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(s);
        }
    }
}
