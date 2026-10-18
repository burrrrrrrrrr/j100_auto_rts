using System;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// Identifies a <c>Unit</c> uniquely within a <c>Match</c>.
    /// Used as a key into the canonical unit collection in <see cref="MatchState"/>.
    /// </summary>
    public readonly struct UnitId : IEquatable<UnitId>
    {
        public readonly uint Value;

        public UnitId(uint value)
        {
            Value = value;
        }

        public bool Equals(UnitId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is UnitId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(UnitId left, UnitId right) => left.Equals(right);

        public static bool operator !=(UnitId left, UnitId right) => !left.Equals(right);

        public override string ToString() => $"Unit({Value})";
    }
}
