using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using StreamingMapDemo.Simulation;

namespace StreamingMapDemo.Drones.Tests
{
    public sealed class DroneSimulationTests
    {
        private sealed class FlatWorld : IWorldQuery
        {
            public bool TryGetTerrainHeight(GlobalPosition position, out double height) { height = 0; return true; }
            public bool HasLineOfSight(GlobalPosition from, GlobalPosition to) => true;
            public bool SweepProjectile(GlobalPosition from, GlobalPosition to, float radius, out WorldHit hit) { hit = default; return false; }
        }

        [Test]
        public void ResetAndStep_AreDeterministicAndMaintainFiveEnemies()
        {
            var a = new LocalDroneSimulation(new FlatWorld(), new GlobalPosition(10, 15, 20));
            var b = new LocalDroneSimulation(new FlatWorld(), new GlobalPosition(10, 15, 20));
            a.Reset(42); b.Reset(42); a.Step(1); b.Step(1);
            SimulationSnapshot sa = a.CaptureSnapshot(), sb = b.CaptureSnapshot();
            Assert.AreEqual(5, sa.Drones.Count(d => d.Faction == DroneFaction.Enemy));
            CollectionAssert.AreEqual(sa.Drones.Select(StateKey), sb.Drones.Select(StateKey));
        }

        [Test]
        public void SimulationRunner_UsesFixedTicksAndRestartsCleanly()
        {
            var runner = new DroneSimulationRunner(
                new FlatWorld(),
                new GlobalPosition(10, 15, 20),
                42);
            var presentedTicks = new List<uint>();

            bool stepped = runner.Advance(
                LocalDroneSimulation.TickDelta * 2.5f,
                Float3.Zero,
                Float3.Forward,
                false,
                (snapshot, events) => presentedTicks.Add(snapshot.Tick));

            Assert.IsTrue(stepped);
            CollectionAssert.AreEqual(new uint[] { 1, 2 }, presentedTicks);
            Assert.AreEqual(2, runner.CurrentSnapshot.Tick);

            SimulationSnapshot restarted = runner.Restart();
            Assert.AreEqual(0, restarted.Tick);
            Assert.AreEqual(MatchOutcome.Running, restarted.Match.Outcome);
            Assert.AreEqual(0, restarted.Match.EnemyKills);
        }

        [Test]
        public void EncounterCadence_HasCeasefireAndAtMostTwoAttackers()
        {
            var simulation = new LocalDroneSimulation(new FlatWorld(), new GlobalPosition(0, 15, 0));
            var events = new List<SimulationEvent>();
            for (uint tick = 1; tick <= 75; tick++) simulation.Step(tick);
            simulation.DrainEvents(events);
            Assert.IsFalse(events.Any(e => e.Type == SimulationEventType.ProjectileSpawned && e.Source != simulation.PlayerId));

            simulation.Step(76);
            Assert.LessOrEqual(simulation.CaptureSnapshot().Drones.Count(d => d.IsTelegraphing), 2);
            for (uint tick = 77; tick <= 99; tick++) simulation.Step(tick);
            events.Clear(); simulation.DrainEvents(events);
            Assert.LessOrEqual(events.Where(e => e.Type == SimulationEventType.ProjectileSpawned && e.Source != simulation.PlayerId)
                .Select(e => e.Source).Distinct().Count(), 2);
        }

        [Test]
        public void EnemyDiesAfterFiveHits_AndTenthKillWins()
        {
            var simulation = new LocalDroneSimulation(new FlatWorld(), new GlobalPosition(0, 15, 0));
            simulation.Step(1);
            for (int kill = 0; kill < 10; kill++)
            {
                EntityId enemy = simulation.CaptureSnapshot().Drones.First(d => d.Faction == DroneFaction.Enemy).Id;
                for (int hit = 0; hit < 5; hit++)
                    Assert.IsTrue(simulation.ApplyDamage(new DamageInfo(simulation.PlayerId, enemy, DroneFaction.Player, 10, (uint)(kill * 10 + hit))));
                if (kill < 9) simulation.Step((uint)(31 + kill * 31));
            }
            Assert.AreEqual(MatchOutcome.Victory, simulation.CaptureSnapshot().Match.Outcome);
            Assert.AreEqual(10, simulation.CaptureSnapshot().Match.EnemyKills);
        }

        [Test]
        public void FriendlyFireAndOwnerDamageAreRejected()
        {
            var simulation = new LocalDroneSimulation(new FlatWorld(), new GlobalPosition(0, 15, 0)); simulation.Step(1);
            Assert.IsFalse(simulation.ApplyDamage(new DamageInfo(simulation.PlayerId, simulation.PlayerId, DroneFaction.Player, 10, 2)));
            EntityId enemy = simulation.CaptureSnapshot().Drones.First(d => d.Faction == DroneFaction.Enemy).Id;
            Assert.IsFalse(simulation.ApplyDamage(new DamageInfo(enemy, enemy, DroneFaction.Enemy, 10, 2)));
        }

        [Test]
        public void LaterShot_DoesNotRedirectExistingProjectile()
        {
            var simulation = new LocalDroneSimulation(new FlatWorld(), new GlobalPosition(0, 15, 0));
            EntityId player = simulation.PlayerId;
            var firstDirection = new Float3(1, 1, 0).Normalized;
            simulation.Submit(new DroneCommand(player, 1, Float3.Zero, firstDirection, true));
            simulation.Step(1);
            ProjectileState first = simulation.CaptureSnapshot().Projectiles.Single(p => p.Owner == player);

            for (uint tick = 2; tick < 7; tick++) simulation.Step(tick);
            var secondDirection = new Float3(-1, 1, 0).Normalized;
            simulation.Submit(new DroneCommand(player, 7, Float3.Zero, secondDirection, true));
            simulation.Step(7);

            ProjectileState unchanged = simulation.CaptureSnapshot().Projectiles.Single(p => p.Id == first.Id);
            ProjectileState second = simulation.CaptureSnapshot().Projectiles.Single(p => p.Id != first.Id && p.Owner == player);
            Assert.Greater(unchanged.Direction.X, 0f);
            Assert.Less(second.Direction.X, 0f);
            Assert.AreEqual(2, simulation.CaptureSnapshot().Projectiles.Count(p => p.Owner == player));
        }

        [Test]
        public void ShooterMovement_DoesNotAlterSubmittedAimDirection()
        {
            var simulation = new LocalDroneSimulation(new FlatWorld(), new GlobalPosition(0, 15, 0));
            EntityId player = simulation.PlayerId;
            simulation.Submit(new DroneCommand(player, 1, new Float3(1, 0, 0), Float3.Forward, true));
            simulation.Step(1);
            var events = new List<SimulationEvent>();
            simulation.DrainEvents(events);
            SimulationEvent shot = events.Last(e => e.Type == SimulationEventType.ProjectileSpawned && e.Source == player);
            Assert.AreEqual(0f, shot.Normal.X, .0001f);
            Assert.AreEqual(0f, shot.Normal.Y, .0001f);
            Assert.Greater(shot.Normal.Z, .999f);
        }

        private static string StateKey(DroneState state) => $"{state.Id}:{state.Position.X:F4}:{state.Position.Y:F4}:{state.Position.Z:F4}";
    }
}
