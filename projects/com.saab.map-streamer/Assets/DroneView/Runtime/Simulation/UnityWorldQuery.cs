using StreamingMapDemo.Simulation;
using UnityEngine;

namespace StreamingMapDemo.Drones
{
    public sealed class UnityWorldQuery : IWorldQuery
    {
        private readonly IWorldOrigin origin;
        private readonly LayerMask mask;
        public UnityWorldQuery(IWorldOrigin origin, LayerMask mask) { this.origin = origin; this.mask = mask; }

        public bool TryGetTerrainHeight(GlobalPosition position, out double height)
        {
            Vector3 local = origin.ToLocal(position);
            if (Physics.Raycast(local + Vector3.up * 1000f, Vector3.down, out RaycastHit hit, 2000f, mask, QueryTriggerInteraction.Ignore))
            {
                height = origin.ToGlobal(hit.point).Y;
                return true;
            }
            // Streaming can legitimately have no collider yet. The director retries next tick.
            height = 0;
            return false;
        }

        public bool HasLineOfSight(GlobalPosition from, GlobalPosition to)
        {
            Vector3 a = origin.ToLocal(from), b = origin.ToLocal(to);
            return !Physics.Linecast(a, b, mask, QueryTriggerInteraction.Ignore);
        }

        public bool SweepProjectile(GlobalPosition from, GlobalPosition to, float radius, out WorldHit worldHit)
        {
            Vector3 a = origin.ToLocal(from), b = origin.ToLocal(to), delta = b - a;
            if (delta.sqrMagnitude > 0f && Physics.SphereCast(a, radius, delta.normalized, out RaycastHit hit,
                    delta.magnitude, mask, QueryTriggerInteraction.Ignore))
            {
                worldHit = new WorldHit(origin.ToGlobal(hit.point), hit.normal.ToFloat3());
                return true;
            }
            worldHit = default;
            return false;
        }
    }
}
