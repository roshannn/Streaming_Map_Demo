using Saab.Foundation.Unity.MapStreamer;

using StreamingMapDemo.Simulation;

using UnityEngine;

namespace StreamingMapDemo.Drones
{
    /// <summary>
    /// Bridges the authoritative global drone position to map streaming and
    /// adapts streamed Unity colliders to the simulation world-query port.
    /// </summary>
    public sealed class DroneMapBridge : MonoBehaviour
    {
        [SerializeField]
        private CameraControl mapCamera;

        [SerializeField]
        private LayerMask worldCollisionMask = Physics.DefaultRaycastLayers;

        private UnityWorldOrigin origin;
        private UnityWorldQuery worldQuery;
        private bool inputLockOwned;

        public IWorldQuery WorldQuery => worldQuery;
        public IWorldOrigin Origin => origin;
        public CameraControl MapCamera => mapCamera;

        public GlobalPosition Initialize()
        {
            if (origin != null)
                return origin.Origin;

            ResolveCamera();
            var initialPosition = mapCamera == null
                ? new GlobalPosition(0, transform.position.y, 0)
                : new GlobalPosition(mapCamera.X, mapCamera.Y, mapCamera.Z);

            origin = new UnityWorldOrigin(initialPosition);
            worldQuery = new UnityWorldQuery(origin, worldCollisionMask);

            if (mapCamera != null)
            {
                mapCamera.InputLocked = true;
                inputLockOwned = true;
            }

            return initialPosition;
        }

        public void FollowPlayer(GlobalPosition position)
        {
            if (origin == null)
                Initialize();

            origin.Origin = position;
            if (mapCamera == null)
                return;

            mapCamera.X = position.X;
            mapCamera.Y = position.Y;
            mapCamera.Z = position.Z;
        }

        public Vector3 ToLocal(GlobalPosition position)
        {
            if (origin == null)
                Initialize();
            return origin.ToLocal(position);
        }

        public void Shutdown()
        {
            if (inputLockOwned && mapCamera != null)
                mapCamera.InputLocked = false;

            inputLockOwned = false;
        }

        private void ResolveCamera()
        {
            if (mapCamera == null && Camera.main != null)
                mapCamera = Camera.main.GetComponent<CameraControl>();
        }

        private void OnDestroy() => Shutdown();
    }
}
