namespace MultiplayerRts.Core
{
    /// <summary>
    /// A request to place a <c>Structure</c> of a given <see cref="Type"/> at a target map
    /// location, submitted by the placing player (Requirement 4.1). Carries exactly the
    /// payload the client sends: structure type, target location, and placing player. A
    /// pure C# type with no Unity dependency so placement validation can be property-tested.
    /// </summary>
    public sealed class PlacementAction : PlayerAction
    {
        /// <summary>The kind of structure to place.</summary>
        public StructureType Type { get; }

        /// <summary>The target location for the structure, in map units.</summary>
        public MapVector TargetLocation { get; }

        public PlacementAction(uint actionId, PlayerId placingPlayer, StructureType type, MapVector targetLocation)
            : base(actionId, placingPlayer)
        {
            Type = type;
            TargetLocation = targetLocation;
        }

        /// <inheritdoc />
        public override PlayerActionKind Kind => PlayerActionKind.Placement;
    }
}
