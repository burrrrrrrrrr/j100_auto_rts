using System;
using System.Collections.Generic;
using MultiplayerRts.Core;
using MultiplayerRts.Net;

namespace MultiplayerRts.Client
{
    /// <summary>
    /// The immutable renderable view the pure client mirror exposes after ingesting a
    /// replicated <see cref="MatchStateSnapshot"/> (Requirements 6.5, 10.x). It is exactly the
    /// data a renderer needs to draw a frame — units at their reported positions keyed by id,
    /// placed structures, per-player resource totals and base health, and the match
    /// phase/outcome — with nothing Unity-specific. Keeping the view pure lets the
    /// mirror→render update path be verified out-of-tree; the concrete sprite renderer
    /// (task 14.2) consumes this view through the <see cref="IUnitRenderer"/> seam.
    ///
    /// <para>
    /// The view is a value-like projection of the most recent snapshot. Units are exposed both
    /// as an ordered list (stable draw order) and by id (so a renderer can move an existing
    /// sprite to its new position rather than rebuild every frame). The latest rejection and
    /// outcome are surfaced here so the presentation layer reads a single coherent view object.
    /// </para>
    /// </summary>
    public sealed class ClientRenderView
    {
        /// <summary>An empty view, used before any snapshot has been ingested.</summary>
        public static readonly ClientRenderView Empty = new ClientRenderView(
            0L,
            MatchPhase.Lobby,
            null,
            Array.Empty<PlayerSnapshot>(),
            Array.Empty<RenderUnit>(),
            Array.Empty<StructureSnapshot>(),
            null);

        /// <summary>The source snapshot's revision, so a renderer can drop a stale view.</summary>
        public long Revision { get; }

        /// <summary>The replicated match phase (Lobby/Active/Paused/Ended).</summary>
        public MatchPhase Phase { get; }

        /// <summary>
        /// The frozen outcome once the match ended, or null while in progress. Mirrored from
        /// the persistent replicated snapshot so a client that missed the one-off result event
        /// still converges to it (Requirement 8.7).
        /// </summary>
        public MatchOutcomeSnapshot? Outcome { get; }

        /// <summary>
        /// Per-player replicated state: resource totals and base health the presentation layer
        /// renders as integers (Requirements 10.3, 10.4). Ordered by player id.
        /// </summary>
        public IReadOnlyList<PlayerSnapshot> Players { get; }

        /// <summary>
        /// All live units at their reported positions, in a stable draw order (by unit id).
        /// A renderer draws each unit here at <see cref="RenderUnit.Position"/> (Requirements
        /// 6.5, 10.1).
        /// </summary>
        public IReadOnlyList<RenderUnit> Units { get; }

        /// <summary>
        /// Fast lookup of a unit's current render state by id, so a renderer can update an
        /// existing sprite's position on replication instead of rebuilding the scene.
        /// </summary>
        public IReadOnlyDictionary<UnitId, RenderUnit> UnitsById { get; }

        /// <summary>All placed structures with location, owner, and type (Requirement 10.2). Ordered by id.</summary>
        public IReadOnlyList<StructureSnapshot> Structures { get; }

        /// <summary>
        /// The most recent server rejection surfaced to this client, or null if none since the
        /// last clear (Requirement 2.4). The mirror sets this from <c>OnActionRejected</c>.
        /// </summary>
        public RejectionReason? LastRejection { get; }

        public ClientRenderView(
            long revision,
            MatchPhase phase,
            MatchOutcomeSnapshot? outcome,
            IReadOnlyList<PlayerSnapshot> players,
            IReadOnlyList<RenderUnit> units,
            IReadOnlyList<StructureSnapshot> structures,
            RejectionReason? lastRejection)
        {
            Revision = revision;
            Phase = phase;
            Outcome = outcome;
            Players = players ?? Array.Empty<PlayerSnapshot>();
            Units = units ?? Array.Empty<RenderUnit>();
            Structures = structures ?? Array.Empty<StructureSnapshot>();
            LastRejection = lastRejection;

            var byId = new Dictionary<UnitId, RenderUnit>(Units.Count);
            for (int i = 0; i < Units.Count; i++)
            {
                byId[Units[i].Id] = Units[i];
            }

            UnitsById = byId;
        }

        /// <summary>
        /// Returns this view with <paramref name="lastRejection"/> replacing the surfaced
        /// rejection, keeping every other field identical. Used by the mirror so recording a
        /// rejection does not disturb the rendered world (Requirement 2.4 is a client-side
        /// notice, not a state change).
        /// </summary>
        public ClientRenderView WithRejection(RejectionReason? lastRejection)
        {
            return new ClientRenderView(
                Revision,
                Phase,
                Outcome,
                Players,
                Units,
                Structures,
                lastRejection);
        }
    }

    /// <summary>
    /// A single unit's renderable state: identity, owner (which drives the per-owner color in
    /// task 14.2, Requirement 10.1), the reported <see cref="Position"/> the renderer draws at
    /// (Requirement 6.5), and whether it is held (so a renderer may choose not to animate it).
    /// A pure projection of <see cref="UnitSnapshot"/> — the view carries exactly the fields a
    /// renderer reads, decoupling the render seam from the full replicated snapshot shape.
    /// </summary>
    public readonly struct RenderUnit : IEquatable<RenderUnit>
    {
        public readonly UnitId Id;
        public readonly PlayerId Owner;
        public readonly MapVector Position;
        public readonly float Health;
        public readonly bool IsHeld;

        public RenderUnit(UnitId id, PlayerId owner, MapVector position, float health, bool isHeld)
        {
            Id = id;
            Owner = owner;
            Position = position;
            Health = health;
            IsHeld = isHeld;
        }

        /// <summary>Projects a replicated <see cref="UnitSnapshot"/> into a renderable unit.</summary>
        public static RenderUnit From(UnitSnapshot snapshot)
        {
            return new RenderUnit(snapshot.Id, snapshot.Owner, snapshot.Position, snapshot.Health, snapshot.IsHeld);
        }

        public bool Equals(RenderUnit other)
        {
            return Id == other.Id
                && Owner == other.Owner
                && Position == other.Position
                && Health.Equals(other.Health)
                && IsHeld == other.IsHeld;
        }

        public override bool Equals(object obj) => obj is RenderUnit other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ Owner.GetHashCode();
                hash = (hash * 397) ^ Position.GetHashCode();
                hash = (hash * 397) ^ Health.GetHashCode();
                hash = (hash * 397) ^ (IsHeld ? 1 : 0);
                return hash;
            }
        }
    }
}
