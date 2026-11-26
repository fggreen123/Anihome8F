using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Anihome.Dormitory
{
    sealed class MonsterChasePath
    {
        const float RepathInterval = 0.18f;
        NavMeshPath path;
        NavMeshData data;
        NavMeshDataInstance instance;
        NavMeshQueryFilter filter;
        Vector3[] corners = System.Array.Empty<Vector3>();
        int nextCorner;
        float nextRepath;

        public bool Build(Transform player, Vector3 corridorCenter, float floorHeight,
            DoorInteractable obstructingDoor = null)
        {
            Release();
            if (NavMesh.GetSettingsCount() == 0) return false;
            path ??= new NavMeshPath();

            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = 0.20f;
            settings.agentHeight = 1.62f;
            settings.agentClimb = 0.22f;
            settings.minRegionArea = 0.3f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.065f;
            filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = NavMesh.AllAreas };

            corridorCenter.y = floorHeight + 0.75f;
            var bounds = new Bounds(corridorCenter, new Vector3(30f, 3.5f, 32f));
            var sources = new List<NavMeshBuildSource>();
            // Doors that the monster can open stay traversable in the planned path.
            NavMeshBuilder.CollectSources(bounds, Physics.DefaultRaycastLayers, NavMeshCollectGeometry.PhysicsColliders,
                0, new List<NavMeshBuildMarkup>(), sources);
            sources.RemoveAll(source =>
            {
                var component = source.component;
                if (!component || component.transform.IsChildOf(player)) return true;
                if (component is Collider collider && (!collider.enabled || collider.isTrigger)) return true;
                var door = component.GetComponentInParent<DoorInteractable>();
                return door && door != obstructingDoor && door.isActiveAndEnabled && !door.locked;
            });
            // A player build cannot rasterize imported meshes whose CPU data was stripped.
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source.sourceObject is not Mesh mesh || mesh.isReadable ||
                    source.component is not Collider collider) continue;
                source.shape = NavMeshBuildSourceShape.Box;
                source.sourceObject = null;
                source.transform = Matrix4x4.Translate(collider.bounds.center);
                source.size = collider.bounds.size;
                sources[i] = source;
            }
            data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (!data) return false;
            instance = NavMesh.AddNavMeshData(data);
            return instance.valid;
        }

        public Vector3 NextPoint(Vector3 position, Vector3 destination, bool requireCompletePath = false)
        {
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + RepathInterval;
                nextCorner = 1;
                corners = System.Array.Empty<Vector3>();
                if (instance.valid && NavMesh.SamplePosition(position, out var start, 0.65f, filter) &&
                    NavMesh.SamplePosition(destination, out var goal, 0.7f, filter) &&
                    NavMesh.CalculatePath(start.position, goal.position, filter, path) &&
                    (!requireCompletePath || path.status == NavMeshPathStatus.PathComplete))
                    corners = path.corners;
            }

            while (nextCorner < corners.Length && (corners[nextCorner] - position).sqrMagnitude < 0.007f)
                nextCorner++;
            return nextCorner < corners.Length ? corners[nextCorner] : position;
        }

        public void Release()
        {
            if (instance.valid) instance.Remove();
            if (data) Object.Destroy(data);
            data = null;
            corners = System.Array.Empty<Vector3>();
            nextRepath = 0f;
        }
    }
}
