namespace MultiplayerRts.Core
{
    /// <summary>
    /// The kind of gameplay action a <c>Player</c> can submit. Lets the canonical action
    /// pipeline route a <see cref="PlayerAction"/> to the correct validator without a type
    /// check (Requirement 2.3).
    /// </summary>
    public enum PlayerActionKind
    {
        /// <summary>A request to place a <c>Structure</c> (Requirement 4.1).</summary>
        Placement
    }

    /// <summary>
    /// Base type for a gameplay action submitted by a <c>Player</c> to the authoritative
    /// server (Requirement 2). Every action carries the submitting <see cref="PlacingPlayer"/>
    /// and a unique <see cref="ActionId"/> so the canonical pipeline can validate against
    /// current state and apply each action exactly once, ignoring re-delivery of an already
    /// applied id (Requirement 2.3). A pure C# type with no Unity dependency so the pipeline
    /// can be property-tested.
    /// </summary>
    public abstract class PlayerAction
    {
        /// <summary>Identifies this action so re-delivery of the same id is applied at most once (Requirement 2.3).</summary>
        public uint ActionId { get; }

        /// <summary>The player that submitted this action.</summary>
        public PlayerId PlacingPlayer { get; }

        protected PlayerAction(uint actionId, PlayerId placingPlayer)
        {
            ActionId = actionId;
            PlacingPlayer = placingPlayer;
        }

        /// <summary>Which kind of action this is, used to route it through validation.</summary>
        public abstract PlayerActionKind Kind { get; }
    }
}
