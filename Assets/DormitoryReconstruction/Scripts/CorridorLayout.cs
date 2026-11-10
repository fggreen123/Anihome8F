using UnityEngine;

namespace Anihome.Dormitory
{
    [DisallowMultipleComponent]
    public sealed class CorridorLayout : MonoBehaviour
    {
        [Tooltip("Player start position belonging to this corridor prefab.")]
        public Transform spawnPoint;

        public bool HasValidSpawn => spawnPoint && spawnPoint.IsChildOf(transform);

        public FloorSign[] CollectSigns() => GetComponentsInChildren<FloorSign>(true);

        public void RestartWalkers(Transform player)
        {
            foreach (var walker in GetComponentsInChildren<CorridorWalker>())
            {
                walker.player = player;
                walker.Restart();
            }
        }

        public void HideWalkers()
        {
            foreach (var walker in GetComponentsInChildren<CorridorWalker>())
                walker.Hide();
        }
    }
}
