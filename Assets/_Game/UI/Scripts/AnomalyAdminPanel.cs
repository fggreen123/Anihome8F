using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Anihome.Dormitory
{
    [RequireComponent(typeof(FloorLoopManager))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class AnomalyAdminPanel : MonoBehaviour
    {
        [SerializeField] TMP_FontAsset font;
        [SerializeField] AnomalyDefinition[] entries;

        const string AccessCode = "7188";
        const float RowHeight = 44f;
        static readonly Color Surface = new Color32(23, 25, 25, 255);
        static readonly Color Text = new Color32(235, 235, 225, 255);
        static readonly Color Muted = new Color32(156, 164, 158, 255);
        static readonly Color Accent = new Color32(213, 192, 137, 255);

        readonly List<Button> applyButtons = new List<Button>();
        FloorLoopManager loop;
        InputAction digits;
        int codeIndex;
        GameObject panel;
        TextMeshProUGUI floorLabel;
        TextMeshProUGUI status;
        int shownFloor;
        bool open;
        DormitoryWalkthrough walker;
        DoorInteractor interactor;
        bool wasWalkingEnabled;
        bool wasInteractionEnabled;
        CursorLockMode savedCursorLock;
        bool savedCursorVisible;

        void Awake()
        {
            loop = GetComponent<FloorLoopManager>();
            digits = new InputAction("Admin sequence", InputActionType.PassThrough, expectedControlType: "Button");
            for (int i = 0; i < 10; i++)
            {
                digits.AddBinding("<Keyboard>/" + i);
                digits.AddBinding("<Keyboard>/numpad" + i);
            }
            digits.performed += ReadDigit;
        }

        void OnEnable() => digits.Enable();

        void OnDisable()
        {
            digits?.Disable();
            codeIndex = 0;
            Close();
        }

        void OnDestroy() => digits?.Dispose();

        void OnApplicationFocus(bool focused)
        {
            if (!focused) codeIndex = 0;
        }

        void ReadDigit(InputAction.CallbackContext context)
        {
            if (context.ReadValue<float>() < 0.5f || !(context.control is KeyControl key)) return;
            var code = key.keyCode;
            int digit;
            if (code == Key.Digit0) digit = 0;
            else if (code >= Key.Digit1 && code <= Key.Digit9) digit = (int)code - (int)Key.Digit1 + 1;
            else if (code >= Key.Numpad0 && code <= Key.Numpad9) digit = (int)code - (int)Key.Numpad0;
            else return;

            char value = (char)('0' + digit);
            if (value != AccessCode[codeIndex])
            {
                codeIndex = value == AccessCode[0] ? 1 : 0;
                return;
            }
            if (++codeIndex != AccessCode.Length) return;
            codeIndex = 0;
            if (open) Close();
            else Open();
        }

        void Update()
        {
            if (!open) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }
            RefreshFloor();
        }

        void Open()
        {
            if (!font || entries == null || entries.Length == 0) return;
            if (!panel) BuildPanel();
            savedCursorLock = Cursor.lockState;
            savedCursorVisible = Cursor.visible;
            if (loop.player)
            {
                walker = loop.player.GetComponent<DormitoryWalkthrough>();
                interactor = loop.player.GetComponent<DoorInteractor>();
                wasWalkingEnabled = walker && walker.enabled;
                wasInteractionEnabled = interactor && interactor.enabled;
                if (walker) walker.enabled = false;
                if (interactor) interactor.enabled = false;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            open = true;
            shownFloor = int.MinValue;
            panel.SetActive(true);
            RefreshFloor();
        }

        void Close()
        {
            if (!open) return;
            open = false;
            panel.SetActive(false);
            if (walker) walker.enabled = wasWalkingEnabled;
            if (interactor) interactor.enabled = wasInteractionEnabled;
            Cursor.lockState = savedCursorLock;
            Cursor.visible = savedCursorVisible;
        }

        void RefreshFloor()
        {
            if (shownFloor != loop.CurrentFloor)
            {
                shownFloor = loop.CurrentFloor;
                floorLabel.text = $"현재 층  {shownFloor}F";
                var active = System.Array.Find(entries, entry => entry && entry.number == loop.ActiveAnomalyNumber);
                status.text = active ? $"현재 층: {active.number:00} {active.displayName}" : "";
            }
            bool canApply = !loop.Busy && !loop.Cleared && shownFloor > loop.goalFloor;
            foreach (var button in applyButtons)
                if (button.interactable != canApply) button.interactable = canApply;
        }

        void Apply(AnomalyDefinition entry)
        {
            status.text = loop.TryApplyAnomaly(entry.number)
                ? $"{loop.CurrentFloor}F에 적용됨: {entry.number:00} {entry.displayName}"
                : "현재 층에 적용할 수 없습니다.";
        }

        void BuildPanel()
        {
            panel = new GameObject("Anomaly Admin Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            panel.transform.SetParent(transform, false);
            panel.SetActive(false);
            var canvas = panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = panel.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960f, 600f);
            scaler.matchWidthOrHeight = 0.5f;

            if (!FindFirstObjectByType<EventSystem>())
            {
                var events = new GameObject("Admin Event System");
                events.transform.SetParent(panel.transform, false);
                events.AddComponent<EventSystem>();
                events.AddComponent<InputSystemUIInputModule>();
            }

            var root = (RectTransform)panel.transform;
            Fill(Area("Scrim", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                new Color32(5, 8, 7, 194), true);
            var window = Area("Admin Window", root, new Vector2(0.13f, 0.09f), new Vector2(0.87f, 0.91f),
                Vector2.zero, Vector2.zero);
            Fill(window, Surface);

            var title = Area("Title", window, new Vector2(0f, 1f), Vector2.one, new Vector2(20f, -58f), new Vector2(-216f, -12f));
            Label(title, "어드민 패널", 29f, TextAlignmentOptions.MidlineLeft, Text);
            var floor = Area("Current Floor", window, Vector2.one, Vector2.one, new Vector2(-202f, -53f), new Vector2(-62f, -14f));
            floorLabel = Label(floor, "", 16f, TextAlignmentOptions.MidlineRight, Accent);
            var close = Area("Close", window, Vector2.one, Vector2.one, new Vector2(-52f, -51f), new Vector2(-16f, -15f));
            MakeButton(close, "X", true).onClick.AddListener(Close);
            Fill(Area("Divider", window, new Vector2(0f, 1f), Vector2.one, new Vector2(20f, -64f), new Vector2(-20f, -63f)),
                new Color32(83, 79, 62, 255));
            var footer = Area("Status", window, Vector2.zero, Vector2.right, new Vector2(20f, 5f), new Vector2(-20f, 38f));
            status = Label(footer, "", 15f, TextAlignmentOptions.MidlineLeft, Accent);

            var scrollArea = Area("Anomaly List", window, Vector2.zero, Vector2.one,
                new Vector2(20f, 42f), new Vector2(-20f, -72f));
            var scroll = scrollArea.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 34f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = Area("Viewport", scrollArea, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-17f, 0f));
            Fill(viewport, new Color32(19, 21, 20, 255), true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Area("Rows", viewport, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, -entries.Length * RowHeight), Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = content;

            var track = Area("Scroll Track", scrollArea, Vector2.right, Vector2.one, new Vector2(-9f, 0f), Vector2.zero);
            Fill(track, new Color32(43, 46, 43, 255), true);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            var slide = Area("Sliding Area", track, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var thumb = Area("Scroll Thumb", slide, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var thumbImage = Fill(thumb, new Color32(123, 121, 103, 255), true);
            bar.handleRect = thumb;
            bar.targetGraphic = thumbImage;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            scroll.verticalNormalizedPosition = 1f;

            for (int i = 0; i < entries.Length; i++)
                if (entries[i]) BuildRow(content, entries[i], i);
        }

        void BuildRow(RectTransform content, AnomalyDefinition entry, int index)
        {
            var row = Area($"Anomaly {entry.number:00}", content, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, -(index + 1) * RowHeight), new Vector2(0f, -index * RowHeight - 1f));
            Fill(row, index % 2 == 0 ? new Color32(32, 35, 34, 255) : new Color32(28, 31, 29, 255));
            var number = Area("Number", row, Vector2.zero, Vector2.up, new Vector2(10f, 0f), new Vector2(40f, 0f));
            Label(number, entry.number.ToString("00"), 15f, TextAlignmentOptions.MidlineLeft, entry.implemented ? Accent : Muted);
            var name = Area("Name", row, Vector2.zero, Vector2.one, new Vector2(48f, 0f), new Vector2(-140f, 0f));
            Label(name, entry.displayName, 17f, TextAlignmentOptions.MidlineLeft, Text);
            var action = Area("Apply", row, Vector2.right, Vector2.one, new Vector2(-132f, 6f), new Vector2(-8f, -6f));
            var button = MakeButton(action, "해당 층에 등장", entry.implemented);
            if (!entry.implemented) return;
            applyButtons.Add(button);
            button.onClick.AddListener(() => Apply(entry));
        }

        Button MakeButton(RectTransform rect, string title, bool available)
        {
            var image = Fill(rect, Color.white, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color32(207, 187, 134, 255);
            colors.highlightedColor = new Color32(230, 210, 153, 255);
            colors.pressedColor = new Color32(177, 156, 103, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color32(49, 52, 49, 255);
            button.colors = colors;
            button.interactable = available;
            Label(Area("Label", rect, Vector2.zero, Vector2.one, new Vector2(3f, 0f), new Vector2(-3f, 0f)),
                title, 15f, TextAlignmentOptions.Center, available ? Surface : Muted);
            return button;
        }

        static RectTransform Area(string title, Transform parent, Vector2 min, Vector2 max, Vector2 from, Vector2 to)
        {
            var rect = (RectTransform)new GameObject(title, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = from;
            rect.offsetMax = to;
            return rect;
        }

        static Image Fill(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        TextMeshProUGUI Label(RectTransform rect, string value, float size, TextAlignmentOptions alignment, Color color)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = value;
            label.fontSize = size;
            label.fontSizeMin = 11f;
            label.fontSizeMax = size;
            label.enableAutoSizing = true;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }
    }
}
