namespace MultiplayerRts.Core
{
    /// <summary>
    /// The kind of <c>Structure</c> a <c>Player</c> can place (Requirement 4).
    /// </summary>
    public enum StructureType
    {
        /// <summary>Periodically produces <c>Units</c> (Spawner_Structure, Requirement 5).</summary>
        Spawner,

        /// <summary>Attacks enemy <c>Units</c> within range (Defensive_Structure, Requirement 7).</summary>
        Defensive,

        /// <summary>Generates <c>Resource</c> over time (Economy_Structure, Requirement 3).</summary>
        Economy
    }

    /// <summary>
    /// The lifecycle phase of a <c>Match</c>.
    /// </summary>
    public enum MatchPhase
    {
        /// <summary>Players are being paired before the match begins (Requirement 1).</summary>
        Lobby,

        /// <summary>The match is running and the simulation is ticking.</summary>
        Active,

        /// <summary>A player disconnected; waiting out the reconnection window (Requirement 9.4).</summary>
        Paused,

        /// <summary>The match has concluded and its outcome is frozen (Requirement 8).</summary>
        Ended
    }

    /// <summary>
    /// The category of a concluded <c>Match</c>'s outcome (Requirement 8).
    /// </summary>
    public enum MatchOutcomeKind
    {
        /// <summary>Exactly one player won (Requirements 8.2, 8.3).</summary>
        Winner,

        /// <summary>Both bases reached zero in the same update; no winner or loser (Requirement 8.4).</summary>
        Draw,

        /// <summary>Both players disconnected; the match ended with no declared winner (Requirement 9.5).</summary>
        NoContest
    }

    /// <summary>
    /// The reason a player action (e.g. placement) was rejected (Requirements 2.4, 3.4, 4.2-4.4, 8.6).
    /// </summary>
    public enum RejectionReason
    {
        /// <summary>Target location lies outside the map boundaries (Requirement 4.1).</summary>
        OutOfBounds,

        /// <summary>Target location lies outside the placing player's <c>Build_Area</c> (Requirement 4.2).</summary>
        NotInBuildArea,

        /// <summary>The structure footprint intersects an existing structure (Requirement 4.3).</summary>
        FootprintOverlap,

        /// <summary>The structure cost exceeds the player's current resource total (Requirements 3.4, 4.4).</summary>
        InsufficientResources,

        /// <summary>The action was submitted after the match had already ended (Requirement 8.6).</summary>
        MatchEnded
    }

    /// <summary>
    /// A player's network connection status, used to drive pause/resume and
    /// disconnect resolution (Requirement 9).
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>The player's client is reachable.</summary>
        Connected,

        /// <summary>The player's client has dropped and the reconnection window has expired.</summary>
        Disconnected,

        /// <summary>The player dropped but is still within the 30-second reconnection window (Requirement 9.1).</summary>
        ReconnectWindow
    }
}
