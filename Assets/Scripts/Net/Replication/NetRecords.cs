using System;
using MultiplayerRts.Core;
using Unity.Netcode;

namespace MultiplayerRts.Net
{
    /// <summary>
    /// Netcode-wire record structs for the replicated collections. Netcode for GameObjects
    /// requires the element type of a <see cref="NetworkList{T}"/> (and the payload of a
    /// <see cref="NetworkVariable{T}"/>) to be an <c>unmanaged</c> value type that implements
    /// <see cref="IEquatable{T}"/> (NGO uses the equality check to suppress redundant syncs)
    /// and, for custom layouts, <see cref="INetworkSerializable"/>. These structs are the
    /// unmanaged, wire-ready mirrors of the pure <see cref="MatchStateSnapshot"/> entry types:
    /// they carry no reference fields and replace the snapshot's nullable outcome with an
    /// explicit <see cref="NetMatchOutcome.HasOutcome"/> flag so the whole graph stays
    /// <c>unmanaged</c>.
    ///
    /// <para>
    /// These live with the NetworkBehaviour (not in the pure projector) because they depend
    /// on the <c>Unity.Netcode</c> assembly for <see cref="INetworkSerializable"/> and
    /// <see cref="BufferSerializer{T}"/>; they compile only after a Unity package restore.
    /// The pure snapshot types remain Netcode-free and independently testable.
    /// </para>
    /// </summary>
    public struct NetPlayer : INetworkSerializable, IEquatable<NetPlayer>
    {
        public uint Id;
        public int ResourceTotal;
        public float BaseHealth;
        public float BaseMaxHealth;
        public byte Connection;

        public static NetPlayer From(PlayerSnapshot p)
        {
            return new NetPlayer
            {
                Id = p.Id.Value,
                ResourceTotal = p.ResourceTotal,
                BaseHealth = p.BaseHealth,
                BaseMaxHealth = p.BaseMaxHealth,
                Connection = (byte)p.Connection,
            };
        }

        /// <summary>
        /// Rebuilds the pure <see cref="PlayerSnapshot"/> from this wire struct. The inverse of
        /// <see cref="From"/>, used by a client mirror to project replicated state back into the
        /// pure snapshot it renders from (task 14.1).
        /// </summary>
        public PlayerSnapshot ToSnapshot()
        {
            return new PlayerSnapshot(
                new PlayerId(Id),
                ResourceTotal,
                BaseHealth,
                BaseMaxHealth,
                (ConnectionState)Connection);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref ResourceTotal);
            serializer.SerializeValue(ref BaseHealth);
            serializer.SerializeValue(ref BaseMaxHealth);
            serializer.SerializeValue(ref Connection);
        }

        public bool Equals(NetPlayer other)
        {
            return Id == other.Id
                && ResourceTotal == other.ResourceTotal
                && BaseHealth.Equals(other.BaseHealth)
                && BaseMaxHealth.Equals(other.BaseMaxHealth)
                && Connection == other.Connection;
        }

