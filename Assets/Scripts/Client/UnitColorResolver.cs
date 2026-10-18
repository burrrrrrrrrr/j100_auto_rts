using System.Collections.Generic;
using MultiplayerRts.Core;

namespace MultiplayerRts.Client
{
    /// <summary>
    /// Pure per-owner unit color resolver (Requirement 10.1): maps a <see cref="PlayerId"/> to a
    /// placeholder <see cref="RenderColor"/> that is <em>unique per owner</em> — two units with
    /// different owners resolve to distinct colors, and two units with the same owner resolve to
    /// the identical color. It has no Unity dependency so the uniqueness contract is verifiable
    /// out-of-tree; the thin renderer turns the returned <see cref="RenderColor"/> into a
    /// <c>UnityEngine.Color</c>.
    ///
    /// <para>
    /// <b>Assignment strategy.</b> A fixed palette of visually distinct colors backs the common
    /// case (a 1v1 match needs only two). Each distinct <see cref="PlayerId"/> is assigned the
    /// next unused palette entry <em>in first-seen order</em> and that assignment is remembered,
    /// so the same owner always maps to the same color (stable across frames) and a different
    /// owner never collides with an already-assigned one. If more owners appear than the palette
    /// has entries (not expected in a 1v1 scope, but kept total), a deterministic generated color
    /// derived from the player's value and assignment index is used; the index keeps generated
    /// colors apart from one another and from the palette so distinctness is preserved.
    /// </para>
    ///
    /// <para>
    /// <b>Totality.</b> Resolving is a pure lookup/assignment that never throws. The resolver is
    /// stateful only in that it remembers assignments (to keep same-owner colors identical); it
    /// is not thread-safe and is driven from the single render path.
    /// </para>
    /// </summary>
    public sealed class UnitColorResolver
    {
        /// <summary>
        /// The default placeholder palette: saturated, mutually distinct colors. The first two
        /// (blue, red) cover a 1v1 match; the rest give headroom. All entries are distinct, which
        /// the uniqueness contract relies on.
        /// </summary>
        private static readonly RenderColor[] DefaultPalette =
        {
            new RenderColor(0.20f, 0.45f, 0.95f), // blue
            new RenderColor(0.90f, 0.25f, 0.20f), // red
            new RenderColor(0.25f, 0.75f, 0.30f), // green
            new RenderColor(0.95f, 0.80f, 0.20f), // yellow
            new RenderColor(0.65f, 0.35f, 0.85f), // purple
            new RenderColor(0.95f, 0.55f, 0.15f), // orange
            new RenderColor(0.20f, 0.80f, 0.80f), // cyan
            new RenderColor(0.95f, 0.45f, 0.75f), // pink
        };

        private readonly RenderColor[] _palette;
        private readonly Dictionary<PlayerId, RenderColor> _assigned = new Dictionary<PlayerId, RenderColor>();

        /// <summary>Constructs a resolver over the built-in placeholder palette.</summary>
        public UnitColorResolver()
            : this(DefaultPalette)
        {
        }

        /// <summary>
        /// Constructs a resolver over a custom <paramref name="palette"/>. The palette entries
        /// should be mutually distinct; a null or empty palette falls back to the built-in one so
        /// the resolver is always usable.
        /// </summary>
        public UnitColorResolver(RenderColor[] palette)
        {
            _palette = (palette != null && palette.Length > 0) ? palette : DefaultPalette;
        }

        /// <summary>
        /// Returns the placeholder color for <paramref name="owner"/>: the same color every time
        /// for a given owner, and a color distinct from every other owner's (Requirement 10.1).
        /// </summary>
        public RenderColor Resolve(PlayerId owner)
        {
            if (_assigned.TryGetValue(owner, out RenderColor existing))
            {
                return existing;
            }

            int index = _assigned.Count;
            RenderColor color = index < _palette.Length
                ? _palette[index]
                : GenerateDistinctColor(owner, index);

            _assigned[owner] = color;
            return color;
        }

        /// <summary>
        /// Deterministic fallback color for owners beyond the palette size. Derives hue-like
        /// channels from the player value and folds in the assignment <paramref name="index"/> so
        /// successive overflow owners stay apart from each other and from the fixed palette.
        /// </summary>
        private static RenderColor GenerateDistinctColor(PlayerId owner, int index)
        {
            // Spread entries around the color wheel using the golden-ratio conjugate so
            // successive indices land far apart; mix in the owner value so distinct owners that
            // happen to share an index slot (they cannot here, but keep it defensive) still differ.
            unchecked
            {
                uint seed = owner.Value * 2654435761u + (uint)index * 40503u;
                float r = ((seed & 0xFF) / 255f);
                float g = (((seed >> 8) & 0xFF) / 255f);
                float b = (((seed >> 16) & 0xFF) / 255f);
                return new RenderColor(r, g, b);
            }
        }
    }
}
