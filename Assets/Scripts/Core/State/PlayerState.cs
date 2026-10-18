namespace MultiplayerRts.Core
{
    /// <summary>
    /// The canonical per-player state within a <c>Match</c>: resource total, base health
    /// and location, build area, and connection status (Requirements 3, 8, 9). Mutable so
    /// the simulation can update resources and base health in place each tick; a pure C#
    /// type with no Unity dependency so economy and victory logic can be property-tested.
    /// </summary>
    public sealed class PlayerState
    {
        /// <summary>Identifies this player uniquely within the match (Requirement 1.7).</summary>
        public PlayerId Id { get; set; }

        /// <summary>
        /// Current resource total, in the inclusive range
        /// [0, <see cref="SimulationConstants.ResourceCap"/>] (Requirements 3.1, 3.2).
        /// </summary>
        public int ResourceTotal { get; set; }

        /// <summary>Current base health; reaching zero ends the match (Requirement 8.1).</summary>
        public float BaseHealth { get; set; }

        /// <summary>Maximum base health, used as the upper bound for rendered health (Requirement 10.4).</summary>
        public float BaseMaxHealth { get; set; }

        /// <summary>Map location of this player's base, the target of opponent units (Requirement 6.3).</summary>
        public MapVector BaseLocation { get; set; }

        /// <summary>The region where this player may place structures (Requirement 4.2).</summary>
        public Build_Area BuildArea { get; set; }

        /// <summary>This player's connection status, driving pause/resume and disconnect resolution (Requirement 9).</summary>
        public ConnectionState Connection { get; set; }
    }
}
