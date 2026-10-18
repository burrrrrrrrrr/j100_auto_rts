using System.Collections.Generic;
using MultiplayerRts.Core;
using MultiplayerRts.Net;

namespace MultiplayerRts.Client
{
    /// <summary>
    /// The pure, framework-independent <see cref="IGameClient"/> implementation: a client-side
    /// mirror of the authoritative state that drives rendering (Requirements 6.5, 10.x),
    /// forwards placement intent to the server (Requirement 4.1), and surfaces the latest
    /// rejection (Requirement 2.4) and match result (Requirements 8.5, 9.3). It references only
    /// Core and the Net <em>pure</em> types, so it compiles and is verifiable out-of-tree with
    /// <c>dotnet</c> — the Netcode/Unity concerns (the actual <c>ServerRpc</c> and sprite
    /// rendering) are injected behind the <see cref="IPlacementSender"/> and
    /// <see cref="IUnitRenderer"/> seams, mirroring the pure-vs-thin split of tasks 13.1–13.3.
    ///
    /// <list type="bullet">
    /// <item><description>
    /// <b><see cref="SubmitPlacement"/>:</b> builds the <see cref="PlacementIntent"/> payload
    /// and hands it to the injected sender. It does no validation (the server is authoritative)
    /// and supplies a placeholder placing-player id — the server overrides it with the
    /// authenticated sender, so a trustworthy id is unnecessary here.
    /// </description></item>
    /// <item><description>
    /// <b><see cref="OnStateReplicated"/>:</b> projects the snapshot into an immutable
    /// <see cref="ClientRenderView"/> (units keyed by id for in-place position updates) and
    /// invokes the renderer once with the new view. Convergent: feeding the current snapshot in
    /// full brings a reconnecting client fully current (Requirement 2.6). The persistent
    /// outcome carried on the snapshot is preserved on the view (Requirement 8.7).
    /// </description></item>
    /// <item><description>
    /// <b><see cref="OnActionRejected"/> / <see cref="OnMatchResult"/>:</b> store the latest
    /// rejection/outcome and surface them (the rejection is also folded onto the current view
    /// so the presentation layer reads one coherent object). Neither mutates the rendered world.
    /// </description></item>
    /// </list>
    ///
    /// <para>
    /// <b>Totality.</b> A null snapshot resets to the empty view rather than throwing, and the
    /// renderer seam is optional (null renderer = headless mirror, used by the out-of-tree
    /// smoke check), so a missing collaborator never aborts state ingestion.
    /// </para>
    /// </summary>
    public sealed class GameClientStateMirror : IGameClient
    {
        private readonly IPlacementSender _sender;
        private readonly IUnitRenderer _renderer;

        /// <summary>
        /// The placing-player id stamped on outgoing intents. The server ignores it and uses the
        /// authenticated sender id (see <c>NetPlacementIntent</c>), so any value is safe; a
        /// caller that knows its own id may supply it for clarity in logs/tests.
        /// </summary>
        private readonly PlayerId _localPlayer;

        private ClientRenderView _view = ClientRenderView.Empty;

        /// <summary>The current renderable view the mirror exposes (Requirements 6.5, 10.x).</summary>
        public ClientRenderView View => _view;

        /// <summary>The latest surfaced rejection reason, or null if none (Requirement 2.4).</summary>
        public RejectionReason? LastRejection { get; private set; }

        /// <summary>The concluded match outcome once received, or null while in progress (Requirements 8.5, 9.3).</summary>
        public MatchOutcome LastOutcome { get; private set; }

        /// <summary>
        /// Constructs the mirror over the transport <paramref name="sender"/> it forwards
        /// placement intent to and the optional <paramref name="renderer"/> it drives after each
        /// replicated snapshot. The <paramref name="localPlayer"/> is only a payload placeholder
        /// (the server re-derives authority). The sender is required; the renderer may be null
        /// for a headless mirror.
        /// </summary>
        public GameClientStateMirror(IPlacementSender sender, IUnitRenderer renderer = null, PlayerId localPlayer = default)
        {
            _sender = sender ?? throw new System.ArgumentNullException(nameof(sender));
            _renderer = renderer;
            _localPlayer = localPlayer;
        }

        /// <inheritdoc />
        public void SubmitPlacement(StructureType type, MapVector location)
        {
            var intent = new PlacementIntent(type, location, _localPlayer);
            _sender.Send(intent);
        }

        /// <inheritdoc />
        public void OnStateReplicated(MatchStateSnapshot snapshot)
        {
            _view = ProjectView(snapshot, LastRejection);
            _renderer?.Render(_view);
        }

        /// <inheritdoc />
        public void OnActionRejected(RejectionReason reason)
        {
            LastRejection = reason;

            // Fold the rejection onto the current view so the presentation layer reads a single
            // coherent object. A rejection is a client-side notice, not a world change, so the
            // rendered entities are untouched and the renderer is not re-invoked.
            _view = _view.WithRejection(reason);
        }

        /// <inheritdoc />
        public void OnMatchResult(MatchOutcome outcome)
        {
            LastOutcome = outcome;
        }

        /// <summary>
        /// Projects a replicated snapshot into the immutable render view, carrying forward the
        /// surfaced <paramref name="lastRejection"/>. A null snapshot yields the empty view
        /// (totality).
        /// </summary>
        private static ClientRenderView ProjectView(MatchStateSnapshot snapshot, RejectionReason? lastRejection)
        {
            if (snapshot == null)
            {
                return ClientRenderView.Empty.WithRejection(lastRejection);
            }

            var units = new List<RenderUnit>(snapshot.Units.Count);
            for (int i = 0; i < snapshot.Units.Count; i++)
            {
                units.Add(RenderUnit.From(snapshot.Units[i]));
            }

            return new ClientRenderView(
                snapshot.Revision,
                snapshot.Phase,
                snapshot.Outcome,
                snapshot.Players,
                units,
                snapshot.Structures,
                lastRejection);
        }
    }
}
