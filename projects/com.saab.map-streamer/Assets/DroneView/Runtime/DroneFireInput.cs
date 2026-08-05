using UnityEngine;
using StreamingMapDemo.Simulation;

namespace StreamingMapDemo.Drones
{
    public sealed class DroneFireInput : MonoBehaviour
    {
        [SerializeField] private DroneView controlledDrone;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private bool autoFindDrone = true;
        [SerializeField, Min(0.01f)] private float aimDistance = 1000f;
        [SerializeField] private LayerMask aimMask = ~0;
        private DroneCombatController combat;
        [Header("Target Acquisition")]
        [SerializeField] private DroneTargetingView targetingView;
        [SerializeField, Min(1f)] private float acquisitionRadiusPixels = 90f;
        [SerializeField, Min(0f)] private float acquisitionTime = 1f;
        [SerializeField, Min(0f)] private float acquisitionGraceTime = .25f;
        [SerializeField, Min(0f)] private float acquisitionDecayMultiplier = 2f;
        [SerializeField, Min(1f)] private float retentionRadiusPixels = 180f;
        [SerializeField, Min(0f)] private float lossGraceTime = .5f;
        [SerializeField, Min(1f)] private float maximumTrackingRange = 100f;
        [SerializeField, Min(0f)] private float lockedTurretSpeed = 300f;
        [SerializeField, Range(0f, 30f)] private float firingAlignmentAngle = 5f;
        private EntityId candidateId = EntityId.None;
        private EntityId lockedId = EntityId.None;
        private float acquisitionProgress;
        private float lossTimer;
        private float acquisitionGraceTimer;

        public DroneView ControlledDrone => controlledDrone;

        public void SetControlledDrone(DroneView drone)
        {
            controlledDrone = drone;
        }

        public void SetAimCamera(Camera camera)
        {
            aimCamera = camera;
        }

