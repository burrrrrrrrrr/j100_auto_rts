namespace MultiplayerRts.Core
{
    /// <summary>
    /// Validates and applies structure placement against the canonical
    /// <see cref="MatchState"/> (Requirement 4). Framework-independent (no Unity or Netcode
    /// dependency) so placement can be property-tested in isolation against an in-memory
    /// state.
    /// </summary>
    public interface IPlacementSystem
    {
        /// <summary>
        /// Attempts to place a structure described by <paramref name="action"/>. Validation
        /// runs in a fixed order and any rejection leaves the match state completely
        /// unchanged (Requirements 4.2, 4.3, 4.4):
        /// <list type="number">
        ///   <item>within map bounds, else <see cref="RejectionReason.OutOfBounds"/> (4.1);</item>
        ///   <item>within the placing player's <c>Build_Area</c>, else
        ///     <see cref="RejectionReason.NotInBuildArea"/> (4.2);</item>
        ///   <item>no footprint overlap with an existing structure, else
        ///     <see cref="RejectionReason.FootprintOverlap"/> (4.3);</item>
        ///   <item>affordable, else <see cref="RejectionReason.InsufficientResources"/>
        ///     (3.4, 4.4).</item>
        /// </list>
        /// On success the structure is created at the target location, owned by the placing
        /// player, and its cost is spent (Requirement 4.5); the new id is returned in
        /// <see cref="PlacementResult.Created"/>.
        /// </summary>
        PlacementResult TryPlace(PlacementAction action);
    }
}
