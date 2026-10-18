using System.Collections.Generic;
using MultiplayerRts.Core;

namespace MultiplayerRts.Bootstrap
{
    // ---------------------------------------------------------------------
    // Minimal demo implementations of the Core injectable seams, so the
    // bootstrap can stand up a playable match. These are deliberately simple
    // placeholder values; tune them for real gameplay.
    // ---------------------------------------------------------------------

    /// <summary>Monotonic UnitId source for a single match (Core expects unique ids).</summary>
    public sealed class SequentialUnitIdAllocator : IUnitIdAllocator
    {
        private uint _next;
        public UnitId Next() => new UnitId(_next++);
    }

    /// <summary>Monotonic StructureId source for a single match.</summary>
    public sealed class SequentialStructureIdAllocator : IStructureIdAllocator
    {
        private uint _next;
        public StructureId Next() => new StructureId(_next++);
    }

    /// <summary>
    /// Demo structure catalog: fixed cost + footprint size per structure type.
    /// Costs are affordable from the 100 starting resources so you can place
    /// immediately.
    /// </summary>
    public sealed class DemoStructureCatalog : IStructureCatalog
    {
        public bool TryGetDefinition(StructureType type, out StructureDefinition definition)
        {
            switch (type)
            {
                case StructureType.Spawner:
                    definition = new StructureDefinition(50, new MapVector(1f, 1f));
                    return true;
                case StructureType.Defensive:
                    definition = new StructureDefinition(40, new MapVector(1f, 1f));
                    return true;
                case StructureType.Economy:
                    definition = new StructureDefinition(30, new MapVector(1f, 1f));
                    return true;
                default:
                    definition = default;
                    return false;
            }
        }
    }

    /// <summary>
    /// Demo path provider: a straight line from the spawner's spawn point to the
    /// opponent base. Returns null only when no opponent base can be found (which
    /// causes the spawn system to hold the unit, per Requirement 5.6).
    /// </summary>
    public sealed class DemoPathProvider : IPathProvider
    {
        public Path TryGetPath(MatchState state, Structure spawner, PlayerId owner)
        {
            PlayerState opponent = state.GetOpponent(owner);
            if (opponent == null)
            {
                return null;
            }

            MapVector target = opponent.BaseLocation;
            var waypoints = new List<MapVector> { spawner.SpawnPoint, target };
            return new Path(waypoints, target);
        }
    }

    /// <summary>
    /// Demo starting-base provider: places the two players' bases at opposite
    /// ends of the map, each with a rectangular build area on its own side.
    /// </summary>
    public sealed class DemoStartingBaseProvider : IStartingBaseProvider
    {
        public void AssignBases(PlayerId playerA, PlayerId playerB, out StartingBase baseA, out StartingBase baseB)
        {
            baseA = new StartingBase(playerA, new MapVector(-30f, 0f), 100f);
            baseB = new StartingBase(playerB, new MapVector(30f, 0f), 100f);
        }

        /// <summary>Build area footprint for a base located at <paramref name="baseLocation"/>.</summary>
        public static Build_Area BuildAreaFor(MapVector baseLocation)
        {
            // A roomy build box on the owner's side of the map, around the base.
            float halfW = 10f, halfH = 20f;
            var region = new Footprint(
                new MapVector(baseLocation.X - halfW, baseLocation.Y - halfH),
                new MapVector(halfW * 2f, halfH * 2f));
            return new Build_Area(new List<Footprint> { region });
        }
    }
}



