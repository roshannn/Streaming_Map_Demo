using System;
using System.Collections.Generic;
using System.Linq;

namespace StreamingMapDemo.Simulation
{
    public sealed class LocalDroneSimulation : IDroneSimulation
    {
        public const float TickDelta = 1f / 30f;
        private const int RequiredKills = 10;
        private const int DesiredEnemies = 5;
        private const float PlayerSpeed = 8f;
        private const float EnemySpeed = 5f;
        private const float PlayerFireIntervalTicks = 6f;
        private const uint RepositionTicks = 75;
        private const uint TelegraphTicks = 23;
        private const uint AttackTicks = 38;
        private const uint RecoveryTicks = 90;
        private const uint EncounterCycleTicks = RepositionTicks + TelegraphTicks + AttackTicks + RecoveryTicks;
        public const float PlayerProjectileSpeed = 150f;
        public const float EnemyProjectileSpeed = 35f;
        public const float ProjectileDrag = .12f;

        private sealed class DroneEntity
        {
            public EntityId Id;
            public DroneFaction Faction;
            public GlobalPosition Position, PatrolAnchor;
            public Float3 Velocity, Forward = Float3.Forward, Aim = Float3.Forward;
            public float Health, MaxHealth, NextFireTick;
            public DroneAiState AiState;
            public int BurstShots;
            public uint NextBurstTick;
            public uint CommittedStrafeUntilTick;
            public int StrafeDirection = 1;
        }

        private sealed class ProjectileEntity
        {
            public EntityId Id, Owner;
            public DroneFaction Faction;
            public GlobalPosition Position;
            public Float3 Velocity;
            public float RemainingLife;
        }

        private readonly IWorldQuery world;
        private readonly GlobalPosition initialPlayerPosition;
        private readonly Dictionary<EntityId, DroneEntity> drones = new Dictionary<EntityId, DroneEntity>();
        private readonly Dictionary<EntityId, ProjectileEntity> projectiles = new Dictionary<EntityId, ProjectileEntity>();
        private readonly Dictionary<EntityId, DroneCommand> commands = new Dictionary<EntityId, DroneCommand>();
        private readonly List<SimulationEvent> events = new List<SimulationEvent>();
        private readonly HashSet<EntityId> activeAttackers = new HashSet<EntityId>();
        private Random random;
        private ulong nextId;
        private uint currentTick, nextSpawnTick;
        private int enemyKills;
        private MatchOutcome outcome;

        public EntityId PlayerId { get; private set; }

        public LocalDroneSimulation(IWorldQuery world, GlobalPosition initialPlayerPosition)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.initialPlayerPosition = initialPlayerPosition;
            Reset(1);
        }

        public void Reset(uint seed)
        {
            drones.Clear(); projectiles.Clear(); commands.Clear(); events.Clear();
            activeAttackers.Clear();
            random = new Random(unchecked((int)seed)); nextId = 1; currentTick = 0;
            nextSpawnTick = 0; enemyKills = 0; outcome = MatchOutcome.Running;
            PlayerId = NewId();
            drones[PlayerId] = new DroneEntity
            {
                Id = PlayerId, Faction = DroneFaction.Player, Position = initialPlayerPosition,
                Health = 100, MaxHealth = 100, AiState = DroneAiState.Player
            };
            events.Add(new SimulationEvent(SimulationEventType.EntitySpawned, PlayerId, EntityId.None,
                initialPlayerPosition, Float3.Zero));
        }

        public void Submit(in DroneCommand command)
        {
            if (outcome == MatchOutcome.Running && command.Entity != EntityId.None)
                commands[command.Entity] = command;
        }

        public void Step(uint tick)
        {
            currentTick = tick;
            if (outcome != MatchOutcome.Running) return;
            SpawnEnemies();
            UpdateEncounterCadence();
            BuildEnemyCommands();
            MoveDrones();
            ProcessWeapons();
            MoveProjectiles();
            commands.Clear();
        }

        private EncounterCombatPhase CurrentEncounterPhase
        {
            get
            {
                uint cycleTick = currentTick == 0 ? 0 : (currentTick - 1) % EncounterCycleTicks;
                if (cycleTick < RepositionTicks) return EncounterCombatPhase.Reposition;
                if (cycleTick < RepositionTicks + TelegraphTicks) return EncounterCombatPhase.Telegraph;
                if (cycleTick < RepositionTicks + TelegraphTicks + AttackTicks) return EncounterCombatPhase.Attack;
                return EncounterCombatPhase.Recovery;
            }
        }

