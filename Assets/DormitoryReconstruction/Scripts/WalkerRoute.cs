using UnityEngine;

namespace Anihome.Dormitory
{
    /// <summary>
    /// NPC 가 걷는 길. 점을 순서대로 이은 꺾은선이다.
    /// 구간마다 계단인지 아닌지를 함께 들고 있어서,
    /// 걷는 쪽에서 계단에서는 비켜 가지 않고 기다리게 할 수 있다.
    ///
    /// 점은 이 오브젝트 기준 좌표로 저장한다.
    /// Tools ▸ Dormitory ▸ Setup Corridor Walker 가 계단 수치로 자동으로 채운다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WalkerRoute : MonoBehaviour
    {
        [Tooltip("경로 점 (이 오브젝트 기준)")]
        public Vector3[] points = new Vector3[0];
        [Tooltip("구간 i = 점 i → 점 i+1 이 계단인지")]
        public bool[] stairs = new bool[0];

        float[] cum;
        float total;

        public int Count => points != null ? points.Length : 0;

        public float Length
        {
            get { Build(); return total; }
        }

        void OnValidate() => cum = null;

        public void Rebuild() => cum = null;

        void Build()
        {
            int n = Count;
            if (cum != null && cum.Length == n) return;
            cum = new float[n];
            total = 0f;
            for (int i = 1; i < n; i++)
            {
                total += Vector3.Distance(points[i - 1], points[i]);
                cum[i] = total;
            }
        }

        public Vector3 Point(int i) => transform.TransformPoint(points[i]);

        public bool IsStairs(int segment)
            => stairs != null && segment >= 0 && segment < stairs.Length && stairs[segment];

        /// 첫 계단 구간이 출발점에서 몇 m 부터 몇 m 까지인지.
        public bool FirstStairs(out float start, out float end)
        {
            Build();
            for (int i = 0; i < Count - 1; i++)
            {
                if (!IsStairs(i)) continue;
                start = cum[i];
                end = cum[i + 1];
                return true;
            }
            start = end = 0f;
            return false;
        }

        /// 출발점에서 s 미터 지난 곳.
        public Vector3 Sample(float s, out int segment)
        {
            Build();
            int n = Count;
            segment = 0;
            if (n == 0) return transform.position;
            if (n == 1 || s <= 0f) return Point(0);
            if (s >= total) { segment = n - 2; return Point(n - 1); }

            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (cum[mid] <= s) lo = mid; else hi = mid;
            }
            segment = lo;
            float len = cum[lo + 1] - cum[lo];
            float t = len > 1e-5f ? (s - cum[lo]) / len : 0f;
            return Vector3.Lerp(Point(lo), Point(lo + 1), t);
        }

        void OnDrawGizmos()
        {
            int n = Count;
            if (n < 2) return;
            for (int i = 0; i < n - 1; i++)
            {
                Gizmos.color = IsStairs(i) ? new Color(1f, 0.55f, 0.15f) : new Color(0.35f, 0.85f, 1f);
                Gizmos.DrawLine(Point(i), Point(i + 1));
            }
            Gizmos.color = new Color(0.35f, 1f, 0.45f);
            Gizmos.DrawWireSphere(Point(0), 0.15f);
            Gizmos.color = new Color(1f, 0.3f, 0.3f);
            Gizmos.DrawWireSphere(Point(n - 1), 0.15f);
        }
    }
}
