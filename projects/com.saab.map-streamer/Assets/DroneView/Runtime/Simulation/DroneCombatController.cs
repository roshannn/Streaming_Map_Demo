using System.Collections.Generic;
using System.Linq;
using Saab.Foundation.Unity.MapStreamer;
using StreamingMapDemo.Pooling;
using StreamingMapDemo.Simulation;
using UnityEngine;

namespace StreamingMapDemo.Drones
{
    /// <summary>Offline authoritative host. Its API is deliberately command/snapshot based so transport can replace it later.</summary>
    public sealed class DroneCombatController : MonoBehaviour
    {
        [SerializeField] private DroneView playerView;
        [SerializeField] private DroneView enemyViewPrefab;
        [SerializeField] private ProjectileView projectileViewPrefab;
        [SerializeField] private CombatHudPresenter hudPrefab;
        [SerializeField] private CombatVfxPresenter vfxPresenter;
        [SerializeField] private CameraControl mapCamera;
        [SerializeField] private LayerMask worldCollisionMask = Physics.DefaultRaycastLayers;
        [SerializeField] private uint seed = 1337;

        private readonly Dictionary<EntityId, DroneView> enemyViews = new Dictionary<EntityId, DroneView>();
        private readonly Dictionary<EntityId, ProjectileView> projectileViews = new Dictionary<EntityId, ProjectileView>();
        private readonly List<SimulationEvent> eventBuffer = new List<SimulationEvent>();
        private ComponentPool<DroneView> enemyPool;
        private ComponentPool<ProjectileView> projectilePool;
        private Transform projectilePoolRoot;
        private UnityWorldOrigin origin;
        private IDroneSimulation simulation;
        private CombatHudPresenter hud;
        private DroneCameraShake cameraShake;
        private Vector3 movement, aim = Vector3.forward;
        private bool fireRequested;
        private float accumulator;
        private uint tick;
        private SimulationSnapshot currentSnapshot;

        public IDroneSimulation Simulation => simulation;
        public SimulationSnapshot CurrentSnapshot => currentSnapshot;
        public Vector3 ToLocal(GlobalPosition position) => origin != null ? origin.ToLocal(position) : Vector3.zero;
        public void SetMovementInput(Vector3 velocity) => movement = velocity;
        public void SetAimDirection(Vector3 direction) { if (direction.sqrMagnitude > 0.0001f) aim = direction.normalized; }
        public void RequestFire() => fireRequested = true;
        public void SetTargetLocked(bool locked) => hud?.SetTargetLocked(locked);
        public void ShowLockedTargetDestroyed() => hud?.ShowTargetDestroyed();

