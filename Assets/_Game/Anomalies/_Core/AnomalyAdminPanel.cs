using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Anihome.Dormitory
{
    [DisallowMultipleComponent]
    public sealed class AnomalyAdminPanel : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text floorLabel;
        [SerializeField] TMP_Text statusLabel;

        const string Code = "7188";
        InputAction digits;
        int nextDigit;
        DormitoryWalkthrough walker;
        DoorInteractor interactor;
        CursorLockMode savedCursorLock;
        bool savedCursorVisible;

        bool IsOpen => panel && panel.activeSelf;

        void Awake()
        {
            if (panel) panel.SetActive(false);
            digits = new InputAction("Admin code", InputActionType.PassThrough);
            for (int i = 0; i <= 9; i++)
            {
                digits.AddBinding("<Keyboard>/" + i);
                digits.AddBinding("<Keyboard>/numpad" + i);
            }
            digits.performed += OnDigit;
        }

        void OnEnable() => digits.Enable();

        void OnDisable()
        {
            digits?.Disable();
            nextDigit = 0;
            Close();
        }

        void OnDestroy() => digits?.Dispose();

        void OnDigit(InputAction.CallbackContext input)
        {
            if (input.ReadValue<float>() < 0.5f || !(input.control is KeyControl key)) return;

            // Bound controls are named "0" through "9" or "numpad0" through "numpad9".
            char digit = key.name[key.name.Length - 1];
            nextDigit = digit == Code[nextDigit] ? nextDigit + 1 : digit == Code[0] ? 1 : 0;
            if (nextDigit != Code.Length) return;

            nextDigit = 0;
            if (IsOpen) Close();
            else Open();
        }

        void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
        }

        void Open()
        {
            var loop = FloorLoopManager.Instance;
            if (!panel || !loop || loop.Busy) return;

            savedCursorLock = Cursor.lockState;
            savedCursorVisible = Cursor.visible;
            walker = loop.player ? loop.player.GetComponent<DormitoryWalkthrough>() : null;
            interactor = loop.player ? loop.player.GetComponent<DoorInteractor>() : null;
            if (walker && walker.enabled) walker.enabled = false;
            else walker = null;
            if (interactor && interactor.enabled) interactor.enabled = false;
            else interactor = null;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (floorLabel) floorLabel.text = $"현재 층  {loop.CurrentFloor}F";
            if (statusLabel) statusLabel.text = loop.ActiveAnomalyNumber > 0
                ? $"현재 층에 등장: {loop.ActiveAnomalyNumber:00}" : "";
            panel.SetActive(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            panel.SetActive(false);
            if (walker) walker.enabled = true;
            if (interactor) interactor.enabled = true;
            Cursor.lockState = savedCursorLock;
            Cursor.visible = savedCursorVisible;
        }

        public void ApplyAnomaly(int number)
        {
            var loop = FloorLoopManager.Instance;
            if (!IsOpen || !loop) return;

            bool applied = loop.TryApplyAnomaly(number);
            if (statusLabel) statusLabel.text = applied
                ? $"{loop.CurrentFloor}F에 적용됨: {number:00}" : "현재 층에 적용할 수 없습니다.";
        }
    }
}
