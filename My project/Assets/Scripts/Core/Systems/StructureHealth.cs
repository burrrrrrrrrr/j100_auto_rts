using System.Collections.Generic;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// Side-table of hit points for structures, so buildings can be engaged and destroyed
    /// without adding an HP field to the spec's <see cref="Structure"/> type. Shared between
    /// the melee movement/combat systems (which target and damage structures) and the
    /// presentation (which can show remaining HP). A structure with no entry is treated as
    /// having full default HP the first time it is seen.
    /// </summary>
    public sealed class StructureHealth
    {
        private readonly Dictionary<StructureId, float> _hp = new Dictionary<StructureId, float>();
        private readonly float _default;

        public StructureHealth(float defaultHp)
        {
            _default = defaultHp;
        }

        /// <summary>Current HP for a structure (initialized to the default on first access).</summary>
        public float Get(StructureId id)
        {
            if (!_hp.TryGetValue(id, out float v)) { v = _default; _hp[id] = v; }
            return v;
        }

        /// <summary>Applies damage; returns the new HP (clamped at 0).</summary>
        public float Damage(StructureId id, float amount)
        {
            float v = Get(id) - amount;
            if (v < 0f) { v = 0f; }
            _hp[id] = v;
            return v;
        }

        public bool IsDead(StructureId id) => Get(id) <= 0f;

        public void Remove(StructureId id) => _hp.Remove(id);

        public float Max => _default;
    }
}
