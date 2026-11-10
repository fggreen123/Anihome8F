using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Anihome.Dormitory
{
    /// <summary>
    /// 플레이어에 붙어서, 바라보는 문을 찾아 E 키로 여닫는다.
    /// 화면 가운데 조준점과 안내 문구를 함께 그린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorInteractor : MonoBehaviour
    {
        [Header("참조")]
        public Camera viewCamera;

        [Header("설정")]
        [Min(0.5f)] public float reach = 2.8f;
        public LayerMask layers = ~0;
        public bool drawCrosshair = true;
        public bool drawPrompt = true;

        DoorInteractable hovered;
        GUIStyle labelStyle;
        GUIStyle shadowStyle;
        Texture2D dot;
        Font uiFont;
        bool fontChecked;

        void Awake()
        {
            if (!viewCamera) viewCamera = GetComponentInChildren<Camera>();
            if (!viewCamera) viewCamera = Camera.main;
        }

        void Update()
        {
            hovered = null;
            if (!viewCamera) return;

            Transform eye = viewCamera.transform;
            if (Physics.Raycast(eye.position, eye.forward, out RaycastHit hit, reach, layers, QueryTriggerInteraction.Collide))
                hovered = hit.collider.GetComponentInParent<DoorInteractable>();

            if (hovered == null || hovered.locked) return;

            bool pressed = false;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null) pressed = keyboard.eKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            pressed = Input.GetKeyDown(KeyCode.E);
#endif
            if (pressed) hovered.Toggle();
        }

        void EnsureStyles()
        {
            if (!fontChecked)
            {
                fontChecked = true;
                // 윈도우에 설치된 한글 글꼴을 빌려 쓴다. 없으면 영문 안내로 넘어간다.
                try
                {
                    uiFont = Font.CreateDynamicFontFromOSFont(
                        new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans KR", "NanumGothic", "Gulim", "Dotum", "Arial Unicode MS" }, 20);
                }
                catch { uiFont = null; }
            }

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    alignment = TextAnchor.MiddleCenter,
                    richText = false
                };
                if (uiFont) labelStyle.font = uiFont;
                labelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.95f);

                shadowStyle = new GUIStyle(labelStyle);
                shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.65f);
            }

            if (dot == null)
            {
                dot = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                dot.SetPixel(0, 0, Color.white);
                dot.Apply();
                dot.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        void OnGUI()
        {
            if (!drawCrosshair && !drawPrompt) return;
            EnsureStyles();

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            if (drawCrosshair)
            {
                float s = hovered != null ? 7f : 4f;
                Color c = GUI.color;
                GUI.color = hovered != null ? new Color(1f, 1f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.45f);
                GUI.DrawTexture(new Rect(cx - s * 0.5f, cy - s * 0.5f, s, s), dot);
                GUI.color = c;
            }

            if (!drawPrompt || hovered == null) return;

            string text = BuildPrompt(hovered);
            var rect = new Rect(cx - 220f, cy + 34f, 440f, 30f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, shadowStyle);
            GUI.Label(rect, text, labelStyle);
        }

        string BuildPrompt(DoorInteractable door)
        {
            if (door.locked)
                return uiFont ? "잠겨 있음" : "Locked";

            if (uiFont)
                return "[E]  " + door.labelKo + "  " + (door.IsOpen ? "닫기" : "열기");

            return "[E]  " + (door.IsOpen ? "Close " : "Open ") + door.labelEn;
        }
    }
}
