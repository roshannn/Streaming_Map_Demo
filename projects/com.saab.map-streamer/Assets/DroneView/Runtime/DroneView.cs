using UnityEngine;
using UnityEngine.UI;
using StreamingMapDemo.Pooling;

namespace StreamingMapDemo.Drones
{
    [ExecuteAlways]
    public sealed class DroneView : MonoBehaviour
    {
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        [SerializeField]
        private Color droneColor = new Color(0.18f, 0.55f, 0.9f, 1f);

        [SerializeField]
        private Renderer[] airframeRenderers = System.Array.Empty<Renderer>();

        [Header("View Parts")]
        [SerializeField] private Transform innerBody;
        [SerializeField] private Transform[] propellers = System.Array.Empty<Transform>();
        [SerializeField] private Transform turretPivot;
        [SerializeField] private Transform turretPoint;
        [SerializeField] private DroneBullet bulletPrefab;
        [SerializeField, Min(0)] private int initialBulletPoolSize = 12;
        [SerializeField] private Image healthFill;
        [SerializeField] private Transform healthCanvas;
        [SerializeField] private Canvas healthCanvasComponent;
        [SerializeField] private Camera healthBarCamera;
        [SerializeField] private Rigidbody droneBody;

        [Header("Motion Presentation")]
        [SerializeField, Min(0f)] private float turnSpeed = 360f;
        [SerializeField, Min(0f)] private float propellerSpeed = 1200f;
        [SerializeField, Range(0f, 45f)] private float maximumBankAngle = 18f;
        [SerializeField, Min(0f)] private float bankResponsiveness = 8f;

        private MaterialPropertyBlock propertyBlock;
        private Vector3 commandedVelocity;
        private ComponentPool<DroneBullet> bulletPool;
        private Transform bulletPoolRoot;
        private bool faceMovement = true;

        public Color DroneColor => droneColor;
        public Vector3 CommandedVelocity => commandedVelocity;
        public Transform TurretPivot => turretPivot;
        public Transform TurretPoint => turretPoint;

        public DroneBullet Shoot()
        {
            return turretPoint == null ? null : Shoot(turretPoint.forward);
        }

        public DroneBullet Shoot(Vector3 worldDirection)
        {
            if (turretPoint == null || bulletPrefab == null || worldDirection.sqrMagnitude < 0.0001f)
            {
                return null;
            }

            EnsureBulletPool();
            DroneBullet bullet = bulletPool.Get(turretPoint.position,
                Quaternion.LookRotation(worldDirection.normalized, Vector3.up));
            bullet.Launch(worldDirection, ReleaseBullet, GetComponentsInChildren<Collider>());
            return bullet;
        }

        public void SetColor(Color color)
        {
            droneColor = color;
            ApplyColor();
        }

        /// <summary>Sets a world-space velocity. Player, AI, and pathfinding controllers can all use this command.</summary>
        public void SetMovementCommand(Vector3 worldVelocity)
        {
            commandedVelocity = worldVelocity;
        }

        public void StopMovement()
        {
            commandedVelocity = Vector3.zero;
        }

        public void SetOrientation(Quaternion worldRotation)
        {
            if (droneBody != null && Application.isPlaying) droneBody.MoveRotation(worldRotation);
            else transform.rotation = worldRotation;
        }

        public void SetPresentedPosition(Vector3 localPosition)
        {
            if (droneBody != null && Application.isPlaying) droneBody.position = localPosition;
            else transform.position = localPosition;
        }

        public void SetFaceMovement(bool enabled)
        {
            faceMovement = enabled;
        }

