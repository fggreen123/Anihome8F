using UnityEngine;

namespace Anihome.Dormitory
{
    /// <summary>
    /// 변기칸 문에 달린 잠김/열림 표시.
    /// 문이 닫혀 있으면 빨강(사용 중), 열려 있으면 초록(비어 있음)으로 바뀐다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorStateIndicator : MonoBehaviour
    {
        public DoorInteractable door;
        public Renderer target;
        [Tooltip("문이 열렸을 때 (비어 있음)")]
        public Material openMaterial;
        [Tooltip("문이 닫혔을 때 (사용 중)")]
        public Material closedMaterial;

        int last = -1;

        void Reset()
        {
            target = GetComponent<Renderer>();
            door = GetComponentInParent<DoorInteractable>();
        }

        void OnEnable()
        {
            last = -1;
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            if (!door) door = GetComponentInParent<DoorInteractable>();
            if (!target) target = GetComponent<Renderer>();
            if (!door || !target) return;

            int now = door.IsOpen ? 1 : 0;
            if (now == last) return;
            last = now;

            Material m = now == 1 ? openMaterial : closedMaterial;
            if (m) target.sharedMaterial = m;
        }
    }
}
