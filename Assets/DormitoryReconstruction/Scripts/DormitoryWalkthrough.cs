using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Anihome.Dormitory
{
    /// <summary>
    /// 1인칭 이동. WASD/화살표로 걷고, Shift 로 조금 빨리 걷는다.
    ///
    /// 공포 게임에 맞춘 걸음걸이 —
    /// 걸음은 느리고, 시점은 한 걸음에 한 번 아주 조금 내려앉는다.
    /// 좌우로 흔드는 양은 거의 없애서 뒤뚱거리지 않게 했고,
    /// 서 있을 때는 숨 쉬는 만큼만 천천히 오르내린다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class DormitoryWalkthrough : MonoBehaviour
    {
        public Camera viewCamera;
        [Min(0.1f)] public float moveSpeed = 1.55f;
        [Min(0.1f)] public float sprintSpeed = 2.95f;
        [Min(0.001f)] public float mouseSensitivity = 0.1f;
        [Tooltip("멈추고 출발할 때 붙었다 떨어지는 느낌. 클수록 즉각적")]
        [Min(0.5f)] public float acceleration = 9f;

        [Header("스태미나")]
        [SerializeField, Min(0.1f)] float sprintDuration = 2.5f;
        [SerializeField, Min(0.1f)] float staminaRecoveryDuration = 6f / 0.85f;

        [Header("걸음 흔들림 (공포 게임용 — 작게)")]
        public bool headBob = true;
        [Tooltip("1 m 를 걸을 때의 걸음 주기 수 (한 주기 = 두 걸음)")]
        [Min(0.05f)] public float stridesPerMeter = 0.70f;
        [Tooltip("한 걸음마다 내려앉는 깊이 (m)")]
        [Min(0f)] public float bobUp = 0.018f;
        [Tooltip("좌우로 밀리는 양 (m). 크면 뒤뚱거린다")]
        [Min(0f)] public float bobSide = 0.008f;
        [Tooltip("좌우로 기우는 각도 (도)")]
        [Min(0f)] public float bobRoll = 0.20f;
        [Tooltip("흔들림이 붙고 사라지는 속도")]
        [Min(0.1f)] public float bobSettle = 4.5f;
        [Tooltip("서 있을 때 숨 쉬는 폭 (m)")]
        [Min(0f)] public float breathe = 0.005f;
        [Tooltip("착지할 때 내려앉는 깊이 (m)")]
        [Min(0f)] public float landDip = 0.05f;

        CharacterController body;
        float pitch;
        float verticalSpeed;

        Vector3 camHome;
        bool camHomeCached;
        Vector3 planarVelocity;
        float phase;          // 걸음 위상 (한 주기 = 두 걸음)
        float breathPhase;
        float bobWeight;
        float dip;
        float dipVel;
        bool wasGrounded = true;
        float stamina = 1f;
        float staminaBarOpacity;
        float staminaBarShakeRemaining;
        bool sprintExhausted;
        const float SprintRecoveryThreshold = 0.5f;
        const float StandingRecoveryMultiplier = 1.25f;
        const float StaminaEpsilon = 0.00001f;
        const float StaminaBarFadeSeconds = 0.25f;
        const float StaminaBarShakeSeconds = 0.36f;
        const float StaminaBarShakePixels = 13f;

        /// 플레이어가 가려고 하는 속도 (벽이나 사람에 막혀도 그대로). NPC 를 밀 때 쓴다.
        public Vector3 DesiredVelocity => planarVelocity;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            if (!viewCamera) viewCamera = GetComponentInChildren<Camera>();
            if (viewCamera)
            {
                pitch = viewCamera.transform.localEulerAngles.x;
                if (pitch > 180f) pitch -= 360f;
                camHome = viewCamera.transform.localPosition;
                camHomeCached = true;
            }
        }

        /// 순간이동 등으로 시점을 원위치시킬 때.
        public void ResetView()
        {
            phase = 0f;
            bobWeight = 0f;
            dip = 0f;
            dipVel = 0f;
            pitch = 0f;
            planarVelocity = Vector3.zero;
            if (viewCamera && camHomeCached)
            {
                viewCamera.transform.localPosition = camHome;
                viewCamera.transform.localRotation = Quaternion.identity;
            }
        }

        void Update()
        {
            if (!viewCamera || !body) return;
            staminaBarShakeRemaining = Mathf.Max(0f, staminaBarShakeRemaining - Time.unscaledDeltaTime);
            if (!camHomeCached)
            {
                camHome = viewCamera.transform.localPosition;
                camHomeCached = true;
            }

            bool capture = false;
            bool release = false;
            bool sprint = false;
            bool sprintPressed = false;
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
                sprintPressed = keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame;
                move.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                       - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                move.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                       - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            capture = Input.GetMouseButtonDown(0);
            release = Input.GetKeyDown(KeyCode.Escape);
            sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            sprintPressed = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
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
            }

            if (sprintExhausted && sprintPressed)
                staminaBarShakeRemaining = StaminaBarShakeSeconds;

            // 목표 속도로 부드럽게 붙인다 — 뚝 끊기지 않게
            bool sprinting = sprint && move.sqrMagnitude > 0f && !sprintExhausted;
            Vector3 wanted = transform.right * move.x + transform.forward * move.y;
            wanted = Vector3.ClampMagnitude(wanted, 1f) * (sprinting ? sprintSpeed : moveSpeed);
            planarVelocity = Vector3.MoveTowards(planarVelocity, wanted,
                                                 acceleration * Time.deltaTime * Mathf.Max(1f, moveSpeed));

            if (body.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            float fallSpeed = verticalSpeed;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;

            Vector3 velocity = planarVelocity;
            velocity.y = verticalSpeed;

            Vector3 before = transform.position;
            body.Move(velocity * Time.deltaTime);
            Vector3 after = transform.position;

            bool groundedNow = body.isGrounded;
            if (groundedNow && !wasGrounded && fallSpeed < -2.5f)
                dip += Mathf.Min(landDip, landDip * (-fallSpeed / 7f));
            wasGrounded = groundedNow;

            float traveled = new Vector3(after.x - before.x, 0f, after.z - before.z).magnitude;
            bool moving = traveled > 0.0001f;
            UpdateStamina(sprinting && moving, groundedNow && !moving, Time.deltaTime);
            staminaBarOpacity = stamina < 1f
                ? 1f
                : Mathf.MoveTowards(staminaBarOpacity, 0f, Time.deltaTime / StaminaBarFadeSeconds);
            ApplyView(traveled, groundedNow);
        }

        void UpdateStamina(bool sprinting, bool standingStill, float deltaTime)
        {
            if (sprinting)
            {
                stamina = Mathf.Max(0f, stamina - deltaTime / Mathf.Max(0.1f, sprintDuration));
                if (stamina <= StaminaEpsilon)
                {
                    stamina = 0f;
                    sprintExhausted = true;
                    staminaBarShakeRemaining = StaminaBarShakeSeconds;
                    planarVelocity = Vector3.ClampMagnitude(planarVelocity, moveSpeed);
                }
                return;
            }

            float recoveryMultiplier = standingStill ? StandingRecoveryMultiplier : 1f;
            stamina = Mathf.Min(1f, stamina + deltaTime * recoveryMultiplier / Mathf.Max(0.1f, staminaRecoveryDuration));
            if (stamina >= 1f - StaminaEpsilon)
                stamina = 1f;
            if (sprintExhausted && stamina >= SprintRecoveryThreshold - StaminaEpsilon)
            {
                stamina = Mathf.Max(stamina, SprintRecoveryThreshold);
                sprintExhausted = false;
            }
        }

        float GetStaminaBarShakeOffset()
        {
            float remaining = staminaBarShakeRemaining / StaminaBarShakeSeconds;
            float phase = (1f - remaining) * 4f * Mathf.PI;
            return Mathf.Sin(phase) * remaining * remaining * StaminaBarShakePixels;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || staminaBarOpacity <= 0f || !viewCamera) return;

            Rect safe = Screen.safeArea;
            float scale = Mathf.Clamp(Mathf.Min(safe.width / 1280f, safe.height / 720f), 0.65f, 1.5f);
            float margin = 24f * scale;
            float width = 180f * scale;
            float height = 12f * scale;
            float inset = 2f * scale;
            float shakeOffset = GetStaminaBarShakeOffset() * scale;
            var bar = new Rect(safe.xMin + margin + shakeOffset, Screen.height - safe.yMin - margin - height, width, height);
            var track = new Rect(bar.x + inset, bar.y + inset, width - 2f * inset, height - 2f * inset);
            Color frame = sprintExhausted ? new Color(0.62f, 0.15f, 0.13f, 0.96f) : new Color(0.08f, 0.09f, 0.10f, 0.96f);
            Color empty = sprintExhausted ? new Color(0.24f, 0.045f, 0.04f, 0.95f) : new Color(0.18f, 0.19f, 0.20f, 0.95f);
            Color fill = sprintExhausted ? new Color(0.94f, 0.28f, 0.23f) : new Color(0.89f, 0.93f, 0.88f);
            frame.a *= staminaBarOpacity;
            empty.a *= staminaBarOpacity;
            fill.a *= staminaBarOpacity;

            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            GUI.depth = 10;
            GUI.color = Color.white;
            GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, frame, 0f, inset);
            GUI.DrawTexture(track, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, empty, 0f, scale);
            track.width *= stamina;
            if (track.width > 0f)
                GUI.DrawTexture(track, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, fill, 0f, Mathf.Min(scale, track.width * 0.5f));
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        void ApplyView(float traveled, bool grounded)
        {
            float roll = 0f;
            Vector3 offset = Vector3.zero;

            // 서 있을 때의 호흡 — 아주 느리게
            breathPhase += Time.deltaTime * 1.35f;
            if (breathPhase > Mathf.PI * 2f) breathPhase -= Mathf.PI * 2f;

            if (headBob)
            {
                // 실제로 움직인 거리로 위상을 민다. 속도가 변해도 보폭이 일정해진다.
                if (grounded && traveled > 0.0001f)
                {
                    phase += traveled * stridesPerMeter * Mathf.PI * 2f;
                    if (phase > Mathf.PI * 2f) phase -= Mathf.PI * 2f;
                }

                float speed = Time.deltaTime > 0f ? traveled / Time.deltaTime : 0f;
                float target = grounded ? Mathf.Clamp01(speed / Mathf.Max(0.1f, moveSpeed)) : 0f;
                bobWeight = Mathf.MoveTowards(bobWeight, target, bobSettle * Time.deltaTime);
                float w = Mathf.SmoothStep(0f, 1f, bobWeight);

                if (w > 0.001f)
                {
                    // 한 주기에 두 번 — 한 걸음마다 한 번 내려앉는다
                    float up = -Mathf.Abs(Mathf.Sin(phase)) * bobUp;
                    float side = Mathf.Sin(phase) * bobSide;
                    offset = new Vector3(side, up, 0f) * w;
                    roll = -Mathf.Sin(phase) * bobRoll * w;
                }

                offset.y += Mathf.Sin(breathPhase) * breathe * (1f - w);
            }
            else
            {
                offset.y += Mathf.Sin(breathPhase) * breathe;
            }

            if (dip > 0.0001f || Mathf.Abs(dipVel) > 0.0001f)
            {
                dipVel += (-dip * 90f - dipVel * 13f) * Time.deltaTime;
                dip += dipVel * Time.deltaTime;
                if (Mathf.Abs(dip) < 0.0005f && Mathf.Abs(dipVel) < 0.005f) { dip = 0f; dipVel = 0f; }
            }
            offset.y -= dip;

            viewCamera.transform.localPosition = camHome + offset;
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, roll);
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
