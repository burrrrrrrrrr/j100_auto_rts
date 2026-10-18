namespace MultiplayerRts.Client
{
    /// <summary>
    /// The seam between the pure client mirror and concrete Unity rendering. When the mirror
    /// ingests a replicated snapshot it rebuilds the <see cref="ClientRenderView"/> and calls
    /// <see cref="Render"/> once, handing the renderer the full current view; the renderer
    /// positions unit sprites at their reported positions (Requirement 6.5) and draws
    /// structures and numeric displays (Requirements 10.x). Keeping rendering behind an
    /// interface makes the mirror→render update path testable out-of-tree (a fake renderer can
    /// record the views it was handed) while the concrete GameObject/sprite renderer — the
    /// Unity-specific part that compiles only after package restore — lives on the other side.
    ///
    /// <para>
    /// This is intentionally a thin, idempotent "draw the whole current view" contract rather
    /// than a stream of deltas: the mirror always holds the latest coherent view, so the
    /// renderer can diff against its own previous frame if it wants to move existing sprites
    /// (<see cref="ClientRenderView.UnitsById"/> supports that) without the mirror tracking
    /// render identity. Task 14.2 implements the concrete renderer (per-owner unit colors,
    /// per-type structure sprites, resource/health numeric displays, and the missing-sprite
    /// fallback) against this same seam.
    /// </para>
    /// </summary>
    public interface IUnitRenderer
    {
        /// <summary>
        /// Draws the current renderable <paramref name="view"/>: units at their reported
        /// positions, structures, and per-player numeric displays. Called by the mirror after
        /// each ingested snapshot (Requirement 6.5). Implementations must be total — a view
        /// with an entity the renderer has no sprite for falls back to a default rather than
        /// aborting the frame (Requirement 10.5, realized in task 14.2).
        /// </summary>
        void Render(ClientRenderView view);
    }
}
