using System;
using MultiplayerRts.Core;
using Unity.Netcode;

namespace MultiplayerRts.Net
{
    /// <summary>
    /// Unmanaged, wire-ready payload structs carried by the placement <c>ServerRpc</c> and the
    /// broadcast <c>ClientRpc</c>s. As with the task-13.1 <c>NetRecords</c>, Netcode for
    /// GameObjects requires an RPC parameter struct to be an <c>unmanaged</c> value type that
    /// implements <see cref="INetworkSerializable"/> (and <see cref="IEquatable{T}"/> by
    /// convention). These are the NGO mirrors of the pure <see cref="PlacementIntent"/>,
    /// <see cref="UnitRemovalEvent"/>, and <see cref="MatchResultEvent"/> translation types;
    /// each pairs with a <c>From</c>/<c>ToIntent</c> converter so the thin behaviour only ever
    /// moves data between the wire struct and the pure type and never embeds translation logic.
    ///
    /// <para>
    /// These depend on the <c>Unity.Netcode</c> assembly and compile only after a Unity package
    /// restore; the pure payload types they mirror compile and are smoke-tested independently.
    /// </para>
    /// </summary>
    public struct NetPlacementIntent : INetworkSerializable, IEquatable<NetPlacementIntent>
    {
        /// <summary>Structure type as its <see cref="StructureType"/> enum ordinal.</summary>
        public byte StructureType;

        public float TargetX;
        public float TargetY;

        /// <summary>
        /// The placing player's <see cref="PlayerId"/> value. Carried in the payload per
        /// Requirement 4.1, but the server does NOT trust it for authority: the server-side
        /// handler overrides it with the <see cref="PlayerId"/> resolved from the authenticated
        /// sender client id, so a client cannot submit placements as another player.
        /// </summary>
        public uint PlacingPlayerId;

        /// <summary>Builds the wire struct from a pure <see cref="PlacementIntent"/>.</summary>
        public static NetPlacementIntent From(PlacementIntent intent)
        {
            return new NetPlacementIntent
            {
                StructureType = (byte)intent.Type,
                TargetX = intent.TargetLocation.X,
                TargetY = intent.TargetLocation.Y,
                PlacingPlayerId = intent.PlacingPlayer.Value,
            };
        }

        /// <summary>
        /// Converts the wire struct to a pure <see cref="PlacementIntent"/>, stamping the
        /// authoritative <paramref name="resolvedPlayer"/> the server derived from the sender
        /// client id rather than the client-supplied <see cref="PlacingPlayerId"/>.
        /// </summary>
        public PlacementIntent ToIntent(PlayerId resolvedPlayer)
        {
            return new PlacementIntent(
                (StructureType)StructureType,
                new MapVector(TargetX, TargetY),
                resolvedPlayer);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref StructureType);
            serializer.SerializeValue(ref TargetX);
            serializer.SerializeValue(ref TargetY);
            serializer.SerializeValue(ref PlacingPlayerId);
        }

        public bool Equals(NetPlacementIntent other)
        {
            return StructureType == other.StructureType
                && TargetX.Equals(other.TargetX)
                && TargetY.Equals(other.TargetY)
                && PlacingPlayerId == other.PlacingPlayerId;
        }

        public override bool Equals(object obj) => obj is NetPlacementIntent other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StructureType;
                hash = (hash * 397) ^ TargetX.GetHashCode();
                hash = (hash * 397) ^ TargetY.GetHashCode();
                hash = (hash * 397) ^ (int)PlacingPlayerId;
                return hash;
            }
        }
    }

    /// <summary>
    /// Wire payload for the targeted rejection <c>ClientRpc</c> (Requirement 2.4): the
    /// canonical <see cref="RejectionReason"/> as its enum ordinal. Sent only to the submitting
    /// client via <c>ClientRpcParams</c>.
    /// </summary>
    public struct NetRejection : INetworkSerializable, IEquatable<NetRejection>
    {
        public byte Reason;

        public static NetRejection From(RejectionReason reason)
        {
            return new NetRejection { Reason = (byte)reason };
        }

        public RejectionReason ToReason() => (RejectionReason)Reason;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Reason);
        }

        public bool Equals(NetRejection other) => Reason == other.Reason;

        public override bool Equals(object obj) => obj is NetRejection other && Equals(other);

        public override int GetHashCode() => Reason;
    }

    /// <summary>Wire payload for the unit-removal broadcast <c>ClientRpc</c> (Requirement 7.5).</summary>
    public struct NetUnitRemoval : INetworkSerializable, IEquatable<NetUnitRemoval>
    {
        public uint UnitId;

        public static NetUnitRemoval From(UnitRemovalEvent e)
        {
            return new NetUnitRemoval { UnitId = e.Unit.Value };
        }

        public UnitRemovalEvent ToEvent() => new UnitRemovalEvent(new UnitId(UnitId));

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref UnitId);
        }

        public bool Equals(NetUnitRemoval other) => UnitId == other.UnitId;

        public override bool Equals(object obj) => obj is NetUnitRemoval other && Equals(other);

        public override int GetHashCode() => (int)UnitId;
    }

    /// <summary>Wire payload for the match-result broadcast <c>ClientRpc</c> (Requirement 8.5).</summary>
    public struct NetMatchResult : INetworkSerializable, IEquatable<NetMatchResult>
    {
        public byte Kind;
        public bool HasWinner;
        public uint Winner;
        public bool HasLoser;
        public uint Loser;

        public static NetMatchResult From(MatchResultEvent e)
        {
            return new NetMatchResult
            {
                Kind = (byte)e.Kind,
                HasWinner = e.HasWinner,
                Winner = e.Winner.Value,
                HasLoser = e.HasLoser,
                Loser = e.Loser.Value,
            };
        }

        public MatchResultEvent ToEvent()
        {
            return new MatchResultEvent(
                (MatchOutcomeKind)Kind,
                HasWinner,
                new PlayerId(Winner),
                HasLoser,
                new PlayerId(Loser));
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref HasWinner);
            serializer.SerializeValue(ref Winner);
            serializer.SerializeValue(ref HasLoser);
            serializer.SerializeValue(ref Loser);
        }

        public bool Equals(NetMatchResult other)
        {
            return Kind == other.Kind
                && HasWinner == other.HasWinner
                && Winner == other.Winner
                && HasLoser == other.HasLoser
                && Loser == other.Loser;
        }

        public override bool Equals(object obj) => obj is NetMatchResult other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Kind;
                hash = (hash * 397) ^ (HasWinner ? 1 : 0);
                hash = (hash * 397) ^ (int)Winner;
                hash = (hash * 397) ^ (HasLoser ? 1 : 0);
                hash = (hash * 397) ^ (int)Loser;
                return hash;
            }
        }
    }
}
