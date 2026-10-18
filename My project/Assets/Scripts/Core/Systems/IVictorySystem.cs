namespace MultiplayerRts.Core
{
    /// <summary>
    /// Resolves when and how a <c>Match</c> ends over the canonical
    /// <see cref="MatchState"/> (Requirements 8, 9). Framework-independent (no Unity or
    /// Netcode dependency) so victory and disconnect resolution can be property-tested in
    /// isolation against an in-memory state.
    /// </summary>
    public interface IVictorySystem
    {
        /// <summary>
        /// Evaluates the base-health end conditions against the canonical state. When a
        /// player's base health has reached zero the match transitions to
        /// <see cref="MatchPhase.Ended"/> and the produced <see cref="MatchOutcome"/> is
        /// recorded on <see cref="MatchState.Outcome"/> and returned:
        /// <list type="bullet">
        /// <item>Exactly one base at zero health ends the match with the survivor as the
        /// winner and the zero-health player as the loser (Requirements 8.1, 8.2, 8.3).</item>
        /// <item>Both bases at zero health in the same evaluation ends the match as a draw
        /// with no winner and no loser (Requirement 8.4).</item>
        /// </list>
        /// Returns null and leaves the state unchanged when no end condition is met. Once
        /// the match has already ended, the frozen outcome is returned unchanged
        /// (Requirement 8.6).
        /// </summary>
        /// <returns>The match outcome when an end condition is met; otherwise null.</returns>
        MatchOutcome Evaluate();

        /// <summary>
        /// Resolves the match when a player's 30-second reconnection window expires without
        /// a reconnect (Requirement 9.1). If exactly one player remains connected, that
        /// player is declared the winner and the timed-out player the loser
        /// (Requirement 9.2); if both players are disconnected, the match ends with no
        /// declared winner (Requirement 9.5). The resulting outcome is recorded on
        /// <see cref="MatchState.Outcome"/> and the match transitions to
        /// <see cref="MatchPhase.Ended"/>. A no-op once the match has already ended so the
        /// recorded result stays frozen (Requirement 8.6).
        /// </summary>
        /// <param name="id">The player whose reconnection window expired.</param>
        void OnDisconnectTimeout(PlayerId id);
    }
}
