using System;

namespace MultiplayerRts.Client
{
    /// <summary>
    /// A pure, framework-independent RGBA color (channels in the normalized [0,1] range), used
    /// so the per-owner unit color resolver (<see cref="UnitColorResolver"/>, Requirement 10.1)
    /// can be verified out-of-tree without a Unity runtime. The thin Unity renderer converts
    /// this to <c>UnityEngine.Color</c> at the draw boundary (<c>new Color(r, g, b, a)</c>),
    /// keeping all assignment logic testable with <c>dotnet</c>.
    ///
    /// <para>
    /// This mirrors the project's pure-vs-thin convention (Core uses its own
    /// <see cref="MultiplayerRts.Core.MapVector"/> rather than <c>UnityEngine.Vector2</c>): the
    /// presentation layer keeps a Unity-free color representation so the color-assignment
    /// contract — distinct per owner, identical for the same owner — is a plain value that
    /// property/smoke tests can compare directly.
    /// </para>
    /// </summary>
    public readonly struct RenderColor : IEquatable<RenderColor>
    {
        public readonly float R;
        public readonly float G;
        public readonly float B;
        public readonly float A;

        public RenderColor(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public bool Equals(RenderColor other)
        {
            return R.Equals(other.R)
                && G.Equals(other.G)
                && B.Equals(other.B)
                && A.Equals(other.A);
        }

        public override bool Equals(object obj) => obj is RenderColor other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = R.GetHashCode();
                hash = (hash * 397) ^ G.GetHashCode();
                hash = (hash * 397) ^ B.GetHashCode();
                hash = (hash * 397) ^ A.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(RenderColor left, RenderColor right) => left.Equals(right);

        public static bool operator !=(RenderColor left, RenderColor right) => !left.Equals(right);

        public override string ToString() => $"RGBA({R}, {G}, {B}, {A})";
    }
}
