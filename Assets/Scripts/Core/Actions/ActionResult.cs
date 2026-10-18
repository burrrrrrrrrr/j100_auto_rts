namespace MultiplayerRts.Core
{
    /// <summary>
    /// The outcome of submitting a <see cref="PlayerAction"/> to the canonical pipeline
    /// (Requirements 2.3, 2.4). A valid action produces an <see cref="Accepted"/> result and
    /// is applied exactly once; an invalid action produces a <see cref="Rejected"/> result
    /// carrying a <see cref="RejectionReason"/> and leaves canonical state unchanged. A
    /// re-delivered, already-applied action produces a <see cref="Duplicate"/> result with no
    /// additional state change (Requirement 2.3). A pure C# type with no Unity dependency so
    /// the pipeline can be property-tested.
    /// </summary>
    public sealed class ActionResult
    {
        /// <summary>True when the action was applied to the canonical state (includes idempotent duplicates).</summary>
        public bool Success { get; }

        /// <summary>
        /// True when this result reflects a re-delivery of an already-applied action, which
        /// caused no additional state change (Requirement 2.3).
        /// </summary>
        public bool WasDuplicate { get; }

        /// <summary>The reason the action was rejected; null when <see cref="Success"/> is true.</summary>
        public RejectionReason? Reason { get; }

        private ActionResult(bool success, bool wasDuplicate, RejectionReason? reason)
        {
            Success = success;
            WasDuplicate = wasDuplicate;
            Reason = reason;
        }

        /// <summary>Creates a result for an action that was validated and applied exactly once.</summary>
        public static ActionResult Accepted()
        {
            return new ActionResult(true, false, null);
        }

        /// <summary>
        /// Creates a result for a re-delivered action whose id was already applied; no
        /// additional change was made to the canonical state (Requirement 2.3).
        /// </summary>
        public static ActionResult Duplicate()
        {
            return new ActionResult(true, true, null);
        }

        /// <summary>Creates a result for an action rejected with the given reason, leaving state unchanged.</summary>
        public static ActionResult Rejected(RejectionReason reason)
        {
            return new ActionResult(false, false, reason);
        }
    }
}
