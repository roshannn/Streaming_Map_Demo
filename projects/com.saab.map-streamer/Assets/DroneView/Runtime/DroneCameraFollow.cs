using Saab.Foundation.Unity.MapStreamer;
using UnityEngine;

namespace StreamingMapDemo.Drones
{
    public sealed class DroneCameraFollow : MonoBehaviour
    {
        [SerializeField] private DroneView target;
        [SerializeField] private Camera followCamera;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 3.5f, -7f);
        [Tooltip("Height above the drone placed at screen center. Larger values frame the drone lower on screen.")]
        [SerializeField] private float lookHeight = 2.2f;
        [SerializeField, Min(0f)] private float positionSharpness = 8f;
        [SerializeField, Min(0f)] private float rotationSharpness = 10f;

        private CameraControl mapCameraControl;

        private void Awake()
        {
            if (target == null) target = GetComponent<DroneView>();
            if (followCamera == null) followCamera = Camera.main;
            LockMapCameraInput(true);
        }

        private void LateUpdate()
        {
            if (target == null || followCamera == null)
            {
                return;
            }

            float positionT = 1f - Mathf.Exp(-positionSharpness * Time.deltaTime);
            float rotationT = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            Vector3 desiredPosition = target.transform.TransformPoint(localOffset);
            followCamera.transform.position = Vector3.Lerp(followCamera.transform.position, desiredPosition, positionT);

            Vector3 lookTarget = target.transform.position + Vector3.up * lookHeight;
            Vector3 lookDirection = lookTarget - followCamera.transform.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
                followCamera.transform.rotation = Quaternion.Slerp(followCamera.transform.rotation, desiredRotation, rotationT);
            }
        }

        private void OnEnable()
        {
            LockMapCameraInput(true);
        }

        private void OnDisable()
        {
            LockMapCameraInput(false);
        }

        private void LockMapCameraInput(bool locked)
        {
            if (followCamera == null) followCamera = Camera.main;
            if (followCamera == null) return;
            if (mapCameraControl == null) mapCameraControl = followCamera.GetComponent<CameraControl>();
            if (mapCameraControl != null) mapCameraControl.InputLocked = locked;
        }
    }
}
