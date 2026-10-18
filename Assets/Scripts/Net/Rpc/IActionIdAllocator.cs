namespace MultiplayerRts.Net
{
    /// <summary>
    /// Allocates the match-unique <c>ActionId</c> that every <see cref="MultiplayerRts.Core.PlayerAction"/>
    /// carries so the canonical pipeline can apply each action exactly once and ignore a
    /// re-delivered id (Requirement 2.3). Core never mints these itself — a
    /// <see cref="MultiplayerRts.Core.PlacementAction"/> is handed an id at construction — so
    /// the server boundary that turns an incoming placement RPC into an action is responsible
    /// for allocating one. This mirrors Core's existing id-allocator pattern
    /// (<c>IStructureIdAllocator</c>, <c>IUnitIdAllocator</c>): a tiny injectable so the thin
    /// NGO behaviour stays free of id bookkeeping and the pure translator can be driven by a
    /// deterministic stub in the smoke check and later property tests.
    ///
    /// <para>
    /// Implementations must return a distinct id on every call for the lifetime of a match so
    /// two placements from the same player are never collapsed as duplicates by the pipeline's
    /// (player, actionId) de-dup key.
    /// </para>
    /// </summary>
    public interface IActionIdAllocator
    {
        /// <summary>Returns an action id not previously returned for this match.</summary>
        uint Next();
    }

    /// <summary>
    /// The default server-side <see cref="IActionIdAllocator"/>: a monotonic counter. The
    /// server is the single assigner of action ids for actions it constructs from incoming
    /// RPCs, so a plain incrementing counter yields ids that are distinct for the match's
    /// lifetime without any coordination. Not thread-safe by design — NGO dispatches server
    /// RPC handlers on the main update loop, so allocation is single-threaded.
    /// </summary>
    public sealed class SequentialActionIdAllocator : IActionIdAllocator
    {
        private uint _next;

        /// <inheritdoc />
        public uint Next()
        {
            // Post-increment: first id is 0, matching the natural "nth action" numbering and
            // the de-dup key's uint domain. Wraparound after uint.MaxValue is not a concern at
            // the scale of a single 1v1 match (a player would have to place billions of
            // structures).
            return _next++;
        }
    }
}
