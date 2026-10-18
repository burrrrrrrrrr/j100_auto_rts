namespace MultiplayerRts.Core
{
    /// <summary>
    /// The immutable definition of a <c>Structure</c> type: the resource cost charged on
    /// placement (Requirements 3.3, 4.5) and the footprint size it occupies on the map
    /// (Requirements 4.2, 4.3). A pure value with no Unity dependency so placement logic
    /// can be property-tested against synthetic definitions.
    /// </summary>
    public readonly struct StructureDefinition
    {
        /// <summary>Resource cost charged to the placing player on a successful placement.</summary>
        public readonly int Cost;

        /// <summary>
        /// Footprint size, in map units, of a structure of this type. Both components are
        /// expected to be non-negative; the placement system positions the footprint at the
        /// target location.
        /// </summary>
        public readonly MapVector FootprintSize;

        public StructureDefinition(int cost, MapVector footprintSize)
        {
            Cost = cost;
            FootprintSize = footprintSize;
        }
    }

    /// <summary>
    /// Supplies the cost and footprint size for each <see cref="StructureType"/>. Injected
    /// into the placement system so structure definitions are decoupled from the simulation
    /// and can be driven deterministically in property tests. Framework-independent (no
    /// Unity or Netcode dependency).
    /// </summary>
    public interface IStructureCatalog
    {
        /// <summary>
        /// Resolves the definition (cost + footprint size) for <paramref name="type"/>,
        /// returning true when the type is known. A false result causes placement to be
        /// treated as a total no-op rejection rather than throwing, so an unknown type
        /// cannot abort the operation.
        /// </summary>
        bool TryGetDefinition(StructureType type, out StructureDefinition definition);
    }
}
