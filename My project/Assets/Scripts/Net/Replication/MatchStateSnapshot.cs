using System;
using System.Collections.Generic;
using MultiplayerRts.Core;

namespace MultiplayerRts.Net
{
    /// <summary>
    /// An immutable, framework-independent projection of the enumerated canonical
    /// <see cref="MatchState"/> that the server replicates to clients (Requirements 2.5,
    /// 2.6, 3.5, 4.6, 8.7): unit positions, structures, per-player resource totals and
    /// base health, and the match phase/outcome. This is the <em>pure</em> half of the
    /// replication design — it has no Netcode dependency, so it can be property-tested
    /// out-of-tree against Core. The thin <c>MatchReplicationBehaviour</c> maps this
    /// snapshot onto <c>NetworkVariable</c>s and pushes it when it changes, and a
    /// reconnecting/late client converges by receiving the current snapshot in full.
    ///
    /// <para>
    /// The snapshot is a value-like copy: it captures only the fields clients need to
    /// render and reconcile, not the full server-side simulation (timers, path objects,
    /// build areas). It carries the source <see cref="Revision"/> so the projector can
    /// detect "has the canonical state changed since the last published snapshot?" without
    /// per-field diffing, matching <see cref="MatchState.MarkChanged"/>'s coarse per-tick
    /// marker contract.
    /// </para>
    /// </summary>
    public sealed class MatchStateSnapshot : IEquatable<MatchStateSnapshot>
    {
        /// <summary>
        /// The <see cref="MatchState.Revision"/> this snapshot was projected from. The
        /// projector compares a last-published revision against the live state's revision
        /// to decide whether a new snapshot must be pushed; clients may use it to drop
        /// stale updates. A snapshot projected directly from a live state always carries
        /// that state's current revision.
        /// </summary>
        public long Revision { get; }

        /// <summary>The replicated match lifecycle phase (Requirement 8: Lobby/Active/Paused/Ended).</summary>
        public MatchPhase Phase { get; }

        /// <summary>
        /// The frozen outcome once the match has ended, or null while in progress. Carried
        /// in persistent state (not only a one-off event) so a client that reconnects after
        /// the match ended still converges to the result (Requirement 8.7).
        /// </summary>
        public MatchOutcomeSnapshot? Outcome { get; }

        /// <summary>
        /// Per-player replicated state (resource total and base health), ordered by player
        /// id for a stable, comparable projection (Requirements 3.5, 2.5).
        /// </summary>
        public IReadOnlyList<PlayerSnapshot> Players { get; }

        /// <summary>All live units, ordered by unit id, with the positions clients render (Requirement 2.5).</summary>
        public IReadOnlyList<UnitSnapshot> Units { get; }

        /// <summary>All placed structures, ordered by structure id, with location and owner (Requirement 4.6).</summary>
        public IReadOnlyList<StructureSnapshot> Structures { get; }

        public MatchStateSnapshot(
            long revision,
            MatchPhase phase,
            MatchOutcomeSnapshot? outcome,
            IReadOnlyList<PlayerSnapshot> players,
            IReadOnlyList<UnitSnapshot> units,
            IReadOnlyList<StructureSnapshot> structures)
        {
            Revision = revision;
            Phase = phase;
            Outcome = outcome;
            Players = players ?? Array.Empty<PlayerSnapshot>();
            Units = units ?? Array.Empty<UnitSnapshot>();
            Structures = structures ?? Array.Empty<StructureSnapshot>();
        }

        /// <summary>
        /// Structural value-equality over the replicated fields (ignoring
        /// <see cref="Revision"/>). The projector uses this to tell a genuine canonical
        /// change from a bumped-but-identical revision, so the Net layer never pushes a
        /// no-op snapshot that differs only by its revision counter.
        /// </summary>
        public bool EqualsIgnoringRevision(MatchStateSnapshot other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Phase == other.Phase
                && Nullable.Equals(Outcome, other.Outcome)
                && SequenceEqual(Players, other.Players)
                && SequenceEqual(Units, other.Units)
                && SequenceEqual(Structures, other.Structures);
        }

        /// <summary>Full value-equality, including <see cref="Revision"/>.</summary>
        public bool Equals(MatchStateSnapshot other)
        {
            return other != null && Revision == other.Revision && EqualsIgnoringRevision(other);
        }

        public override bool Equals(object obj) => obj is MatchStateSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Revision.GetHashCode();
                hash = (hash * 397) ^ Phase.GetHashCode();
                hash = (hash * 397) ^ (Outcome?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ Players.Count;
                hash = (hash * 397) ^ Units.Count;
                hash = (hash * 397) ^ Structures.Count;
                return hash;
            }
        }

