using System;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// Identifies a <c>Structure</c> uniquely within a <c>Match</c>.
    /// Used as a key into the canonical structure collection in <see cref="MatchState"/>.
    /// </summary>
    public readonly struct StructureId : IEquatable<StructureId>
    {
        public readonly uint Value;

        public StructureId(uint value)
        {
            Value = value;
        }

        public bool Equals(StructureId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is StructureId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(StructureId left, StructureId right) => left.Equals(right);

        public static bool operator !=(StructureId left, StructureId right) => !left.Equals(right);

        public override string ToString() => $"Structure({Value})";
    }
}
