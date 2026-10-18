using System.Collections.Generic;
using CsCheck;
using MultiplayerRts.Core;
using MultiplayerRts.Tests.Fixtures;

namespace MultiplayerRts.Tests.Generators
{
    /// <summary>
    /// Generators for the entity and per-player state the simulation operates on
    /// (design.md: "player/resource/structure states", "unit sets with varied path
    /// progression"). Each generator produces fully-formed, internally consistent entities
    /// via the fixture builders so later property tests get valid inputs without repeating
    /// construction detail. Exposed as lazily-evaluated properties (see
    /// <c>Gens.Primitives.cs</c>).
    /// </summary>
    public static partial class Gens
    {
        // --- Units ---

        /// <summary>
        /// A single unit with random owner, position, health, speed, damage, path, and path
        /// progress. Health spans zero so removal properties see dead units (Requirement 7.4).
        /// </summary>
        public static Gen<Unit> Unit =>
            Gen.Select(
                UnitId,
                PlayerId,
                MapVector,
                Gen.Float[0f, 200f],
                Gen.Float[0f, 20f],
                Gen.Float[0f, 100f],
                Path,
                (id, owner, pos, health, speed, damage, path) =>
                    UnitBuilder.AUnit()
                        .WithId(id)
                        .WithOwner(owner)
                        .WithPosition(pos)
                        .WithHealth(health)
                        .WithMovementSpeed(speed)
                        .WithDamage(damage)
                        .WithPath(path)
                        .WithWaypointIndex(0)
                        .Build());

        /// <summary>
        /// A set of units with varied path progression: each unit's waypoint index is spread
        /// across its own path, so targeting/furthest-progressed properties see a realistic
        /// mix of leaders and stragglers (design.md: "unit sets with varied path
        /// progression"; Requirement 7.1). Ids are made unique within the set and a single
        /// owner is assigned.
        /// </summary>
        public static Gen<IReadOnlyList<Unit>> UnitSet =>
            Gen.Select(PlayerId, VariedProgressionUnit.List[0, 8], (owner, units) =>
            {
                var result = new List<Unit>(units.Count);
                for (int i = 0; i < units.Count; i++)
                {
                    Unit u = units[i];
                    u.Id = new UnitId((uint)(i + 1));
                    u.Owner = owner;
                    result.Add(u);
                }

                return (IReadOnlyList<Unit>)result;
            });

        /// <summary>
        /// A unit whose waypoint index is a valid, random position along its own path, so a
        /// set built from it shows varied progression (Requirement 7.1).
        /// </summary>
        private static Gen<Unit> VariedProgressionUnit =>
            Gen.Select(
                MapVector,
                Gen.Float[1f, 200f],
                Gen.Float[0.1f, 20f],
                Gen.Float[0f, 100f],
                PathWithIndex,
                (pos, health, speed, damage, pathWithIndex) =>
                    UnitBuilder.AUnit()
                        .WithPosition(pos)
                        .WithHealth(health)
                        .WithMovementSpeed(speed)
                        .WithDamage(damage)
                        .WithPath(pathWithIndex.Path)
                        .WithWaypointIndex(pathWithIndex.WaypointIndex)
                        .Build());

        // --- Structures ---

        /// <summary>A spawner structure with a valid interval and spawn point (Requirement 5).</summary>
        public static Gen<Structure> SpawnerStructure =>
            Gen.Select(StructureId, PlayerId, NonDegenerateFootprint, SpawnInterval, MapVector, SpawnTimer, Gen.Bool,
                (id, owner, fp, interval, spawnPoint, timer, active) =>
                    StructureBuilder.AStructure()
                        .WithId(id).WithOwner(owner).WithFootprint(fp).Active(active)
                        .AsSpawner(interval, spawnPoint, timer)
                        .Build());

        /// <summary>A defensive structure with a range and attack schedule (Requirement 7).</summary>
        public static Gen<Structure> DefensiveStructure =>
            Gen.Select(StructureId, PlayerId, NonDegenerateFootprint, Gen.Float[1f, 50f], AttackInterval, Gen.Float[1f, 100f], AttackTimer,
                (id, owner, fp, range, interval, damage, timer) =>
                    StructureBuilder.AStructure()
                        .WithId(id).WithOwner(owner).WithFootprint(fp).Active()
                        .AsDefensive(range, interval, damage, timer)
                        .Build());

        /// <summary>An economy structure with a generation rate (Requirement 3).</summary>
        public static Gen<Structure> EconomyStructure =>
            Gen.Select(StructureId, PlayerId, NonDegenerateFootprint, GenerationRate, Gen.Float[0f, 1f], Gen.Bool,
                (id, owner, fp, rate, timer, active) =>
                    StructureBuilder.AStructure()
                        .WithId(id).WithOwner(owner).WithFootprint(fp).Active(active)
                        .AsEconomy(rate, timer)
                        .Build());

        /// <summary>Any structure, uniformly across the three types.</summary>
        public static Gen<Structure> Structure =>
            Gen.OneOf(SpawnerStructure, DefensiveStructure, EconomyStructure);

        // --- Player state ---

        /// <summary>
        /// A per-player state with a random resource total (valid [0, cap]), base health,
        /// build area, and connection state (Requirements 3.1, 8.1, 9).
        /// </summary>
        public static Gen<PlayerState> PlayerState =>
            Gen.Select(PlayerId, Resources, Gen.Float[0f, 1000f], Gen.Float[1f, 1000f], MapVector, BuildArea, ConnectionState,
                (id, resources, health, maxHealth, baseLoc, buildArea, connection) =>
                    PlayerStateBuilder.APlayer()
                        .WithId(id)
                        .WithResources(resources)
                        .WithBaseHealth(health, maxHealth + health)
                        .WithBaseLocation(baseLoc)
                        .WithBuildArea(buildArea)
                        .WithConnection(connection)
                        .Build());
    }
}
