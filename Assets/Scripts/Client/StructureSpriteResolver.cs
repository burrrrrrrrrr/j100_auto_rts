using System.Collections.Generic;
using MultiplayerRts.Core;

namespace MultiplayerRts.Client
{
    /// <summary>
    /// Pure per-type structure sprite-key resolver (Requirements 10.2, 10.5): maps a
    /// <see cref="StructureType"/> to a placeholder sprite <em>key</em> (a string identifier the
    /// thin renderer looks up in its sprite table) that is <em>unique per type</em> — Spawner,
    /// Defensive, and Economy resolve to three distinct keys, and the same type always resolves
    /// to the same key. It carries no Unity dependency so both the uniqueness contract and the
    /// missing-sprite fallback are verifiable out-of-tree.
    ///
    /// <para>
    /// <b>Fallback without interruption (Requirement 10.5).</b> When a type has no key assigned
    /// — because the map contains no entry for it, or an unknown/out-of-range enum value reaches
    /// the resolver — <see cref="Resolve"/> returns the configured <see cref="FallbackKey"/>
    /// rather than throwing. This lets the renderer substitute a default placeholder for the one
    /// missing entry and keep drawing every other object. <see cref="TryResolve"/> exposes
    /// whether a real assignment existed, so a renderer can log the gap while still rendering.
    /// </para>
    ///
    /// <para>
    /// The default assignment gives each type a stable, human-readable key
    /// (<c>"structure.spawner"</c>, <c>"structure.defensive"</c>, <c>"structure.economy"</c>) and
    /// a default fallback of <c>"structure.default"</c>. All three type keys and the fallback are
    /// distinct, which the uniqueness contract relies on.
    /// </para>
    /// </summary>
    public sealed class StructureSpriteResolver
    {
        /// <summary>The default fallback sprite key returned when a type has no assignment (Requirement 10.5).</summary>
        public const string DefaultFallbackKey = "structure.default";

        private static readonly IReadOnlyDictionary<StructureType, string> DefaultKeys =
            new Dictionary<StructureType, string>
            {
                { StructureType.Spawner, "structure.spawner" },
                { StructureType.Defensive, "structure.defensive" },
                { StructureType.Economy, "structure.economy" },
            };

        private readonly Dictionary<StructureType, string> _keys;

        /// <summary>The key returned for any type that has no assigned sprite (Requirement 10.5).</summary>
        public string FallbackKey { get; }

        /// <summary>Constructs a resolver with the built-in per-type keys and default fallback.</summary>
        public StructureSpriteResolver()
            : this(DefaultKeys, DefaultFallbackKey)
        {
        }

        /// <summary>
        /// Constructs a resolver over a custom <paramref name="keys"/> map and
        /// <paramref name="fallbackKey"/>. A null map yields a resolver with no assignments (every
        /// type falls back); a null/empty fallback is replaced with <see cref="DefaultFallbackKey"/>
        /// so the resolver always has a usable default.
        /// </summary>
        public StructureSpriteResolver(IReadOnlyDictionary<StructureType, string> keys, string fallbackKey)
        {
            _keys = new Dictionary<StructureType, string>();
            if (keys != null)
            {
                foreach (KeyValuePair<StructureType, string> kvp in keys)
                {
                    if (!string.IsNullOrEmpty(kvp.Value))
                    {
                        _keys[kvp.Key] = kvp.Value;
                    }
                }
            }

            FallbackKey = string.IsNullOrEmpty(fallbackKey) ? DefaultFallbackKey : fallbackKey;
        }

        /// <summary>
        /// Returns the sprite key for <paramref name="type"/>: the assigned unique-per-type key,
        /// or <see cref="FallbackKey"/> when none is assigned (Requirements 10.2, 10.5). Never
        /// throws, so resolving a missing key does not interrupt resolving the rest.
        /// </summary>
        public string Resolve(StructureType type)
        {
            return _keys.TryGetValue(type, out string key) ? key : FallbackKey;
        }

        /// <summary>
        /// Like <see cref="Resolve"/> but reports whether a real assignment existed:
        /// <paramref name="key"/> is set to the assigned key and returns <c>true</c> when the type
        /// is mapped, or set to <see cref="FallbackKey"/> and returns <c>false</c> when it falls
        /// back. Lets the renderer note a content gap (e.g. log once) while still drawing the
        /// fallback and continuing (Requirement 10.5).
        /// </summary>
        public bool TryResolve(StructureType type, out string key)
        {
            if (_keys.TryGetValue(type, out key))
            {
                return true;
            }

            key = FallbackKey;
            return false;
        }
    }
}
