namespace MultiplayerRts.Core
{
    /// <summary>
    /// Describes the playable map boundary and resolves whether a structure footprint lies
    /// fully within it (Requirement 4.1). Injected into the placement system because the
    /// canonical <see cref="MatchState"/> carries no map extent of its own, keeping the
    /// bounds source swappable and the placement logic property-testable. Framework-
    /// independent (no Unity or Netcode dependency).
    /// </summary>
    public interface IMapBounds
    {
        /// <summary>
        /// Returns true when <paramref name="footprint"/> lies entirely within the map
        /// boundary. A footprint extending past any edge is out of bounds and the placement
        /// is rejected with <see cref="RejectionReason.OutOfBounds"/> (Requirement 4.1).
        /// </summary>
        bool Contains(Footprint footprint);
    }

    /// <summary>
    /// An axis-aligned rectangular map boundary defined by a single <see cref="Footprint"/>.
    /// The simplest concrete <see cref="IMapBounds"/>; a footprint is contained when it lies
    /// fully inside the boundary rectangle (edge contact permitted).
    /// </summary>
    public sealed class RectangularMapBounds : IMapBounds
    {
        private readonly Footprint _extent;

        /// <summary>Creates a boundary spanning the given rectangular <paramref name="extent"/>.</summary>
        public RectangularMapBounds(Footprint extent)
        {
            _extent = extent;
        }

        /// <inheritdoc />
        public bool Contains(Footprint footprint)
        {
            return footprint.MinX >= _extent.MinX
                && footprint.MaxX <= _extent.MaxX
                && footprint.MinY >= _extent.MinY
                && footprint.MaxY <= _extent.MaxY;
        }
    }
}
