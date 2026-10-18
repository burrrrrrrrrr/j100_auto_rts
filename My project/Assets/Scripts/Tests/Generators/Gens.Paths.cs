using System.Collections.Generic;
using CsCheck;
using MultiplayerRts.Core;

namespace MultiplayerRts.Tests.Generators
{
    /// <summary>
    /// Generators for paths and waypoint positions, including the 0.1-map-unit reach
    /// boundary movement depends on (design.md: "paths and waypoint positions (including
    /// the 0.1-unit boundary)"; Requirements 6.2, 6.3, 6.4). The waypoint-distance
    /// generator straddles <see cref="SimulationConstants.WaypointReachThreshold"/> so the
    /// movement properties see positions just inside, exactly at, and just outside the
    /// threshold. Exposed as lazily-evaluated properties (see <c>Gens.Primitives.cs</c>).
    /// </summary>
    public static partial class Gens
    {
        /// <summary>
        /// A distance from a target waypoint spanning the 0.1 reach threshold: just inside,
        /// exactly at, just outside, and a spread of larger values (Requirement 6.2).
        /// </summary>
        public static Gen<float> WaypointDistance => Gen.OneOf(
            Gen.Const(0f),
            Gen.Const(SimulationConstants.WaypointReachThreshold - 0.001f),
            Gen.Const(SimulationConstants.WaypointReachThreshold),
            Gen.Const(SimulationConstants.WaypointReachThreshold + 0.001f),
            Gen.Float[0f, 50f]);

        /// <summary>
        /// An ordered multi-waypoint path leading to a target base. Waypoints are random
        /// points; the final waypoint coincides with the target base so a unit reaching it
        /// is treated as having arrived (Requirement 6.3).
        /// </summary>
        public static Gen<Path> Path =>
            Gen.Select(MapVector.List[1, 6], MapVector, (waypoints, targetBase) =>
            {
                var pts = new List<MapVector>(waypoints) { targetBase };
                return new Path(pts, targetBase);
            });

        /// <summary>
        /// A single-waypoint path whose only waypoint is the opponent base — the minimal
        /// arrival case for base-damage tests (Requirement 6.3).
        /// </summary>
        public static Gen<Path> SingleWaypointPath =>
            MapVector.Select(targetBase => new Path(new List<MapVector> { targetBase }, targetBase));

        /// <summary>
        /// A dead-end path: waypoints that do NOT terminate at the target base, so a unit
        /// completing it halts without dealing base damage (Requirement 6.4). The target
        /// base is placed far from the last waypoint to make the dead-end explicit.
        /// </summary>
        public static Gen<Path> DeadEndPath =>
            MapVector.List[1, 5].Select(waypoints =>
            {
                // Target base deliberately offset from the final waypoint.
                var last = waypoints[waypoints.Count - 1];
                var targetBase = new MapVector(last.X + 1000f, last.Y + 1000f);
                return new Path(new List<MapVector>(waypoints), targetBase);
            });

        /// <summary>
        /// A path paired with a valid current waypoint index into it, for progression tests
        /// (Requirement 6.2). The index is constrained to the path's waypoint count.
        /// </summary>
        public static Gen<(Path Path, int WaypointIndex)> PathWithIndex =>
            Path.SelectMany(path =>
                Gen.Int[0, path.WaypointCount - 1].Select(idx => (path, idx)));
    }
}
