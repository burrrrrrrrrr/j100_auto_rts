namespace MultiplayerRts.Core
{
    /// <summary>
    /// Top-level authoritative coordinator for a single <c>Match</c>. Owns the canonical
    /// <see cref="MatchState"/> (the single source of truth, Requirement 2.1) and the
    /// fixed-tick simulation loop, and is the entry point for validating and applying
    /// player actions (Requirements 2.3, 2.4, 8.6). Framework-independent (no Unity or
    /// Netcode dependency) so the action pipeline can be property-tested in isolation
    /// against an in-memory state and a mocked transport.
    /// </summary>
    public interface IMatchServer
    {
        /// <summary>
        /// The canonical match state this server owns and resolves game logic against
        /// (Requirement 2.1).
        /// </summary>
        MatchState State { get; }

        /// <summary>
        /// Advances the fixed-step simulation by <paramref name="deltaSeconds"/>, driving the
        /// ordered Economy -> Spawn -> Movement -> Combat -> Victory pipeline and marking
        /// changed canonical state for replication (Requirements 2.1, 6.1). Implemented by
        /// task 10.1.
        /// </summary>
        void Tick(float deltaSeconds);

        /// <summary>
        /// Validates <paramref name="action"/> against the current canonical state and, only
        /// when valid, applies it exactly once (Requirements 2.3, 2.4). A re-delivered action
        /// whose id was already applied makes no additional state change and reports a
        /// duplicate (Requirement 2.3). While the match is ended, every action is rejected and
        /// the frozen result/state is left unchanged (Requirement 8.6).
        /// </summary>
        /// <param name="action">The player action to validate and (if valid) apply.</param>
        /// <returns>
        /// <see cref="ActionResult.Accepted"/> when applied, <see cref="ActionResult.Duplicate"/>
        /// for an already-applied re-delivery, or <see cref="ActionResult.Rejected"/> with a
        /// <see cref="RejectionReason"/> when invalid.
        /// </returns>
        ActionResult SubmitAction(PlayerAction action);

        /// <summary>
        /// Handles a client connection for <paramref name="id"/> (Requirement 9.4, 2.6).
        /// Wired by the Net tasks (task 13.x).
        /// </summary>
        void OnClientConnected(PlayerId id);

        /// <summary>
        /// Handles a client disconnection for <paramref name="id"/> (Requirement 9). Wired by
        /// the Net tasks (task 13.x).
        /// </summary>
        void OnClientDisconnected(PlayerId id);
    }
}
