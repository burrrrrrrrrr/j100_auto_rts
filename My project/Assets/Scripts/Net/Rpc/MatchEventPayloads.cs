using MultiplayerRts.Core;

namespace MultiplayerRts.Net
{
    /// <summary>
    /// Pure (Netcode-free) payloads for the two broadcast events this task sends to every
    /// connected client — a unit removal (Requirement 7.5) and the frozen match result
    /// (Requirement 8.5) — together with the small translators that build them from Core
    /// types. These mirror the task-13.1 pattern: a pure, testable payload with a matching
    /// unmanaged NGO wire struct (in <c>NetRpcRecords.cs</c>) that actually crosses the
    /// <c>ClientRpc</c>.
    ///
    /// <para>
    /// <b>Delivery to connected clients only (Requirements 7.6, 8.5).</b> Neither payload
    /// carries any addressing: an NGO <c>ClientRpc</c> with no <c>ClientRpcParams</c> targets
    /// exactly the currently-connected clients, and NGO never delivers to a disconnected one.
    /// So "exclude the absent client and complete the removal without waiting" (Requirement
    /// 7.6) is satisfied by relying on NGO's native broadcast semantics rather than any list
    /// of recipients computed here. A client that was absent for the event does not miss the
    /// effect: unit removals and the match outcome are also part of the persistent replicated
    /// state from task 13.1 (the removed unit simply disappears from the replicated unit set;
    /// the outcome is a persistent NetworkVariable), so a reconnecting client converges to the
    /// current world without needing the one-off event (Requirements 2.6, 8.7). The broadcast
    /// is thus a low-latency "nudge" for present clients, and the replicated state is the
    /// durable source of truth for absent ones.
    /// </para>
    /// </summary>
    public readonly struct UnitRemovalEvent
    {
        /// <summary>The unit that was removed from the match (Requirement 7.5).</summary>
        public readonly UnitId Unit;

        public UnitRemovalEvent(UnitId unit)
        {
            Unit = unit;
        }

        /// <summary>Builds the broadcast payload for a removed unit.</summary>
        public static UnitRemovalEvent For(UnitId unit) => new UnitRemovalEvent(unit);
    }

    /// <summary>
    /// Pure broadcast payload for the concluded match result (Requirement 8.5): the outcome
    /// kind plus optional winner/loser. Carries explicit <see cref="HasWinner"/> /
    /// <see cref="HasLoser"/> flags so the value stays a plain struct (the nullable
    /// <see cref="PlayerId"/>s on <see cref="MatchOutcome"/> become flag+value pairs on the
    /// wire, matching the task-13.1 <c>NetMatchOutcome</c> style).
    /// </summary>
    public readonly struct MatchResultEvent
    {
        public readonly MatchOutcomeKind Kind;
        public readonly bool HasWinner;
        public readonly PlayerId Winner;
        public readonly bool HasLoser;
        public readonly PlayerId Loser;

        public MatchResultEvent(MatchOutcomeKind kind, bool hasWinner, PlayerId winner, bool hasLoser, PlayerId loser)
        {
            Kind = kind;
            HasWinner = hasWinner;
            Winner = winner;
            HasLoser = hasLoser;
            Loser = loser;
        }

        /// <summary>
        /// Builds the broadcast payload from a Core <see cref="MatchOutcome"/>, flattening its
        /// nullable winner/loser into flag+value pairs. A null outcome is treated as a draw so
        /// the payload is always well-formed (totality).
        /// </summary>
        public static MatchResultEvent From(MatchOutcome outcome)
        {
            if (outcome == null)
            {
                return new MatchResultEvent(MatchOutcomeKind.Draw, false, default, false, default);
            }

            return new MatchResultEvent(
                outcome.Kind,
                outcome.Winner.HasValue,
                outcome.Winner ?? default,
                outcome.Loser.HasValue,
                outcome.Loser ?? default);
        }
    }
}
