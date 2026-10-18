namespace MultiplayerRts.Core
{
    /// <summary>
    /// A player-placed building occupying a <see cref="Footprint"/> on the map
    /// (Requirement 4). A single type holds the fields for all three
    /// <see cref="StructureType"/>s; only the fields relevant to <see cref="Type"/> are
    /// meaningful. Mutable so timers advance in place each tick; a pure C# type with no
    /// Unity dependency so placement, spawn, and combat logic can be property-tested.
    /// </summary>
    public sealed class Structure
    {
        /// <summary>Identifies this structure uniquely within the match.</summary>
        public StructureId Id { get; set; }

        /// <summary>The player that owns this structure (assigned on placement, Requirement 4.5).</summary>
        public PlayerId Owner { get; set; }

        /// <summary>Which kind of structure this is, selecting the relevant field group.</summary>
        public StructureType Type { get; set; }

        /// <summary>The area this structure occupies, used for containment and overlap checks (Requirements 4.2, 4.3).</summary>
        public Footprint Footprint { get; set; }

        /// <summary>
        /// Whether the structure participates in the simulation. Inactive spawners and
        /// economy structures produce nothing (Requirements 5.4, 3.2).
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>Resource cost charged to the owner when this structure is placed (Requirements 3.3, 4.5).</summary>
        public int Cost { get; set; }

        // --- Spawner_Structure fields (Requirement 5) ---

        /// <summary>Seconds between unit spawns; valid range is [1, 600] (Requirement 5.1).</summary>
        public float SpawnIntervalSeconds { get; set; }

        /// <summary>Map location at which spawned units appear (Requirement 5.1).</summary>
        public MapVector SpawnPoint { get; set; }

        /// <summary>Accumulated time toward the next spawn (Requirement 5.1).</summary>
        public float SpawnTimer { get; set; }

        // --- Defensive_Structure fields (Requirement 7) ---

        /// <summary>Attack range in map units (Requirement 7.1).</summary>
        public float Range { get; set; }

        /// <summary>Seconds between successive attacks (Requirement 7.2).</summary>
        public float AttackInterval { get; set; }

        /// <summary>Damage applied to the current target per attack (Requirement 7.2).</summary>
        public float AttackDamage { get; set; }

        /// <summary>Accumulated time toward the next attack; retained when no target is in range (Requirement 7.3).</summary>
        public float AttackTimer { get; set; }

        /// <summary>The unit currently targeted, or null when none is in range (Requirement 7.1).</summary>
        public UnitId? CurrentTarget { get; set; }

        // --- Economy_Structure fields (Requirement 3) ---

        /// <summary>Resources added to the owner's total per generation interval (Requirement 3.2).</summary>
        public int GenerationRate { get; set; }

        /// <summary>Accumulated time toward the next resource generation (Requirement 3.2).</summary>
        public float GenerationTimer { get; set; }
    }
}
