using System.Collections.Generic;
using CsCheck;
using MultiplayerRts.Core;

namespace MultiplayerRts.Tests.Generators
{
    /// <summary>
    /// Generators for lobby join sequences and connection-state sets (design.md: "random
    /// lobbies and join sequences … and connection-state sets"; Requirements 1.1–1.7, 9).
    /// Join sequences may contain repeats so the pairing properties see the same player
    /// attempting to join more than once (Requirement 1.2), and connection-state sets cover
    /// every combination driving pause/resume and disconnect resolution (Requirement 9).
    /// Exposed as lazily-evaluated properties (see <c>Gens.Primitives.cs</c>).
    /// </summary>
    public static partial class Gens
    {
        /// <summary>Any connection state (Requirement 9).</summary>
        public static Gen<ConnectionState> ConnectionState => Gen.Enum<ConnectionState>();

        /// <summary>
        /// A join sequence: an ordered list of player ids requesting to join, possibly with
        /// repeats, so pairing/capacity/re-join properties see duplicates and overflow
        /// (Requirements 1.1, 1.2, 1.3). Length runs from empty up to well past lobby
        /// capacity (two).
        /// </summary>
        public static Gen<IReadOnlyList<PlayerId>> JoinSequence =>
            PlayerId.List[0, 6].Select(list => (IReadOnlyList<PlayerId>)list);

        /// <summary>
        /// A join sequence guaranteed to contain a duplicate join by the same player, for the
        /// "assigned players cannot re-join" property (Requirement 1.2).
        /// </summary>
        public static Gen<IReadOnlyList<PlayerId>> JoinSequenceWithRepeat =>
            Gen.Select(PlayerId, PlayerId.List[0, 4], (repeated, rest) =>
            {
                var list = new List<PlayerId> { repeated };
                list.AddRange(rest);
                list.Add(repeated); // ensure at least one repeat
                return (IReadOnlyList<PlayerId>)list;
            });

        /// <summary>
        /// A pair of distinct player ids, as assigned at match start with unique ids
        /// (Requirement 1.7).
        /// </summary>
        public static Gen<(PlayerId A, PlayerId B)> DistinctPlayerPair =>
            Gen.Select(Gen.UInt[0, 20], Gen.UInt[0, 20], (a, b) => (a, b))
                .Where(t => t.Item1 != t.Item2)
                .Select(t => (new PlayerId(t.Item1), new PlayerId(t.Item2)));

        /// <summary>
        /// A connection-state pair for the two players, covering every combination that
        /// drives pause/resume and disconnect resolution — both connected, one dropped, both
        /// dropped, reconnect-window (Requirements 9.1, 9.2, 9.4, 9.5).
        /// </summary>
        public static Gen<(ConnectionState First, ConnectionState Second)> ConnectionStatePair =>
            Gen.Select(ConnectionState, ConnectionState, (a, b) => (a, b));

        /// <summary>
        /// A lobby wait duration in seconds spanning the 120-second timeout boundary: just
        /// below, exactly at, and just above, plus a wider spread (Requirements 1.4, 1.5).
        /// </summary>
        public static Gen<float> LobbyWaitSeconds => Gen.OneOf(
            Gen.Const(119.999f),
            Gen.Const(120f),
            Gen.Const(120.001f),
            Gen.Float[0f, 240f]);

        /// <summary>
        /// A reconnection delay in seconds spanning the 30-second window boundary
        /// (Requirements 9.1, 9.4).
        /// </summary>
        public static Gen<float> ReconnectSeconds => Gen.OneOf(
            Gen.Const(29.999f),
            Gen.Const(30f),
            Gen.Const(30.001f),
            Gen.Float[0f, 60f]);
    }
}
