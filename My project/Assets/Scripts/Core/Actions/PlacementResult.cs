namespace MultiplayerRts.Core
{
    /// <summary>
    /// The result of attempting a <see cref="PlacementAction"/>. On success,
    /// <see cref="Created"/> holds the id of the new structure and <see cref="Reason"/> is
    /// null; on rejection, <see cref="Reason"/> explains why and no structure was created,
    /// leaving match state unchanged (Requirements 4.2, 4.3, 4.4, 4.5). A pure C# type with
    /// no Unity dependency so placement validation can be property-tested.
    /// </summary>
    public sealed class PlacementResult
    {
        /// <summary>True when the structure was created and the cost charged.</summary>
        public bool Success { get; }

        /// <summary>The id of the created structure on success; null on rejection.</summary>
        public StructureId? Created { get; }

        /// <summary>The reason for rejection; null on success.</summary>
        public RejectionReason? Reason { get; }

        private PlacementResult(bool success, StructureId? created, RejectionReason? reason)
        {
            Success = success;
            Created = created;
            Reason = reason;
        }

        /// <summary>Creates a successful result referencing the newly created structure.</summary>
        public static PlacementResult Accepted(StructureId created)
        {
            return new PlacementResult(true, created, null);
        }

        /// <summary>Creates a rejected result with the given reason and no created structure.</summary>
        public static PlacementResult Rejected(RejectionReason reason)
        {
            return new PlacementResult(false, null, reason);
        }
    }
}
