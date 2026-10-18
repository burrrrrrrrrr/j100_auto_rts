using MultiplayerRts.Core;
using Unity.Netcode;

namespace MultiplayerRts.Net
{
    /// <summary>
    /// The thin, Netcode-for-GameObjects half of disconnection pause/resume and match-result
    /// delivery (task 13.3, Requirements 9.3, 9.4, 8.7). It carries no state-machine logic of
    /// its own: the pause/resume + 30-second-window behavior lives in the pure
    /// <see cref="DisconnectionCoordinator"/> and the retain-and-resend behavior in the pure
    /// <see cref="MatchResultRetainer"/>, both of which are verified by the out-of-tree dotnet
    /// smoke check without a Unity runtime. This behaviour only moves data across the
    /// transport boundary:
    ///
    /// <list type="bullet">
    /// <item><description>
    /// <b>Connection callbacks.</b> On the server it subscribes to NGO's
    /// <see cref="NetworkManager.OnClientConnectedCallback"/> /
    /// <see cref="NetworkManager.OnClientDisconnectCallback"/>, resolves each transport client
    /// id to a Core <see cref="PlayerId"/> via the shared <see cref="ClientIdPlayerMap"/>, and
    /// drives the pure coordinator (pause on disconnect; resume on timely reconnect preserving
    /// state, Requirement 9.4).
    /// </description></item>
    /// <item><description>
    /// <b>Window countdown.</b> Each server tick it advances the coordinator's 30-second timer;
    /// on expiry the coordinator resolves the match via <see cref="IVictorySystem.OnDisconnectTimeout"/>
    /// (Requirements 9.1, 9.2, 9.5).
    /// </description></item>
    /// <item><description>
    /// <b>Result retain + targeted resend.</b> Each server tick it offers the canonical outcome
    /// to the retainer so a concluded result is frozen (Requirement 8.5), and when a client
    /// reconnects within the session it resends the retained result to just that client via a
    /// targeted result <c>ClientRpc</c> on the shared <see cref="MatchRpcBehaviour"/>
    /// (Requirements 8.7, 9.3). The persistent replicated outcome from task 13.1 remains the
    /// durable backstop; this resend is the immediate, low-latency hand-off.
    /// </description></item>
    /// </list>
    ///
    /// <para>
    /// <b>Environment note.</b> This file depends on the <c>Unity.Netcode</c> assembly and
    /// compiles only after a Unity package restore. The pure
    /// <see cref="DisconnectionCoordinator"/> / <see cref="MatchResultRetainer"/> it delegates
    /// to compile and are tested independently of Netcode.
    /// </para>
    /// </summary>
    public sealed class MatchReconnectionBehaviour : NetworkBehaviour
    {
        /// <summary>Shared id bridge between transport client ids and Core player ids (task 13.2).</summary>
        private ClientIdPlayerMap _idMap;

        /// <summary>Shared RPC behaviour that owns the match-result broadcast/targeted-resend path.</summary>
        private MatchRpcBehaviour _rpc;

        /// <summary>Pure pause/resume + reconnection-timer state machine. Null until <see cref="BindServer"/>.</summary>
        private DisconnectionCoordinator _coordinator;

        /// <summary>Pure retain-and-resend memory for the concluded match result. Null until <see cref="BindServer"/>.</summary>
        private MatchResultRetainer _retainer;

        /// <summary>Canonical state the coordinator pauses/resumes and the retainer reads the outcome from.</summary>
        private MatchState _state;

        private bool _subscribed;

        /// <summary>
        /// Binds the server-side pieces this behaviour drives. Call once on the server after the
        /// match is created, passing the authoritative <paramref name="state"/>, the
        /// <paramref name="victory"/> system used to resolve a timeout, the shared
        /// <paramref name="idMap"/> populated from connection callbacks, and the shared
        /// <paramref name="rpc"/> behaviour used to resend the result. Subscribes to the NGO
        /// connection callbacks so disconnects/reconnects drive the pure coordinator.
        /// </summary>
        public void BindServer(
            MatchState state,
            IVictorySystem victory,
            ClientIdPlayerMap idMap,
            MatchRpcBehaviour rpc)
        {
            _state = state;
            _idMap = idMap;
            _rpc = rpc;
            _coordinator = new DisconnectionCoordinator(state, victory);
            _retainer = new MatchResultRetainer();

            if (IsServer && !_subscribed && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback += HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
                _subscribed = true;
            }
        }

        /// <inheritdoc />
        public override void OnNetworkDespawn()
        {
            if (_subscribed && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                _subscribed = false;
            }

            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Server per-tick entry point, called from the match loop after
        /// <see cref="MatchServer.Tick"/>. Advances the reconnection window(s) by
        /// <paramref name="deltaSeconds"/> (resolving a timeout on expiry) and offers the current
        /// canonical outcome to the retainer so a concluded result is frozen for resend. Safe to
        /// call every tick: both operations no-op when there is nothing to do.
        /// </summary>
        public void ServerReconnectionTick(float deltaSeconds)
        {
            if (!IsServer || _coordinator == null)
            {
                return;
            }

            _coordinator.Tick(deltaSeconds);
            _retainer.CaptureFrom(_state);
        }

        private void HandleClientConnected(ulong clientId)
        {
            if (!IsServer || _coordinator == null)
            {
                return;
            }

            // A connection only matters here if it maps to a player already admitted to this
            // match (a reconnection). A brand-new client with no mapping is the lobby/admission
            // path's concern, not reconnection.
            if (_idMap == null || !_idMap.TryGetPlayer(clientId, out PlayerId playerId))
            {
                return;
            }

            _coordinator.OnReconnected(playerId);

            // Whether or not this reconnect resumed the match, a client reconnecting within the
            // session must receive the retained result if the match already ended (Requirement
            // 8.7) or was resolved by disconnect (Requirement 9.3). The persistent replicated
            // outcome is the durable backstop; this is the immediate targeted hand-off.
            ResendResultTo(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!IsServer || _coordinator == null)
            {
                return;
            }

            if (_idMap == null || !_idMap.TryGetPlayer(clientId, out PlayerId playerId))
            {
                return;
            }

            _coordinator.OnDisconnected(playerId);
        }

        /// <summary>
        /// Resends the retained match result to exactly the reconnecting client, if a result has
        /// been retained. Delegates the pure decision ("is there a result, and what is it?") to
        /// the retainer and the transport send to the shared <see cref="MatchRpcBehaviour"/>.
        /// </summary>
        private void ResendResultTo(ulong clientId)
        {
            if (_retainer == null || _rpc == null)
            {
                return;
            }

            if (!_retainer.TryBuildResend(out MatchResultEvent payload))
            {
                return;
            }

            _rpc.SendMatchResultTo(clientId, payload);
        }
    }
}