        public override bool Equals(object obj) => obj is NetPlayer other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Id;
                hash = (hash * 397) ^ ResourceTotal;
                hash = (hash * 397) ^ BaseHealth.GetHashCode();
                hash = (hash * 397) ^ BaseMaxHealth.GetHashCode();
                hash = (hash * 397) ^ Connection;
                return hash;
            }
        }
    }

    /// <summary>Unmanaged wire mirror of <see cref="UnitSnapshot"/> (Requirements 2.5, 6.5).</summary>
    public struct NetUnit : INetworkSerializable, IEquatable<NetUnit>
    {
        public uint Id;
        public uint Owner;
        public float PositionX;
        public float PositionY;
        public float Health;
        public bool IsHeld;

        public static NetUnit From(UnitSnapshot u)
        {
            return new NetUnit
            {
                Id = u.Id.Value,
                Owner = u.Owner.Value,
                PositionX = u.Position.X,
                PositionY = u.Position.Y,
                Health = u.Health,
                IsHeld = u.IsHeld,
            };
        }

        /// <summary>Rebuilds the pure <see cref="UnitSnapshot"/> from this wire struct (inverse of <see cref="From"/>).</summary>
        public UnitSnapshot ToSnapshot()
        {
            return new UnitSnapshot(
                new UnitId(Id),
                new PlayerId(Owner),
                new MapVector(PositionX, PositionY),
                Health,
                IsHeld);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Owner);
            serializer.SerializeValue(ref PositionX);
            serializer.SerializeValue(ref PositionY);
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref IsHeld);
        }

        public bool Equals(NetUnit other)
        {
            return Id == other.Id
                && Owner == other.Owner
                && PositionX.Equals(other.PositionX)
                && PositionY.Equals(other.PositionY)
                && Health.Equals(other.Health)
                && IsHeld == other.IsHeld;
        }

        public override bool Equals(object obj) => obj is NetUnit other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Id;
                hash = (hash * 397) ^ (int)Owner;
                hash = (hash * 397) ^ PositionX.GetHashCode();
                hash = (hash * 397) ^ PositionY.GetHashCode();
                hash = (hash * 397) ^ Health.GetHashCode();
                hash = (hash * 397) ^ (IsHeld ? 1 : 0);
                return hash;
            }
        }
    }

    /// <summary>Unmanaged wire mirror of <see cref="StructureSnapshot"/> (Requirement 4.6).</summary>
    public struct NetStructure : INetworkSerializable, IEquatable<NetStructure>
    {
        public uint Id;
        public uint Owner;
        public byte Type;
        public float OriginX;
        public float OriginY;
        public float SizeX;
        public float SizeY;
        public bool IsActive;

        public static NetStructure From(StructureSnapshot s)
        {
            return new NetStructure
            {
                Id = s.Id.Value,
                Owner = s.Owner.Value,
                Type = (byte)s.Type,
                OriginX = s.Footprint.Origin.X,
                OriginY = s.Footprint.Origin.Y,
                SizeX = s.Footprint.Size.X,
                SizeY = s.Footprint.Size.Y,
                IsActive = s.IsActive,
            };
        }

        /// <summary>Rebuilds the pure <see cref="StructureSnapshot"/> from this wire struct (inverse of <see cref="From"/>).</summary>
        public StructureSnapshot ToSnapshot()
        {
            return new StructureSnapshot(
                new StructureId(Id),
                new PlayerId(Owner),
                (StructureType)Type,
                new Footprint(new MapVector(OriginX, OriginY), new MapVector(SizeX, SizeY)),
                IsActive);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Owner);
            serializer.SerializeValue(ref Type);
            serializer.SerializeValue(ref OriginX);
            serializer.SerializeValue(ref OriginY);
            serializer.SerializeValue(ref SizeX);
            serializer.SerializeValue(ref SizeY);
            serializer.SerializeValue(ref IsActive);
        }

        public bool Equals(NetStructure other)
        {
            return Id == other.Id
                && Owner == other.Owner
                && Type == other.Type
                && OriginX.Equals(other.OriginX)
                && OriginY.Equals(other.OriginY)
                && SizeX.Equals(other.SizeX)
                && SizeY.Equals(other.SizeY)
                && IsActive == other.IsActive;
        }

        public override bool Equals(object obj) => obj is NetStructure other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Id;
                hash = (hash * 397) ^ (int)Owner;
                hash = (hash * 397) ^ Type;
                hash = (hash * 397) ^ OriginX.GetHashCode();
                hash = (hash * 397) ^ OriginY.GetHashCode();
                hash = (hash * 397) ^ SizeX.GetHashCode();
                hash = (hash * 397) ^ SizeY.GetHashCode();
                hash = (hash * 397) ^ (IsActive ? 1 : 0);
                return hash;
            }
        }
    }

    /// <summary>
    /// Unmanaged wire mirror of the match outcome (Requirements 8.5, 8.7). Replaces the pure
    /// snapshot's <c>MatchOutcomeSnapshot?</c> with an explicit <see cref="HasOutcome"/> flag
    /// plus validity flags for winner/loser so the struct stays <c>unmanaged</c> and carries
    /// no nullable reference payload across the wire.
    /// </summary>
    public struct NetMatchOutcome : INetworkSerializable, IEquatable<NetMatchOutcome>
    {
        public bool HasOutcome;
        public byte Kind;
        public bool HasWinner;
        public uint Winner;
        public bool HasLoser;
        public uint Loser;

        public static NetMatchOutcome From(MatchOutcomeSnapshot? outcome)
        {
            if (!outcome.HasValue)
            {
                return default;
            }

            MatchOutcomeSnapshot o = outcome.Value;
            return new NetMatchOutcome
            {
                HasOutcome = true,
                Kind = (byte)o.Kind,
                HasWinner = o.Winner.HasValue,
                Winner = o.Winner?.Value ?? 0u,
                HasLoser = o.Loser.HasValue,
                Loser = o.Loser?.Value ?? 0u,
            };
        }

        /// <summary>
        /// Rebuilds the pure nullable <see cref="MatchOutcomeSnapshot"/> from this wire struct:
        /// null when <see cref="HasOutcome"/> is false, otherwise the outcome with winner/loser
        /// restored from their validity flags (inverse of <see cref="From"/>).
        /// </summary>
        public MatchOutcomeSnapshot? ToSnapshot()
        {
            if (!HasOutcome)
            {
                return null;
            }

            return new MatchOutcomeSnapshot(
                (MatchOutcomeKind)Kind,
                HasWinner ? new PlayerId(Winner) : (PlayerId?)null,
                HasLoser ? new PlayerId(Loser) : (PlayerId?)null);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref HasOutcome);
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref HasWinner);
            serializer.SerializeValue(ref Winner);
            serializer.SerializeValue(ref HasLoser);
            serializer.SerializeValue(ref Loser);
        }

        public bool Equals(NetMatchOutcome other)
        {
            return HasOutcome == other.HasOutcome
                && Kind == other.Kind
                && HasWinner == other.HasWinner
                && Winner == other.Winner
                && HasLoser == other.HasLoser
                && Loser == other.Loser;
        }

        public override bool Equals(object obj) => obj is NetMatchOutcome other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = HasOutcome ? 1 : 0;
                hash = (hash * 397) ^ Kind;
                hash = (hash * 397) ^ (HasWinner ? 1 : 0);
                hash = (hash * 397) ^ (int)Winner;
                hash = (hash * 397) ^ (HasLoser ? 1 : 0);
                hash = (hash * 397) ^ (int)Loser;
                return hash;
            }
        }
    }
}
