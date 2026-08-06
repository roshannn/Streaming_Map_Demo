using System.Collections.Generic;
using System.Linq;

using StreamingMapDemo.Pooling;
using StreamingMapDemo.Simulation;

using UnityEngine;

namespace StreamingMapDemo.Drones
{
    /// <summary>
    /// Owns all Unity presentation derived from simulation snapshots and events.
    /// It never advances or mutates authoritative combat state.
    /// </summary>
    public sealed class DroneCombatPresenter : MonoBehaviour
    {
        [SerializeField]
        private DroneView playerView;

        [SerializeField]
        private DroneView enemyViewPrefab;

        [SerializeField]
        private ProjectileView projectileViewPrefab;

        [SerializeField]
        private CombatHudPresenter hudPrefab;

        [SerializeField]
        private CombatVfxPresenter vfxPresenter;

        private readonly Dictionary<EntityId, DroneView> enemyViews =
            new Dictionary<EntityId, DroneView>();
        private readonly Dictionary<EntityId, ProjectileView> projectileViews =
            new Dictionary<EntityId, ProjectileView>();

        private ComponentPool<DroneView> enemyPool;
        private ComponentPool<ProjectileView> projectilePool;
        private Transform projectilePoolRoot;
        private CombatHudPresenter hud;
        private DroneCameraShake cameraShake;
        private bool initialized;

        public void Initialize()
        {
            if (initialized)
                return;

            if (playerView == null)
                playerView = GetComponent<DroneView>();

            if (enemyViewPrefab != null)
                enemyPool = new ComponentPool<DroneView>(
                    enemyViewPrefab,
                    3,
                    transform.parent);

            if (projectileViewPrefab != null)
            {
                projectilePoolRoot =
                    new GameObject("Projectile Presentation Pool").transform;
                projectilePool = new ComponentPool<ProjectileView>(
                    projectileViewPrefab,
                    24,
                    projectilePoolRoot);
            }

            if (hudPrefab != null)
                hud = Instantiate(hudPrefab);
            hud?.SetTargetLocked(false);

            if (Camera.main != null)
            {
                cameraShake = Camera.main.GetComponent<DroneCameraShake>();
                if (cameraShake == null)
                    cameraShake =
                        Camera.main.gameObject.AddComponent<DroneCameraShake>();
            }

            vfxPresenter?.Initialize();
            initialized = true;
        }

        public void Present(
            SimulationSnapshot snapshot,
            DroneMapBridge mapBridge)
        {
            Initialize();

            var player = snapshot.Drones.FirstOrDefault(
                drone => drone.Id == snapshot.Match.Player);
            PresentDrone(playerView, player, Vector3.zero);

            var expectedEnemies = new HashSet<EntityId>();
            foreach (var state in snapshot.Drones.Where(
                         drone => drone.Faction == DroneFaction.Enemy))
            {
                expectedEnemies.Add(state.Id);
                if (!enemyViews.TryGetValue(state.Id, out var view) &&
                    enemyPool != null)
                {
                    view = enemyPool.Get(
                        mapBridge.ToLocal(state.Position),
                        Quaternion.identity);
                    enemyViews.Add(state.Id, view);
                }

                PresentDrone(
                    view,
                    state,
                    mapBridge.ToLocal(state.Position));
            }

            foreach (var id in enemyViews.Keys
                         .Where(id => !expectedEnemies.Contains(id))
                         .ToArray())
            {
                enemyPool.Release(enemyViews[id]);
                enemyViews.Remove(id);
            }

            var expectedProjectiles = new HashSet<EntityId>();
            foreach (var state in snapshot.Projectiles)
            {
                expectedProjectiles.Add(state.Id);
                if (!projectileViews.TryGetValue(state.Id, out var view) &&
                    projectilePool != null)
                {
                    var direction = state.Direction.ToVector3();
                    view = projectilePool.Get(
                        mapBridge.ToLocal(state.Position),
                        Quaternion.LookRotation(direction));
                    view.transform.SetParent(null, true);
                    view.Prepare(
                        state.Faction == DroneFaction.Player
                            ? new Color(1f, .7f, .15f)
                            : Color.red,
                        direction);
                    projectileViews.Add(state.Id, view);
                }

                view?.PresentPosition(mapBridge.ToLocal(state.Position));
            }

            foreach (var id in projectileViews.Keys
                         .Where(id => !expectedProjectiles.Contains(id))
                         .ToArray())
            {
                projectilePool.Release(projectileViews[id]);
                projectileViews.Remove(id);
            }

            hud?.Present(snapshot.Match);
        }

        public void PresentEvents(
            IReadOnlyList<SimulationEvent> events,
            EntityId playerId,
            DroneMapBridge mapBridge)
        {
            for (var index = 0; index < events.Count; ++index)
            {
                var simulationEvent = events[index];
                vfxPresenter?.Present(simulationEvent, mapBridge.Origin);
                if (simulationEvent.Type == SimulationEventType.Damaged &&
                    simulationEvent.Entity == playerId)
                {
                    cameraShake?.Play();
                }
            }
        }

        public void SetTargetLocked(bool locked) =>
            hud?.SetTargetLocked(locked);

        public void ShowLockedTargetDestroyed() =>
            hud?.ShowTargetDestroyed();

        public void ResetPresentation()
        {
            hud?.ResetTargetStatus();
            ReleaseAll();
        }

        private static void PresentDrone(
            DroneView view,
            DroneState state,
            Vector3 position)
        {
            if (view == null)
                return;

            view.SetPresentedPosition(position);
            view.SetMovementCommand(state.Velocity.ToVector3());
            view.SetHealth(state.Health, state.MaximumHealth);
            view.AimTurret(state.AimDirection.ToVector3());

            if (state.Faction != DroneFaction.Enemy)
                return;

            var color = state.IsTelegraphing
                ? new Color(1f, .72f, .08f)
                : state.IsAttacking
                    ? new Color(1f, .08f, .03f)
                    : new Color(.85f, .16f, .12f);
            view.SetColor(color);
        }

        private void ReleaseAll()
        {
            foreach (var view in enemyViews.Values)
                enemyPool?.Release(view);
            enemyViews.Clear();

            foreach (var view in projectileViews.Values)
                projectilePool?.Release(view);
            projectileViews.Clear();
        }

        private void OnDestroy()
        {
            ReleaseAll();
            enemyPool?.Dispose();
            projectilePool?.Dispose();

            if (projectilePoolRoot != null)
                Destroy(projectilePoolRoot.gameObject);
            if (hud != null)
                Destroy(hud.gameObject);
        }
    }
}
