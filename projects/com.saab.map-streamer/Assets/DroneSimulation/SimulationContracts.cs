using System;
using System.Collections.Generic;

namespace StreamingMapDemo.Simulation
{
    public static class SimulationContract
    {
        public const ushort Version = 1;
    }

    public readonly struct EntityId : IEquatable<EntityId>
    {
        public readonly ulong Value;
        public EntityId(ulong value) => Value = value;
        public bool Equals(EntityId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
        public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);
        public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);
        public static readonly EntityId None = new EntityId(0);
    }

    public readonly struct Float3
    {
        public readonly float X, Y, Z;
        public Float3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public float LengthSquared => X * X + Y * Y + Z * Z;
        public float Length => (float)Math.Sqrt(LengthSquared);
        public Float3 Normalized => Length > 0.0001f ? this / Length : Zero;
        public static Float3 operator +(Float3 a, Float3 b) => new Float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Float3 operator -(Float3 a, Float3 b) => new Float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Float3 operator *(Float3 a, float b) => new Float3(a.X * b, a.Y * b, a.Z * b);
        public static Float3 operator /(Float3 a, float b) => new Float3(a.X / b, a.Y / b, a.Z / b);
        public static readonly Float3 Zero = new Float3(0, 0, 0);
        public static readonly Float3 Forward = new Float3(0, 0, 1);
    }

    public readonly struct GlobalPosition
    {
        public readonly double X, Y, Z;
        public GlobalPosition(double x, double y, double z) { X = x; Y = y; Z = z; }
        public GlobalPosition AddUnityDisplacement(Float3 displacement) =>
            new GlobalPosition(X + displacement.X, Y + displacement.Y, Z - displacement.Z);
        public static Float3 UnityDelta(GlobalPosition from, GlobalPosition to) =>
            new Float3((float)(to.X - from.X), (float)(to.Y - from.Y), (float)-(to.Z - from.Z));
    }

    public enum DroneFaction : byte { Player, Enemy }
    public enum DroneAiState : byte { Player, Patrol, Chase, Attack, Destroyed }
    public enum MatchOutcome : byte { Running, Victory, Defeat }
    public enum EncounterCombatPhase : byte { Reposition, Telegraph, Attack, Recovery }
    public enum SimulationEventType : byte
    {
        EntitySpawned, ProjectileSpawned, ProjectileImpacted, Damaged,
        EntityDestroyed, KillConfirmed, Victory, Defeat
    }

    public readonly struct DroneCommand
    {
        public readonly EntityId Entity;
        public readonly uint Tick;
        public readonly Float3 Movement;
        public readonly Float3 AimDirection;
        public readonly bool Fire;
        public DroneCommand(EntityId entity, uint tick, Float3 movement, Float3 aimDirection, bool fire)
        { Entity = entity; Tick = tick; Movement = movement; AimDirection = aimDirection; Fire = fire; }
    }

    public readonly struct DamageInfo
    {
        public readonly EntityId Source, Target;
        public readonly DroneFaction SourceFaction;
        public readonly float Amount;
        public readonly uint Tick;
        public DamageInfo(EntityId source, EntityId target, DroneFaction faction, float amount, uint tick)
        { Source = source; Target = target; SourceFaction = faction; Amount = amount; Tick = tick; }
    }

    public readonly struct DroneState
    {
        public readonly EntityId Id;
        public readonly DroneFaction Faction;
        public readonly GlobalPosition Position;
        public readonly Float3 Velocity, Forward, AimDirection;
        public readonly float Health, MaximumHealth;
        public readonly DroneAiState AiState;
        public readonly bool IsAlive;
        public readonly bool IsTelegraphing, IsAttacking;
        public DroneState(EntityId id, DroneFaction faction, GlobalPosition position, Float3 velocity,
            Float3 forward, Float3 aimDirection, float health, float maximumHealth, DroneAiState aiState, bool alive,
            bool isTelegraphing = false, bool isAttacking = false)
        { Id = id; Faction = faction; Position = position; Velocity = velocity; Forward = forward;
          AimDirection = aimDirection; Health = health; MaximumHealth = maximumHealth; AiState = aiState; IsAlive = alive;
          IsTelegraphing = isTelegraphing; IsAttacking = isAttacking; }
    }

    public readonly struct ProjectileState
    {
        public readonly EntityId Id, Owner;
        public readonly DroneFaction Faction;
        public readonly GlobalPosition Position;
        public readonly Float3 Direction;
        public ProjectileState(EntityId id, EntityId owner, DroneFaction faction, GlobalPosition position, Float3 direction)
        { Id = id; Owner = owner; Faction = faction; Position = position; Direction = direction; }
    }

    public readonly struct MatchState
    {
        public readonly EntityId Player;
        public readonly float PlayerHealth;
        public readonly int EnemyKills, RequiredKills;
        public readonly MatchOutcome Outcome;
        public MatchState(EntityId player, float health, int kills, int required, MatchOutcome outcome)
        { Player = player; PlayerHealth = health; EnemyKills = kills; RequiredKills = required; Outcome = outcome; }
    }

    public readonly struct WorldHit
    {
        public readonly GlobalPosition Position;
        public readonly Float3 Normal;
        public WorldHit(GlobalPosition position, Float3 normal) { Position = position; Normal = normal; }
    }

    public readonly struct SimulationEvent
    {
        public readonly SimulationEventType Type;
        public readonly EntityId Entity, Source;
        public readonly GlobalPosition Position;
        public readonly Float3 Normal;
        public readonly float Value;
        public readonly bool Applied;
        public SimulationEvent(SimulationEventType type, EntityId entity, EntityId source,
            GlobalPosition position, Float3 normal, float value = 0, bool applied = false)
        { Type = type; Entity = entity; Source = source; Position = position; Normal = normal; Value = value; Applied = applied; }
    }

    public sealed class SimulationSnapshot
    {
        public uint Tick { get; internal set; }
        public MatchState Match { get; internal set; }
        public IReadOnlyList<DroneState> Drones { get; internal set; }
        public IReadOnlyList<ProjectileState> Projectiles { get; internal set; }
    }

    public interface IWorldQuery
    {
        bool TryGetTerrainHeight(GlobalPosition position, out double height);
        bool HasLineOfSight(GlobalPosition from, GlobalPosition to);
        bool SweepProjectile(GlobalPosition from, GlobalPosition to, float radius, out WorldHit hit);
    }

    public interface IDroneSimulation
    {
        EntityId PlayerId { get; }
        void Submit(in DroneCommand command);
        void Step(uint tick);
        SimulationSnapshot CaptureSnapshot();
        void DrainEvents(ICollection<SimulationEvent> destination);
        void Reset(uint seed);
    }
}
