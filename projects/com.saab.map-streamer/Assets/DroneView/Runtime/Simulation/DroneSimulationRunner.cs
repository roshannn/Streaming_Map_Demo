using System;
using System.Collections.Generic;

using StreamingMapDemo.Simulation;

namespace StreamingMapDemo.Drones
{
    /// <summary>
    /// Owns the authoritative local simulation clock and command/snapshot/event
    /// exchange. It has no Unity scene or presentation responsibilities.
    /// </summary>
    public sealed class DroneSimulationRunner
    {
        private readonly uint seed;
        private readonly List<SimulationEvent> events =
            new List<SimulationEvent>();

        private float accumulator;
        private uint tick;

        public DroneSimulationRunner(
            IWorldQuery world,
            GlobalPosition initialPosition,
            uint seed)
        {
            this.seed = seed;
            Simulation = new LocalDroneSimulation(world, initialPosition);
            Simulation.Reset(seed);
            CurrentSnapshot = Simulation.CaptureSnapshot();
        }

        public IDroneSimulation Simulation { get; }
        public SimulationSnapshot CurrentSnapshot { get; private set; }

        public bool Advance(
            float deltaTime,
            Float3 movement,
            Float3 aim,
            bool fire,
            Action<SimulationSnapshot, IReadOnlyList<SimulationEvent>> onStep)
        {
            accumulator += Math.Max(0f, deltaTime);
            var stepped = false;
            var fireOnThisStep = fire;

            while (accumulator >= LocalDroneSimulation.TickDelta)
            {
                accumulator -= LocalDroneSimulation.TickDelta;
                ++tick;

                Simulation.Submit(new DroneCommand(
                    Simulation.PlayerId,
                    tick,
                    movement,
                    aim,
                    fireOnThisStep));
                fireOnThisStep = false;

                Simulation.Step(tick);
                CurrentSnapshot = Simulation.CaptureSnapshot();

                events.Clear();
                Simulation.DrainEvents(events);
                onStep?.Invoke(CurrentSnapshot, events);
                stepped = true;
            }

            return stepped;
        }

        public SimulationSnapshot Restart()
        {
            tick = 0;
            accumulator = 0;
            events.Clear();
            Simulation.Reset(seed);
            CurrentSnapshot = Simulation.CaptureSnapshot();
            return CurrentSnapshot;
        }
    }
}
