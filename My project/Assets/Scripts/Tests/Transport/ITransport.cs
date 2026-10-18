using MultiplayerRts.Core;

namespace MultiplayerRts.Tests.Transport
{
    /// <summary>
    /// A framework-independent abstraction over the client&lt;-&gt;server message boundary,
    /// standing in for the Netcode for GameObjects RPC/replication layer (design.md:
    /// "the simulation core is tested against an in-memory state and mocked transport so
    /// properties run cheaply without real network or AWS/Unity runtime costs").
    /// <para/>
    /// The interface deliberately mirrors only the message shapes the canonical simulation
    /// produces and consumes, so property tests for later tasks (the action pipeline,
    /// removal notifications, match-result delivery, reconnection convergence) can assert
    /// against recorded traffic without a real network. The single implementation used by
    /// tests is <see cref="MockTransport"/>.
    /// </summary>
    public interface ITransport
    {
        /// <summary>
        /// Client → server: submit a player action for canonical validation and
        /// application (Requirements 2.3, 4.1). Mirrors the placement <c>ServerRpc</c>.
        /// </summary>
        void SubmitAction(PlayerAction action);

        /// <summary>
        /// Server → a single client: the typed result of a submitted action, including a
        /// targeted rejection reason (Requirements 2.4, 4.1).
        /// </summary>
        void SendActionResult(PlayerId recipient, uint actionId, ActionResult result);

        /// <summary>
        /// Server → connected clients: a snapshot of the canonical state used by
        /// reconnecting/late clients to converge (Requirements 2.5, 2.6, 8.7). The mock
        /// records snapshots per recipient so convergence can be asserted.
        /// </summary>
        void ReplicateState(PlayerId recipient, MatchState state);

        /// <summary>
        /// Server → connected clients only: a unit was removed from the simulation
        /// (Requirements 7.5, 7.6). Disconnected recipients must not receive this.
        /// </summary>
        void NotifyUnitRemoved(PlayerId recipient, UnitId unit);

        /// <summary>
        /// Server → a client: the frozen match result (Requirements 8.5, 8.7). Resent to a
        /// client that reconnects within the match session.
        /// </summary>
        void SendMatchResult(PlayerId recipient, MatchOutcome outcome);
    }
}
