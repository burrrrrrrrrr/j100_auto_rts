using System.Collections.Generic;
using CsCheck;
using MultiplayerRts.Core;

namespace MultiplayerRts.Tests.Generators
{
    /// <summary>
    /// Generators for footprints and build areas, including the overlapping and boundary
    /// cases placement validation depends on (design.md: "build areas and footprints
    /// (including overlapping and boundary cases)"; Requirements 4.2, 4.3). Overlap and
    /// edge-contact are generated deliberately rather than left to chance, so the
    /// placement properties exercise the exact boundary the simulation treats as "touching
    /// but not overlapping". Exposed as lazily-evaluated properties to stay independent of
    /// static-field initialization order (see <c>Gens.Primitives.cs</c>).
    /// </summary>
    public static partial class Gens
    {
        /// <summary>An axis-aligned footprint with a non-negative size somewhere in the play field.</summary>
        public static Gen<Footprint> Footprint =>
            Gen.Select(MapVector, Extent, Extent, (origin, w, h) =>
                new Footprint(origin, new Core.MapVector(w, h)));

        /// <summary>A non-degenerate footprint (strictly positive width and height).</summary>
        public static Gen<Footprint> NonDegenerateFootprint =>
            Gen.Select(MapVector, PositiveExtent, PositiveExtent, (origin, w, h) =>
                new Footprint(origin, new Core.MapVector(w, h)));

        /// <summary>
        /// A pair of footprints guaranteed to overlap: the second's origin is placed strictly
        /// inside the first, with a size that keeps a positive-area intersection
        /// (Requirement 4.3 — overlaps are rejected).
        /// </summary>
        public static Gen<(Footprint First, Footprint Second)> OverlappingFootprints =>
            Gen.Select(
                MapVector,
                Gen.Float[2f, 50f],
                Gen.Float[2f, 50f],
                Gen.Float[0.1f, 0.9f],
                Gen.Float[0.1f, 0.9f],
                PositiveExtent,
                PositiveExtent,
                (origin, w, h, fx, fy, w2, h2) =>
                {
                    var first = new Footprint(origin, new Core.MapVector(w, h));
                    // Second origin lies strictly inside the first rectangle.
                    var secondOrigin = new Core.MapVector(origin.X + (w * fx), origin.Y + (h * fy));
                    var second = new Footprint(secondOrigin, new Core.MapVector(w2, h2));
                    return (first, second);
                });

        /// <summary>
        /// A pair of footprints that share an edge but have zero overlap area (the second sits
        /// immediately to the right of the first). Per <see cref="Core.Footprint.Overlaps"/>,
        /// edge contact alone is NOT an overlap, so abutting placements are permitted
        /// (Requirement 4.3). This is the boundary case the overlap property must respect.
        /// </summary>
        public static Gen<(Footprint First, Footprint Second)> EdgeTouchingFootprints =>
            Gen.Select(MapVector, PositiveExtent, PositiveExtent, PositiveExtent, PositiveExtent,
                (origin, w, h, w2, h2) =>
                {
                    var first = new Footprint(origin, new Core.MapVector(w, h));
                    var secondOrigin = new Core.MapVector(origin.X + w, origin.Y);
                    var second = new Footprint(secondOrigin, new Core.MapVector(w2, h2));
                    return (first, second);
                });

        /// <summary>
        /// A build area of one to three rectangular regions. Suitable for containment checks
        /// where placement must fall inside some region (Requirement 4.2).
        /// </summary>
        public static Gen<Build_Area> BuildArea =>
            NonDegenerateFootprint.List[1, 3].Select(regions => new Build_Area(regions));

        /// <summary>
        /// A build area paired with a point guaranteed to lie inside one of its regions —
        /// the "in area" boundary case for placement acceptance (Requirement 4.2).
        /// </summary>
        public static Gen<(Build_Area Area, MapVector Inside)> BuildAreaWithInsidePoint =>
            Gen.Select(MapVector, PositiveExtent, PositiveExtent, Gen.Float[0f, 1f], Gen.Float[0f, 1f],
                (origin, w, h, fx, fy) =>
                {
                    var region = new Footprint(origin, new Core.MapVector(w, h));
                    var area = new Build_Area(new List<Footprint> { region });
                    var inside = new Core.MapVector(origin.X + (w * fx), origin.Y + (h * fy));
                    return (area, inside);
                });

        /// <summary>
        /// A build area paired with a point guaranteed to lie outside every region — the
        /// "out of area" boundary case placement must reject (Requirement 4.2).
        /// </summary>
        public static Gen<(Build_Area Area, MapVector Outside)> BuildAreaWithOutsidePoint =>
            Gen.Select(MapVector, PositiveExtent, PositiveExtent, PositiveExtent, PositiveExtent,
                (origin, w, h, dx, dy) =>
                {
                    var region = new Footprint(origin, new Core.MapVector(w, h));
                    var area = new Build_Area(new List<Footprint> { region });
                    // Shift strictly past the right/top edge so the point cannot be contained.
                    var outside = new Core.MapVector(origin.X + w + dx, origin.Y + h + dy);
                    return (area, outside);
                });
    }
}
