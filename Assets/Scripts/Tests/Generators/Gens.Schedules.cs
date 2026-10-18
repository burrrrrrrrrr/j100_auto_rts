using CsCheck;
using MultiplayerRts.Core;

namespace MultiplayerRts.Tests.Generators
{
    /// <summary>
    /// Generators for spawn and attack schedules (design.md: "spawn/attack schedules").
    /// Spawn intervals cover the inclusive [1, 600] second range including both endpoints
    /// (Requirement 5.1); attack intervals and accumulated timers are generated so combat
    /// timing properties see the once-per-interval boundary (Requirement 7.2). Exposed as
    /// lazily-evaluated properties (see <c>Gens.Primitives.cs</c>).
    /// </summary>
    public static partial class Gens
    {
        /// <summary>
        /// A spawn interval in the valid inclusive range [1, 600] seconds, including both
        /// endpoints so the spawn property exercises the full permitted span
        /// (Requirement 5.1).
        /// </summary>
        public static Gen<float> SpawnInterval => Gen.OneOf(
            Gen.Const(SimulationConstants.MinSpawnIntervalSeconds),
            Gen.Const(SimulationConstants.MaxSpawnIntervalSeconds),
            Gen.Float[SimulationConstants.MinSpawnIntervalSeconds, SimulationConstants.MaxSpawnIntervalSeconds]);

        /// <summary>Accumulated time toward the next spawn, within one interval span.</summary>
        public static Gen<float> SpawnTimer => Gen.Float[0f, SimulationConstants.MaxSpawnIntervalSeconds];

        /// <summary>
        /// A spawner schedule: interval, accumulated timer, and an elapsed delta. Lets spawn
        /// tests reason about how many whole intervals elapse in a tick (Requirements 5.1, 5.5).
        /// </summary>
        public static Gen<(float Interval, float Timer, float Elapsed)> SpawnSchedule =>
            Gen.Select(SpawnInterval, SpawnTimer, DeltaSeconds, (interval, timer, elapsed) => (interval, timer, elapsed));

        /// <summary>An attack interval in seconds (Requirement 7.2).</summary>
        public static Gen<float> AttackInterval => Gen.Float[0.1f, 10f];

        /// <summary>Accumulated time toward the next attack (retained when no target is in range, Requirement 7.3).</summary>
        public static Gen<float> AttackTimer => Gen.Float[0f, 10f];

        /// <summary>
        /// An attack schedule: interval, accumulated timer, and elapsed delta, for
        /// once-per-interval damage timing (Requirement 7.2).
        /// </summary>
        public static Gen<(float Interval, float Timer, float Elapsed)> AttackSchedule =>
            Gen.Select(AttackInterval, AttackTimer, DeltaSeconds, (interval, timer, elapsed) => (interval, timer, elapsed));
    }
}
