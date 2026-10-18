using System;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// A 2D position or displacement expressed in map units. Framework-independent
    /// (deliberately not <c>UnityEngine.Vector2</c>) so simulation geometry can be
    /// property-tested without a Unity runtime.
    /// </summary>
    public readonly struct MapVector : IEquatable<MapVector>
    {
        public readonly float X;
        public readonly float Y;

        public MapVector(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly MapVector Zero = new MapVector(0f, 0f);

        public static MapVector operator +(MapVector a, MapVector b) => new MapVector(a.X + b.X, a.Y + b.Y);

        public static MapVector operator -(MapVector a, MapVector b) => new MapVector(a.X - b.X, a.Y - b.Y);

        public static MapVector operator *(MapVector v, float scalar) => new MapVector(v.X * scalar, v.Y * scalar);

        public static MapVector operator *(float scalar, MapVector v) => v * scalar;

        /// <summary>Squared length. Cheaper than <see cref="Magnitude"/> for comparisons.</summary>
        public float SqrMagnitude => (X * X) + (Y * Y);

        /// <summary>Euclidean length in map units.</summary>
        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);

        /// <summary>Euclidean distance between two points in map units.</summary>
        public static float Distance(MapVector a, MapVector b) => (a - b).Magnitude;

        /// <summary>Squared Euclidean distance; avoids the square root for threshold comparisons.</summary>
        public static float SqrDistance(MapVector a, MapVector b) => (a - b).SqrMagnitude;

        /// <summary>
        /// Returns a unit-length vector in the same direction, or <see cref="Zero"/>
        /// when this vector has (near-)zero length.
        /// </summary>
        public MapVector Normalized()
        {
            float mag = Magnitude;
            if (mag <= float.Epsilon)
            {
                return Zero;
            }

            return new MapVector(X / mag, Y / mag);
        }

        public bool Equals(MapVector other) => X.Equals(other.X) && Y.Equals(other.Y);

        public override bool Equals(object obj) => obj is MapVector other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public static bool operator ==(MapVector left, MapVector right) => left.Equals(right);

        public static bool operator !=(MapVector left, MapVector right) => !left.Equals(right);

        public override string ToString() => $"({X}, {Y})";
    }
}
