using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Anihome.Dormitory
{
    /// <summary>
    /// 플레이어가 바라보는 문과 내려가는 계단을 E 키로 조작한다.
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
        Component badgeTarget;
        Vector3 badgeLocalAnchor;
        float badgeOpacity;
        float badgePressRemaining;
        GUIStyle labelStyle;
        GUIStyle shadowStyle;
        GUIStyle keyStyle;
        GUIStyle actionStyle;
        Font uiFont;
        bool fontChecked;

        const float BadgeFadeInSeconds = 0.16f;
        const float BadgeFadeOutSeconds = 0.24f;
        const float BadgePressSeconds = 0.24f;
        static readonly Color BadgeRim = new Color(0.78f, 0.67f, 0.46f, 0.98f);
        static readonly Color BadgeSurface = new Color(0.07f, 0.08f, 0.08f, 0.97f);
        static readonly Color CaptionSurface = new Color(0.06f, 0.07f, 0.07f, 0.9f);

        void Awake()
        {
            if (!viewCamera) viewCamera = GetComponentInChildren<Camera>();
            if (!viewCamera) viewCamera = Camera.main;
        }

        void Update()
        {
            hovered = null;
            StairGate stair = null;
            RaycastHit hit = default;
            if (viewCamera)
            {
                Transform eye = viewCamera.transform;
                var sight = new Ray(eye.position, eye.forward);
                if (Physics.Raycast(sight, out hit, reach, layers, QueryTriggerInteraction.Collide))
                    hovered = hit.collider.GetComponentInParent<DoorInteractable>();
                if (!hovered)
                    stair = StairGate.FindFocused(transform, sight, hit, layers);
            }
            Component focused = stair;
            if (!focused && hovered && hovered.showDoorBadge)
                focused = hovered;
            UpdateBadge(focused, hit.collider);

            var manager = FloorLoopManager.Instance;
            if (manager && manager.Busy) return;
            if (!stair && (!hovered || hovered.locked)) return;

            bool pressed = false;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null) pressed = keyboard.eKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            pressed = Input.GetKeyDown(KeyCode.E);
#endif
            if (!pressed) return;

            if (stair)
            {
                bool animate = drawPrompt && badgeTarget == stair;
                if (manager.RequestDescend(stair.side, animate ? BadgePressSeconds : 0f) && animate)
                    badgePressRemaining = BadgePressSeconds;
                return;
            }

            bool wasOpen = hovered.IsOpen;
            hovered.Toggle();
            if (drawPrompt && badgeTarget == hovered && wasOpen != hovered.IsOpen)
                badgePressRemaining = BadgePressSeconds;
        }

        void UpdateBadge(Component target, Collider hitCollider)
        {
            badgePressRemaining = Mathf.Max(0f, badgePressRemaining - Time.unscaledDeltaTime);
            bool playingPress = badgeTarget && badgePressRemaining > 0f;
            bool focused = drawPrompt && target;
            if (focused && !playingPress && badgeTarget != target)
            {
                badgeTarget = target;
                Vector3 anchor;
                if (target is StairGate stair)
                    anchor = stair.BadgeAnchor;
                else
                {
                    Collider surface = target.TryGetComponent(out Collider doorCollider) ? doorCollider : hitCollider;
                    Bounds bounds = surface.bounds;
                    anchor = bounds.center + Vector3.up * (bounds.size.y * 0.1f);
                }
                badgeLocalAnchor = target.transform.InverseTransformPoint(anchor);
                badgeOpacity = 0f;
            }

            bool visible = drawPrompt && (focused || playingPress);
            float duration = visible ? BadgeFadeInSeconds : BadgeFadeOutSeconds;
            badgeOpacity = Mathf.MoveTowards(badgeOpacity, visible ? 1f : 0f, Time.unscaledDeltaTime / duration);
            if (badgeOpacity <= 0f) badgeTarget = null;
        }

        void OnDisable()
        {
            hovered = null;
            badgeTarget = null;
            badgeOpacity = 0f;
            badgePressRemaining = 0f;
        }

        void OnDestroy()
        {
            if (uiFont) Destroy(uiFont);
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
                keyStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold };
                keyStyle.normal.textColor = new Color(1f, 0.94f, 0.81f);
                actionStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold };
                actionStyle.normal.textColor = new Color(0.97f, 0.97f, 0.94f);
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || (!drawCrosshair && !drawPrompt)) return;
            EnsureStyles();

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            bool badgeVisible = drawPrompt && DrawBadge();

            if (drawCrosshair && !badgeVisible)
            {
                float s = hovered != null ? 7f : 4f;
                Color c = GUI.color;
                GUI.color = hovered != null ? new Color(1f, 1f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.45f);
                GUI.DrawTexture(new Rect(cx - s * 0.5f, cy - s * 0.5f, s, s), Texture2D.whiteTexture);
                GUI.color = c;
            }

            if (!drawPrompt || hovered == null || hovered.showDoorBadge || badgeVisible) return;

            string text = BuildPrompt(hovered);
            var rect = new Rect(cx - 220f, cy + 34f, 440f, 30f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, shadowStyle);
            GUI.Label(rect, text, labelStyle);
        }

        bool DrawBadge()
        {
            if (!viewCamera || !badgeTarget || badgeOpacity <= 0f) return false;

            Vector3 point = viewCamera.WorldToScreenPoint(badgeTarget.transform.TransformPoint(badgeLocalAnchor));
            if (point.z <= viewCamera.nearClipPlane) return false;

            float scale = Mathf.Clamp(Screen.height / 720f, 0.8f, 1.5f);
            scale *= BadgePressScale(BadgePressSeconds - badgePressRemaining);
            float radius = 24f * scale;
            float captionWidth = 102f * scale;
            float captionHeight = 27f * scale;
            float centerY = Screen.height - point.y;
            var circle = new Rect(point.x - radius, centerY - radius, radius * 2f, radius * 2f);
            var caption = new Rect(point.x - captionWidth * 0.5f, circle.yMax + 7f * scale, captionWidth, captionHeight);
            if (caption.xMax < 0f || caption.xMin > Screen.width || caption.yMax < 0f || circle.yMin > Screen.height)
                return false;

            Color previousColor = GUI.color;
            int previousDepth = GUI.depth;
            GUI.depth = 10;
            GUI.color = Color.white;
            var shadow = new Rect(circle.x - scale, circle.y + scale, circle.width + 2f * scale, circle.height + 2f * scale);
            DrawBadgeShape(shadow, new Color(0f, 0f, 0f, 0.48f), radius + scale);
            DrawBadgeShape(circle, BadgeRim, radius);
            float rim = 1.5f * scale;
            var inner = new Rect(circle.x + rim, circle.y + rim, circle.width - 2f * rim, circle.height - 2f * rim);
            DrawBadgeShape(inner, BadgeSurface, radius - rim);
            DrawBadgeShape(caption, CaptionSurface, 4f * scale);

            GUI.color = new Color(1f, 1f, 1f, badgeOpacity);
            keyStyle.fontSize = Mathf.RoundToInt(23f * scale);
            actionStyle.fontSize = Mathf.RoundToInt(16f * scale);
            var door = badgeTarget as DoorInteractable;
            GUI.Label(circle, door && door.locked ? "-" : "E", keyStyle);
            string action = uiFont ? "내려가기" : "Go down";
            if (door)
            {
                action = door.locked
                    ? (uiFont ? "잠겨 있음" : "Locked")
                    : (door.IsOpen ? (uiFont ? "문 닫기" : "Close door") : (uiFont ? "문 열기" : "Open door"));
            }
            GUI.Label(caption, action, actionStyle);
            GUI.depth = previousDepth;
            GUI.color = previousColor;
            return true;
        }

        static float BadgePressScale(float elapsed)
        {
            const float compressedAt = 0.055f;
            const float reboundAt = 0.15f;
            const float compressedScale = 0.74f;
            if (elapsed < compressedAt)
                return Mathf.SmoothStep(1f, compressedScale, elapsed / compressedAt);
            if (elapsed < reboundAt)
                return Mathf.SmoothStep(compressedScale, 1.06f, (elapsed - compressedAt) / (reboundAt - compressedAt));
            return Mathf.SmoothStep(1.06f, 1f, (elapsed - reboundAt) / (BadgePressSeconds - reboundAt));
        }

        void DrawBadgeShape(Rect rect, Color color, float radius)
        {
            color.a *= badgeOpacity;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
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