        private void UpdateEncounterCadence()
        {
            uint cycleTick = currentTick == 0 ? 0 : (currentTick - 1) % EncounterCycleTicks;
            if (cycleTick == 0) activeAttackers.Clear();
            if (cycleTick != RepositionTicks) return;
            activeAttackers.Clear();
            List<DroneEntity> eligible = drones.Values.Where(d => d.Faction == DroneFaction.Enemy &&
                GlobalPosition.UnityDelta(d.Position, drones[PlayerId].Position).Length <= 70f).ToList();
            for (int i = eligible.Count - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                DroneEntity temp = eligible[i]; eligible[i] = eligible[swap]; eligible[swap] = temp;
            }
            foreach (DroneEntity attacker in eligible.Take(2))
            {
                activeAttackers.Add(attacker.Id);
                attacker.BurstShots = 3;
                attacker.NextFireTick = currentTick + TelegraphTicks;
                attacker.CommittedStrafeUntilTick = currentTick + TelegraphTicks + AttackTicks;
            }
        }

        public SimulationSnapshot CaptureSnapshot()
        {
            DroneEntity player = drones.TryGetValue(PlayerId, out DroneEntity found) ? found : null;
            return new SimulationSnapshot
            {
                Tick = currentTick,
                Match = new MatchState(PlayerId, player?.Health ?? 0, enemyKills, RequiredKills, outcome),
                Drones = drones.Values.Select(ToState).ToArray(),
                Projectiles = projectiles.Values.Select(p => new ProjectileState(
                    p.Id, p.Owner, p.Faction, p.Position, p.Velocity.Normalized)).ToArray()
            };
        }

        public void DrainEvents(ICollection<SimulationEvent> destination)
        {
            foreach (SimulationEvent simulationEvent in events) destination.Add(simulationEvent);
            events.Clear();
        }

        public bool ApplyDamage(in DamageInfo damage)
        {
            if (outcome != MatchOutcome.Running || damage.Source == damage.Target || damage.Amount <= 0 ||
                !drones.TryGetValue(damage.Source, out DroneEntity source) ||
                !drones.TryGetValue(damage.Target, out DroneEntity target) || source.Faction != damage.SourceFaction ||
                source.Faction == target.Faction) return false;
            ApplyDamage(source, target, damage.Amount);
            return true;
        }

        private void SpawnEnemies()
        {
            if (currentTick < nextSpawnTick || !drones.TryGetValue(PlayerId, out DroneEntity player)) return;
            int count = drones.Values.Count(d => d.Faction == DroneFaction.Enemy);
            for (int attempt = 0; count < DesiredEnemies && attempt < 20; attempt++)
            {
                double angle = random.NextDouble() * Math.PI * 2;
                double radius = 45 + random.NextDouble() * 45;
                var candidate = new GlobalPosition(
                    player.Position.X + Math.Cos(angle) * radius,
                    player.Position.Y,
                    player.Position.Z + Math.Sin(angle) * radius);
                if (!world.TryGetTerrainHeight(candidate, out double terrainHeight)) continue;
                candidate = new GlobalPosition(candidate.X, terrainHeight + 8 + random.NextDouble() * 10, candidate.Z);
                if (drones.Values.Any(d => GlobalPosition.UnityDelta(d.Position, candidate).Length < 12)) continue;
                EntityId id = NewId();
                drones[id] = new DroneEntity
                {
                    Id = id, Faction = DroneFaction.Enemy, Position = candidate, PatrolAnchor = candidate,
                    Health = 50, MaxHealth = 50, AiState = DroneAiState.Patrol,
                    NextBurstTick = currentTick + (uint)random.Next(15, 45)
                };
                events.Add(new SimulationEvent(SimulationEventType.EntitySpawned, id, EntityId.None, candidate, Float3.Zero));
                count++;
            }
        }

