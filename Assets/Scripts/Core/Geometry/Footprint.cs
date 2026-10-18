using System;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// An axis-aligned rectangular area a <c>Structure</c> occupies on the map,
    /// defined by a bottom-left <see cref="Origin"/> and a non-negative <see cref="Size"/>.
    /// Used for build-area containment and overlap rejection (Requirements 4.2, 4.3).
    /// </summary>
    public readonly struct Footprint : IEquatable<Footprint>
    {
        public readonly MapVector Origin;
        public readonly MapVector Size;

        public Footprint(MapVector origin, MapVector size)
        {
            Origin = origin;
            Size = size;
        }

        public float MinX => Origin.X;
        public float MinY => Origin.Y;
        public float MaxX => Origin.X + Size.X;
        public float MaxY => Origin.Y + Size.Y;

        /// <summary>
        /// Returns true when this footprint intersects <paramref name="other"/>.
        /// Edge contact alone (shared boundary, zero overlap area) does not count as an
        /// intersection, so abutting structures are permitted (Requirement 4.3).
        /// </summary>
        public bool Overlaps(Footprint other)
        {
            return MinX < other.MaxX
                && MaxX > other.MinX
                && MinY < other.MaxY
                && MaxY > other.MinY;
        }

        /// <summary>Returns true when the given point lies within this footprint (inclusive of edges).</summary>
        public bool Contains(MapVector point)
        {
            return point.X >= MinX
                && point.X <= MaxX
                && point.Y >= MinY
                && point.Y <= MaxY;
        }

        public bool Equals(Footprint other) => Origin.Equals(other.Origin) && Size.Equals(other.Size);

        public override bool Equals(object obj) => obj is Footprint other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Origin.GetHashCode() * 397) ^ Size.GetHashCode();
            }
        }

        public static bool operator ==(Footprint left, Footprint right) => left.Equals(right);

        public static bool operator !=(Footprint left, Footprint right) => !left.Equals(right);

        public override string ToString() => $"Footprint(origin={Origin}, size={Size})";
    }
}
