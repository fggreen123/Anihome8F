using UnityEngine;

namespace Anihome.Dormitory
{
    /// <summary>
    /// 한 장의 문짝을 경첩 축 기준으로 여닫는다.
    /// 경첩 축은 이 오브젝트의 로컬 좌표로 지정하므로, 부모를 새로 만들거나
    /// 프리팹 계층을 바꾸지 않아도 모델에 들어 있는 문짝에 그대로 붙일 수 있다.
    /// 걸쇠·잠김 표시처럼 문에 붙어 있어야 하는 부속은 carried 에 넣으면
    /// 같은 회전을 그대로 받는다 (계층을 바꾸지 않아도 된다).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorInteractable : MonoBehaviour
    {
        [Header("경첩 (닫힌 상태의 로컬 좌표)")]
        [Tooltip("경첩 축이 지나가는 점")]
        public Vector3 hingeAnchor = Vector3.zero;
        [Tooltip("경첩 축 방향. 보통 위쪽")]
        public Vector3 hingeAxis = Vector3.up;

        [Header("동작")]
        [Tooltip("열렸을 때의 각도(도). 부호가 회전 방향을 정한다")]
        public float openAngle = 90f;
        [Tooltip("문을 열 때의 최대 회전 속도 (도/초)")]
        [Min(10f)] public float swingSpeed = 190f;
        [SerializeField, Min(0.01f)] float openingSmoothTime = 0.16f;
        [SerializeField, Min(0.01f)] float closingSmoothTime = 0.19f;
        public bool startOpen = false;
        [Tooltip("잠겨 있으면 상호작용해도 열리지 않는다")]
        public bool locked = false;

        [Header("함께 움직일 부속")]
        [Tooltip("걸쇠·표시등처럼 문짝에 붙어 있어야 하는 오브젝트")]
        public Transform[] carried = new Transform[0];

        [Header("화면 안내 문구")]
        public string labelKo = "문";
        public string labelEn = "Door";
        [Tooltip("문 위에 원형 상호작용 안내를 표시합니다.")]
        public bool showDoorBadge;

        [Header("소리 (선택)")]
        public AudioSource audioSource;
        public AudioClip openClip;
        public AudioClip closeClip;

        Vector3 pivotWorld;
        Vector3 axisWorld;
        Vector3 closedPos;
        Quaternion closedRot;
        Vector3[] carriedPos;
        Quaternion[] carriedRot;
        float current;
        float target;
        float angularVelocity;
        bool ready;

        const float ClosingSpeedMultiplier = 0.8f;
        const float SettleAngle = 0.08f;
        const float SettleSpeed = 1f;

        public bool IsOpen => !Mathf.Approximately(target, 0f);
        public bool IsMoving => current != target || angularVelocity != 0f;

        void Awake()
        {
            CacheClosedPose();
            current = target = startOpen ? openAngle : 0f;
            Apply(current);

            if (!audioSource && (openClip || closeClip))
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
                audioSource.dopplerLevel = 0f;
                audioSource.minDistance = 1.5f;
                audioSource.maxDistance = 12f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
            }
        }

        void CacheClosedPose()
        {
            closedPos = transform.position;
            closedRot = transform.rotation;
            pivotWorld = transform.TransformPoint(hingeAnchor);
            axisWorld = transform.TransformDirection(hingeAxis);
            axisWorld = axisWorld.sqrMagnitude < 1e-8f ? Vector3.up : axisWorld.normalized;

            int n = carried != null ? carried.Length : 0;
            carriedPos = new Vector3[n];
            carriedRot = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                if (!carried[i]) continue;
                carriedPos[i] = carried[i].position;
                carriedRot[i] = carried[i].rotation;
            }
            ready = true;
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            if (!ready || !IsMoving || deltaTime <= 0f) return;

            float smoothTime = IsOpen ? openingSmoothTime : closingSmoothTime;
            float maxSpeed = Mathf.Max(10f, swingSpeed) * (IsOpen ? 1f : ClosingSpeedMultiplier);
            current = Mathf.SmoothDamp(current, target, ref angularVelocity, smoothTime, maxSpeed, deltaTime);

            if (Mathf.Abs(current - target) <= SettleAngle && Mathf.Abs(angularVelocity) <= SettleSpeed)
            {
                current = target;
                angularVelocity = 0f;
            }
            Apply(current);
        }

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (locked) return;
            float next = open ? openAngle : 0f;
            if (Mathf.Approximately(next, target)) return;
            // Keep the hinge momentum when the player changes direction mid-swing.
            target = next;
            if (audioSource)
            {
                AudioClip clip = open ? openClip : closeClip;
                if (clip)
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                }
            }
        }

        void Apply(float angle)
        {
            Quaternion q = Quaternion.AngleAxis(angle, axisWorld);
            transform.rotation = q * closedRot;
            transform.position = pivotWorld + q * (closedPos - pivotWorld);

            if (carried == null || carriedPos == null) return;
            int n = Mathf.Min(carried.Length, carriedPos.Length);
            for (int i = 0; i < n; i++)
            {
                Transform t = carried[i];
                if (!t) continue;
                t.SetPositionAndRotation(
                    pivotWorld + q * (carriedPos[i] - pivotWorld),
                    q * carriedRot[i]);
            }
        }

        void OnDrawGizmosSelected()
        {
            Vector3 p = Application.isPlaying && ready ? pivotWorld : transform.TransformPoint(hingeAnchor);
            Vector3 a = Application.isPlaying && ready ? axisWorld : transform.TransformDirection(hingeAxis).normalized;
            if (a.sqrMagnitude < 1e-8f) a = Vector3.up;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(p - a * 1.3f, p + a * 1.3f);
            Gizmos.DrawWireSphere(p, 0.05f);
        }
    }
}
