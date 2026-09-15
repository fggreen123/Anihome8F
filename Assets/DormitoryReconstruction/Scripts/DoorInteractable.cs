using UnityEngine;

namespace Anihome.Dormitory
{
    /// <summary>
    /// 한 장의 문짝을 경첩 축 기준으로 여닫는다.
    /// 경첩 축은 이 오브젝트의 로컬 좌표로 지정하므로, 부모를 새로 만들거나
    /// 프리팹 계층을 바꾸지 않아도 모델에 들어 있는 문짝에 그대로 붙일 수 있다.
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
        [Min(10f)] public float swingSpeed = 190f;
        public bool startOpen = false;
        [Tooltip("잠겨 있으면 상호작용해도 열리지 않는다")]
        public bool locked = false;

        [Header("화면 안내 문구")]
        public string labelKo = "문";
        public string labelEn = "Door";

        [Header("소리 (선택)")]
        public AudioSource audioSource;
        public AudioClip openClip;
        public AudioClip closeClip;

        Vector3 pivotWorld;
        Vector3 axisWorld;
        Vector3 closedPos;
        Quaternion closedRot;
        float current;
        float target;
        bool ready;

        public bool IsOpen => !Mathf.Approximately(target, 0f);
        public bool IsMoving => !Mathf.Approximately(current, target);

        void Awake()
        {
            CacheClosedPose();
            current = target = startOpen ? openAngle : 0f;
            Apply(current);
        }

        void CacheClosedPose()
        {
            closedPos = transform.position;
            closedRot = transform.rotation;
            pivotWorld = transform.TransformPoint(hingeAnchor);
            axisWorld = transform.TransformDirection(hingeAxis);
            axisWorld = axisWorld.sqrMagnitude < 1e-8f ? Vector3.up : axisWorld.normalized;
            ready = true;
        }

        void Update()
        {
            if (!ready || Mathf.Approximately(current, target)) return;
            current = Mathf.MoveTowards(current, target, swingSpeed * Time.deltaTime);
            Apply(current);
        }

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (locked) return;
            float next = open ? openAngle : 0f;
            if (Mathf.Approximately(next, target)) return;
            target = next;
            if (audioSource)
            {
                AudioClip clip = open ? openClip : closeClip;
                if (clip) audioSource.PlayOneShot(clip);
            }
        }

        void Apply(float angle)
        {
            Quaternion q = Quaternion.AngleAxis(angle, axisWorld);
            transform.rotation = q * closedRot;
            transform.position = pivotWorld + q * (closedPos - pivotWorld);
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
