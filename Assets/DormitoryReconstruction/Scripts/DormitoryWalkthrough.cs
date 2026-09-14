using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Anihome.Dormitory
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class DormitoryWalkthrough : MonoBehaviour
    {
        public Camera viewCamera;
        [Min(0.1f)] public float moveSpeed = 2.6f;
        [Min(0.1f)] public float sprintSpeed = 4.2f;
        [Min(0.001f)] public float mouseSensitivity = 0.1f;

        CharacterController body;
        float pitch;
        float verticalSpeed;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            if (!viewCamera) viewCamera = GetComponentInChildren<Camera>();
            if (viewCamera)
            {
                pitch = viewCamera.transform.localEulerAngles.x;
                if (pitch > 180f) pitch -= 360f;
            }
        }

        void Update()
        {
            if (!viewCamera || !body) return;

            bool capture = false;
            bool release = false;
            bool sprint = false;
            Vector2 move = Vector2.zero;
            Vector2 mouse = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            Mouse pointer = Mouse.current;
            if (pointer != null)
            {
                capture = pointer.leftButton.wasPressedThisFrame;
                mouse = pointer.delta.ReadValue();
            }
            if (keyboard != null)
            {
                release = keyboard.escapeKey.wasPressedThisFrame;
                sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                move.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                       - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                move.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                       - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            capture = Input.GetMouseButtonDown(0);
            release = Input.GetKeyDown(KeyCode.Escape);
            sprint = Input.GetKey(KeyCode.LeftShift);
            move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            mouse = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
#endif

            if (release) ReleasePointer();
            else if (capture)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                transform.Rotate(Vector3.up, mouse.x * mouseSensitivity, Space.World);
                pitch = Mathf.Clamp(pitch - mouse.y * mouseSensitivity, -85f, 85f);
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            if (body.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            Vector3 velocity = transform.right * move.x + transform.forward * move.y;
            velocity = Vector3.ClampMagnitude(velocity, 1f) * (sprint ? sprintSpeed : moveSpeed);
            velocity.y = verticalSpeed;
            body.Move(velocity * Time.deltaTime);
        }

        void OnDisable() => ReleasePointer();
        void OnApplicationFocus(bool focused) { if (!focused) ReleasePointer(); }

        static void ReleasePointer()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
