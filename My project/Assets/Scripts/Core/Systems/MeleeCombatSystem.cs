using System.Collections.Generic;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// Melee unit-vs-unit combat (demo combat model). Each tick, a unit within
    /// <see cref="AttackRange"/> of the closest enemy unit deals <see cref="Unit.Damage"/>
    /// once per <see cref="AttackInterval"/> seconds; units at or below zero health are
    /// removed within the same tick. Per-unit attack cooldown is tracked in a side table
    /// keyed by <see cref="UnitId"/>, so the spec's <see cref="Unit"/> type is left unchanged.
    /// Towers keep using the separate <see cref="CombatSystem"/>; this only governs units.
    /// Pure C# (no Unity dependency), mutates unit health in place.
    /// </summary>
    public sealed class MeleeCombatSystem
    {
        /// <summary>Distance within which a unit can strike an enemy unit.</summary>
        public const float AttackRange = 1.0f;

        /// <summary>Seconds between a unit's melee strikes.</summary>
        public const float AttackInterval = 1.0f;

        private readonly MatchState _state;
        private readonly StructureHealth _structureHp;
        private readonly UnitKinds _kinds;
        private readonly Dictionary<UnitId, float> _cooldown = new Dictionary<UnitId, float>();

        /// <summary>
        /// Raised once per melee strike, with the map position of the hit (the target's
        /// position). The presentation layer uses this to flash a spark; pure callers may
        /// leave it null. Kept as a plain delegate so the Core stays Unity-free.
        /// </summary>
        public System.Action<MapVector> OnStrike;

        public MeleeCombatSystem(MatchState state, StructureHealth structureHp, UnitKinds kinds)
        {
            _state = state;
            _structureHp = structureHp;
            _kinds = kinds;
        }

        /// <summary>Raised when a RANGED unit shoots: (fromUnitPos, toTargetPos) for a tracer.</summary>
        public System.Action<MapVector, MapVector> OnRangedShot;

        /// <summary>Raised when a structure is destroyed by melee, with its id and center.</summary>
        public System.Action<StructureId> OnStructureDestroyed;

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || _state.Units.Count == 0) { return; }

            var units = new List<Unit>(_state.Units.Count);
            foreach (var kv in _state.Units)
            {
                if (kv.Value != null) { units.Add(kv.Value); }
            }

            // Advance cooldowns and apply strikes.
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (u.Health <= 0f || u.IsHeld) { continue; }

                float cd = _cooldown.TryGetValue(u.Id, out float c) ? c : 0f;
                cd -= deltaSeconds;

                float range = _kinds != null ? _kinds.AttackRange(u.Id) : AttackRange;
                bool ranged = _kinds != null && _kinds.IsRanged(u.Id);

                Unit target = FindNearestEnemyInRange(u, units, range);
                if (target != null && cd <= 0f)
                {
                    float nh = target.Health - u.Damage;
                    target.Health = nh;
                    cd = AttackInterval; // reset cooldown after a strike
                    if (ranged) { OnRangedShot?.Invoke(u.Position, target.Position); }
                    else { OnStrike?.Invoke(target.Position); }
                }
                else if (target == null && cd <= 0f && _structureHp != null)
                {
                    // No enemy unit in range: strike an enemy structure in range if present.
                    if (TryFindEnemyStructureInRange(u, range, out StructureId sid, out MapVector spos))
                    {
                        _structureHp.Damage(sid, u.Damage);
                        cd = AttackInterval;
                        if (ranged) { OnRangedShot?.Invoke(u.Position, spos); }
                        else { OnStrike?.Invoke(spos); }
                    }
                }

                if (cd < 0f) { cd = 0f; }
                _cooldown[u.Id] = cd;
            }

            // Remove dead units this tick (and their cooldown entries).
            List<UnitId> dead = null;
            foreach (var kv in _state.Units)
            {
                if (kv.Value != null && kv.Value.Health <= 0f)
                {
                    (dead ??= new List<UnitId>()).Add(kv.Key);
                }
            }
            if (dead != null)
            {
                for (int i = 0; i < dead.Count; i++)
                {
                    _state.Units.Remove(dead[i]);
                    _cooldown.Remove(dead[i]);
                    _kinds?.Remove(dead[i]);
                }
            }

            // Remove destroyed structures (HP <= 0).
            if (_structureHp != null)
            {
                List<StructureId> deadStructs = null;
                foreach (var kv in _state.Structures)
                {
                    if (kv.Value != null && _structureHp.IsDead(kv.Key))
                    {
                        (deadStructs ??= new List<StructureId>()).Add(kv.Key);
                    }
                }
                if (deadStructs != null)
                {
                    for (int i = 0; i < deadStructs.Count; i++)
                    {
                        _state.Structures.Remove(deadStructs[i]);
                        _structureHp.Remove(deadStructs[i]);
                        OnStructureDestroyed?.Invoke(deadStructs[i]);
                    }
                }
            }
        }

        private bool TryFindEnemyStructureInRange(Unit self, float range, out StructureId id, out MapVector pos)
        {
            id = default; pos = default;
            float bestSqr = range * range;
            bool found = false;
            foreach (var kv in _state.Structures)
            {
                var s = kv.Value;
                if (s == null || s.Owner == self.Owner) { continue; }
                if (_structureHp.IsDead(s.Id)) { continue; }
                float cx = s.Footprint.Origin.X + s.Footprint.Size.X * 0.5f;
                float cy = s.Footprint.Origin.Y + s.Footprint.Size.Y * 0.5f;
                var c = new MapVector(cx, cy);
                float sq = MapVector.SqrDistance(self.Position, c);
                if (sq <= bestSqr) { bestSqr = sq; id = s.Id; pos = c; found = true; }
            }
            return found;
        }

        private Unit FindNearestEnemyInRange(Unit self, List<Unit> units, float range)
        {
            Unit best = null;
            float bestSqr = range * range;
            for (int i = 0; i < units.Count; i++)
            {
                Unit o = units[i];
                if (o.Owner == self.Owner || o.Health <= 0f) { continue; }
                float sq = MapVector.SqrDistance(self.Position, o.Position);
                if (sq <= bestSqr) { bestSqr = sq; best = o; }
            }
            return best;
        }
    }
}



