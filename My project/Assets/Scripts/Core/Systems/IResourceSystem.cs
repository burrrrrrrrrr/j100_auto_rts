namespace MultiplayerRts.Core
{
    /// <summary>
    /// Owns resource accrual and spending over the canonical <see cref="MatchState"/>
    /// (Requirement 3). Framework-independent (no Unity or Netcode dependency) so the
    /// economy can be property-tested in isolation against an in-memory state.
    /// </summary>
    public interface IResourceSystem
    {
        /// <summary>
        /// Initializes both players' resource totals to the defined starting amount of
        /// 100 (Requirement 3.1). Called when a match starts.
        /// </summary>
        /// <param name="a">The first player's identifier.</param>
        /// <param name="b">The second player's identifier.</param>
        void StartMatch(PlayerId a, PlayerId b);

        /// <summary>
        /// Advances economy generation by <paramref name="deltaSeconds"/>. Each active
        /// <c>Economy_Structure</c> adds its generation rate to its owner's resource total
        /// once per 1-second generation interval, clamped at the 999,999 cap
        /// (Requirement 3.2).
        /// </summary>
        /// <param name="deltaSeconds">Elapsed simulation time for this step, in seconds.</param>
        void AccrueTick(float deltaSeconds);

        /// <summary>
        /// Returns true when the player's current resource total is greater than or equal
        /// to <paramref name="cost"/> (Requirement 3.3/3.4).
        /// </summary>
        bool CanAfford(PlayerId id, int cost);

        /// <summary>
        /// Decreases the player's resource total by exactly <paramref name="cost"/> when
        /// affordable and returns true; leaves the total unchanged and returns false
        /// otherwise (Requirements 3.3, 3.4).
        /// </summary>
        bool TrySpend(PlayerId id, int cost);

        /// <summary>
        /// Returns the player's current resource total, or 0 when the player is unknown.
        /// </summary>
        int GetTotal(PlayerId id);
    }
}