        private void Awake()
        {
            if (playerView == null) playerView = GetComponent<DroneView>();
            if (mapCamera == null && Camera.main != null) mapCamera = Camera.main.GetComponent<CameraControl>();
            GlobalPosition initial = mapCamera == null ? new GlobalPosition(0, transform.position.y, 0) :
                new GlobalPosition(mapCamera.X, mapCamera.Y, mapCamera.Z);
            origin = new UnityWorldOrigin(initial);
            simulation = new LocalDroneSimulation(new UnityWorldQuery(origin, worldCollisionMask), initial);
            if (enemyViewPrefab != null) enemyPool = new ComponentPool<DroneView>(enemyViewPrefab, 3, transform.parent);
            if (projectileViewPrefab != null)
            {
                projectilePoolRoot = new GameObject("Projectile Presentation Pool").transform;
                projectilePool = new ComponentPool<ProjectileView>(projectileViewPrefab, 24, projectilePoolRoot);
            }
            if (hudPrefab != null) hud = Instantiate(hudPrefab);
            if (hud != null) hud.SetTargetLocked(false);
            if (Camera.main != null)
            {
                cameraShake = Camera.main.GetComponent<DroneCameraShake>();
                if (cameraShake == null) cameraShake = Camera.main.gameObject.AddComponent<DroneCameraShake>();
            }
            if (vfxPresenter != null) vfxPresenter.Initialize();
            if (mapCamera != null) mapCamera.InputLocked = true;
            Present(simulation.CaptureSnapshot());
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R)) Restart();
            accumulator += Time.deltaTime;
            while (accumulator >= LocalDroneSimulation.TickDelta)
            {
                accumulator -= LocalDroneSimulation.TickDelta;
                tick++;
                simulation.Submit(new DroneCommand(simulation.PlayerId, tick, movement.ToFloat3(), aim.ToFloat3(), fireRequested));
                fireRequested = false;
                simulation.Step(tick);
                SimulationSnapshot snapshot = simulation.CaptureSnapshot();
                Present(snapshot);
                eventBuffer.Clear(); simulation.DrainEvents(eventBuffer);
                foreach (SimulationEvent e in eventBuffer)
                {
                    vfxPresenter?.Present(e, origin);
                    if (e.Type == SimulationEventType.Damaged && e.Entity == simulation.PlayerId)
                        cameraShake?.Play();
                }
            }
        }

        public void Restart()
        {
            tick = 0; accumulator = 0; fireRequested = false;
            hud?.ResetTargetStatus();
            ReleaseAll(); simulation.Reset(seed); Present(simulation.CaptureSnapshot());
        }

        private void Present(SimulationSnapshot snapshot)
        {
            currentSnapshot = snapshot;
            DroneState player = snapshot.Drones.FirstOrDefault(d => d.Id == snapshot.Match.Player);
            origin.Origin = player.Position;
            if (mapCamera != null) { mapCamera.X = player.Position.X; mapCamera.Y = player.Position.Y; mapCamera.Z = player.Position.Z; }
            PresentDrone(playerView, player, Vector3.zero);

            var expectedEnemies = new HashSet<EntityId>();
            foreach (DroneState state in snapshot.Drones.Where(d => d.Faction == DroneFaction.Enemy))
            {
                expectedEnemies.Add(state.Id);
                if (!enemyViews.TryGetValue(state.Id, out DroneView view) && enemyPool != null)
                { view = enemyPool.Get(origin.ToLocal(state.Position), Quaternion.identity); enemyViews.Add(state.Id, view); }
                PresentDrone(view, state, origin.ToLocal(state.Position));
            }
            foreach (EntityId id in enemyViews.Keys.Where(id => !expectedEnemies.Contains(id)).ToArray())
            { enemyPool.Release(enemyViews[id]); enemyViews.Remove(id); }

            var expectedProjectiles = new HashSet<EntityId>();
            foreach (ProjectileState state in snapshot.Projectiles)
            {
                expectedProjectiles.Add(state.Id);
                if (!projectileViews.TryGetValue(state.Id, out ProjectileView view) && projectilePool != null)
                {
                    Vector3 capturedDirection = state.Direction.ToVector3();
                    view = projectilePool.Get(origin.ToLocal(state.Position), Quaternion.LookRotation(capturedDirection));
                    // Active shots must not inherit later rotation from the drone or its presentation parent.
                    view.transform.SetParent(null, true);
                    view.Prepare(state.Faction == DroneFaction.Player ? new Color(1f,.7f,.15f) : Color.red, capturedDirection);
                    projectileViews.Add(state.Id, view);
                }
                // Always render from authoritative global state in the current origin frame.
                // This keeps the visible projectile aligned with entity and terrain collision.
                if (view != null) view.PresentPosition(origin.ToLocal(state.Position));
            }
            foreach (EntityId id in projectileViews.Keys.Where(id => !expectedProjectiles.Contains(id)).ToArray())
            { projectilePool.Release(projectileViews[id]); projectileViews.Remove(id); }
            hud?.Present(snapshot.Match);
        }

        private static void PresentDrone(DroneView view, DroneState state, Vector3 position)
        {
            if (view == null) return;
            view.SetPresentedPosition(position); view.SetMovementCommand(state.Velocity.ToVector3());
            view.SetHealth(state.Health, state.MaximumHealth); view.AimTurret(state.AimDirection.ToVector3());
            if (state.Faction == DroneFaction.Enemy)
            {
                Color color = state.IsTelegraphing ? new Color(1f,.72f,.08f) :
                    state.IsAttacking ? new Color(1f,.08f,.03f) : new Color(.85f,.16f,.12f);
                view.SetColor(color);
            }
        }

        private void ReleaseAll()
        {
            foreach (DroneView view in enemyViews.Values) enemyPool?.Release(view); enemyViews.Clear();
            foreach (ProjectileView view in projectileViews.Values) projectilePool?.Release(view); projectileViews.Clear();
        }
        private void OnDestroy()
        {
            ReleaseAll(); enemyPool?.Dispose(); projectilePool?.Dispose();
            if (projectilePoolRoot != null) Destroy(projectilePoolRoot.gameObject);
            if (mapCamera != null) mapCamera.InputLocked = false;
        }
    }
}
