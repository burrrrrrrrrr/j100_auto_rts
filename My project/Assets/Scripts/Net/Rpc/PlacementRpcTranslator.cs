using MultiplayerRts.Core;

namespace MultiplayerRts.Net
{
    /// <summary>
    /// The raw placement intent a client sends to the server, in pure (Netcode-free) form:
    /// exactly the payload Requirement 4.1 enumerates — the structure <see cref="Type"/>, the
    /// <see cref="TargetLocation"/>, and the <see cref="PlacingPlayer"/>. It is the Core-side
    /// mirror of the unmanaged NGO wire struct that crosses the <c>ServerRpc</c>; keeping a
    /// pure counterpart lets the translation be tested without a Unity runtime, exactly as
    /// task 13.1 kept the snapshot pure alongside the wire records.
    /// </summary>
    public readonly struct PlacementIntent
    {
        /// <summary>The kind of structure the client asked to place.</summary>
        public readonly StructureType Type;

        /// <summary>The target map location for the structure.</summary>
        public readonly MapVector TargetLocation;

        /// <summary>The player that submitted the intent, as resolved from the sender client id.</summary>
        public readonly PlayerId PlacingPlayer;

        public PlacementIntent(StructureType type, MapVector targetLocation, PlayerId placingPlayer)
        {
            Type = type;
            TargetLocation = targetLocation;
            PlacingPlayer = placingPlayer;
        }
    }

    /// <summary>
    /// A pure description of the single targeted response a submitted placement warrants: a
    /// rejection <c>ClientRpc</c> sent only to the submitting player (Requirement 2.4), or
    /// nothing. The translator returns this instead of touching Netcode so the decision — "did
    /// this action get rejected, and if so with what reason and to whom?" — is testable out of
    /// tree. The thin NGO behaviour turns a <see cref="ShouldReject"/> decision into an actual
    /// targeted <c>ClientRpc</c> addressed at the player's client id.
    ///
    /// <para>
    /// An accepted or idempotent-duplicate action produces <see cref="None"/>: there is no
    /// per-client success RPC, because a successful placement is observed by every client
    /// through the persistent replicated structure set from task 13.1 (Requirement 4.6), not a
    /// one-off event. A duplicate (an already-applied re-delivery) also sends no rejection:
    /// the action did succeed once, so rejecting its re-delivery would mislead the client.
    /// </para>
    /// </summary>
    public readonly struct RejectionDecision
    {
        /// <summary>True when a rejection <c>ClientRpc</c> should be sent to <see cref="TargetPlayer"/>.</summary>
        public readonly bool ShouldReject;

        /// <summary>The player to target with the rejection; meaningful only when <see cref="ShouldReject"/>.</summary>
        public readonly PlayerId TargetPlayer;

        /// <summary>The reason to carry in the rejection; meaningful only when <see cref="ShouldReject"/>.</summary>
        public readonly RejectionReason Reason;

        private RejectionDecision(bool shouldReject, PlayerId targetPlayer, RejectionReason reason)
        {
            ShouldReject = shouldReject;
            TargetPlayer = targetPlayer;
            Reason = reason;
        }

        /// <summary>No targeted response (the action was accepted or was an idempotent duplicate).</summary>
        public static readonly RejectionDecision None = new RejectionDecision(false, default, default);

        /// <summary>A rejection to be delivered only to <paramref name="player"/> carrying <paramref name="reason"/>.</summary>
        public static RejectionDecision Reject(PlayerId player, RejectionReason reason)
        {
            return new RejectionDecision(true, player, reason);
        }
    }

