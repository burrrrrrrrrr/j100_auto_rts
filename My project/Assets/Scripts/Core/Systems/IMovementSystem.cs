namespace MultiplayerRts.Core
{
    /// <summary>
    /// Advances active <c>Units</c> along their assigned <see cref="Path"/> toward the
    /// opponent's <c>Base</c> over the canonical <see cref="MatchState"/> (Requirement 6).
    /// Framework-independent (no Unity or Netcode dependency) so movement can be
    /// property-tested in isolation against an in-memory state.
    /// </summary>
    public interface IMovementSystem
    {
        /// <summary>
        /// Advances every active (non-held) unit with an assigned path by
        /// <paramref name="deltaSeconds"/>:
        /// <list type="bullet">
        /// <item>Moves toward the current target waypoint at the unit's movement speed,
        /// clamped so it never overshoots the waypoint (Requirement 6.1).</item>
        /// <item>Treats a waypoint as reached once the unit is within
        /// <see cref="SimulationConstants.WaypointReachThreshold"/> map units and advances
        /// the waypoint index (Requirement 6.2).</item>
        /// <item>On reaching the final waypoint at the opponent base, applies the unit's
        /// damage to that base's health and removes the unit from the match
        /// (Requirement 6.3).</item>
        /// <item>Halts a unit in place without applying base damage when it has no further
        /// waypoints and is not at the opponent base (dead-end path, Requirement 6.4).</item>
        /// </list>
        /// Total over its valid domain: units that are held, pathless, or on degenerate
        /// paths are skipped or halted rather than throwing, so one bad unit cannot abort
        /// the tick for the rest.
        /// </summary>
        /// <param name="deltaSeconds">Elapsed simulation time for this step, in seconds.</param>
        void Tick(float deltaSeconds);
    }
}
