namespace MultiplayerRts.Core
{
    /// <summary>
    /// Supplies a <see cref="Path"/> leading from a spawner's spawn point toward the
    /// opponent's base (Requirement 5.3). Injected into the spawn system so pathing is
    /// decoupled from the simulation and can be driven deterministically in property
    /// tests, including the "no path available" case (Requirement 5.6).
    /// </summary>
    public interface IPathProvider
    {
        /// <summary>
        /// Returns a path from <paramref name="spawner"/> toward the opponent's base for a
        /// unit owned by <paramref name="owner"/>, or <c>null</c> when no path is currently
        /// available. A null result causes the created unit to be held at the spawn point
        /// with its owner retained until a path becomes available (Requirement 5.6).
        /// </summary>
        /// <param name="state">The canonical match state, used to locate the opponent base.</param>
        /// <param name="spawner">The spawner producing the unit.</param>
        /// <param name="owner">The owning player of the unit (the spawner's owner).</param>
        Path TryGetPath(MatchState state, Structure spawner, PlayerId owner);
    }
}
