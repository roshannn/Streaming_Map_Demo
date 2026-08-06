using System;

using StreamingMapDemo.Simulation;

using UnityEngine;

namespace StreamingMapDemo.Drones
{
    /// <summary>
    /// Thin Unity entry point that coordinates input, the fixed-tick simulation,
    /// the streamed-map bridge, and combat presentation.
    /// </summary>
    [RequireComponent(typeof(DroneMapBridge), typeof(DroneCombatPresenter))]
    public sealed class DroneCombatController : MonoBehaviour
    {
        [SerializeField]
        private DroneMapBridge mapBridge;

        [SerializeField]
        private DroneCombatPresenter presenter;

        [SerializeField]
        private uint seed = 1337;

        private DroneSimulationRunner runner;
        private Vector3 movement;
        private Vector3 aim = Vector3.forward;
        private bool fireRequested;

        public IDroneSimulation Simulation => runner?.Simulation;
        public SimulationSnapshot CurrentSnapshot => runner?.CurrentSnapshot;

        public Vector3 ToLocal(GlobalPosition position) =>
            mapBridge != null ? mapBridge.ToLocal(position) : Vector3.zero;

        public void SetMovementInput(Vector3 velocity) => movement = velocity;

        public void SetAimDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude > 0.0001f)
                aim = direction.normalized;
        }

        public void RequestFire() => fireRequested = true;

        public void SetTargetLocked(bool locked) =>
            presenter?.SetTargetLocked(locked);

        public void ShowLockedTargetDestroyed() =>
            presenter?.ShowLockedTargetDestroyed();

        private void Awake()
        {
            if (mapBridge == null)
                mapBridge = GetComponent<DroneMapBridge>();
            if (presenter == null)
                presenter = GetComponent<DroneCombatPresenter>();

            if (mapBridge == null || presenter == null)
            {
                Debug.LogError(
                    "DroneCombatController requires DroneMapBridge and " +
                    "DroneCombatPresenter on the same GameObject.",
                    this);
                enabled = false;
                return;
            }

            var initialPosition = mapBridge.Initialize();
            presenter.Initialize();
            runner = new DroneSimulationRunner(
                mapBridge.WorldQuery,
                initialPosition,
                seed);

            PresentFrame(runner.CurrentSnapshot, Array.Empty<SimulationEvent>());
        }

        private void Update()
        {
            if (runner == null)
                return;

            if (Input.GetKeyDown(KeyCode.R))
                Restart();

            var stepped = runner.Advance(
                Time.deltaTime,
                movement.ToFloat3(),
                aim.ToFloat3(),
                fireRequested,
                PresentFrame);

            if (stepped)
                fireRequested = false;
        }

        public void Restart()
        {
            if (runner == null)
                return;

            fireRequested = false;
            presenter.ResetPresentation();
            var snapshot = runner.Restart();
            PresentFrame(snapshot, Array.Empty<SimulationEvent>());
        }

        private void PresentFrame(
            SimulationSnapshot snapshot,
            System.Collections.Generic.IReadOnlyList<SimulationEvent> events)
        {
            if (TryGetPlayer(snapshot, out var player))
                mapBridge.FollowPlayer(player.Position);

            presenter.Present(snapshot, mapBridge);
            presenter.PresentEvents(events, snapshot.Match.Player, mapBridge);
        }

        private static bool TryGetPlayer(
            SimulationSnapshot snapshot,
            out DroneState player)
        {
            if (snapshot?.Drones != null)
            {
                for (var index = 0; index < snapshot.Drones.Count; ++index)
                {
                    var candidate = snapshot.Drones[index];
                    if (candidate.Id == snapshot.Match.Player)
                    {
                        player = candidate;
                        return true;
                    }
                }
            }

            player = default;
            return false;
        }

        private void OnDestroy()
        {
            mapBridge?.Shutdown();
        }
    }
}
