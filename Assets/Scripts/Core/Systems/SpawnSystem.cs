using System;
using System.Collections.Generic;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// Produces <c>Unit</c>s from active <c>Spawner_Structure</c>s (Requirement 5).
    ///
    /// Each tick the system, for every <em>active</em> spawner, accrues its
    /// <see cref="Structure.SpawnTimer"/> and converts each fully elapsed spawn interval
    /// (clamped to the valid [1, 600]s range, Requirement 5.1) into one owed unit. Inactive
    /// spawners accrue nothing and owe nothing, so no unit is produced for any interval that
    /// elapses while the spawner is inactive (Requirement 5.4).
    ///
    /// Owed units are fulfilled only while the spawn point is unoccupied. When occupied the
    /// owed creation is retained and fulfilled once the point frees, so a blocked spawn is
    /// conserved - never lost and never duplicated (Requirement 5.5). A created unit inherits
    /// the spawner's owner (Requirement 5.2) and, when a path toward the opponent base is
    /// available, a base-targeted path assigned before it becomes active (Requirement 5.3).
    /// When no path is available the unit is created and held at the spawn point without
    /// movement, its owner retained, until a path becomes available (Requirement 5.6).
    ///
    /// A pure C# type with no Unity dependency; unit-id allocation and pathing are injected
    /// so the system can be property-tested against an in-memory <see cref="MatchState"/>.
    /// </summary>
    public sealed class SpawnSystem : ISpawnSystem
    {
        private readonly MatchState _state;
        private readonly IUnitIdAllocator _idAllocator;
        private readonly IPathProvider _pathProvider;

        /// <summary>
        /// Units owed per spawner that could not yet be created because the spawn point was
        /// occupied when their interval elapsed. Keyed by spawner id; conserves exactly one
        /// owed unit per blocked interval (Requirement 5.5).
        /// </summary>
        private readonly Dictionary<StructureId, int> _pending = new Dictionary<StructureId, int>();

        public SpawnSystem(MatchState state, IUnitIdAllocator idAllocator, IPathProvider pathProvider)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _idAllocator = idAllocator ?? throw new ArgumentNullException(nameof(idAllocator));
            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        }

        /// <inheritdoc />
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
            {
                // Negative time never moves the simulation; treat as a no-op (totality).
                return;
            }

            // Resolve units already held awaiting a path first, so a path that became
            // available this tick releases them before new spawns are considered (5.6).
            ResolveHeldPathlessUnits();

            // Snapshot the spawner ids so creating units mid-iteration cannot disturb the
            // structure enumeration.
            List<Structure> spawners = CollectSpawners();
            for (int i = 0; i < spawners.Count; i++)
            {
                TickSpawner(spawners[i], deltaSeconds);
            }
        }

        private List<Structure> CollectSpawners()
        {
            var spawners = new List<Structure>();
            foreach (KeyValuePair<StructureId, Structure> entry in _state.Structures)
            {
                Structure structure = entry.Value;
                if (structure != null && structure.Type == StructureType.Spawner)
                {
                    spawners.Add(structure);
                }
            }

            return spawners;
        }

        private void TickSpawner(Structure spawner, float deltaSeconds)
        {
            if (!spawner.IsActive)
            {
                // Inactive spawners accrue no time and owe no units; any interval that
                // elapses while inactive produces nothing (Requirement 5.4).
                return;
            }

            float interval = ClampInterval(spawner.SpawnIntervalSeconds);

            // Accrue elapsed time and convert each full interval into one owed unit (5.1).
            spawner.SpawnTimer += deltaSeconds;
            while (spawner.SpawnTimer >= interval)
            {
                spawner.SpawnTimer -= interval;
                AddPending(spawner.Id, 1);
            }

            // Fulfil owed units while the spawn point stays unoccupied. A unit created with
            // a path moves off next movement tick; a held pathless unit keeps occupying the
            // point, so at most one unit leaves per free spawn point - conserving owed units
            // rather than duplicating them (Requirements 5.5, 5.6).
            while (GetPending(spawner.Id) > 0 && !IsSpawnPointOccupied(spawner))
            {
                CreateUnit(spawner);
                AddPending(spawner.Id, -1);
            }
        }

        private void ResolveHeldPathlessUnits()
        {
            foreach (KeyValuePair<UnitId, Unit> entry in _state.Units)
            {
                Unit unit = entry.Value;
                if (unit == null)
                {
                    continue;
                }

                // Only units held specifically for lack of a path are candidates here; a
                // held unit already has a path assigned (it would not be pathless).
                if (!unit.IsHeld || unit.AssignedPath != null)
                {
                    continue;
                }

                Structure spawner = FindSpawnerForHeldUnit(unit);
                if (spawner == null)
                {
                    continue;
                }

                Path path = _pathProvider.TryGetPath(_state, spawner, unit.Owner);
                if (path != null)
                {
                    // Path became available: release the unit so it begins moving, while
                    // its owner is retained throughout the hold (Requirement 5.6).
                    unit.AssignedPath = path;
                    unit.WaypointIndex = 0;
                    unit.IsHeld = false;
                }
            }
        }

        private Structure FindSpawnerForHeldUnit(Unit unit)
        {
            // A pathless held unit sits at its spawner's spawn point; match on owner and the
            // spawn point location to re-associate it with the spawner that produced it.
            foreach (KeyValuePair<StructureId, Structure> entry in _state.Structures)
            {
                Structure structure = entry.Value;
                if (structure != null
                    && structure.Type == StructureType.Spawner
                    && structure.Owner == unit.Owner
                    && AtSpawnPoint(unit.Position, structure.SpawnPoint))
                {
                    return structure;
                }
            }

            return null;
        }

        private void CreateUnit(Structure spawner)
        {
            // Assign owner (Requirement 5.2) and, when available, a base-targeted path
            // (Requirement 5.3) before the unit becomes active.
            PlayerId owner = spawner.Owner;
            Path path = _pathProvider.TryGetPath(_state, spawner, owner);

            var unit = new Unit
            {
                Id = _idAllocator.Next(),
                Owner = owner,
                Position = spawner.SpawnPoint,
                Health = 1f,
                MovementSpeed = 0f,
                Damage = 0f,
                AssignedPath = path,
                WaypointIndex = 0,

                // Held when no path is available yet; it stays at the spawn point without
                // movement until a path becomes available (Requirement 5.6).
                IsHeld = path == null,
            };

            _state.Units[unit.Id] = unit;
        }

        /// <summary>
        /// A spawn point is occupied when any existing unit currently sits at it (within the
        /// waypoint-reach threshold). A held pathless unit keeps its spawn point occupied,
        /// which is what defers further owed spawns until it is released (Requirements 5.5, 5.6).
        /// </summary>
        private bool IsSpawnPointOccupied(Structure spawner)
        {
            foreach (KeyValuePair<UnitId, Unit> entry in _state.Units)
            {
                Unit unit = entry.Value;
                if (unit != null && AtSpawnPoint(unit.Position, spawner.SpawnPoint))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AtSpawnPoint(MapVector position, MapVector spawnPoint)
        {
            float threshold = SimulationConstants.WaypointReachThreshold;
            return MapVector.SqrDistance(position, spawnPoint) <= threshold * threshold;
        }

        private static float ClampInterval(float intervalSeconds)
        {
            if (intervalSeconds < SimulationConstants.MinSpawnIntervalSeconds)
            {
                return SimulationConstants.MinSpawnIntervalSeconds;
            }

            if (intervalSeconds > SimulationConstants.MaxSpawnIntervalSeconds)
            {
                return SimulationConstants.MaxSpawnIntervalSeconds;
            }

            return intervalSeconds;
        }

        private int GetPending(StructureId id)
        {
            return _pending.TryGetValue(id, out int count) ? count : 0;
        }

        private void AddPending(StructureId id, int delta)
        {
            int next = GetPending(id) + delta;
            if (next <= 0)
            {
                _pending.Remove(id);
            }
            else
            {
                _pending[id] = next;
            }
        }
    }
}
