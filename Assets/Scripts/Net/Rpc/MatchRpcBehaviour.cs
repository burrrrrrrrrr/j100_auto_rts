using System;
using MultiplayerRts.Core;
using Unity.Netcode;

namespace MultiplayerRts.Net
{
    /// <summary>
    /// The thin, Netcode-for-GameObjects half of the player-action RPC boundary (task 13.2,
    /// Requirements 4.1, 2.4, 7.5, 7.6, 8.5). It owns the <c>ServerRpc</c> that receives a
    /// client's placement intent and the <c>ClientRpc</c>s that send a targeted rejection to
    /// the submitter and broadcast unit removals and the match result to connected clients.
    /// It carries no logic of its own: every translation lives in the pure
    /// <see cref="PlacementRpcTranslator"/>, <see cref="ClientIdPlayerMap"/>, and the
    /// <c>MatchEventPayloads</c>/<c>NetRpcRecords</c> converters, all of which are verified by
    /// the out-of-tree dotnet smoke check without a Unity runtime.
    ///
    /// <para>
    /// <b>Split, mirroring task 13.1.</b> This behaviour only moves data across the transport:
    /// on a <c>ServerRpc</c> it resolves the authenticated sender client id to a
    /// <see cref="PlayerId"/>, hands the pure translator a <see cref="PlacementIntent"/>, and
    /// turns the returned <see cref="RejectionDecision"/> into an addressed <c>ClientRpc</c> or
    /// nothing. On a removal/result event it converts the pure payload to its wire struct and
    /// fires an unaddressed broadcast <c>ClientRpc</c>. No validation, no id arithmetic, no
    /// reason mapping happens here.
    /// </para>
    ///
    /// <para>
    /// <b>Delivery semantics (Requirements 7.6, 8.5).</b> Broadcast <c>ClientRpc</c>s carry no
    /// <c>ClientRpcParams</c>, so NGO delivers them to exactly the currently-connected clients
    /// and silently skips disconnected ones — satisfying "exclude the absent client and
    /// complete the removal without waiting" (7.6) without this layer enumerating recipients.
    /// A client that was absent reconciles from the persistent replicated state in task 13.1
    /// (the removed unit is gone from the replicated unit set; the outcome is a persistent
    /// NetworkVariable), Requirements 2.6, 8.7.
    /// </para>
    ///
    /// <para>
    /// <b>Environment note.</b> This file depends on the <c>Unity.Netcode</c> assembly and the
    /// NGO source-generated RPC machinery; it compiles only after a Unity package restore. The
    /// pure types it delegates to compile and are tested independently of Netcode.
    /// </para>
    /// </summary>
    public sealed class MatchRpcBehaviour : NetworkBehaviour
    {
        /// <summary>
        /// Pure id bridge between transport client ids and Core <see cref="PlayerId"/>s. Server
        /// instance populates it from connection callbacks; see <see cref="BindClient"/>.
        /// </summary>
        private readonly ClientIdPlayerMap _idMap = new ClientIdPlayerMap();

        /// <summary>Pure translation/routing core. Null until <see cref="BindServer"/> on the server.</summary>
        private PlacementRpcTranslator _translator;

        /// <summary>
        /// Optional client-side sink invoked when a rejection arrives, so the presentation layer
        /// (task 14.1 <c>OnActionRejected</c>) can surface it. Left null when unused.
        /// </summary>
        public Action<RejectionReason> OnRejectionReceived;

        /// <summary>Optional client-side sink for a unit-removal broadcast (task 14 rendering).</summary>
        public Action<UnitRemovalEvent> OnUnitRemovalReceived;

        /// <summary>Optional client-side sink for the match-result broadcast (task 14.1 <c>OnMatchResult</c>).</summary>
        public Action<MatchResultEvent> OnMatchResultReceived;

        /// <summary>
        /// Binds the server-side translation core over the authoritative match server. Call once
        /// on the server after the match is created. The action-id source defaults to a
        /// sequential allocator but can be supplied for deterministic tests.
        /// </summary>
        public void BindServer(IMatchServer server, IActionIdAllocator actionIds = null)
        {
            _translator = new PlacementRpcTranslator(server, actionIds ?? new SequentialActionIdAllocator());
        }

        /// <summary>Associates a connected transport client id with its admitted <see cref="PlayerId"/>.</summary>
        public void BindClient(ulong clientId, PlayerId playerId) => _idMap.Bind(clientId, playerId);

        /// <summary>Drops the id pairing for a disconnected transport client.</summary>
        public void UnbindClient(ulong clientId) => _idMap.UnbindClient(clientId);

        // ---- Client -> Server: placement intent (Requirement 4.1) ----

