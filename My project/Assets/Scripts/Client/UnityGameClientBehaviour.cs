using MultiplayerRts.Core;
using MultiplayerRts.Net;
using Unity.Netcode;
using UnityEngine;

namespace MultiplayerRts.Client
{
    /// <summary>
    /// The thin, Unity/Netcode-specific half of the client presentation layer (task 14.1,
    /// Requirements 4.1, 6.5, 2.4, 8.5, 9.3). It owns no game logic: it wires the pure
    /// <see cref="GameClientStateMirror"/> to the live transport by (1) implementing
    /// <see cref="IPlacementSender"/> as a call onto <c>MatchRpcBehaviour</c>'s placement
    /// <c>ServerRpc</c>, (2) subscribing the behaviour's client-side sinks
    /// (<c>OnRejectionReceived</c>, <c>OnMatchResultReceived</c>, <c>OnUnitRemovalReceived</c>)
    /// to the mirror's <see cref="IGameClient"/> callbacks, and (3) forwarding the replicated
    /// <c>NetworkVariable</c>/<c>NetworkList</c> state from <c>MatchReplicationBehaviour</c> to
    /// <see cref="GameClientStateMirror.OnStateReplicated"/> whenever it changes, which drives
    /// the injected renderer (Requirement 6.5). All projection, view-building, and surfacing
    /// logic lives in the pure mirror, verified out-of-tree.
    ///
    /// <para>
    /// <b>Environment note.</b> This file depends on <c>UnityEngine</c> (MonoBehaviour) and the
    /// <c>Unity.Netcode</c> assembly (via the Net behaviours it binds to). It compiles only
    /// after a Unity package restore brings NGO into the project; the pure mirror and view it
    /// drives compile and are smoke-tested independently of Netcode. The concrete sprite
    /// renderer (per-owner color, per-type sprite, numeric displays, missing-sprite fallback)
    /// is task 14.2 and plugs into the same <see cref="IUnitRenderer"/> seam â€” this behaviour
    /// only needs <em>some</em> renderer injected, so the render seam stays clean for it.
    /// </para>
    /// </summary>
    public sealed class UnityGameClientBehaviour : MonoBehaviour, IPlacementSender
    {
        [SerializeField]
        private MatchRpcBehaviour _rpc;

        [SerializeField]
        private MatchReplicationBehaviour _replication;

        /// <summary>
        /// The concrete renderer to drive. Assigned in-editor (task 14.2's renderer component)
        /// or left null for a headless client; the mirror tolerates a null renderer.
        /// </summary>
        private IUnitRenderer _renderer;

        private GameClientStateMirror _mirror;

        /// <summary>The pure client contract, exposed so UI/input code can submit placements.</summary>
        public IGameClient Client => _mirror;

        /// <summary>
        /// Builds the mirror over this behaviour (as the placement sender) and the supplied
        /// <paramref name="renderer"/>, then subscribes the transport sinks. Call after the
        /// Net behaviours are present (e.g. from <c>OnNetworkSpawn</c> wiring or a bootstrap).
        /// The <paramref name="localPlayer"/> is only a payload placeholder; the server
        /// re-derives authority from the authenticated sender.
        /// </summary>
        public void Initialize(IUnitRenderer renderer, PlayerId localPlayer = default)
        {
            _renderer = renderer;
            _mirror = new GameClientStateMirror(this, renderer, localPlayer);
            SubscribeTransport();
            ForwardReplicatedState();
        }

        private void SubscribeTransport()
        {
            if (_rpc != null)
            {
                _rpc.OnRejectionReceived += OnRejectionReceived;
                _rpc.OnMatchResultReceived += OnMatchResultReceived;
                _rpc.OnUnitRemovalReceived += OnUnitRemovalReceived;
            }

            if (_replication != null)
            {
                _replication.Phase.OnValueChanged += OnPhaseChanged;
                _replication.Outcome.OnValueChanged += OnOutcomeChanged;
                _replication.Players.OnListChanged += OnPlayersChanged;
                _replication.Units.OnListChanged += OnUnitsChanged;
                _replication.Structures.OnListChanged += OnStructuresChanged;
            }
        }

        private void OnDestroy()
        {
            if (_rpc != null)
            {
                _rpc.OnRejectionReceived -= OnRejectionReceived;
                _rpc.OnMatchResultReceived -= OnMatchResultReceived;
                _rpc.OnUnitRemovalReceived -= OnUnitRemovalReceived;
            }

            if (_replication != null)
            {
                _replication.Phase.OnValueChanged -= OnPhaseChanged;
                _replication.Outcome.OnValueChanged -= OnOutcomeChanged;
                _replication.Players.OnListChanged -= OnPlayersChanged;
                _replication.Units.OnListChanged -= OnUnitsChanged;
                _replication.Structures.OnListChanged -= OnStructuresChanged;
            }
        }

