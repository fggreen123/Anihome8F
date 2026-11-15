using System.Collections.Generic;
using UnityEngine;

namespace Anihome.Dormitory
{
    [DisallowMultipleComponent]
    public sealed class FloorAnomalyController : MonoBehaviour
    {
        FloorAnomaly[] anomalies = System.Array.Empty<FloorAnomaly>();
        FloorAnomaly active;
        public int ActiveNumber => active ? active.Number : 0;

        public bool BeginFloor(Transform player, Transform spawnPoint, FloorLoopManager loop)
        {
            ResetFloor();
            anomalies = GetComponentsInChildren<FloorAnomaly>(true);
            var candidates = new List<FloorAnomaly>();
            foreach (var anomaly in anomalies)
                if (anomaly && anomaly.IsAvailable) candidates.Add(anomaly);

            var context = new AnomalyContext(player, spawnPoint, loop);
            while (candidates.Count > 0)
            {
                float totalWeight = 0f;
                foreach (var candidate in candidates) totalWeight += candidate.SelectionWeight;
                float roll = Random.value * totalWeight;
                int selected = candidates.Count - 1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    roll -= candidates[i].SelectionWeight;
                    if (roll < 0f) { selected = i; break; }
                }
                var choice = candidates[selected];
                candidates.RemoveAt(selected);
                try
                {
                    if (!choice.TryBeginFloor(context)) continue;
                    active = choice;
                    return true;
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception, choice);
                }
            }
            return false;
        }

        public void ResetFloor()
        {
            if (anomalies.Length == 0) anomalies = GetComponentsInChildren<FloorAnomaly>(true);
            foreach (var anomaly in anomalies)
            {
                if (!anomaly) continue;
                try { anomaly.ResetFloor(); }
                catch (System.Exception exception) { Debug.LogException(exception, anomaly); }
            }
            active = null;
        }

        void OnDisable() => ResetFloor();
    }
}
