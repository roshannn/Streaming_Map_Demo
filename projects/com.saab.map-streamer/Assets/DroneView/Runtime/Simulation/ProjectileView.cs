using UnityEngine;

namespace StreamingMapDemo.Drones
{
    public sealed class ProjectileView : MonoBehaviour
    {
        [SerializeField] private TrailRenderer trail;
        private Vector3 cachedDirection = Vector3.forward;

        public Vector3 CachedDirection => cachedDirection;

        public void Prepare(Color color, Vector3 worldDirection)
        {
            if (worldDirection.sqrMagnitude > 0.0001f)
                cachedDirection = worldDirection.normalized;
            transform.rotation = Quaternion.LookRotation(cachedDirection, Vector3.up);
            if (trail == null) trail = GetComponent<TrailRenderer>();
            if (trail != null) { trail.Clear(); trail.startColor = color; trail.endColor = new Color(color.r, color.g, color.b, 0f); }
        }

        public void PresentPosition(Vector3 worldPosition)
        {
            transform.position = worldPosition;
        }
        private void OnDisable() { if (trail != null) trail.Clear(); }
    }
}
