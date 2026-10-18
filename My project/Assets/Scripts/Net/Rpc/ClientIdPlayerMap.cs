using System.Collections.Generic;
using MultiplayerRts.Core;

namespace MultiplayerRts.Net
{
    /// <summary>
    /// The pure, Netcode-free bridge between the two id spaces this feature straddles: Core
    /// deals exclusively in <see cref="PlayerId"/> (a match-unique identity assigned at match
    /// start, Requirement 1.7) while Netcode for GameObjects deals in a transport-assigned
    /// <c>ulong</c> client id. Every RPC crossing the server boundary needs this translation —
    /// a <c>ServerRpc</c> arrives tagged with the sender's client id and must be routed to a
    /// <see cref="PlacementAction"/> carrying the sender's <see cref="PlayerId"/> (Requirement
    /// 4.1), and a targeted rejection <c>ClientRpc</c> must resolve the rejected player's
    /// <see cref="PlayerId"/> back to the client id NGO delivers to (Requirement 2.4).
    ///
    /// <para>
    /// This is an injectable abstraction kept deliberately pure (it references only Core's
    /// <see cref="PlayerId"/>, no Netcode types) so the whole translation path can be
    /// exercised by the out-of-tree dotnet smoke check without a Unity runtime. The thin
    /// NGO behaviour owns one instance and populates it from connection callbacks
    /// (<see cref="MatchServer.OnClientConnected"/> / <see cref="MatchServer.OnClientDisconnected"/>),
    /// so the behaviour never does id arithmetic itself.
    /// </para>
    ///
    /// <para>
    /// <b>Totality.</b> Lookups never throw: they return <c>false</c> for an unknown id. A
    /// <c>ServerRpc</c> from a client with no mapped <see cref="PlayerId"/> (e.g. one that
    /// connected to the transport but was never admitted to the match) therefore resolves to
    /// "no player" and the caller drops it rather than forging an action, and a rejection for
    /// a player with no live client id resolves to "no target" and is simply not dispatched.
    /// </para>
    /// </summary>
    public sealed class ClientIdPlayerMap
    {
        private readonly Dictionary<ulong, PlayerId> _clientToPlayer = new Dictionary<ulong, PlayerId>();
        private readonly Dictionary<PlayerId, ulong> _playerToClient = new Dictionary<PlayerId, ulong>();

        /// <summary>The number of currently mapped client/player pairs.</summary>
        public int Count => _clientToPlayer.Count;

        /// <summary>
        /// Associates a transport <paramref name="clientId"/> with the Core
        /// <paramref name="playerId"/> it was admitted to the match as. Re-binding either side
        /// (e.g. a player reconnecting under a new transport client id, Requirement 9.4) first
        /// clears any stale pairing for both ids so the map stays a strict one-to-one bijection
        /// and a reconnection never leaves a dangling reverse entry pointing at a dead client.
        /// </summary>
        public void Bind(ulong clientId, PlayerId playerId)
        {
            // Drop any prior pairing that would otherwise leave this map non-bijective: the
            // client id might have mapped to a different player, and the player might have
            // mapped to a different (now-dead) client id.
            if (_clientToPlayer.TryGetValue(clientId, out PlayerId priorPlayer) && !priorPlayer.Equals(playerId))
            {
                _playerToClient.Remove(priorPlayer);
            }

            if (_playerToClient.TryGetValue(playerId, out ulong priorClient) && priorClient != clientId)
            {
                _clientToPlayer.Remove(priorClient);
            }

            _clientToPlayer[clientId] = playerId;
            _playerToClient[playerId] = clientId;
        }

        /// <summary>
        /// Removes the pairing for a disconnected transport client, in both directions. A
        /// no-op for an unknown client id. After this the player has no live client id, so a
        /// subsequent broadcast correctly excludes them (Requirements 7.6, 8.5) and a targeted
        /// rejection for them resolves to "no target".
        /// </summary>
        public void UnbindClient(ulong clientId)
        {
            if (_clientToPlayer.TryGetValue(clientId, out PlayerId playerId))
            {
                _clientToPlayer.Remove(clientId);
                _playerToClient.Remove(playerId);
            }
        }

        /// <summary>
        /// Resolves the <see cref="PlayerId"/> a <c>ServerRpc</c>'s sender client id maps to.
        /// Returns false for an unmapped client id so the caller drops the RPC rather than
        /// forging a <see cref="PlacementAction"/> for a non-player (Requirement 4.1).
        /// </summary>
        public bool TryGetPlayer(ulong clientId, out PlayerId playerId)
        {
            return _clientToPlayer.TryGetValue(clientId, out playerId);
        }

        /// <summary>
        /// Resolves the live transport client id to target a <c>ClientRpc</c> at the given
        /// player. Returns false when the player has no connected client (e.g. mid-disconnect),
        /// in which case the targeted rejection is simply not dispatched (Requirement 2.4) —
        /// the client reconciles from the persistent replicated state on reconnect.
        /// </summary>
        public bool TryGetClient(PlayerId playerId, out ulong clientId)
        {
            return _playerToClient.TryGetValue(playerId, out clientId);
        }
    }
}
