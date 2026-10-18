using System;
using System.Collections.Generic;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// A predefined sequence of waypoints along which a <c>Unit</c> travels from its
    /// spawn point toward the opponent's <c>Base</c> (Requirement 6). Waypoints are
    /// ordered spawn-first, final waypoint last. <see cref="TargetBase"/> records the
    /// opponent base location the path leads to, used on arrival to apply base damage
    /// (Requirement 6.3).
    /// </summary>
    public sealed class Path
    {
        /// <summary>Ordered waypoints from the spawn point toward the opponent base.</summary>
        public IReadOnlyList<MapVector> Waypoints { get; }

        /// <summary>The opponent base location this path targets.</summary>
        public MapVector TargetBase { get; }

        public Path(IReadOnlyList<MapVector> waypoints, MapVector targetBase)
        {
            Waypoints = waypoints ?? throw new ArgumentNullException(nameof(waypoints));
            TargetBase = targetBase;
        }

        /// <summary>Number of waypoints on this path.</summary>
        public int WaypointCount => Waypoints.Count;

        /// <summary>True when the path has at least one waypoint to travel toward.</summary>
        public bool HasWaypoints => Waypoints.Count > 0;

        /// <summary>
        /// Returns true when <paramref name="index"/> is the final waypoint of this path.
        /// A unit reaching the final waypoint is considered to have arrived at the
        /// opponent base (Requirement 6.3).
        /// </summary>
        public bool IsFinalIndex(int index) => Waypoints.Count > 0 && index == Waypoints.Count - 1;
    }
}
