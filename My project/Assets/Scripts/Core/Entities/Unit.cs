namespace MultiplayerRts.Core
{
    /// <summary>
    /// An automatically controlled entity produced by a <c>Spawner_Structure</c> that
    /// travels along its assigned <see cref="AssignedPath"/> toward the opponent's
    /// <c>Base</c> (Requirements 5, 6). Units carry their owner, mutable position and
    /// path progress, and the health/damage used by combat and base arrival. Mutable
    /// so the simulation can advance it in place each tick; a pure C# type with no Unity
    /// dependency so movement/combat logic can be property-tested.
    /// </summary>
    public sealed class Unit
    {
        /// <summary>Identifies this unit uniquely within the match.</summary>
        public UnitId Id { get; set; }

        /// <summary>The player that owns this unit (inherited from its spawner, Requirement 5.2).</summary>
        public PlayerId Owner { get; set; }

        /// <summary>Current position in map units.</summary>
        public MapVector Position { get; set; }

        /// <summary>Remaining health; the unit is removed when this reaches zero or below (Requirement 7.4).</summary>
        public float Health { get; set; }

        /// <summary>Movement speed in map units per second (Requirement 6.1).</summary>
        public float MovementSpeed { get; set; }

        /// <summary>Damage applied to the opponent's base on arrival (Requirement 6.3).</summary>
        public float Damage { get; set; }

        /// <summary>
        /// The path this unit travels, leading toward the opponent's base. Null while the
        /// unit is held awaiting an available path (Requirement 5.6).
        /// </summary>
        public Path AssignedPath { get; set; }

        /// <summary>Index of the current target waypoint on <see cref="AssignedPath"/> (Requirement 6.2).</summary>
        public int WaypointIndex { get; set; }

        /// <summary>
        /// True while the unit is waiting for a free spawn point or an available path and
        /// therefore does not move (Requirements 5.5, 5.6).
        /// </summary>
        public bool IsHeld { get; set; }
    }
}
