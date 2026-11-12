using System.Collections.Generic;
using UnityEngine;

namespace Anihome.Dormitory
{
    /// 어느 쪽 계단인지.
    public enum StairSide
    {
        Microwave,   // 전자레인지 앞 계단 (근거리)
        Opposite     // 반대편 계단 (원거리)
    }

    [RequireComponent(typeof(BoxCollider))]
    public sealed class StairGate : MonoBehaviour
    {
        public StairSide side = StairSide.Opposite;
        public string label = "계단";
        [Tooltip("구역 바깥으로 이만큼까지는 가까이 온 것으로 친다 (m)")]
        [Min(0f)] public float reach = 0.35f;

        static readonly List<StairGate> ActiveGates = new List<StairGate>();
        BoxCollider box;

        internal Vector3 BadgeAnchor
        {
            get
            {
                Vector3 offset = new Vector3(0f, box.size.y * 0.17f, box.size.z * 0.5f);
                return transform.TransformPoint(box.center + offset);
            }
        }

        void OnEnable()
        {
            box = GetComponent<BoxCollider>();
            ActiveGates.Add(this);
        }

        void OnDisable() => ActiveGates.Remove(this);

        internal static StairGate FindFocused(Transform player, Ray sight, RaycastHit hit, LayerMask layers)
        {
            var manager = FloorLoopManager.Instance;
            if (!manager || manager.player != player || manager.Busy || manager.Cleared)
                return null;

            if (hit.collider && hit.collider.TryGetComponent(out StairGate direct) && direct.CanFocus(player, sight.direction))
                return direct;

            // A raycast cannot hit the gate while the camera is already inside its trigger.
            foreach (var gate in ActiveGates)
            {
                if (!gate || gate.gameObject.scene != player.gameObject.scene
                    || (layers.value & (1 << gate.gameObject.layer)) == 0
                    || !gate.CanFocus(player, sight.direction))
                    continue;
                if (gate.box.ClosestPoint(sight.origin) == sight.origin)
                    return gate;
            }
            return null;
        }

        bool CanFocus(Transform player, Vector3 direction)
        {
            if (!isActiveAndEnabled || !box || !box.enabled || Vector3.Dot(direction, transform.forward) <= 0.45f)
                return false;
            Vector3 position = player.position;
            return (box.ClosestPoint(position) - position).sqrMagnitude <= reach * reach;
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
