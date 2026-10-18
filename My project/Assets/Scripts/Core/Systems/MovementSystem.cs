using System;
using System.Collections.Generic;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// Default <see cref="IMovementSystem"/> implementation. Moves each active unit along
    /// its assigned <see cref="Path"/> toward the opponent's base, advancing waypoints as
    /// they are reached, damaging the opponent base on arrival and removing the arrived
    /// unit, and halting dead-end units in place (Requirement 6). Operates in place over
    /// the canonical <see cref="MatchState"/>; a pure C# type with no Unity dependency so
    /// the behavior can be property-tested against an in-memory state.
    /// </summary>
    public sealed class MovementSystem : IMovementSystem
    {
        private readonly MatchState _state;

        public MovementSystem(MatchState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <inheritdoc />
        public void Tick(float deltaSeconds)
        {
            // Non-positive time or no units: nothing to advance.
            if (deltaSeconds <= 0f || _state.Units.Count == 0)
            {
                return;
            }

            // Units that arrive at the opponent base are removed after iteration to avoid
            // mutating the collection while enumerating it.
            List<UnitId> arrived = null;

            foreach (KeyValuePair<UnitId, Unit> entry in _state.Units)
            {
                Unit unit = entry.Value;

                // Held units (waiting for a free spawn point or a path) do not move
                // (Requirements 5.5, 5.6), and a unit without a path cannot advance.
                if (unit == null || unit.IsHeld || unit.AssignedPath == null)
                {
                    continue;
                }

                if (AdvanceUnit(unit, deltaSeconds))
                {
                    if (arrived == null)
                    {
                        arrived = new List<UnitId>();
                    }

                    arrived.Add(entry.Key);
                }
            }

            if (arrived != null)
            {
                foreach (UnitId id in arrived)
                {
                    _state.Units.Remove(id);
                }
            }
        }

        /// <summary>
        /// Advances a single unit along its path by <paramref name="deltaSeconds"/> worth of
        /// travel. Returns true when the unit reached the opponent base and should be removed
        /// from the match (Requirement 6.3); false otherwise (still travelling, or halted on a
        /// dead-end path per Requirement 6.4).
        /// </summary>
        private bool AdvanceUnit(Unit unit, float deltaSeconds)
        {
            Path path = unit.AssignedPath;
            IReadOnlyList<MapVector> waypoints = path.Waypoints;

            // A path with no waypoints is a dead end: the unit halts with no base damage
            // (Requirement 6.4).
            if (waypoints.Count == 0)
            {
                return false;
            }

            // Clamp a stale or out-of-range index into [0, last] so a degenerate index cannot
            // index out of bounds; the unit then resolves against the final waypoint below.
            if (unit.WaypointIndex < 0)
            {
                unit.WaypointIndex = 0;
            }
            else if (unit.WaypointIndex > waypoints.Count - 1)
            {
                unit.WaypointIndex = waypoints.Count - 1;
            }

            // Travel budget for this tick, in map units. Negative speed is treated as zero so
            // the unit does not move backward, but it can still be counted as having reached a
            // waypoint it is already within threshold of.
            float remaining = unit.MovementSpeed * deltaSeconds;
            if (remaining < 0f)
            {
                remaining = 0f;
            }

            // Consume the travel budget across as many waypoints as the unit reaches this tick.
            while (true)
            {
                MapVector target = waypoints[unit.WaypointIndex];
                float distanceToTarget = MapVector.Distance(unit.Position, target);

                if (distanceToTarget <= SimulationConstants.WaypointReachThreshold)
                {
                    // Current waypoint reached (Requirement 6.2). Snap to it so progress is
                    // exact, then either arrive, advance, or halt.
                    unit.Position = target;

                    if (path.IsFinalIndex(unit.WaypointIndex))
                    {
                        // Final waypoint: arrival at the opponent base damages it and removes
                        // the unit (Requirement 6.3); a dead-end final waypoint halts the unit
                        // without damage (Requirement 6.4).
                        return ResolveFinalWaypoint(unit, path);
                    }

                    // Advance toward the next waypoint and keep spending any remaining budget.
                    unit.WaypointIndex++;
                    continue;
                }

                // Not yet within threshold: move toward the target, clamped so the unit never
                // overshoots it this tick (Requirement 6.1).
                if (remaining <= 0f)
                {
                    return false;
                }

                if (remaining >= distanceToTarget)
                {
                    // Enough budget to reach the waypoint exactly; snap and let the next loop
                    // iteration treat it as reached (and spend any leftover budget).
                    remaining -= distanceToTarget;
                    unit.Position = target;
                    continue;
                }

                // Partial step toward the target.
                MapVector direction = (target - unit.Position).Normalized();
                unit.Position = unit.Position + (direction * remaining);
                return false;
            }
        }

        /// <summary>
        /// Resolves a unit sitting on the final waypoint of its path. When that waypoint is
        /// the opponent's base, applies the unit's damage to the base and signals removal
        /// (Requirement 6.3). When it is not at the opponent base (dead-end path), the unit
        /// halts in place with no base damage (Requirement 6.4).
        /// </summary>
        private bool ResolveFinalWaypoint(Unit unit, Path path)
        {
            if (!IsAtOpponentBase(path))
            {
                // Dead-end path: halt, no damage (Requirement 6.4).
                return false;
            }

            PlayerState opponent = ResolveBaseOwner(unit, path.TargetBase);
            if (opponent == null)
            {
                // No base to damage (degenerate target); halt rather than throw so the tick
                // stays total over its domain.
                return false;
            }

            float newHealth = opponent.BaseHealth - unit.Damage;
            opponent.BaseHealth = newHealth < 0f ? 0f : newHealth;
            return true;
        }

        /// <summary>
        /// Returns true when the path's final waypoint coincides with the opponent base the
        /// path targets (within the waypoint-reach threshold), i.e. the path actually ends at
        /// the base rather than dead-ending elsewhere.
        /// </summary>
        private static bool IsAtOpponentBase(Path path)
        {
            IReadOnlyList<MapVector> waypoints = path.Waypoints;
            MapVector finalWaypoint = waypoints[waypoints.Count - 1];
            return MapVector.Distance(finalWaypoint, path.TargetBase)
                <= SimulationConstants.WaypointReachThreshold;
        }

        /// <summary>
        /// Finds the player state whose base the arriving unit damages. Prefers the owner's
        /// opponent in the two-player match; when that cannot be resolved, falls back to the
        /// player whose base location matches the path's target base. Returns null when no
        /// such player exists.
        /// </summary>
        private PlayerState ResolveBaseOwner(Unit unit, MapVector targetBase)
        {
            PlayerState opponent = _state.GetOpponent(unit.Owner);
            if (opponent != null)
            {
                return opponent;
            }

            foreach (KeyValuePair<PlayerId, PlayerState> entry in _state.Players)
            {
                if (entry.Key == unit.Owner)
                {
                    continue;
                }

                if (MapVector.Distance(entry.Value.BaseLocation, targetBase)
                    <= SimulationConstants.WaypointReachThreshold)
                {
                    return entry.Value;
                }
            }

            return null;
        }
    }
}
