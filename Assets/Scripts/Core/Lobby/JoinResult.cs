namespace MultiplayerRts.Core
{
    /// <summary>
    /// The result of a <c>Player</c> requesting to join a <c>Match</c> via
    /// <see cref="ILobbyManager.RequestJoin"/> (Requirements 1.1, 1.2).
    ///
    /// A request is <see cref="Accepted"/> only when the player was not already assigned
    /// to a lobby or match and there was room in a lobby of at most
    /// <see cref="SimulationConstants.LobbyCapacity"/> players (Requirement 1.1). A player
    /// who is already assigned is <see cref="RejectedAlreadyAssigned"/>, and that player's
    /// existing assignment is preserved unchanged (Requirement 1.2).
    ///
    /// A pure C# value type with no Unity dependency so lobby pairing can be
    /// property-tested in isolation.
    /// </summary>
    public readonly struct JoinResult
    {
        /// <summary>The category of this join result.</summary>
        public JoinResultKind Kind { get; }

        private JoinResult(JoinResultKind kind)
        {
            Kind = kind;
        }

        /// <summary>True when the player was placed into a lobby (Requirement 1.1).</summary>
        public bool WasAccepted => Kind == JoinResultKind.Accepted;

        /// <summary>
        /// The player was placed into a lobby that holds at most
        /// <see cref="SimulationConstants.LobbyCapacity"/> players (Requirement 1.1).
        /// </summary>
        public static readonly JoinResult Accepted = new JoinResult(JoinResultKind.Accepted);

        /// <summary>
        /// The player was already assigned to a lobby or match, so the request was
        /// rejected and the prior assignment preserved (Requirement 1.2).
        /// </summary>
        public static readonly JoinResult RejectedAlreadyAssigned =
            new JoinResult(JoinResultKind.RejectedAlreadyAssigned);
    }

    /// <summary>
    /// The category of a <see cref="JoinResult"/> (Requirements 1.1, 1.2).
    /// </summary>
    public enum JoinResultKind
    {
        /// <summary>The player was placed into a lobby (Requirement 1.1).</summary>
        Accepted,

        /// <summary>
        /// The player was already assigned to a lobby or match; the request was rejected
        /// and the prior assignment preserved (Requirement 1.2).
        /// </summary>
        RejectedAlreadyAssigned
    }
}