        public void AimTurret(Vector3 worldDirection)
        {
            if (turretPivot == null || worldDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            turretPivot.rotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
        }

        public void AimTurret(Vector3 worldDirection, float degreesPerSecond, float deltaTime)
        {
            if (turretPivot == null || worldDirection.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
            turretPivot.rotation = Quaternion.RotateTowards(turretPivot.rotation, target,
                Mathf.Max(0f, degreesPerSecond) * Mathf.Max(0f, deltaTime));
        }

        public void SetHealth(float current, float maximum)
        {
            ResolveHealthReferences();
            if (healthFill == null)
            {
                return;
            }

            float normalized = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
            // Keep fillAmount as useful state, but resize the solid generated image directly.
            // A sprite-less Image does not render Filled mode consistently in Unity 2021.3.
            healthFill.fillAmount = normalized;
            healthFill.type = Image.Type.Simple;
            RectTransform fillRect = healthFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(normalized, 1f);
            healthFill.color = Color.Lerp(new Color(.9f, .08f, .04f, 1f), new Color(.16f, .9f, .22f, 1f), normalized);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            UpdateFacing(deltaTime);
            UpdateBank(deltaTime);
            RotatePropellers(deltaTime);
        }

        private void FixedUpdate()
        {
            if (droneBody != null)
            {
                // Translation is applied to CameraControl.GlobalPosition by
                // DroneMapMovementController. Keep the drone in the local
                // physics bubble so streaming and Unity coordinates agree.
                droneBody.velocity = Vector3.zero;
                droneBody.angularVelocity = Vector3.zero;
            }
        }

        private void LateUpdate()
        {
            ResolveHealthReferences();
            if (healthCanvas == null) return;
            if (healthBarCamera == null) healthBarCamera = Camera.main;
            if (healthBarCamera != null)
            {
                if (healthCanvasComponent != null && healthCanvasComponent.worldCamera != healthBarCamera)
                    healthCanvasComponent.worldCamera = healthBarCamera;
                // Match camera rotation so the world-space UI remains flat and readable.
                healthCanvas.rotation = healthBarCamera.transform.rotation;
            }
        }

        private void Awake()
        {
            ResolveHealthReferences();
            ApplyColor();
            if (Application.isPlaying) EnsureBulletPool();
        }

        private void OnDestroy()
        {
            bulletPool?.Dispose();
            bulletPool = null;
            if (bulletPoolRoot != null)
            {
                if (Application.isPlaying) Destroy(bulletPoolRoot.gameObject);
                else DestroyImmediate(bulletPoolRoot.gameObject);
            }
        }

        private void OnEnable()
        {
            ResolveHealthReferences();
            ApplyColor();
        }

        private void ResolveHealthReferences()
        {
            if (healthCanvasComponent == null)
            {
                Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
                foreach (Canvas candidate in canvases)
                {
                    if (candidate.name != "HealthCanvas") continue;
                    healthCanvasComponent = candidate;
                    break;
                }
            }
            if (healthCanvas == null && healthCanvasComponent != null)
                healthCanvas = healthCanvasComponent.transform;
            if (healthFill == null && healthCanvasComponent != null)
            {
                Image[] images = healthCanvasComponent.GetComponentsInChildren<Image>(true);
                foreach (Image candidate in images)
                {
                    if (candidate.name != "Fill") continue;
                    healthFill = candidate;
                    break;
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyColor();
        }
#endif

        private void ApplyColor()
        {
            if (airframeRenderers == null)
            {
                return;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            foreach (Renderer airframeRenderer in airframeRenderers)
            {
                if (airframeRenderer == null)
                {
                    continue;
                }

                airframeRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(ColorProperty, droneColor);
                airframeRenderer.SetPropertyBlock(propertyBlock);
                propertyBlock.Clear();
            }
        }

        private void UpdateFacing(float deltaTime)
        {
            if (!faceMovement)
            {
                return;
            }

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(commandedVelocity, Vector3.up);
            if (horizontalVelocity.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(horizontalVelocity.normalized, Vector3.up);
            Quaternion rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * deltaTime);
            if (droneBody != null) droneBody.MoveRotation(rotation);
            else transform.rotation = rotation;
        }

        private void UpdateBank(float deltaTime)
        {
            if (innerBody == null)
            {
                return;
            }

            Vector3 localVelocity = transform.InverseTransformDirection(commandedVelocity);
            float pitch = Mathf.Clamp(localVelocity.z, -1f, 1f) * maximumBankAngle;
            float roll = Mathf.Clamp(-localVelocity.x, -1f, 1f) * maximumBankAngle;
            Quaternion target = Quaternion.Euler(pitch, 0f, roll);
            innerBody.localRotation = Quaternion.Slerp(innerBody.localRotation, target,
                1f - Mathf.Exp(-bankResponsiveness * deltaTime));
        }

        private void RotatePropellers(float deltaTime)
        {
            if (propellers == null)
            {
                return;
            }

            for (int i = 0; i < propellers.Length; i++)
            {
                if (propellers[i] != null)
                {
                    float direction = i % 2 == 0 ? 1f : -1f;
                    propellers[i].Rotate(Vector3.up, propellerSpeed * direction * deltaTime, Space.Self);
                }
            }
        }

        private void EnsureBulletPool()
        {
            if (bulletPool != null || bulletPrefab == null)
            {
                return;
            }

            bulletPoolRoot = new GameObject($"{name} Bullet Pool").transform;
            bulletPool = new ComponentPool<DroneBullet>(bulletPrefab, initialBulletPoolSize, bulletPoolRoot);
        }

        private void ReleaseBullet(DroneBullet bullet)
        {
            bulletPool?.Release(bullet);
        }
    }
}
