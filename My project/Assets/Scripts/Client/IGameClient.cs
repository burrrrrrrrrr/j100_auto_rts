using MultiplayerRts.Core;
using MultiplayerRts.Net;

namespace MultiplayerRts.Client
{
    /// <summary>
    /// The presentation-only client contract from design.md ("GameClient (presentation
    /// only)"). A <c>Game_Client</c> never owns or mutates canonical state: it captures the
    /// player's placement intent and forwards it to the authoritative server (Requirement
    /// 4.1), mirrors the replicated <see cref="MatchStateSnapshot"/> so it can render units at
    /// their reported positions (Requirements 6.5, 10.x), and surfaces the two one-off events
    /// the server addresses to it — an action rejection (Requirement 2.4) and the concluded
    /// match result (Requirements 8.5, 9.3).
    ///
    /// <para>
    /// <b>Pure-vs-thin split (mirrors tasks 13.1–13.3).</b> This interface and its pure
    /// implementation (<see cref="GameClientStateMirror"/>) reference only the Core and Net
    /// <em>pure</em> types, so they compile and are verifiable out-of-tree with <c>dotnet</c>
    /// without a Unity runtime. The design's signature uses <c>Vector2</c> for the placement
    /// location; to keep this layer pure we use the project's own <see cref="MapVector"/>
    /// instead of <c>UnityEngine.Vector2</c>, exactly as Core does. The concrete Unity/NGO
    /// wiring — forwarding <see cref="SubmitPlacement"/> onto <c>MatchRpcBehaviour</c>'s
    /// placement <c>ServerRpc</c>, subscribing the client-side rejection/result sinks, and
    /// driving a sprite renderer — lives in the thin behaviour that compiles only after a
    /// Unity package restore.
    /// </para>
    /// </summary>
    public interface IGameClient
    {
        /// <summary>
        /// Captures the player's intent to place a structure of <paramref name="type"/> at
        /// <paramref name="location"/> and submits it to the authoritative server (Requirement
        /// 4.1). The pure mirror builds the placement payload (<see cref="PlacementIntent"/>)
        /// and hands it to an injected sender seam; the concrete sender turns it into the
        /// <c>SubmitPlacementServerRpc</c> call. The client performs no validation of its own —
        /// the server validates against canonical state and may answer with
        /// <see cref="OnActionRejected"/>.
        /// </summary>
        void SubmitPlacement(StructureType type, MapVector location);

        /// <summary>
        /// Ingests a newly replicated canonical-state <paramref name="snapshot"/> (Requirement
        /// 2.5) and updates the renderable view so units are drawn at their reported positions
        /// within the latency budget (Requirements 6.5, 10.x). Idempotent and convergent: a
        /// reconnecting client that receives the current snapshot in full converges to it
        /// (Requirement 2.6).
        /// </summary>
        void OnStateReplicated(MatchStateSnapshot snapshot);

        /// <summary>
        /// Surfaces a server rejection of this client's submitted action (Requirements 2.4,
        /// 3.4, 4.2–4.4). The client records the latest reason for the presentation layer to
        /// show (e.g. a placement preview flashing invalid) and takes no state-changing action.
        /// </summary>
        void OnActionRejected(RejectionReason reason);

        /// <summary>
        /// Surfaces the concluded match result (Requirements 8.5, 9.3). The client records the
        /// outcome so the presentation layer can display win/lose/draw; the persistent
        /// replicated outcome in the next <see cref="OnStateReplicated"/> remains the durable
        /// source of truth for a client that missed the one-off event.
        /// </summary>
        void OnMatchResult(MatchOutcome outcome);
    }
}