        // ---- IPlacementSender: client -> server placement (Requirement 4.1) ----

        /// <summary>
        /// Transport realization of the sender seam: converts the pure
        /// <see cref="PlacementIntent"/> into the unmanaged wire struct and fires the placement
        /// <c>ServerRpc</c>. The server ignores the payload's player id and stamps the
        /// authenticated sender.
        /// </summary>
        public void Send(PlacementIntent intent)
        {
            if (_rpc == null)
            {
                return;
            }

            _rpc.SubmitPlacementServerRpc(NetPlacementIntent.From(intent));
        }

        // ---- Transport sinks -> pure mirror callbacks ----

        private void OnRejectionReceived(RejectionReason reason) => _mirror?.OnActionRejected(reason);

        private void OnMatchResultReceived(MatchResultEvent result) => _mirror?.OnMatchResult(ToOutcome(result));

        /// <summary>
        /// A unit-removal broadcast (Requirement 7.5) is a low-latency nudge; the authoritative
        /// effect already shows up as the unit leaving the replicated unit list, which triggers
        /// <see cref="OnUnitsChanged"/> and a re-render. We simply re-forward the current
        /// replicated state so the view reflects the removal immediately.
        /// </summary>
        private void OnUnitRemovalReceived(UnitRemovalEvent removal) => ForwardReplicatedState();

        // ---- Replicated NetworkVariable/NetworkList changes -> re-project the full snapshot ----

        private void OnPhaseChanged(MatchPhase previous, MatchPhase current) => ForwardReplicatedState();

        private void OnOutcomeChanged(NetMatchOutcome previous, NetMatchOutcome current) => ForwardReplicatedState();

        private void OnPlayersChanged(NetworkListEvent<NetPlayer> change) => ForwardReplicatedState();

        private void OnUnitsChanged(NetworkListEvent<NetUnit> change) => ForwardReplicatedState();

        private void OnStructuresChanged(NetworkListEvent<NetStructure> change) => ForwardReplicatedState();

        /// <summary>
        /// Rebuilds a pure <see cref="MatchStateSnapshot"/> from the current replicated
        /// variables and hands it to the mirror, which re-projects the render view and drives
        /// the renderer (Requirement 6.5). Called on any replicated change so the client always
        /// renders the latest reported positions; because the full snapshot is rebuilt each
        /// time, a reconnecting client that receives the full replicated set converges
        /// (Requirement 2.6).
        /// </summary>
        private void ForwardReplicatedState()
        {
            if (_mirror == null || _replication == null)
            {
                return;
            }

            _mirror.OnStateReplicated(BuildSnapshotFromReplication());
        }

        private MatchStateSnapshot BuildSnapshotFromReplication()
        {
            var players = new System.Collections.Generic.List<PlayerSnapshot>(_replication.Players.Count);
            foreach (NetPlayer p in _replication.Players)
            {
                players.Add(p.ToSnapshot());
            }

            var units = new System.Collections.Generic.List<UnitSnapshot>(_replication.Units.Count);
            foreach (NetUnit u in _replication.Units)
            {
                units.Add(u.ToSnapshot());
            }

            var structures = new System.Collections.Generic.List<StructureSnapshot>(_replication.Structures.Count);
            foreach (NetStructure s in _replication.Structures)
            {
                structures.Add(s.ToSnapshot());
            }

            // Revision is server-side bookkeeping not carried on NetworkVariables; the client
            // rebuilds a fresh view on every change, so a monotonically-increasing local tick
            // stamp is unnecessary. Use 0 as a "client-derived" revision marker.
            return new MatchStateSnapshot(0L, _replication.Phase.Value, _replication.Outcome.Value.ToSnapshot(), players, units, structures);
        }

        private static MatchOutcome ToOutcome(MatchResultEvent result)
        {
            switch (result.Kind)
            {
                case MatchOutcomeKind.Winner when result.HasWinner && result.HasLoser:
                    return MatchOutcome.ForWinner(result.Winner, result.Loser);
                case MatchOutcomeKind.NoContest:
                    return MatchOutcome.ForNoContest();
                default:
                    return MatchOutcome.ForDraw();
            }
        }
    }
}

