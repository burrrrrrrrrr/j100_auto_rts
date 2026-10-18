using System;
using System.Collections.Generic;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// The region of the map where a <c>Player</c> is permitted to place
    /// <c>Structure</c>s (Requirement 4.2). Composed of one or more rectangular
    /// <see cref="Footprint"/> regions; the build area is dynamic and may grow as
    /// structures expand it. A pure C# type with no Unity dependency so containment
    /// checks can be property-tested.
    /// </summary>
    public sealed class Build_Area
    {
        /// <summary>The rectangular regions that together make up the buildable area.</summary>
        public IReadOnlyList<Footprint> Regions { get; }

        public Build_Area(IReadOnlyList<Footprint> regions)
        {
            Regions = regions ?? throw new ArgumentNullException(nameof(regions));
        }

        /// <summary>An empty build area with no buildable regions.</summary>
        public static readonly Build_Area Empty = new Build_Area(Array.Empty<Footprint>());

        /// <summary>
        /// Returns true when <paramref name="point"/> lies within any of this build area's
        /// regions. Used to reject out-of-area placements (Requirement 4.2).
        /// </summary>
        public bool Contains(MapVector point)
        {
            for (int i = 0; i < Regions.Count; i++)
            {
                if (Regions[i].Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true when <paramref name="footprint"/> is fully contained within a
        /// single region of this build area. A footprint straddling the boundary between
        /// two abutting regions is not considered contained.
        /// </summary>
        public bool Contains(Footprint footprint)
        {
            for (int i = 0; i < Regions.Count; i++)
            {
                Footprint region = Regions[i];
                if (footprint.MinX >= region.MinX
                    && footprint.MaxX <= region.MaxX
                    && footprint.MinY >= region.MinY
                    && footprint.MaxY <= region.MaxY)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