        private static bool SequenceEqual<T>(IReadOnlyList<T> a, IReadOnlyList<T> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null || a.Count != b.Count)
            {
                return false;
            }

            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < a.Count; i++)
            {
                if (!comparer.Equals(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Replicated per-player state: the owning <see cref="PlayerId"/>, the current
    /// <see cref="ResourceTotal"/> (pushed on change, Requirement 3.5), and base health
    /// (current and max) so a client can render the health value (Requirement 10.4) and a
    /// reconnecting client converges to it (Requirement 2.6).
    /// </summary>
    public readonly struct PlayerSnapshot : IEquatable<PlayerSnapshot>
    {
        public readonly PlayerId Id;
        public readonly int ResourceTotal;
        public readonly float BaseHealth;
        public readonly float BaseMaxHealth;
        public readonly ConnectionState Connection;

        public PlayerSnapshot(PlayerId id, int resourceTotal, float baseHealth, float baseMaxHealth, ConnectionState connection)
        {
            Id = id;
            ResourceTotal = resourceTotal;
            BaseHealth = baseHealth;
            BaseMaxHealth = baseMaxHealth;
            Connection = connection;
        }

        public bool Equals(PlayerSnapshot other)
        {
            return Id == other.Id
                && ResourceTotal == other.ResourceTotal
                && BaseHealth.Equals(other.BaseHealth)
                && BaseMaxHealth.Equals(other.BaseMaxHealth)
                && Connection == other.Connection;
        }

        public override bool Equals(object obj) => obj is PlayerSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ ResourceTotal;
                hash = (hash * 397) ^ BaseHealth.GetHashCode();
                hash = (hash * 397) ^ BaseMaxHealth.GetHashCode();
                hash = (hash * 397) ^ (int)Connection;
                return hash;
            }
        }
    }

    /// <summary>
    /// Replicated unit state: identity, owner, and the <see cref="Position"/> clients
    /// render at (Requirement 2.5, 6.5). Health is included so clients can reflect damage;
    /// held units are flagged so a client can choose not to animate them.
    /// </summary>
    public readonly struct UnitSnapshot : IEquatable<UnitSnapshot>
    {
        public readonly UnitId Id;
        public readonly PlayerId Owner;
        public readonly MapVector Position;
        public readonly float Health;
        public readonly bool IsHeld;

        public UnitSnapshot(UnitId id, PlayerId owner, MapVector position, float health, bool isHeld)
        {
            Id = id;
            Owner = owner;
            Position = position;
            Health = health;
            IsHeld = isHeld;
        }

        public bool Equals(UnitSnapshot other)
        {
            return Id == other.Id
                && Owner == other.Owner
                && Position == other.Position
                && Health.Equals(other.Health)
                && IsHeld == other.IsHeld;
        }

        public override bool Equals(object obj) => obj is UnitSnapshot other && Equals(other);

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

    /// <summary>
    /// Replicated structure state: identity, owner, type, and footprint (location). Sent
    /// so a newly created structure is replicated to each client (Requirement 4.6) and a
    /// reconnecting client converges to the full set (Requirement 2.6).
    /// </summary>
    public readonly struct StructureSnapshot : IEquatable<StructureSnapshot>
    {
        public readonly StructureId Id;
        public readonly PlayerId Owner;
        public readonly StructureType Type;
        public readonly Footprint Footprint;
        public readonly bool IsActive;

        public StructureSnapshot(StructureId id, PlayerId owner, StructureType type, Footprint footprint, bool isActive)
        {
            Id = id;
            Owner = owner;
            Type = type;
            Footprint = footprint;
            IsActive = isActive;
        }

        public bool Equals(StructureSnapshot other)
        {
            return Id == other.Id
                && Owner == other.Owner
                && Type == other.Type
                && Footprint == other.Footprint
                && IsActive == other.IsActive;
        }

        public override bool Equals(object obj) => obj is StructureSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Id.GetHashCode();
                hash = (hash * 397) ^ Owner.GetHashCode();
                hash = (hash * 397) ^ (int)Type;
                hash = (hash * 397) ^ Footprint.GetHashCode();
                hash = (hash * 397) ^ (IsActive ? 1 : 0);
                return hash;
            }
        }
    }

    /// <summary>
    /// Replicated match outcome mirroring <see cref="MatchOutcome"/>, kept as persistent
    /// state so a client reconnecting after the match ended converges to the result
    /// (Requirements 8.5, 8.7).
    /// </summary>
    public readonly struct MatchOutcomeSnapshot : IEquatable<MatchOutcomeSnapshot>
    {
        public readonly MatchOutcomeKind Kind;
        public readonly PlayerId? Winner;
        public readonly PlayerId? Loser;

        public MatchOutcomeSnapshot(MatchOutcomeKind kind, PlayerId? winner, PlayerId? loser)
        {
            Kind = kind;
            Winner = winner;
            Loser = loser;
        }

        /// <summary>Projects a <see cref="MatchOutcome"/>, or null when the match has no outcome yet.</summary>
        public static MatchOutcomeSnapshot? FromOutcome(MatchOutcome outcome)
        {
            if (outcome == null)
            {
                return null;
            }

            return new MatchOutcomeSnapshot(outcome.Kind, outcome.Winner, outcome.Loser);
        }

        public bool Equals(MatchOutcomeSnapshot other)
        {
            return Kind == other.Kind
                && Nullable.Equals(Winner, other.Winner)
                && Nullable.Equals(Loser, other.Loser);
        }

        public override bool Equals(object obj) => obj is MatchOutcomeSnapshot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ (Winner?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (Loser?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
