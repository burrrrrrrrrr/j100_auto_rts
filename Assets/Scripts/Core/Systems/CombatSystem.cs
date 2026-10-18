using System.Collections.Generic;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// Canonical combat resolution over a <see cref="MatchState"/> (Requirement 7): for each
    /// active <c>Defensive_Structure</c>, selects the single furthest-progressed enemy
    /// <c>Unit</c> within range as the target (7.1), applies the structure's damage to that
    /// target once per attack interval while it stays in range (7.2), applies no damage but
    /// retains the accumulated timing when no enemy is in range (7.3), and removes any unit
    /// whose health reaches zero or below within the same tick (7.4).
    ///
    /// Pure C# with no Unity or Netcode dependency so it can be property-tested against an
    /// in-memory state. Every operation is total over its valid domain (clamping timers and
    /// skipping degenerate structures rather than throwing) so a single bad entity cannot
    /// abort a tick. Network removal notifications (7.5/7.6) are handled by the Net layer;
    /// this system performs only the pure simulation removal.
    /// </summary>
    public sealed class CombatSystem : ICombatSystem
    {
        private readonly MatchState _state;

        // Reused scratch list for collecting units to remove after a tick, so damage
        // applied across all structures is settled before any unit leaves the match.
        private readonly List<UnitId> _removalBuffer = new List<UnitId>();

        /// <summary>
        /// Creates a combat system bound to <paramref name="state"/>, the canonical match
        /// state it reads from and mutates in place.
        /// </summary>
        public CombatSystem(MatchState state)
        {
            _state = state;
        }

        /// <inheritdoc />
        public void Tick(float deltaSeconds)
        {
            if (_state == null)
            {
                return;
            }

            // Non-positive steps advance no time; no interval can elapse so no damage is
            // applied and all accumulated timing is retained (Requirements 7.2, 7.3).
            if (deltaSeconds <= 0f)
            {
                return;
            }

            foreach (KeyValuePair<StructureId, Structure> entry in _state.Structures)
            {
                Structure structure = entry.Value;

                // Only active defensive structures engage units (Requirement 7.1).
                if (structure == null
                    || structure.Type != StructureType.Defensive
                    || !structure.IsActive)
                {
                    continue;
                }

                TickStructure(structure, deltaSeconds);
            }

            RemoveDeadUnits();
        }

        /// <summary>
        /// Advances one defensive structure's attack timer and applies its damage to the
        /// furthest-progressed in-range enemy unit once per elapsed attack interval while
        /// that target remains in range (Requirement 7.2). When no enemy is in range at the
        /// moment an interval elapses, no damage is applied and the accumulated timing is
        /// retained (Requirement 7.3).
        /// </summary>
        private void TickStructure(Structure structure, float deltaSeconds)
        {
            float interval = structure.AttackInterval;

            structure.AttackTimer += deltaSeconds;

            // A non-positive interval is degenerate; keep the accumulated time, record no
            // target, and skip rather than looping or throwing.
            if (interval <= 0f)
            {
                structure.CurrentTarget = null;
                return;
            }

            // Reflect the currently selected target for this tick; null when none in range.
            Unit target = SelectTarget(structure);
            structure.CurrentTarget = target?.Id;

            // Apply damage once per whole attack interval that has elapsed, re-selecting the
            // target each interval so a target leaving range stops being hit and timing is
            // retained when the range is empty (Requirements 7.2, 7.3).
            while (structure.AttackTimer >= interval)
            {
                structure.AttackTimer -= interval;

                if (target == null)
                {
                    // Empty range at the moment the interval elapses: no damage, timing
                    // already retained by the subtraction above (Requirement 7.3).
                    continue;
                }

                target.Health -= structure.AttackDamage;

                // Once a target is dead, further intervals in this tick find a new target
                // (or none) rather than overkilling a removed unit.
                if (target.Health <= 0f)
                {
                    target = SelectTarget(structure);
                    structure.CurrentTarget = target?.Id;
                }
            }
        }

        /// <summary>
        /// Selects the single enemy unit (owner different from the structure's owner) within
        /// the structure's range that has progressed furthest along its path
        /// (Requirement 7.1). Progression is measured by waypoint index first, then by
        /// proximity to the current target waypoint (smaller remaining distance is further),
        /// with the lower unit id breaking any remaining tie for a deterministic choice.
        /// Returns null when no enemy unit is in range.
        /// </summary>
        private Unit SelectTarget(Structure structure)
        {
            Unit best = null;

            foreach (KeyValuePair<UnitId, Unit> entry in _state.Units)
            {
                Unit unit = entry.Value;

                if (unit == null)
                {
                    continue;
                }

                // Only living enemy units are targetable (Requirement 7.1).
                if (unit.Owner == structure.Owner || unit.Health <= 0f)
                {
                    continue;
                }

                if (!IsInRange(structure, unit))
                {
                    continue;
                }

                if (best == null || IsFurtherProgressed(unit, best))
                {
                    best = unit;
                }
            }

            return best;
        }

        /// <summary>
        /// Returns true when <paramref name="unit"/> lies within the structure's range,
        /// measured as Euclidean distance between the unit and the structure's footprint
        /// center. Uses squared distance to avoid the square root. A non-positive range
        /// admits no units.
        /// </summary>
        private static bool IsInRange(Structure structure, Unit unit)
        {
            if (structure.Range <= 0f)
            {
                return false;
            }

            MapVector center = FootprintCenter(structure.Footprint);
            float sqrDistance = MapVector.SqrDistance(center, unit.Position);
            return sqrDistance <= structure.Range * structure.Range;
        }

        /// <summary>
        /// Returns the center point of a footprint (bottom-left origin plus half its size),
        /// the reference point a defensive structure measures its range from.
        /// </summary>
        private static MapVector FootprintCenter(Footprint footprint)
        {
            return new MapVector(
                footprint.Origin.X + (footprint.Size.X * 0.5f),
                footprint.Origin.Y + (footprint.Size.Y * 0.5f));
        }

        /// <summary>
        /// Returns true when <paramref name="candidate"/> has progressed further along its
        /// path than <paramref name="current"/>. Higher waypoint index wins; on a tie, the
        /// unit nearer its current target waypoint (smaller remaining distance) wins; on a
        /// further tie, the lower unit id wins so selection is deterministic.
        /// </summary>
        private static bool IsFurtherProgressed(Unit candidate, Unit current)
        {
            if (candidate.WaypointIndex != current.WaypointIndex)
            {
                return candidate.WaypointIndex > current.WaypointIndex;
            }

            float candidateRemaining = RemainingDistanceToTargetWaypoint(candidate);
            float currentRemaining = RemainingDistanceToTargetWaypoint(current);

            if (candidateRemaining != currentRemaining)
            {
                return candidateRemaining < currentRemaining;
            }

            return candidate.Id.Value < current.Id.Value;
        }

        /// <summary>
        /// Returns the squared distance from a unit to its current target waypoint, used as
        /// the proximity tie-breaker for equal waypoint indices. Returns positive infinity
        /// when the unit has no path or its waypoint index is out of range, so such units
        /// rank as least-progressed among equals.
        /// </summary>
        private static float RemainingDistanceToTargetWaypoint(Unit unit)
        {
            Path path = unit.AssignedPath;
            if (path == null || !path.HasWaypoints)
            {
                return float.PositiveInfinity;
            }

            int index = unit.WaypointIndex;
            if (index < 0 || index >= path.WaypointCount)
            {
                return float.PositiveInfinity;
            }

            return MapVector.SqrDistance(unit.Position, path.Waypoints[index]);
        }

        /// <summary>
        /// Removes every unit whose health has reached zero or below from the canonical
        /// state after all structures have applied their damage this tick, so a unit at or
        /// below zero health is gone within the same tick (Requirement 7.4). Any defensive
        /// structure still pointing at a removed unit has its stale target cleared.
        /// </summary>
        private void RemoveDeadUnits()
        {
            _removalBuffer.Clear();

            foreach (KeyValuePair<UnitId, Unit> entry in _state.Units)
            {
                Unit unit = entry.Value;
                if (unit == null || unit.Health <= 0f)
                {
                    _removalBuffer.Add(entry.Key);
                }
            }

            if (_removalBuffer.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _removalBuffer.Count; i++)
            {
                _state.Units.Remove(_removalBuffer[i]);
            }

            // Clear any now-dangling targets so no structure references a removed unit.
            foreach (KeyValuePair<StructureId, Structure> entry in _state.Structures)
            {
                Structure structure = entry.Value;
                if (structure?.CurrentTarget != null
                    && !_state.Units.ContainsKey(structure.CurrentTarget.Value))
                {
                    structure.CurrentTarget = null;
                }
            }

            _removalBuffer.Clear();
        }
    }
}
