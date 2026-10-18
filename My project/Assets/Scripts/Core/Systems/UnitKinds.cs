using System.Collections.Generic;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// Demo-layer classification of units as melee or ranged, kept in a side-table so the
    /// spec's <see cref="Unit"/> type needs no "kind" field. A unit not marked ranged is
    /// treated as melee. Ranged units engage and strike from a longer distance.
    /// </summary>
    public sealed class UnitKinds
    {
        private readonly HashSet<UnitId> _ranged = new HashSet<UnitId>();

        // Melee engagement/strike distances (match the previous constants).
        public const float MeleeEngageRange = 0.9f;
        public const float MeleeAttackRange = 1.0f;

        // Ranged engagement/strike distances (stand off and shoot).
        public const float RangedEngageRange = 7.0f;
        public const float RangedAttackRange = 8.0f;

        public void MarkRanged(UnitId id) => _ranged.Add(id);
        public bool IsRanged(UnitId id) => _ranged.Contains(id);
        public void Remove(UnitId id) => _ranged.Remove(id);

        /// <summary>Distance a unit stops approaching its target (so ranged units stand off).</summary>
        public float EngageRange(UnitId id) => _ranged.Contains(id) ? RangedEngageRange : MeleeEngageRange;

        /// <summary>Distance within which a unit can strike/shoot its target.</summary>
        public float AttackRange(UnitId id) => _ranged.Contains(id) ? RangedAttackRange : MeleeAttackRange;
    }
}
