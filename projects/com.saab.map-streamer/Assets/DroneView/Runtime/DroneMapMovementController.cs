using Saab.Foundation.Unity.MapStreamer;
using UnityEngine;

namespace StreamingMapDemo.Drones
{
    /// <summary>
    /// Converts source-agnostic drone velocity commands into authoritative map movement.
    /// The drone remains in the local physics bubble while the streamed world advances.
    /// </summary>
    public sealed class DroneMapMovementController : MonoBehaviour
    {
        [SerializeField] private DroneView drone;
        [SerializeField] private CameraControl mapCamera;

        public CameraControl MapCamera => mapCamera;

        private void Awake()
        {
            ResolveReferences();
        }

        private void FixedUpdate()
        {
            ResolveReferences();
            if (drone == null || mapCamera == null)
            {
                return;
            }

            mapCamera.ApplyGlobalMovement(
                drone.CommandedVelocity * Time.fixedDeltaTime);
        }

        private void ResolveReferences()
        {
            if (drone == null) drone = GetComponent<DroneView>();
            if (mapCamera == null && Camera.main != null)
            {
                mapCamera = Camera.main.GetComponent<CameraControl>();
            }
        }
    }
}
