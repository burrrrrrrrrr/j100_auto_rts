using System.Collections.Generic;
using MultiplayerRts.Core;
using MultiplayerRts.Net;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerRts.Client
{
    /// <summary>
    /// The thin, Unity-specific placeholder renderer (task 14.2, Requirements 10.1–10.5). It is
    /// the concrete <see cref="IUnitRenderer"/> the pure <see cref="GameClientStateMirror"/>
    /// drives once per ingested snapshot; it owns no game logic and holds no canonical state. It
    /// turns the immutable <see cref="ClientRenderView"/> into GameObjects/sprites and HUD text
    /// by delegating every decision to the pure resolvers/formatter:
    ///
    /// <list type="bullet">
    /// <item><description>
    /// <b>Unit color (Requirement 10.1):</b> <see cref="UnitColorResolver"/> maps each unit's
    /// owner to a unique <see cref="RenderColor"/>, converted here to <c>UnityEngine.Color</c>.
    /// </description></item>
    /// <item><description>
    /// <b>Structure sprite (Requirements 10.2, 10.5):</b> <see cref="StructureSpriteResolver"/>
    /// maps each structure's type to a unique sprite key, or the fallback key when none is
    /// assigned; this behaviour looks the key up in <see cref="_spriteTable"/> and, on a miss,
    /// draws <see cref="_fallbackSprite"/> and continues (no interruption, Requirement 10.5).
    /// </description></item>
    /// <item><description>
    /// <b>Numeric displays (Requirements 10.3, 10.4):</b> <see cref="NumericDisplayFormatter"/>
    /// formats each player's resource total and base health as clamped integers assigned to the
    /// bound HUD text fields.
    /// </description></item>
    /// </list>
    ///
    /// <para>
    /// <b>Totality / no interruption (Requirement 10.5).</b> Each per-object draw is wrapped so a
    /// failure drawing one unit or structure (e.g. a missing sprite or a transient sprite-load
    /// error) is contained: the renderer logs it and proceeds to the remaining objects, matching
    /// the <see cref="IUnitRenderer"/> contract that a view with an unrenderable entity falls back
    /// rather than aborting the frame.
    /// </para>
    ///
    /// <para>
    /// <b>Environment note.</b> This file depends on <c>UnityEngine</c> (MonoBehaviour,
    /// SpriteRenderer, Color) and <c>UnityEngine.UI</c> (Text). It compiles only after a Unity
    /// package restore brings those assemblies into the project; the pure resolvers and formatter
    /// it delegates to compile and are smoke-tested independently with <c>dotnet</c>.
    /// </para>
    /// </summary>
    public sealed class UnitSpriteRendererBehaviour : MonoBehaviour, IUnitRenderer
    {
        [Header("Unit rendering (Req 10.1)")]
        [SerializeField]
        private GameObject _unitSpritePrefab;

        [Header("Structure rendering (Req 10.2, 10.5)")]
        [SerializeField]
        private GameObject _structureSpritePrefab;

        [Tooltip("Placeholder sprites keyed by the resolver's sprite key (structure.spawner, ...).")]
        [SerializeField]
        private SpriteEntry[] _spriteEntries;

        [Tooltip("Default fallback sprite drawn when a structure type has no assigned sprite (Req 10.5).")]
        [SerializeField]
        private Sprite _fallbackSprite;

        [Header("Numeric HUD (Req 10.3, 10.4)")]
        [SerializeField]
        private PlayerHudBinding[] _playerHud;

        private readonly UnitColorResolver _colorResolver = new UnitColorResolver();
        private readonly StructureSpriteResolver _spriteResolver = new StructureSpriteResolver();

        private readonly Dictionary<string, Sprite> _spriteTable = new Dictionary<string, Sprite>();
        private readonly Dictionary<UnitId, GameObject> _unitObjects = new Dictionary<UnitId, GameObject>();
        private readonly Dictionary<StructureId, GameObject> _structureObjects = new Dictionary<StructureId, GameObject>();

        private bool _spriteTableBuilt;

        /// <summary>
        /// A sprite-table entry wiring a resolver key (e.g. <c>"structure.spawner"</c>) to the
        /// placeholder <see cref="Sprite"/> for that key, assigned in the inspector.
        /// </summary>
        [System.Serializable]
        public struct SpriteEntry
        {
            public string Key;
            public Sprite Sprite;
        }

        /// <summary>
        /// Binds one player's resource/health HUD text fields to a <see cref="PlayerId"/> so the
        /// formatter's integer strings can be assigned on each frame (Requirements 10.3, 10.4).
        /// </summary>
        [System.Serializable]
        public struct PlayerHudBinding
        {
            public uint PlayerIdValue;
            public Text ResourceText;
            public Text BaseHealthText;
        }

        /// <inheritdoc />
        public void Render(ClientRenderView view)
        {
            if (view == null)
            {
                return;
            }

            BuildSpriteTableIfNeeded();

            RenderUnits(view);
            RenderStructures(view);
            RenderNumericDisplays(view);
        }

        private void BuildSpriteTableIfNeeded()
        {
            if (_spriteTableBuilt)
            {
                return;
            }

            if (_spriteEntries != null)
            {
                for (int i = 0; i < _spriteEntries.Length; i++)
                {
                    SpriteEntry entry = _spriteEntries[i];
                    if (!string.IsNullOrEmpty(entry.Key) && entry.Sprite != null)
                    {
                        _spriteTable[entry.Key] = entry.Sprite;
                    }
                }
            }

            _spriteTableBuilt = true;
        }

        // ---- Units: position + per-owner color (Req 6.5, 10.1) ----

        private void RenderUnits(ClientRenderView view)
        {
            var seen = new HashSet<UnitId>();

            for (int i = 0; i < view.Units.Count; i++)
            {
                RenderUnit unit = view.Units[i];
                seen.Add(unit.Id);

                // Contain per-unit draw failures so one bad unit never aborts the frame (Req 10.5).
                try
                {
                    DrawUnit(unit);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[Renderer] Skipped unit {unit.Id}: {ex.Message}");
                }
            }

            RemoveStale(_unitObjects, seen);
        }

        private void DrawUnit(RenderUnit unit)
        {
            GameObject go = GetOrCreateUnitObject(unit.Id);
            go.transform.position = new Vector3(unit.Position.X, unit.Position.Y, 0f);

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = ToUnityColor(_colorResolver.Resolve(unit.Owner));
            }
        }

        private GameObject GetOrCreateUnitObject(UnitId id)
        {
            if (_unitObjects.TryGetValue(id, out GameObject existing) && existing != null)
            {
                return existing;
            }

            GameObject go = _unitSpritePrefab != null
                ? Instantiate(_unitSpritePrefab, transform)
                : CreateDefaultSpriteObject($"Unit_{id.Value}");

            _unitObjects[id] = go;
            return go;
        }

        // ---- Structures: per-type sprite with fallback (Req 10.2, 10.5) ----

        private void RenderStructures(ClientRenderView view)
        {
            var seen = new HashSet<StructureId>();

            for (int i = 0; i < view.Structures.Count; i++)
            {
                StructureSnapshot structure = view.Structures[i];
                seen.Add(structure.Id);

                // Contain per-structure draw failures (Req 10.5).
                try
                {
                    DrawStructure(structure);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[Renderer] Skipped structure {structure.Id}: {ex.Message}");
                }
            }

            RemoveStale(_structureObjects, seen);
        }

        private void DrawStructure(StructureSnapshot structure)
        {
            GameObject go = GetOrCreateStructureObject(structure.Id);

            Footprint fp = structure.Footprint;
            go.transform.position = new Vector3(fp.Origin.X, fp.Origin.Y, 0f);

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = ResolveStructureSprite(structure.Type);
            }
        }

        /// <summary>
        /// Resolves the sprite for a structure type via the pure resolver, then looks the key up
        /// in the sprite table. A missing assignment (resolver returns the fallback key) or a key
        /// absent from the table both fall back to <see cref="_fallbackSprite"/> — the frame keeps
        /// drawing (Requirement 10.5).
        /// </summary>
        private Sprite ResolveStructureSprite(StructureType type)
        {
            bool assigned = _spriteResolver.TryResolve(type, out string key);

            if (_spriteTable.TryGetValue(key, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            if (!assigned)
            {
                Debug.Log($"[Renderer] No sprite assigned for {type}; using fallback.");
            }

            return _fallbackSprite;
        }

        private GameObject GetOrCreateStructureObject(StructureId id)
        {
            if (_structureObjects.TryGetValue(id, out GameObject existing) && existing != null)
            {
                return existing;
            }

            GameObject go = _structureSpritePrefab != null
                ? Instantiate(_structureSpritePrefab, transform)
                : CreateDefaultSpriteObject($"Structure_{id.Value}");

            _structureObjects[id] = go;
            return go;
        }

        // ---- Numeric displays: resource + base health as clamped integers (Req 10.3, 10.4) ----

        private void RenderNumericDisplays(ClientRenderView view)
        {
            if (_playerHud == null)
            {
                return;
            }

            for (int i = 0; i < _playerHud.Length; i++)
            {
                PlayerHudBinding hud = _playerHud[i];
                if (!TryFindPlayer(view, hud.PlayerIdValue, out PlayerSnapshot player))
                {
                    continue;
                }

                try
                {
                    if (hud.ResourceText != null)
                    {
                        hud.ResourceText.text = NumericDisplayFormatter.FormatResourceTotal(player.ResourceTotal);
                    }

                    if (hud.BaseHealthText != null)
                    {
                        hud.BaseHealthText.text = NumericDisplayFormatter.FormatBaseHealth(player.BaseHealth, player.BaseMaxHealth);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[Renderer] Skipped HUD for player {hud.PlayerIdValue}: {ex.Message}");
                }
            }
        }

        private static bool TryFindPlayer(ClientRenderView view, uint playerIdValue, out PlayerSnapshot player)
        {
            for (int i = 0; i < view.Players.Count; i++)
            {
                if (view.Players[i].Id.Value == playerIdValue)
                {
                    player = view.Players[i];
                    return true;
                }
            }

            player = default;
            return false;
        }

        // ---- Shared helpers ----

        private static UnityEngine.Color ToUnityColor(RenderColor c) => new UnityEngine.Color(c.R, c.G, c.B, c.A);

        private GameObject CreateDefaultSpriteObject(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.AddComponent<SpriteRenderer>();
            return go;
        }

        /// <summary>
        /// Destroys render objects whose keys are no longer present in the latest view (units
        /// removed, structures gone), so the scene tracks the replicated set.
        /// </summary>
        private static void RemoveStale<TKey>(Dictionary<TKey, GameObject> objects, HashSet<TKey> seen)
        {
            if (objects.Count == 0)
            {
                return;
            }

            var toRemove = new List<TKey>();
            foreach (KeyValuePair<TKey, GameObject> kvp in objects)
            {
                if (!seen.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                GameObject go = objects[toRemove[i]];
                if (go != null)
                {
                    Destroy(go);
                }

                objects.Remove(toRemove[i]);
            }
        }
    }
}
