using UnityEngine;

namespace Anihome.Dormitory
{
    /// 계단실 층수 표지판. FloorLoopManager 가 층이 바뀔 때 재질을 갈아 끼운다.
    [DisallowMultipleComponent]
    public sealed class FloorSign : MonoBehaviour
    {
        public Renderer target;

        void Reset() => target = GetComponent<Renderer>();

        public void SetMaterial(Material m)
        {
            if (!target) target = GetComponent<Renderer>();
            if (target && m) target.sharedMaterial = m;
        }
    }
}
