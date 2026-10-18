namespace MultiplayerRts.Core
{
    /// <summary>
    /// Supplies the starting <c>Base</c> assigned to each <c>Player</c> when a match starts
    /// (Requirement 1.7). Injected into the lobby so base placement (map locations, starting
    /// health) is decoupled from pairing and can be replaced with a deterministic stub in
    /// property tests.
    /// </summary>
    public interface IStartingBaseProvider
    {
        /// <summary>
        /// Returns the starting bases for the two paired players. Implementations must
        /// assign <paramref name="playerA"/> and <paramref name="playerB"/> each a base
        /// owned by that player (<see cref="StartingBase.Owner"/>) at distinct locations.
        /// </summary>
        /// <param name="playerA">The first paired player's identifier.</param>
        /// <param name="playerB">The second paired player's identifier.</param>
        /// <param name="baseA">The starting base assigned to <paramref name="playerA"/>.</param>
        /// <param name="baseB">The starting base assigned to <paramref name="playerB"/>.</param>
        void AssignBases(PlayerId playerA, PlayerId playerB, out StartingBase baseA, out StartingBase baseB);
    }
}
