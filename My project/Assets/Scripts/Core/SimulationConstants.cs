namespace MultiplayerRts.Core
{
    /// <summary>
    /// Centralized economy, spawn, geometry, and timing constants for the canonical
    /// simulation. Values are sourced directly from the acceptance criteria so that a
    /// single edit propagates across every system. Framework-independent (no Unity or
    /// Netcode dependency) so the simulation core can be property-tested in isolation.
    /// </summary>
    public static class SimulationConstants
    {
        /// <summary>
        /// Resources each <c>Player</c> starts a <c>Match</c> with (Requirement 3.1).
        /// </summary>
        public const int StartingResources = 100;

        /// <summary>
        /// Maximum <c>Resource</c> total a <c>Player</c> may accumulate; accrual clamps
        /// here (Requirement 3.2).
        /// </summary>
        public const int ResourceCap = 999_999;

        /// <summary>
        /// Interval, in seconds, at which an active <c>Economy_Structure</c> adds its
        /// generation rate to its owner's <c>Resource</c> total (Requirement 3.2).
        /// </summary>
        public const float EconomyGenerationIntervalSeconds = 1f;

        /// <summary>
        /// Smallest permitted <c>Spawner_Structure</c> spawn interval, in seconds
        /// (Requirement 5.1).
        /// </summary>
        public const float MinSpawnIntervalSeconds = 1f;

        /// <summary>
        /// Largest permitted <c>Spawner_Structure</c> spawn interval, in seconds
        /// (Requirement 5.1).
        /// </summary>
        public const float MaxSpawnIntervalSeconds = 600f;

        /// <summary>
        /// Distance, in map units, within which a <c>Unit</c> is treated as having
        /// reached its current target waypoint (Requirement 6.2).
        /// </summary>
        public const float WaypointReachThreshold = 0.1f;

        /// <summary>
        /// Minimum rate, in hertz, at which the simulation updates <c>Unit</c> positions.
        /// Drives the fixed-tick cadence (Requirement 6.1).
        /// </summary>
        public const int TickRateHz = 20;

        /// <summary>
        /// Duration, in seconds, of a single fixed simulation tick, derived from
        /// <see cref="TickRateHz"/> (Requirement 6.1).
        /// </summary>
        public const float TickIntervalSeconds = 1f / TickRateHz;

        /// <summary>
        /// Maximum number of <c>Player</c>s a <c>Lobby</c> may contain; a match starts at
        /// exactly this many (Requirements 1.1, 1.3).
        /// </summary>
        public const int LobbyCapacity = 2;

        /// <summary>
        /// Time, in seconds, a lone waiting <c>Player</c> is retained in a <c>Lobby</c>
        /// before being removed with a no-opponent signal (Requirements 1.4, 1.5).
        /// </summary>
        public const float LobbyWaitTimeout = 120f;

        /// <summary>
        /// Duration, in seconds, of the reconnection window a disconnected <c>Player</c> is
        /// given before the <c>Match</c> is resolved by disconnect timeout. While the window
        /// is open the match is paused and resumes on a timely reconnect; on expiry the match
        /// ends via <c>IVictorySystem.OnDisconnectTimeout</c> (Requirements 9.1, 9.4).
        /// </summary>
        public const float ReconnectWindowSeconds = 30f;

        /// <summary>
        /// Returns true when <paramref name="intervalSeconds"/> lies within the permitted
        /// <c>Spawner_Structure</c> spawn-interval range (Requirement 5.1), inclusive.
        /// </summary>
        public static bool IsValidSpawnInterval(float intervalSeconds)
        {
            return intervalSeconds >= MinSpawnIntervalSeconds
                && intervalSeconds <= MaxSpawnIntervalSeconds;
        }

        /// <summary>
        /// Clamps a prospective <c>Resource</c> total to the inclusive range
        /// [0, <see cref="ResourceCap"/>] (Requirement 3.2).
        /// </summary>
        public static int ClampResources(long resourceTotal)
        {
            if (resourceTotal < 0)
            {
                return 0;
            }

            if (resourceTotal > ResourceCap)
            {
                return ResourceCap;
            }

            return (int)resourceTotal;
        }
    }
}