    /// <summary>
    /// The pure, Netcode-free core of the client→server placement RPC path (Requirements 4.1,
    /// 2.4). It does the two translations that have nothing to do with the transport and
    /// everything to do with Core, so both are verifiable by the out-of-tree dotnet smoke
    /// check:
    ///
    /// <list type="number">
    /// <item><description>
    /// <b>Intent → action → result.</b> <see cref="Submit"/> turns a <see cref="PlacementIntent"/>
    /// into a <see cref="PlacementAction"/> — allocating a match-unique <c>ActionId</c> via the
    /// injected <see cref="IActionIdAllocator"/> and attaching the resolved
    /// <see cref="PlayerId"/> — and routes it to <see cref="IMatchServer.SubmitAction"/>. All
    /// validation (bounds, build area, overlap, affordability) happens server-side inside Core
    /// (Requirements 4.1, 2.4); this translator intentionally performs none, so it cannot drift
    /// from the canonical rules.
    /// </description></item>
    /// <item><description>
    /// <b>Result → targeted response.</b> <see cref="DecideRejection"/> maps an
    /// <see cref="ActionResult"/> to a <see cref="RejectionDecision"/>: a rejected result
    /// yields a rejection targeted at the submitting player carrying the canonical
    /// <see cref="RejectionReason"/> (Requirement 2.4); accepted and duplicate results yield
    /// <see cref="RejectionDecision.None"/>.
    /// </description></item>
    /// </list>
    ///
    /// <para>
    /// The thin NGO behaviour is left with only transport mechanics: resolve the sender's
    /// client id to a <see cref="PlayerId"/> via the <see cref="ClientIdPlayerMap"/>, call
    /// <see cref="Submit"/>, and — if the returned decision says so — resolve the target
    /// player back to a client id and fire the targeted rejection <c>ClientRpc</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Totality.</b> A null <see cref="ActionResult"/> from the server (defensive) is
    /// treated as a rejection so the client still hears back rather than silently waiting.
    /// </para>
    /// </summary>
    public sealed class PlacementRpcTranslator
    {
        private readonly IMatchServer _server;
        private readonly IActionIdAllocator _actionIds;

        /// <summary>
        /// Constructs the translator over the authoritative <paramref name="server"/> it routes
        /// actions to and the <paramref name="actionIds"/> source it stamps each action with.
        /// Both are required; a null is a construction-time programming error.
        /// </summary>
        public PlacementRpcTranslator(IMatchServer server, IActionIdAllocator actionIds)
        {
            _server = server ?? throw new System.ArgumentNullException(nameof(server));
            _actionIds = actionIds ?? throw new System.ArgumentNullException(nameof(actionIds));
        }

        /// <summary>
        /// Builds a <see cref="PlacementAction"/> from <paramref name="intent"/> (allocating a
        /// fresh <c>ActionId</c>) and submits it to the canonical pipeline, returning the
        /// server's <see cref="ActionResult"/>. Pure with respect to the transport: it performs
        /// no validation of its own — the server validates against canonical state
        /// (Requirements 4.1, 2.4).
        /// </summary>
        public ActionResult Submit(PlacementIntent intent)
        {
            var action = new PlacementAction(
                _actionIds.Next(),
                intent.PlacingPlayer,
                intent.Type,
                intent.TargetLocation);

            return _server.SubmitAction(action);
        }

        /// <summary>
        /// Maps a submission result for <paramref name="placingPlayer"/> to the targeted
        /// response it warrants. A rejected result (or a defensive null) produces a rejection
        /// targeted at that player carrying the canonical reason (Requirement 2.4); an accepted
        /// or duplicate result produces <see cref="RejectionDecision.None"/> (success is
        /// observed via replicated state, Requirement 4.6, and a duplicate's original
        /// application already succeeded).
        /// </summary>
        public static RejectionDecision DecideRejection(ActionResult result, PlayerId placingPlayer)
        {
            // A null result should not happen for a non-null action, but treat it as a
            // rejection so the submitting client always hears back (totality). SubmitAction uses
            // MatchEnded as its generic "couldn't apply" reason, so reuse it here.
            if (result == null)
            {
                return RejectionDecision.Reject(placingPlayer, RejectionReason.MatchEnded);
            }

            // Accepted, or an idempotent re-delivery of an already-applied action: no rejection.
            if (result.Success)
            {
                return RejectionDecision.None;
            }

            // Rejected: carry the canonical reason to the submitting client only. Reason is
            // non-null on a rejected result by ActionResult's contract; default defensively.
            return RejectionDecision.Reject(placingPlayer, result.Reason ?? RejectionReason.MatchEnded);
        }

        /// <summary>
        /// Convenience that runs <see cref="Submit"/> then <see cref="DecideRejection"/> for the
        /// intent's player in one call — the exact sequence the server-side RPC handler needs.
        /// </summary>
        public RejectionDecision SubmitAndDecide(PlacementIntent intent)
        {
            ActionResult result = Submit(intent);
            return DecideRejection(result, intent.PlacingPlayer);
        }
    }
}
