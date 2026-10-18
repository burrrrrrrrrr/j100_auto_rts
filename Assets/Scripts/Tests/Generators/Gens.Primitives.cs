using CsCheck;
using MultiplayerRts.Core;

namespace MultiplayerRts.Tests.Generators
{
    /// <summary>
    /// Custom CsCheck generators for the multiplayer-rts domain (design.md, Testing
    /// Strategy). This file holds the primitive building blocks — identities, map vectors,
    /// resource totals, intervals, and enums — that the composite generators in the other
    /// <c>Gens.*</c> files build on. Ranges are chosen to exercise the acceptance-criteria
    /// boundaries (resource cap 999,999; spawn interval [1, 600]; formatter ranges) rather
    /// than arbitrary spans, so generated cases land on the edges the properties care about.
    /// <para/>
    /// Generators are exposed as expression-bodied static properties (not static fields) so
    /// that a composite generator referencing another never depends on static-field
    /// initialization order across the <c>partial</c> class files — each is built lazily on
    /// access. CsCheck generators are cheap, immutable descriptions, so re-building per
    /// access is fine.
    /// </summary>
    public static partial class Gens
    {
        // --- Identities ---

        /// <summary>A player id. Kept to a small range so distinct-player cases collide often.</summary>
        public static Gen<PlayerId> PlayerId => Gen.UInt[0, 8].Select(v => new PlayerId(v));

        /// <summary>A unit id across the full uint space.</summary>
        public static Gen<UnitId> UnitId => Gen.UInt.Select(v => new UnitId(v));

        /// <summary>A structure id across the full uint space.</summary>
        public static Gen<StructureId> StructureId => Gen.UInt.Select(v => new StructureId(v));

        // --- Geometry ---

        /// <summary>A finite map coordinate within a generous play-field span.</summary>
        public static Gen<float> Coordinate => Gen.Float[-1000f, 1000f];

        /// <summary>A point anywhere in the play field.</summary>
        public static Gen<MapVector> MapVector =>
            Gen.Select(Coordinate, Coordinate, (x, y) => new MapVector(x, y));

        /// <summary>A small, non-negative extent for footprint/area sizing.</summary>
        public static Gen<float> Extent => Gen.Float[0f, 50f];

        /// <summary>A strictly positive extent (non-degenerate rectangles).</summary>
        public static Gen<float> PositiveExtent => Gen.Float[0.1f, 50f];

        // --- Economy (Requirements 3.1, 3.2; formatter ranges 10.3) ---

        /// <summary>
        /// A resource total within the valid inclusive range [0, 999,999]
        /// (Requirement 3.2).
        /// </summary>
        public static Gen<int> Resources => Gen.Int[0, SimulationConstants.ResourceCap];

        /// <summary>
        /// A resource-like value that spans the cap, including values at and beyond
        /// 999,999 so accrual/clamping properties see the boundary (design.md: "Economy
        /// generators include values at and beyond the 999,999 cap").
        /// </summary>
        public static Gen<int> ResourcesAroundCap => Gen.OneOf(
            Gen.Const(0),
            Gen.Const(SimulationConstants.ResourceCap - 1),
            Gen.Const(SimulationConstants.ResourceCap),
            Gen.Const(SimulationConstants.ResourceCap + 1),
            Gen.Int[0, int.MaxValue]);

        /// <summary>
        /// A value across the resource-display formatter range [0, 999,999,999]
        /// (Requirement 10.3).
        /// </summary>
        public static Gen<int> ResourceDisplayValue => Gen.OneOf(
            Gen.Const(0),
            Gen.Const(999_999_999),
            Gen.Int[0, 999_999_999]);

        /// <summary>A per-interval economy generation rate.</summary>
        public static Gen<int> GenerationRate => Gen.Int[0, 10_000];

        // --- Timing / schedules ---

        /// <summary>A single fixed-tick delta time in seconds (Requirement 6.1).</summary>
        public static Gen<float> TickDeltaSeconds => Gen.Const(SimulationConstants.TickIntervalSeconds);

        /// <summary>A positive elapsed-time delta in seconds, from sub-tick to multi-second.</summary>
        public static Gen<float> DeltaSeconds => Gen.Float[0.001f, 10f];

        // --- Enums ---

        /// <summary>Any structure type.</summary>
        public static Gen<StructureType> StructureType => Gen.Enum<StructureType>();

        /// <summary>Any match phase.</summary>
        public static Gen<MatchPhase> MatchPhase => Gen.Enum<MatchPhase>();

        /// <summary>Any rejection reason.</summary>
        public static Gen<RejectionReason> RejectionReason => Gen.Enum<RejectionReason>();
    }
}
