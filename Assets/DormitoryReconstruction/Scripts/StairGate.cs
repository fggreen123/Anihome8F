using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Anihome.Dormitory
{
    /// 어느 쪽 계단인지.
    public enum StairSide
    {
        Microwave,   // 전자레인지 앞 계단 (근거리)
        Opposite     // 반대편 계단 (원거리)
    }

    /// <summary>
    /// 내려가는 계단 앞 확인 구역. 플레이어가 가까이 오면
    /// "내려가시겠습니까?" 를 띄우고, Y 를 누르면 층을 넘긴다.
    ///
    /// 물리 트리거 이벤트에 기대지 않고 매 프레임 거리로 판정한다.
    /// (CharacterController·레이어·Rigidbody 설정에 상관없이 동작하도록)
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class StairGate : MonoBehaviour
    {
        public StairSide side = StairSide.Opposite;
        public string label = "계단";
        [Tooltip("구역 바깥으로 이만큼까지는 가까이 온 것으로 친다 (m)")]
        [Min(0f)] public float reach = 0.35f;

        BoxCollider box;
        Transform player;
        bool inside;
        bool declined;
        bool warned;

        GUIStyle title, hint, shadow;
        Texture2D panel;
        Font uiFont;
        bool fontChecked;

        void Reset()
        {
            box = GetComponent<BoxCollider>();
            if (box) box.isTrigger = true;
        }

        void Awake()
        {
            box = GetComponent<BoxCollider>();
            if (box) box.isTrigger = true;
        }

        Transform Player()
        {
            if (player) return player;
            var mgr = FloorLoopManager.Instance;
            if (mgr && mgr.player) { player = mgr.player; return player; }
            var w = Object.FindFirstObjectByType<DormitoryWalkthrough>();
            if (w) player = w.transform;
            return player;
        }

        void Update()
        {
            Transform pl = Player();
            bool near = false;
            if (pl && box)
            {
                Vector3 p = pl.position;
                Vector3 c = box.ClosestPoint(p);
                near = (c - p).sqrMagnitude <= reach * reach;
            }

            if (near != inside)
            {
                inside = near;
                if (!inside) declined = false;
            }
            if (!inside || declined) return;

            var mgr2 = FloorLoopManager.Instance;
            if (mgr2 != null && (mgr2.Busy || mgr2.Cleared)) return;

            bool yes = false, no = false;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                yes = kb.yKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame
                      || kb.numpadEnterKey.wasPressedThisFrame;
                no = kb.nKey.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            yes = Input.GetKeyDown(KeyCode.Y) || Input.GetKeyDown(KeyCode.Return)
                  || Input.GetKeyDown(KeyCode.KeypadEnter);
            no = Input.GetKeyDown(KeyCode.N);
#endif
            if (no) { declined = true; return; }
            if (!yes) return;

            if (mgr2 == null)
            {
                if (!warned)
                {
                    warned = true;
                    Debug.LogError("[StairGate] FloorLoopManager 가 씬에 없습니다. " +
                                   "Tools ▸ Dormitory ▸ Setup Floor Loop 을 실행하세요.", this);
                }
                return;
            }
            inside = false;
            mgr2.RequestDescend(side);
        }

        void EnsureStyles()
        {
            if (!fontChecked)
            {
                fontChecked = true;
                try
                {
                    uiFont = Font.CreateDynamicFontFromOSFont(
                        new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans KR", "NanumGothic", "Gulim", "Dotum" }, 26);
                }
                catch { uiFont = null; }
            }
            if (title != null) return;

            title = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            hint = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            if (uiFont) { title.font = uiFont; hint.font = uiFont; }
            title.normal.textColor = new Color(1f, 1f, 1f, 0.97f);
            hint.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
            shadow = new GUIStyle(title);
            shadow.normal.textColor = new Color(0f, 0f, 0f, 0.72f);

            panel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            panel.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.64f));
            panel.Apply();
            panel.hideFlags = HideFlags.HideAndDontSave;
        }

        void OnGUI()
        {
            if (!inside || declined) return;
            var mgr = FloorLoopManager.Instance;
            if (mgr != null && (mgr.Busy || mgr.Cleared)) return;

            EnsureStyles();
            float w = 470f, h = 136f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.58f;

            GUI.DrawTexture(new Rect(x, y, w, h), panel);

            string q = uiFont ? "내려가시겠습니까?" : "Go down?";
            string k = uiFont ? "Y — 네        N — 아니오" : "Y - Yes        N - No";

            var r1 = new Rect(x, y + 22f, w, 34f);
            GUI.Label(new Rect(r1.x + 1f, r1.y + 1f, r1.width, r1.height), q, shadow);
            GUI.Label(r1, q, title);
            GUI.Label(new Rect(x, y + 66f, w, 28f), k, hint);
            GUI.Label(new Rect(x, y + 96f, w, 24f), uiFont ? label : side.ToString(), hint);
        }

        void OnDrawGizmosSelected()
        {
            var b = GetComponent<BoxCollider>();
            if (!b) return;
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(b.center, b.size);
        }
    }
}