        private void BuildEnemyCommands()
        {
            if (!drones.TryGetValue(PlayerId, out DroneEntity player)) return;
            foreach (DroneEntity enemy in drones.Values.Where(d => d.Faction == DroneFaction.Enemy).ToArray())
            {
                Float3 toPlayer = GlobalPosition.UnityDelta(enemy.Position, player.Position);
                float distance = toPlayer.Length;
                Float3 move = Float3.Zero;
                Float3 aim = toPlayer.Normalized;
                bool fire = false;
                if (distance > 70)
                {
                    enemy.AiState = DroneAiState.Patrol;
                    Float3 toAnchor = GlobalPosition.UnityDelta(enemy.Position, enemy.PatrolAnchor);
                    float phase = (currentTick + enemy.Id.Value * 31) * 0.025f;
                    move = (toAnchor.Normalized + new Float3((float)Math.Cos(phase), 0.15f * (float)Math.Sin(phase * 0.7f), (float)Math.Sin(phase))).Normalized;
                }
                else if (distance > 35)
                {
                    enemy.AiState = DroneAiState.Chase;
                    move = toPlayer.Normalized;
                }
                else
                {
                    enemy.AiState = DroneAiState.Attack;
                    Float3 radial = toPlayer.Normalized;
                    if (currentTick >= enemy.CommittedStrafeUntilTick)
                    {
                        enemy.StrafeDirection = random.Next(0, 2) == 0 ? -1 : 1;
                        enemy.CommittedStrafeUntilTick = currentTick + 45;
                    }
                    Float3 tangent = new Float3(-radial.Z, 0, radial.X) * enemy.StrafeDirection;
                    move = (tangent + radial * (distance > 25 ? 0.25f : -0.35f)).Normalized;
                }
                float dragRatio = Math.Min(.98f, distance * ProjectileDrag / EnemyProjectileSpeed);
                float flightTime = -(float)Math.Log(1f - dragRatio) / ProjectileDrag;
                aim = (toPlayer + player.Velocity * flightTime).Normalized;
                if (CurrentEncounterPhase == EncounterCombatPhase.Attack && activeAttackers.Contains(enemy.Id) &&
                    enemy.BurstShots > 0 && currentTick >= enemy.NextFireTick &&
                    world.HasLineOfSight(enemy.Position, player.Position))
                {
                    fire = true;
                    enemy.BurstShots--;
                }
                commands[enemy.Id] = new DroneCommand(enemy.Id, currentTick, move, aim, fire);
            }
        }

        private void MoveDrones()
        {
            foreach (DroneEntity drone in drones.Values)
            {
                if (!commands.TryGetValue(drone.Id, out DroneCommand command)) { drone.Velocity = Float3.Zero; continue; }
                Float3 direction = command.Movement.LengthSquared > 1 ? command.Movement.Normalized : command.Movement;
                float speed = drone.Faction == DroneFaction.Player ? PlayerSpeed : EnemySpeed;
                drone.Velocity = direction * speed;
                drone.Position = drone.Position.AddUnityDisplacement(drone.Velocity * TickDelta);
                if (command.AimDirection.LengthSquared > 0.001f) drone.Aim = command.AimDirection.Normalized;
                if (drone.Velocity.LengthSquared > 0.01f) drone.Forward = drone.Velocity.Normalized;
            }
        }

        private void ProcessWeapons()
        {
            foreach (DroneCommand command in commands.Values.ToArray())
            {
                if (!command.Fire || !drones.TryGetValue(command.Entity, out DroneEntity drone) || currentTick < drone.NextFireTick) continue;
                Float3 direction = command.AimDirection.LengthSquared > 0.001f ? command.AimDirection.Normalized : drone.Forward;
                float muzzleSpeed = drone.Faction == DroneFaction.Player ? PlayerProjectileSpeed : EnemyProjectileSpeed;
                // Aim direction is authoritative. Drone movement must never steer a fired shot.
                Float3 launchVelocity = direction * muzzleSpeed;
                EntityId id = NewId();
                var projectile = new ProjectileEntity
                {
                    Id = id, Owner = drone.Id, Faction = drone.Faction,
                    Position = drone.Position.AddUnityDisplacement(direction * 1.2f),
                    Velocity = launchVelocity, RemainingLife = 5
                };
                projectiles[id] = projectile;
                drone.NextFireTick = currentTick + (uint)(drone.Faction == DroneFaction.Player ? PlayerFireIntervalTicks : 6);
                events.Add(new SimulationEvent(SimulationEventType.ProjectileSpawned, id, drone.Id, projectile.Position, projectile.Velocity.Normalized));
            }
        }

