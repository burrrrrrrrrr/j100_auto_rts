using System.Collections.Generic;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// Demo-layer registry of which structures are "wall" segments: team-owned, attackable,
    /// solid blockers that neither spawn nor shoot. Kept in a side-table so the spec's
    /// <see cref="Structure"/> / <see cref="StructureType"/> need no new wall kind. The melee
    /// movement system uses it to block units against living wall footprints; combat already
    /// lets enemy units damage any enemy structure (including these), and the renderer uses it
    /// to draw walls as blocks.
    /// </summary>
    public sealed class WallRegistry
    {
        private readonly HashSet<StructureId> _walls = new HashSet<StructureId>();

        public void Mark(StructureId id) => _walls.Add(id);
        public void Remove(StructureId id) => _walls.Remove(id);
        public bool IsWall(StructureId id) => _walls.Contains(id);
        public int Count => _walls.Count;
    }
}