        private void Start()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0)) FireAtScreenCenter();
        }

        private void LateUpdate()
        {
            UpdateTargetAcquisition();
            AimAtScreenCenter();
        }

        public bool AimAtScreenCenter()
        {
            ResolveReferences();
            if (controlledDrone == null || controlledDrone.TurretPoint == null || aimCamera == null)
            {
                return false;
            }

            Vector3 direction;
            if (TryGetLockedDirection(out Vector3 lockedDirection, out _))
            {
                direction = lockedDirection;
                controlledDrone.AimTurret(direction, lockedTurretSpeed, Time.unscaledDeltaTime);
            }
            else
            {
                direction = GetCrosshairDirection();
                controlledDrone.AimTurret(direction);
            }
            combat?.SetAimDirection(controlledDrone.TurretPoint.forward);
            return true;
        }

        public DroneBullet FireAtScreenCenter()
        {
            ResolveReferences();
            if (controlledDrone == null)
            {
                return null;
            }

            if (aimCamera == null)
            {
                return controlledDrone.Shoot();
            }

            Vector3 direction = GetCrosshairDirection();
            if (TryGetLockedDirection(out Vector3 lockedDirection, out float screenDistance) &&
                screenDistance <= retentionRadiusPixels)
            {
                if (Vector3.Angle(controlledDrone.TurretPoint.forward, lockedDirection) > firingAlignmentAngle)
                    return null;
                direction = controlledDrone.TurretPoint.forward;
            }
            if (combat != null) { combat.SetAimDirection(direction); combat.RequestFire(); return null; }
            return controlledDrone.Shoot(direction);
        }

        private Vector3 GetCrosshairDirection()
        {
            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 aimPoint = Physics.Raycast(ray, out RaycastHit hit, aimDistance, aimMask,
                QueryTriggerInteraction.Ignore)
                ? hit.point
                : ray.GetPoint(aimDistance);
            return (aimPoint - controlledDrone.TurretPoint.position).normalized;
        }

        private void ResolveReferences()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            if (controlledDrone == null && autoFindDrone)
            {
                controlledDrone = FindObjectOfType<DroneView>();
            }
            if (combat == null && controlledDrone != null) combat = controlledDrone.GetComponent<DroneCombatController>();
            if (targetingView == null) targetingView = GetComponent<DroneTargetingView>();
        }

        private void UpdateTargetAcquisition()
        {
            if (combat == null || aimCamera == null || combat.CurrentSnapshot == null)
            {
                combat?.SetTargetLocked(false);
                targetingView?.Clear();
                return;
            }

            Vector2 center = new Vector2(Screen.width * .5f, Screen.height * .5f);
            DroneState best = default;
            Vector2 bestScreen = default;
            float bestDistance = float.MaxValue;
            bool foundBest = false;
            foreach (DroneState state in combat.CurrentSnapshot.Drones)
            {
                if (state.Faction != DroneFaction.Enemy || !state.IsAlive) continue;
                Vector3 local = combat.ToLocal(state.Position);
                if ((local - aimCamera.transform.position).sqrMagnitude > maximumTrackingRange * maximumTrackingRange) continue;
                Vector3 screen = aimCamera.WorldToScreenPoint(local);
                if (screen.z <= 0f) continue;
                float distance = Vector2.Distance(center, screen);
                if (distance < bestDistance) { best = state; bestScreen = screen; bestDistance = distance; foundBest = true; }
            }

            if (lockedId != EntityId.None)
            {
                if (TryGetEnemy(lockedId, out DroneState locked, out Vector2 targetScreen))
                {
                    float distance = Vector2.Distance(center, targetScreen);
                    lossTimer = distance <= retentionRadiusPixels ? 0f : lossTimer + Time.unscaledDeltaTime;
                    if (lossTimer <= lossGraceTime)
                    {
                        combat.SetTargetLocked(true);
                        bool aligned = controlledDrone != null && controlledDrone.TurretPoint != null &&
                            Vector3.Angle(controlledDrone.TurretPoint.forward,
                                (combat.ToLocal(locked.Position) - controlledDrone.TurretPoint.position).normalized) <= firingAlignmentAngle;
                        targetingView?.PresentLock(targetScreen, targetScreen, false, aligned);
                        return;
                    }
                }
                else if (!ContainsEnemy(lockedId))
                {
                    combat.ShowLockedTargetDestroyed();
                }
                ClearLock();
            }

            combat.SetTargetLocked(false);

            if (!foundBest || bestDistance > acquisitionRadiusPixels)
            {
                acquisitionGraceTimer += Time.unscaledDeltaTime;
                if (acquisitionGraceTimer > acquisitionGraceTime)
                    acquisitionProgress = Mathf.Max(0f, acquisitionProgress - Time.unscaledDeltaTime * acquisitionDecayMultiplier);
                if (acquisitionProgress <= 0f) { candidateId = EntityId.None; targetingView?.Clear(); }
                return;
            }
            if (candidateId != best.Id) { candidateId = best.Id; acquisitionProgress = 0f; }
            acquisitionGraceTimer = 0f;
            acquisitionProgress += Time.unscaledDeltaTime;
            targetingView?.PresentAcquisition(bestScreen, acquisitionTime <= 0f ? 1f : acquisitionProgress / acquisitionTime);
            if (acquisitionProgress >= acquisitionTime)
            {
                lockedId = candidateId; candidateId = EntityId.None; acquisitionProgress = 0f; lossTimer = 0f;
                combat.SetTargetLocked(true);
            }
        }

        private bool TryGetEnemy(EntityId id, out DroneState found, out Vector2 screenPosition)
        {
            foreach (DroneState state in combat.CurrentSnapshot.Drones)
            {
                if (state.Id != id || state.Faction != DroneFaction.Enemy || !state.IsAlive) continue;
                Vector3 screen = aimCamera.WorldToScreenPoint(combat.ToLocal(state.Position));
                found = state; screenPosition = screen; return screen.z > 0f;
            }
            found = default; screenPosition = default; return false;
        }

        private bool ContainsEnemy(EntityId id)
        {
            if (combat == null || combat.CurrentSnapshot == null) return false;
            foreach (DroneState state in combat.CurrentSnapshot.Drones)
                if (state.Id == id && state.Faction == DroneFaction.Enemy && state.IsAlive) return true;
            return false;
        }

        private bool TryGetLockedDirection(out Vector3 direction, out float screenDistance)
        {
            direction = Vector3.zero; screenDistance = float.MaxValue;
            if (lockedId == EntityId.None || controlledDrone == null || controlledDrone.TurretPoint == null ||
                !TryGetEnemy(lockedId, out DroneState target, out Vector2 targetScreen)) return false;
            screenDistance = Vector2.Distance(new Vector2(Screen.width * .5f, Screen.height * .5f), targetScreen);
            direction = (combat.ToLocal(target.Position) - controlledDrone.TurretPoint.position).normalized;
            return direction.sqrMagnitude > .001f;
        }

        private void ClearLock()
        {
            candidateId = EntityId.None; lockedId = EntityId.None; acquisitionProgress = 0f;
            lossTimer = 0f; acquisitionGraceTimer = 0f; targetingView?.Clear();
            combat?.SetTargetLocked(false);
        }
    }
}
