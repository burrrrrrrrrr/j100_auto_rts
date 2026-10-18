namespace MultiplayerRts.Core
{
    /// <summary>
    /// The starting <c>Base</c> assigned to a <c>Player</c> when a <c>Match</c> begins
    /// (Requirement 1.7): the base's map location and its full starting health. Carried in
    /// the <see cref="MatchDescriptor"/> so the match-start handoff is self-contained and
    /// the lobby stays decoupled from <see cref="MatchState"/> construction.
    ///
    /// A pure C# value type with no Unity dependency so match-start assignment can be
    /// property-tested in isolation.
    /// </summary>
    public readonly struct StartingBase
    {
        /// <summary>The player this base belongs to (Requirement 1.7).</summary>
        public PlayerId Owner { get; }

        /// <summary>Map location of the base, the target of opponent units (Requirement 6.3).</summary>
        public MapVector Location { get; }

        /// <summary>
        /// The base's maximum (and starting) health; reaching zero ends the match
        /// (Requirements 8.1, 10.4).
        /// </summary>
        public float MaxHealth { get; }

        public StartingBase(PlayerId owner, MapVector location, float maxHealth)
        {
            Owner = owner;
            Location = location;
            MaxHealth = maxHealth;
        }
    }
}
