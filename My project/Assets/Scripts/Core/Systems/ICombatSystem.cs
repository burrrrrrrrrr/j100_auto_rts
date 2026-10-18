namespace MultiplayerRts.Core
{
    /// <summary>
    /// Resolves combat between active <c>Defensive_Structure</c>s and enemy <c>Unit</c>s
    /// over the canonical <see cref="MatchState"/> (Requirement 7). Framework-independent
    /// (no Unity or Netcode dependency) so combat targeting, interval damage, and removal
    /// can be property-tested in isolation against an in-memory state.
    /// </summary>
    public interface ICombatSystem
    {
        /// <summary>
        /// Advances combat by <paramref name="deltaSeconds"/>. For each active defensive
        /// structure: selects the single enemy unit that has progressed furthest along its
        /// path and is within range as the target (Requirement 7.1); applies the structure's
        /// damage to that target once per attack interval while the target remains in range
        /// (Requirement 7.2); applies no damage but retains the accumulated interval timing
        /// when no enemy is in range (Requirement 7.3). Any unit whose health reaches zero
        /// or below from applied damage is removed from the match within the same tick
        /// (Requirement 7.4).
        /// </summary>
        /// <param name="deltaSeconds">Elapsed simulation time for this step, in seconds.</param>
        void Tick(float deltaSeconds);
    }
}