        /// <summary>
        /// Receives a client's placement intent (Requirement 4.1). NGO stamps the caller's
        /// authenticated client id into <paramref name="rpcParams"/>; the server resolves it to
        /// a <see cref="PlayerId"/> (ignoring any player id the client put in the payload, so a
        /// client cannot act as another player), submits the intent through the pure translator,
        /// and — only when the result is a rejection — sends a targeted rejection back to just
        /// that caller (Requirement 2.4). Validation is entirely server-side in Core.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void SubmitPlacementServerRpc(NetPlacementIntent payload, ServerRpcParams rpcParams = default)
        {
            if (_translator == null)
            {
                return;
            }

            ulong senderClientId = rpcParams.Receive.SenderClientId;

            // Unknown sender (connected to transport but not admitted to the match): drop rather
            // than forge an action for a non-player.
            if (!_idMap.TryGetPlayer(senderClientId, out PlayerId senderPlayer))
            {
                return;
            }

            PlacementIntent intent = payload.ToIntent(senderPlayer);
            RejectionDecision decision = _translator.SubmitAndDecide(intent);

            if (!decision.ShouldReject)
            {
                return;
            }

            // Resolve the rejected player back to a live client id to target the rejection at
            // only them. If they have no live client id (mid-disconnect), skip: they reconcile
            // from replicated state on reconnect.
            if (!_idMap.TryGetClient(decision.TargetPlayer, out ulong targetClientId))
            {
                return;
            }

            SendRejection(targetClientId, decision.Reason);
        }

        /// <summary>
        /// Sends a rejection <c>ClientRpc</c> addressed to exactly one client via
        /// <see cref="ClientRpcParams"/> (Requirement 2.4). Allocates a one-element target array
        /// for the single recipient.
        /// </summary>
        private void SendRejection(ulong targetClientId, RejectionReason reason)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { targetClientId },
                },
            };

            PlacementRejectedClientRpc(NetRejection.From(reason), clientRpcParams);
        }

        // ---- Server -> Client: targeted rejection (Requirement 2.4) ----

        /// <summary>
        /// Delivers a rejection to the single targeted client (addressed by the
        /// <see cref="ClientRpcParams"/> the server passes). Forwards the reason to
        /// <see cref="OnRejectionReceived"/> for the presentation layer.
        /// </summary>
        [ClientRpc]
        private void PlacementRejectedClientRpc(NetRejection rejection, ClientRpcParams rpcParams = default)
        {
            OnRejectionReceived?.Invoke(rejection.ToReason());
        }

        // ---- Server -> Client: broadcasts to connected clients only (Requirements 7.5, 7.6, 8.5) ----

        /// <summary>
        /// Broadcasts a unit removal to all connected clients (Requirement 7.5). Server entry
        /// point: converts the pure event to its wire struct and fires the unaddressed
        /// broadcast, which NGO delivers only to currently-connected clients (Requirement 7.6).
        /// </summary>
        public void BroadcastUnitRemoval(UnitRemovalEvent removal)
        {
            if (!IsServer)
            {
                return;
            }

            UnitRemovedClientRpc(NetUnitRemoval.From(removal));
        }

        [ClientRpc]
        private void UnitRemovedClientRpc(NetUnitRemoval removal)
        {
            OnUnitRemovalReceived?.Invoke(removal.ToEvent());
        }

        /// <summary>
        /// Broadcasts the concluded match result to all connected clients (Requirement 8.5). A
        /// disconnected client is skipped by NGO and converges to the outcome via the persistent
        /// replicated NetworkVariable on reconnect (Requirement 8.7).
        /// </summary>
        public void BroadcastMatchResult(MatchResultEvent result)
        {
            if (!IsServer)
            {
                return;
            }

            MatchResultClientRpc(NetMatchResult.From(result));
        }

        [ClientRpc]
        private void MatchResultClientRpc(NetMatchResult result)
        {
            OnMatchResultReceived?.Invoke(result.ToEvent());
        }

        /// <summary>
        /// Resends the concluded match result to a single (re)connecting client (task 13.3,
        /// Requirements 8.7, 9.3). A client that was absent when the result was broadcast — e.g.
        /// mid-disconnect — learns the outcome the moment it reconnects within the same match
        /// session via this targeted <c>ClientRpc</c>, complementing the persistent replicated
        /// outcome (task 13.1) with an immediate low-latency hand-off. Server entry point:
        /// addresses the same <see cref="MatchResultClientRpc"/> at exactly the reconnecting
        /// client via <see cref="ClientRpcParams"/>.
        /// </summary>
        public void SendMatchResultTo(ulong targetClientId, MatchResultEvent result)
        {
            if (!IsServer)
            {
                return;
            }

            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { targetClientId },
                },
            };

            TargetedMatchResultClientRpc(NetMatchResult.From(result), clientRpcParams);
        }

        /// <summary>
        /// Delivers the retained match result to the single targeted client (addressed by the
        /// <see cref="ClientRpcParams"/> the server passes). Forwards to the same
        /// <see cref="OnMatchResultReceived"/> sink as the broadcast so the presentation layer
        /// handles a resend identically to the original result.
        /// </summary>
        [ClientRpc]
        private void TargetedMatchResultClientRpc(NetMatchResult result, ClientRpcParams rpcParams = default)
        {
            OnMatchResultReceived?.Invoke(result.ToEvent());
        }
    }
}
