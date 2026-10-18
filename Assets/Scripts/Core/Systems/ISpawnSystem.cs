namespace MultiplayerRts.Core
{
    /// <summary>
    /// Produces <c>Unit</c>s from active <c>Spawner_Structure</c>s each fixed tick
    /// (Requirement 5). Emits one unit per elapsed spawn interval, assigns owner
    /// (Requirement 5.2) and a base-targeted path (Requirement 5.3) before a unit becomes
    /// active, holds pending creations when the spawn point is occupied (Requirement 5.5),
    /// and holds pathless units at the spawn point with owner retained (Requirement 5.6).
    /// </summary>
    public interface ISpawnSystem
    {
        /// <summary>
        /// Advances every active spawner by <paramref name="deltaSeconds"/>, emitting units
        /// on each elapsed spawn interval and resolving held/pending units against the
        /// current state (Requirement 5).
        /// </summary>
        void Tick(float deltaSeconds);
    }
}
