using System.Collections.Generic;
using UnityEngine;

namespace Anihome.Dormitory
{
    /// <summary>
    /// 층이 바뀌면 숫자가 따라 바뀌는 번호판.
    ///
    /// 아틀라스 한 장에 1층부터 8층까지 세로로 쌓여 있고,
    /// 가로 칸(몇 호실인지)은 판때기 메시의 UV 에 이미 박혀 있다.
    /// 그래서 여기서는 세로 위치만 옮기면 되고, 그 값은
    /// 같은 재질을 쓰는 모든 번호판이 함께 쓴다.
    /// (렌더러마다 값을 따로 주는 방식은 URP 일괄 처리와 충돌해서 쓰지 않는다.)
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class FloorNumberDisplay : MonoBehaviour
    {
        [Tooltip("아틀라스 세로 칸 수 = 표현할 수 있는 층 수")]
        [Min(1)] public int rows = 8;
        public Renderer target;

        static readonly List<FloorNumberDisplay> All = new List<FloorNumberDisplay>();
        static int floorNow = 8;

        public static int Current => floorNow;

        /// 모든 번호판을 이 층으로 맞춘다.
        public static void SetFloor(int floor)
        {
            floorNow = floor;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                if (All[i] == null) { All.RemoveAt(i); continue; }
                All[i].Apply();
            }
        }

        void Reset() => target = GetComponent<Renderer>();

        void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            var mgr = FloorLoopManager.Instance;
            if (mgr != null && mgr.CurrentFloor > 0) floorNow = mgr.CurrentFloor;
            Apply();
        }

        void OnDisable() => All.Remove(this);

        public void Apply()
        {
            if (!target) target = GetComponent<Renderer>();
            if (!target) return;

            Material m = target.sharedMaterial;
            if (!m) return;

            int r = Mathf.Max(1, rows);
            int f = Mathf.Clamp(floorNow, 1, r);
            var scale = new Vector2(1f, 1f / r);
            var offset = new Vector2(0f, (f - 1) / (float)r);

            if (m.HasProperty("_BaseMap"))
            {
                m.SetTextureScale("_BaseMap", scale);
                m.SetTextureOffset("_BaseMap", offset);
            }
            if (m.HasProperty("_MainTex"))
            {
                m.SetTextureScale("_MainTex", scale);
                m.SetTextureOffset("_MainTex", offset);
            }
        }
    }
}