        private void MoveProjectiles()
        {
            foreach (ProjectileEntity projectile in projectiles.Values.ToArray())
            {
                GlobalPosition from = projectile.Position;
                float decay = (float)Math.Exp(-ProjectileDrag * TickDelta);
                Float3 displacement = projectile.Velocity * ((1f - decay) / ProjectileDrag);
                GlobalPosition to = from.AddUnityDisplacement(displacement);
                DroneEntity target = FindProjectileTarget(projectile, from, to);
                if (target != null)
                {
                    events.Add(new SimulationEvent(SimulationEventType.ProjectileImpacted, projectile.Id,
                        projectile.Owner, target.Position, projectile.Velocity.Normalized * -1, 10, true));
                    if (drones.TryGetValue(projectile.Owner, out DroneEntity source)) ApplyDamage(source, target, 10);
                    projectiles.Remove(projectile.Id);
                    continue;
                }
                if (world.SweepProjectile(from, to, 0.12f, out WorldHit hit))
                {
                    events.Add(new SimulationEvent(SimulationEventType.ProjectileImpacted, projectile.Id,
                        projectile.Owner, hit.Position, hit.Normal, 0, false));
                    projectiles.Remove(projectile.Id);
                    continue;
                }
                projectile.Position = to;
                projectile.Velocity = projectile.Velocity * decay;
                projectile.RemainingLife -= TickDelta;
                if (projectile.RemainingLife <= 0) projectiles.Remove(projectile.Id);
            }
        }

        private DroneEntity FindProjectileTarget(ProjectileEntity projectile, GlobalPosition from, GlobalPosition to)
        {
            Float3 segment = GlobalPosition.UnityDelta(from, to);
            float lengthSq = segment.LengthSquared;
            foreach (DroneEntity drone in drones.Values)
            {
                if (drone.Id == projectile.Owner || drone.Faction == projectile.Faction) continue;
                Float3 point = GlobalPosition.UnityDelta(from, drone.Position);
                float t = lengthSq <= 0 ? 0 : Math.Max(0, Math.Min(1,
                    (point.X * segment.X + point.Y * segment.Y + point.Z * segment.Z) / lengthSq));
                Float3 closest = segment * t;
                float hitRadius = projectile.Faction == DroneFaction.Player ? 1.8f : 1.5f;
                if ((point - closest).LengthSquared <= hitRadius * hitRadius) return drone;
            }
            return null;
        }

        private void ApplyDamage(DroneEntity source, DroneEntity target, float amount)
        {
            target.Health = Math.Max(0, target.Health - amount);
            events.Add(new SimulationEvent(SimulationEventType.Damaged, target.Id, source.Id,
                target.Position, Float3.Zero, amount, true));
            if (target.Health > 0) return;
            target.AiState = DroneAiState.Destroyed;
            events.Add(new SimulationEvent(SimulationEventType.EntityDestroyed, target.Id,
                source.Id, target.Position, Float3.Zero));
            drones.Remove(target.Id);
            if (target.Faction == DroneFaction.Enemy && source.Faction == DroneFaction.Player)
            {
                enemyKills++;
                events.Add(new SimulationEvent(SimulationEventType.KillConfirmed, target.Id,
                    source.Id, target.Position, Float3.Zero, enemyKills));
                nextSpawnTick = currentTick + 30;
                if (enemyKills >= RequiredKills)
                {
                    outcome = MatchOutcome.Victory;
                    events.Add(new SimulationEvent(SimulationEventType.Victory, PlayerId, EntityId.None,
                        target.Position, Float3.Zero));
                }
            }
            else if (target.Id == PlayerId)
            {
                outcome = MatchOutcome.Defeat;
                events.Add(new SimulationEvent(SimulationEventType.Defeat, PlayerId, source.Id,
                    target.Position, Float3.Zero));
            }
        }

        private DroneState ToState(DroneEntity drone) => new DroneState(drone.Id, drone.Faction,
            drone.Position, drone.Velocity, drone.Forward, drone.Aim, drone.Health, drone.MaxHealth, drone.AiState, true,
            drone.Faction == DroneFaction.Enemy && activeAttackers.Contains(drone.Id) && CurrentEncounterPhase == EncounterCombatPhase.Telegraph,
            drone.Faction == DroneFaction.Enemy && activeAttackers.Contains(drone.Id) && CurrentEncounterPhase == EncounterCombatPhase.Attack);
        private EntityId NewId() => new EntityId(nextId++);
    }
}
