using UnityEngine;

namespace Anihome.Dormitory
{
    public sealed class AnomalyContext
    {
        public Transform Player { get; }
        public Transform SpawnPoint { get; }
        public FloorLoopManager Loop { get; }
        public bool CanTrigger => Loop && !Loop.Busy && !Loop.Cleared;

        public AnomalyContext(Transform player, Transform spawnPoint, FloorLoopManager loop)
        {
            Player = player;
            SpawnPoint = spawnPoint;
            Loop = loop;
        }
    }

    public abstract class FloorAnomaly : MonoBehaviour
    {
        public AnomalyDefinition definition;
        public abstract int Number { get; }
        public float SelectionWeight => definition ? definition.selectionWeight : 1f;
        public bool IsAvailable => isActiveAndEnabled && Number > 0 &&
            (!definition || (definition.implemented && definition.number == Number)) && SelectionWeight > 0f;
        protected AnomalyContext Context { get; private set; }
        protected bool CanTrigger => Context != null && Context.CanTrigger;

        internal bool TryBeginFloor(AnomalyContext context)
        {
            ResetFloor();
            Context = context;
            try
            {
                if (OnBeginFloor(context)) return true;
                ResetFloor();
                return false;
            }
            catch
            {
                ResetFloor();
                throw;
            }
        }

        public void ResetFloor()
        {
            try { OnResetFloor(); }
            finally { Context = null; }
        }

        // Return false if required scene references are missing; this floor stays normal.
        protected abstract bool OnBeginFloor(AnomalyContext context);
        // Must also be safe before activation and when called more than once.
        protected abstract void OnResetFloor();
        protected virtual void OnDisable() => ResetFloor();
    }
}
