using System;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// Identifies a <c>Player</c> uniquely within a <c>Match</c> (Requirement 1.7).
    /// A framework-independent value type so canonical state can be property-tested
    /// without any Unity or Netcode dependency.
    /// </summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public readonly uint Value;

        public PlayerId(uint value)
        {
            Value = value;
        }

        public bool Equals(PlayerId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(PlayerId left, PlayerId right) => left.Equals(right);

        public static bool operator !=(PlayerId left, PlayerId right) => !left.Equals(right);

        public override string ToString() => $"Player({Value})";
    }
}
