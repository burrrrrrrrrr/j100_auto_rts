namespace MultiplayerRts.Core
{
    /// <summary>
    /// The frozen result of a concluded <c>Match</c> (Requirement 8). The
    /// <see cref="Kind"/> distinguishes a decisive win, a draw, and a no-contest
    /// (both players disconnected). <see cref="Winner"/> and <see cref="Loser"/> are set
    /// only for a decisive <see cref="MatchOutcomeKind.Winner"/> result and are null
    /// otherwise (Requirements 8.2, 8.3, 8.4, 9.5). A pure C# type with no Unity
    /// dependency so victory logic can be property-tested.
    /// </summary>
    public sealed class MatchOutcome
    {
        /// <summary>The category of this outcome.</summary>
        public MatchOutcomeKind Kind { get; }

        /// <summary>The winning player, or null for a draw or no-contest.</summary>
        public PlayerId? Winner { get; }

        /// <summary>The losing player, or null for a draw or no-contest.</summary>
        public PlayerId? Loser { get; }

        private MatchOutcome(MatchOutcomeKind kind, PlayerId? winner, PlayerId? loser)
        {
            Kind = kind;
            Winner = winner;
            Loser = loser;
        }

        /// <summary>
        /// Creates a decisive outcome with a single winner and loser (Requirements 8.2, 8.3).
        /// </summary>
        public static MatchOutcome ForWinner(PlayerId winner, PlayerId loser)
        {
            return new MatchOutcome(MatchOutcomeKind.Winner, winner, loser);
        }

        /// <summary>
        /// Creates a draw outcome with no winner and no loser (Requirement 8.4).
        /// </summary>
        public static MatchOutcome ForDraw()
        {
            return new MatchOutcome(MatchOutcomeKind.Draw, null, null);
        }

        /// <summary>
        /// Creates a no-contest outcome (both players disconnected) with no declared
        /// winner (Requirement 9.5).
        /// </summary>
        public static MatchOutcome ForNoContest()
        {
            return new MatchOutcome(MatchOutcomeKind.NoContest, null, null);
        }
    }
}
