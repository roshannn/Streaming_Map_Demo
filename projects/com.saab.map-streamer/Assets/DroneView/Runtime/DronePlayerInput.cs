using UnityEngine;

namespace StreamingMapDemo.Drones
{
    public sealed class DronePlayerInput : MonoBehaviour
    {
        [SerializeField] private DroneView controlledDrone;
        [SerializeField] private Camera movementCamera;
        [SerializeField, Min(0f)] private float movementSpeed = 8f;
        [SerializeField, Min(1f)] private float sprintMultiplier = 2f;
        [SerializeField, Min(0f)] private float verticalSpeed = 4f;
        [Header("Mouse Steering")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 2f;
        [Tooltip("Higher values follow the mouse more quickly; lower values feel smoother.")]
        [SerializeField, Min(0f)] private float mouseDamping = 14f;
        [SerializeField] private bool invertMouseY;
        [SerializeField] private Vector2 pitchLimits = new Vector2(-35f, 35f);
        [SerializeField] private bool lockCursor = true;

        private float yaw;
        private float pitch;
        private float targetYaw;
        private float targetPitch;
        private DroneCombatController combat;

        private void Awake()
        {
            if (controlledDrone == null) controlledDrone = GetComponent<DroneView>();
            combat = GetComponent<DroneCombatController>();
            if (movementCamera == null) movementCamera = Camera.main;
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = NormalizeAngle(angles.x);
            targetYaw = yaw;
            targetPitch = pitch;
            controlledDrone?.SetFaceMovement(false);
            if (lockCursor) SetCursorLocked(true);
        }

        private void Update()
        {
            if (controlledDrone == null)
            {
                return;
            }

            UpdateMouseRotation();
            if (movementCamera == null) movementCamera = Camera.main;
            Transform reference = movementCamera != null ? movementCamera.transform : transform;
            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;

            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            float up = 0f;
            if (Input.GetKey(KeyCode.Space)) up += 1f;
            if (Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl)) up -= 1f;

            float speed = movementSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);
            Vector3 planar = Vector3.ClampMagnitude(forward * vertical + right * horizontal, 1f) * speed;
            Vector3 command = planar + Vector3.up * (up * verticalSpeed);
            if (combat != null) combat.SetMovementInput(command);
            else controlledDrone.SetMovementCommand(command);
        }

        private void OnDisable()
        {
            controlledDrone?.StopMovement();
            combat?.SetMovementInput(Vector3.zero);
            controlledDrone?.SetFaceMovement(true);
            if (lockCursor) SetCursorLocked(false);
        }

        private void UpdateMouseRotation()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SetCursorLocked(false);
            }
            else if (lockCursor && Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            {
                SetCursorLocked(true);
            }

            if (!lockCursor || Cursor.lockState == CursorLockMode.Locked)
            {
                targetYaw += Input.GetAxisRaw("Mouse X") * mouseSensitivity;
                float yDirection = invertMouseY ? 1f : -1f;
                targetPitch += Input.GetAxisRaw("Mouse Y") * mouseSensitivity * yDirection;
                targetPitch = Mathf.Clamp(targetPitch, pitchLimits.x, pitchLimits.y);

                float damping = mouseDamping <= 0f
                    ? 1f
                    : 1f - Mathf.Exp(-mouseDamping * Time.deltaTime);
                yaw = Mathf.LerpAngle(yaw, targetYaw, damping);
                pitch = Mathf.Lerp(pitch, targetPitch, damping);
                controlledDrone.SetOrientation(Quaternion.Euler(pitch, yaw, 0f));
            }
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
