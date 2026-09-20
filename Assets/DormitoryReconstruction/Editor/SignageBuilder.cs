using System.Collections.Generic;
using Anihome.Dormitory;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anihome.Dormitory.EditorTools
{
    /// <summary>
    /// 층에 따라 바뀌는 번호판과 변기칸 잠김/열림 표시를 장면에 붙인다.
    ///
    ///   · 방 번호   — 8층이면 801·802 … , 5층이면 501 … 로 바뀐다
    ///   · 엘리베이터 층 표시 — 늘 3 이던 것을 현재 층으로
    ///   · 변기칸 문 — 걸쇠·표시가 문과 같이 움직이고, 닫히면 빨강·열리면 초록
    ///
    /// 판때기는 유니티 기본 Quad 를 쓰지 않고 직접 만든 메시를 쓴다.
    /// 앞뒷면과 UV 를 확실히 잡아 두어야 안 보이는 일이 없다.
    /// 호실 칸은 메시 UV 에 박아 넣고, 층(세로 위치)만 재질에서 옮긴다.
    ///
    /// 메뉴: Tools ▸ Dormitory ▸ Setup Signage
    /// 여러 번 눌러도 안전하다.
    /// </summary>
    public static class SignageBuilder
    {
        const string DormRoot = "Assets/DormitoryReconstruction";
        const string TexRoot = DormRoot + "/Textures";
        const string MeshRoot = DormRoot + "/Meshes";
        const string RoomTexPath = TexRoot + "/Room_Numbers.png";
        const string ElevTexPath = TexRoot + "/Elevator_Floors.png";

        static string MatRoot => DormMaterialFolder.Path;

        const int RoomCols = 10;
        const int FloorRows = 8;            // 최고층 8층
        const float Cap = 0.62f;            // 아틀라스 칸 높이 대비 숫자 높이
        const float RoomCellAspect = 2.5f;  // 칸 200 × 80
        const float ElevCellAspect = 1f;    // 칸 256 × 256

        const string RoomDisplay = "Room_Number_Display";
        const string ElevDisplay = "Elevator_Floor_Display";
        const string StallFace = "Stall_Indicator_Face";
        const string StallBezel = "Stall_Indicator_Bezel";

        // ---------------------------------------------------------------- 메뉴

        [MenuItem("Tools/Dormitory/Setup Signage", false, 52)]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("번호판 설정", "DormitoryFloor 장면을 먼저 열어 주세요.", "확인");
                return;
            }

            Texture2D roomTex = Tex(RoomTexPath);
            Texture2D elevTex = Tex(ElevTexPath);
            if (roomTex == null || elevTex == null)
            {
                EditorUtility.DisplayDialog("번호판 설정",
                    "Textures 폴더에서 Room_Numbers.png / Elevator_Floors.png 를 찾지 못했습니다.", "확인");
                return;
            }

            Material roomMat = EnsureMat("Room_Number_Sheet", m =>
            {
                Paint(m, Color.white, 0f, 0.22f);
                SetMap(m, roomTex);
                Cutout(m, 0.35f);
                TwoSided(m);
                Emit(m, new Color(0.30f, 0.30f, 0.32f));   // 어두운 복도에서도 읽히게 아주 약하게
            });

            Material elevMat = EnsureMat("Elevator_Floor_Sheet", m =>
            {
                Paint(m, Color.white, 0f, 0.30f);
                SetMap(m, elevTex);
                Cutout(m, 0.35f);
                TwoSided(m);
                Emit(m, new Color(1f, 0.72f, 0.30f) * 2.4f);
            });

            Material vacant = EnsureMat("WR_Indicator_Vacant", m =>
            {
                Paint(m, new Color(0.16f, 0.72f, 0.34f), 0f, 0.45f);
                TwoSided(m);
                Emit(m, new Color(0.16f, 0.72f, 0.34f) * 1.6f);
            });

            Material occupied = EnsureMat("WR_Indicator_Occupied", m =>
            {
                Paint(m, new Color(0.84f, 0.18f, 0.16f), 0f, 0.45f);
                TwoSided(m);
                Emit(m, new Color(0.84f, 0.18f, 0.16f) * 1.6f);
            });

            Material bezel = EnsureMat("WR_Indicator_Bezel", m =>
            {
                Paint(m, new Color(0.13f, 0.13f, 0.15f), 0.2f, 0.4f);
                TwoSided(m);
            });

            // 호실마다 UV 가 다른 판때기 메시
            var roomMesh = new Mesh[RoomCols + 1];
            for (int c = 1; c <= RoomCols; c++)
                roomMesh[c] = PlateMesh($"Number_Plate_Col_{c:00}", (c - 1) / (float)RoomCols, c / (float)RoomCols);
            Mesh fullMesh = PlateMesh("Number_Plate_Full", 0f, 1f);

            List<Transform> all = AllTransforms();

            int rooms = BuildRooms(all, roomMat, roomMesh);
            int lifts = BuildElevators(all, elevMat, fullMesh);
            int stalls = BuildStalls(all, fullMesh, vacant, occupied, bezel);

            FloorNumberDisplay.SetFloor(FloorRows);

            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[Signage v3 좌우 교정] 방 번호 {rooms}개, 엘리베이터 층 표시 {lifts}개, 변기칸 {stalls}개. " +
                      $"재질 폴더: {MatRoot}. Ctrl+S 로 저장하세요.");

            if (rooms == 0) Debug.LogWarning("[Signage] 'Door_Number' 오브젝트를 찾지 못했습니다.");
            if (stalls == 0) Debug.LogWarning("[Signage] 'WR_Cubicle_Door' 오브젝트를 찾지 못했습니다.");
        }

        [MenuItem("Tools/Dormitory/Remove Signage", false, 53)]
        public static void Remove()
        {
            foreach (Transform t in AllTransforms())
            {
                if (t == null) continue;
                ClearChild(t, RoomDisplay);
                ClearChild(t, ElevDisplay);
                ClearChild(t, StallFace);
                ClearChild(t, StallBezel);

                if (t.name.StartsWith("Door_Number")
                    || t.name.StartsWith("Elevator_FloorNumber")
                    || t.name.StartsWith("WR_Stall_Occupancy_Indicator"))
                {
                    var r = t.GetComponent<MeshRenderer>();
                    if (r && !r.enabled) { Undo.RecordObject(r, "Remove Signage"); r.enabled = true; }
                }
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Signage] 번호판과 변기칸 표시를 지우고 원래 숫자를 다시 켰습니다.");
        }

        // ---------------------------------------------------------------- 방 번호

        static int BuildRooms(List<Transform> all, Material mat, Mesh[] meshByRoom)
        {
            int n = 0;
            foreach (Transform t in all)
            {
                if (t == null || !t.name.StartsWith("Door_Number")) continue;
                var rend = t.GetComponent<MeshRenderer>();
                if (rend == null) continue;

                int room = RoomIndexOf(t);
                if (room <= 0 || room > RoomCols) continue;

                Bounds b = rend.bounds;
                ClearChild(t, RoomDisplay);
                if (rend.enabled) { Undo.RecordObject(rend, "Setup Signage"); rend.enabled = false; }

                Vector3 outward = Outward(b);
                float h = b.size.y / Cap;
                float w = h * RoomCellAspect;

                GameObject q = MakePlate(t, RoomDisplay, b.center + outward * 0.006f, outward,
                                         w, h, meshByRoom[room], mat);
                var d = Undo.AddComponent<FloorNumberDisplay>(q);
                d.rows = FloorRows;
                d.target = q.GetComponent<MeshRenderer>();
                n++;
            }
            return n;
        }

        /// "Room Door 308" 같은 부모 이름에서 호실 번호를 읽는다.
        /// 자기 이름("Door_Number.009")은 호실과 상관없으므로 부모부터 본다.
        static int RoomIndexOf(Transform t)
        {
            Transform p = t.parent;
            for (int depth = 0; depth < 4 && p != null; depth++, p = p.parent)
            {
                int v = TwoDigits(p.name);
                if (v > 0) return v;
            }
            return -1;
        }

        /// 이름 끝의 숫자 세 자리 이상에서 뒤 두 자리를 읽는다. "Room Door 310" → 10
        static int TwoDigits(string s)
        {
            int end = -1;
            for (int i = s.Length - 1; i >= 0; i--)
                if (s[i] >= '0' && s[i] <= '9') { end = i; break; }
            if (end < 2) return -1;

            int start = end;
            while (start > 0 && s[start - 1] >= '0' && s[start - 1] <= '9') start--;
            if (end - start + 1 < 3) return -1;

            if (!int.TryParse(s.Substring(end - 1, 2), out int two)) return -1;
            if (two <= 0) two = RoomCols;
            return Mathf.Clamp(two, 1, RoomCols);
        }

        // ---------------------------------------------------------------- 엘리베이터

        static int BuildElevators(List<Transform> all, Material mat, Mesh mesh)
        {
            int n = 0;
            foreach (Transform t in all)
            {
                if (t == null || !t.name.StartsWith("Elevator_FloorNumber")) continue;
                var rend = t.GetComponent<MeshRenderer>();
                if (rend == null) continue;

                Bounds b = rend.bounds;
                ClearChild(t, ElevDisplay);
                if (rend.enabled) { Undo.RecordObject(rend, "Setup Signage"); rend.enabled = false; }

                Vector3 outward = Outward(b);
                float h = b.size.y / Cap;
                float w = h * ElevCellAspect;

                GameObject q = MakePlate(t, ElevDisplay, b.center + outward * 0.008f, outward, w, h, mesh, mat);
                var d = Undo.AddComponent<FloorNumberDisplay>(q);
                d.rows = FloorRows;
                d.target = q.GetComponent<MeshRenderer>();
                n++;
            }
            return n;
        }

        // ---------------------------------------------------------------- 변기칸

        static int BuildStalls(List<Transform> all, Mesh mesh, Material vacant, Material occupied, Material bezel)
        {
            var doors = new List<DoorInteractable>();
            var latches = new List<Transform>();
            var marks = new List<Transform>();

            foreach (Transform t in all)
            {
                if (t == null) continue;
                if (t.name.StartsWith("WR_Cubicle_Door"))
                {
                    var rend = t.GetComponent<MeshRenderer>();
                    if (rend == null) continue;
                    var di = t.GetComponent<DoorInteractable>();
                    if (di == null) di = HookStallDoor(t, rend);
                    if (di != null) doors.Add(di);
                }
                else if (t.name.StartsWith("WR_Stall_Latch")) latches.Add(t);
                else if (t.name.StartsWith("WR_Stall_Occupancy_Indicator")) marks.Add(t);
            }

            if (doors.Count == 0) return 0;

            var carried = new Dictionary<DoorInteractable, List<Transform>>();
            var markOf = new Dictionary<DoorInteractable, Transform>();
            foreach (DoorInteractable d in doors) carried[d] = new List<Transform>();

            Assign(doors, latches, carried, null);
            Assign(doors, marks, carried, markOf);

            int n = 0;
            foreach (DoorInteractable d in doors)
            {
                Transform dt = d.transform;
                var dr = dt.GetComponent<MeshRenderer>();
                if (dr == null) continue;
                Bounds db = dr.bounds;

                Undo.RecordObject(d, "Setup Signage");
                d.carried = carried[d].ToArray();
                EditorUtility.SetDirty(d);

                markOf.TryGetValue(d, out Transform mark);

                Vector3 pos;
                Vector3 outward;
                if (mark != null)
                {
                    var mr = mark.GetComponent<MeshRenderer>();
                    pos = mr != null ? mr.bounds.center : mark.position;
                    outward = FaceToward(db, pos);
                    if (mr != null && mr.enabled) { Undo.RecordObject(mr, "Setup Signage"); mr.enabled = false; }
                }
                else
                {
                    outward = Outward(db);
                    pos = db.center + outward * 0.02f;
                }

                ClearChild(dt, StallBezel);
                ClearChild(dt, StallFace);

                MakePlate(dt, StallBezel, pos + outward * 0.004f, outward, 0.094f, 0.052f, mesh, bezel);
                GameObject face = MakePlate(dt, StallFace, pos + outward * 0.007f, outward, 0.074f, 0.036f, mesh, occupied);

                var ind = Undo.AddComponent<DoorStateIndicator>(face);
                ind.door = d;
                ind.target = face.GetComponent<MeshRenderer>();
                ind.openMaterial = vacant;
                ind.closedMaterial = occupied;
                n++;
            }
            return n;
        }

        /// 부속을 가장 가까운 문에 붙인다.
        static void Assign(List<DoorInteractable> doors, List<Transform> parts,
                           Dictionary<DoorInteractable, List<Transform>> carried,
                           Dictionary<DoorInteractable, Transform> pick)
        {
            foreach (Transform part in parts)
            {
                DoorInteractable best = null;
                float bestSq = 1.6f * 1.6f;
                foreach (DoorInteractable d in doors)
                {
                    var dr = d.GetComponent<MeshRenderer>();
                    Vector3 c = dr != null ? dr.bounds.center : d.transform.position;
                    float sq = (c - part.position).sqrMagnitude;
                    if (sq < bestSq) { bestSq = sq; best = d; }
                }
                if (best == null) continue;
                if (!carried[best].Contains(part)) carried[best].Add(part);
                if (pick != null && !pick.ContainsKey(best)) pick[best] = part;
            }
        }

        static DoorInteractable HookStallDoor(Transform t, MeshRenderer rend)
        {
            if (t.GetComponent<Collider>() == null)
            {
                var mf = t.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    var bc = Undo.AddComponent<BoxCollider>(t.gameObject);
                    bc.center = mf.sharedMesh.bounds.center;
                    bc.size = mf.sharedMesh.bounds.size;
                }
            }

            Bounds b = rend.bounds;
            var d = Undo.AddComponent<DoorInteractable>(t.gameObject);
            d.hingeAnchor = t.InverseTransformPoint(new Vector3(b.max.x, b.center.y, b.min.z));
            d.hingeAxis = t.InverseTransformDirection(Vector3.up);
            d.openAngle = -70f;
            d.swingSpeed = 170f;
            d.labelKo = "변기칸 문";
            d.labelEn = "Toilet Stall";
            EditorUtility.SetDirty(t.gameObject);
            Debug.Log($"[Signage] '{t.name}' 에 여닫이 기능이 없어서 새로 붙였습니다.", t.gameObject);
            return d;
        }

        // ---------------------------------------------------------------- 판때기

        /// 1 × 1 사각형. 유니티 기본 Quad 와 같이 앞면이 -Z 를 본다.
        /// (글자가 바로 읽히려면 판의 앞쪽 축이 보는 사람 반대편을 향해야 한다)
        /// UV 가로 범위를 지정해 아틀라스 한 칸만 쓰게 한다.
        static Mesh PlateMesh(string name, float u0, float u1)
        {
            if (!AssetDatabase.IsValidFolder(MeshRoot))
                AssetDatabase.CreateFolder(DormRoot, "Meshes");

            string path = MeshRoot + "/" + name + ".mesh";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = m == null;
            if (created) m = new Mesh();

            m.Clear();
            m.name = name;
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f)
            };
            m.uv = new[]
            {
                new Vector2(u0, 0f), new Vector2(u1, 0f),
                new Vector2(u1, 1f), new Vector2(u0, 1f)
            };
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            m.RecalculateTangents();

            if (created) AssetDatabase.CreateAsset(m, path);
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject MakePlate(Transform parent, string name, Vector3 center, Vector3 outward,
                                    float width, float height, Mesh mesh, Material mat)
        {
            var q = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(q, "Setup Signage");

            q.transform.SetParent(parent, false);
            // 판의 앞쪽 축은 보는 사람 반대편(-outward)을 향해야 글자가 바로 읽힌다.
            q.transform.SetPositionAndRotation(center, Quaternion.LookRotation(-outward, Vector3.up));

            // 부모 배율이 1 이 아닐 수 있으므로 되돌려 준다.
            Vector3 lossy = parent != null ? parent.lossyScale : Vector3.one;
            float sx = Axis(q.transform.right, lossy);
            float sy = Axis(q.transform.up, lossy);
            q.transform.localScale = new Vector3(width / Mathf.Max(0.0001f, sx),
                                                 height / Mathf.Max(0.0001f, sy), 1f);

            q.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return q;
        }

        // ---------------------------------------------------------------- 도우미

        /// 납작한 판의 앞면 방향. 가장 얇은 축을 골라 통로(가운데) 쪽을 본다.
        static Vector3 Outward(Bounds b)
        {
            Vector3 s = b.size;
            int axis = 0;
            if (s.y < s[axis]) axis = 1;
            if (s.z < s[axis]) axis = 2;

            Vector3 dir = Vector3.zero;
            dir[axis] = 1f;
            float c = b.center[axis];
            if (c > 0.0001f) dir[axis] = -1f;
            else if (c < -0.0001f) dir[axis] = 1f;
            return dir;
        }

        /// 문판의 앞면 중, 지정한 점이 있는 쪽.
        static Vector3 FaceToward(Bounds b, Vector3 point)
        {
            Vector3 s = b.size;
            int axis = 0;
            if (s.y < s[axis]) axis = 1;
            if (s.z < s[axis]) axis = 2;

            Vector3 dir = Vector3.zero;
            float delta = point[axis] - b.center[axis];
            dir[axis] = delta >= 0f ? 1f : -1f;
            return dir;
        }

        static float Axis(Vector3 dir, Vector3 lossy)
            => Mathf.Abs(dir.x) * Mathf.Abs(lossy.x)
             + Mathf.Abs(dir.y) * Mathf.Abs(lossy.y)
             + Mathf.Abs(dir.z) * Mathf.Abs(lossy.z);

        static void ClearChild(Transform parent, string name)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform c = parent.GetChild(i);
                if (c.name == name) Undo.DestroyObjectImmediate(c.gameObject);
            }
        }

        static List<Transform> AllTransforms()
        {
            var list = new List<Transform>();
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return list;
            var stack = new Stack<Transform>();
            foreach (GameObject root in scene.GetRootGameObjects()) stack.Push(root.transform);
            while (stack.Count > 0)
            {
                Transform t = stack.Pop();
                list.Add(t);
                for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));
            }
            return list;
        }

        // ---------------------------------------------------------------- 재질·텍스처

        static Shader Lit()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            return s != null ? s : Shader.Find("Standard");
        }

        static Material EnsureMat(string name, System.Action<Material> setup)
        {
            string path = MatRoot + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Lit());
                AssetDatabase.CreateAsset(m, path);
            }
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Paint(Material m, Color c, float metallic, float smooth)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        }

        static void SetMap(Material m, Texture tex)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        }

        static void Emit(Material m, Color c)
        {
            if (!m.HasProperty("_EmissionColor")) return;
            m.SetColor("_EmissionColor", c);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        /// 앞뒤 어느 쪽에서 봐도 보이게. 방향을 잘못 잡아도 안 보이는 일이 없다.
        static void TwoSided(Material m)
        {
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            m.doubleSidedGI = true;
        }

        static void Cutout(Material m, float cutoff)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", cutoff);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = 2450;
        }

        static Texture2D Tex(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                bool change = false;
                if (imp.textureType != TextureImporterType.Default) { imp.textureType = TextureImporterType.Default; change = true; }
                if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; change = true; }
                if (imp.wrapMode != TextureWrapMode.Clamp) { imp.wrapMode = TextureWrapMode.Clamp; change = true; }
                if (imp.maxTextureSize < 2048) { imp.maxTextureSize = 2048; change = true; }
                if (imp.textureCompression != TextureImporterCompression.Uncompressed)
                { imp.textureCompression = TextureImporterCompression.Uncompressed; change = true; }
                if (change) imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
