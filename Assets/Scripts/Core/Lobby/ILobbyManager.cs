namespace MultiplayerRts.Core
{
    /// <summary>
    /// Handles pairing before a <c>Match</c> begins (Requirement 1): placing unassigned
    /// players into a lobby of at most <see cref="SimulationConstants.LobbyCapacity"/>
    /// players, rejecting already-assigned players, starting a match once two players are
    /// present, enforcing the wait timeout for a lone player, and removing a player on
    /// disconnect.
    ///
    /// Framework-independent (no Unity or Netcode dependency) so pairing can be
    /// property-tested in isolation. Every operation is total over its valid domain.
    /// </summary>
    public interface ILobbyManager
    {
        /// <summary>
        /// Places an unassigned player into a lobby that holds at most
        /// <see cref="SimulationConstants.LobbyCapacity"/> players and returns
        /// <see cref="JoinResult.Accepted"/> (Requirement 1.1). If the player is already
        /// assigned to a lobby or match, the request is rejected with
        /// <see cref="JoinResult.RejectedAlreadyAssigned"/> and the player's existing
        /// assignment is preserved unchanged (Requirement 1.2).
        /// </summary>
        JoinResult RequestJoin(PlayerId id);

        /// <summary>
        /// Starts a match when exactly <see cref="SimulationConstants.LobbyCapacity"/>
        /// players are present, assigning each a player identifier that is unique within
        /// the match and a starting base (Requirements 1.3, 1.7). Returns true and outputs
        /// the <paramref name="match"/> descriptor on success; returns false with a null
        /// descriptor when fewer than two players are present.
        /// </summary>
        bool TryStartMatch(out MatchDescriptor match);

        /// <summary>
        /// Advances the lobby wait clock by <paramref name="deltaSeconds"/>. While fewer
        /// than two players are present and fewer than
        /// <see cref="SimulationConstants.LobbyWaitTimeout"/> seconds have elapsed since the
        /// waiting player joined, the waiting player is retained (Requirement 1.4). Once the
        /// timeout is reached with fewer than two players, the waiting player is removed and
        /// a no-opponent signal is produced (Requirement 1.5).
        /// </summary>
        void TickWaiting(float deltaSeconds);

        /// <summary>
        /// Removes a player from the lobby on disconnect, removing only the departing player
        /// and retaining any remaining waiting player (Requirement 1.6). A no-op when the
        /// player is not in the lobby.
        /// </summary>
        void RemovePlayer(PlayerId id);
    }
}
