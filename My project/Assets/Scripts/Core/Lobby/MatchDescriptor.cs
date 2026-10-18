using System;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// The handoff produced when a full <c>Lobby</c> starts a <c>Match</c>
    /// (Requirements 1.3, 1.7). Describes the two paired players, each with a player
    /// identifier that is unique within the match (<see cref="PlayerA"/> != <see cref="PlayerB"/>)
    /// and a starting <c>Base</c> assigned to them (<see cref="BaseA"/>, <see cref="BaseB"/>).
    ///
    /// A pure C# value type with no Unity dependency so match-start assignment can be
    /// property-tested in isolation. Construction enforces the uniqueness invariant so a
    /// descriptor can never carry two equal player ids (Requirement 1.7).
    /// </summary>
    public sealed class MatchDescriptor
    {
        /// <summary>The first player's identifier, distinct from <see cref="PlayerB"/> (Requirement 1.7).</summary>
        public PlayerId PlayerA { get; }

        /// <summary>The second player's identifier, distinct from <see cref="PlayerA"/> (Requirement 1.7).</summary>
        public PlayerId PlayerB { get; }

        /// <summary>The starting base assigned to <see cref="PlayerA"/> (Requirement 1.7).</summary>
        public StartingBase BaseA { get; }

        /// <summary>The starting base assigned to <see cref="PlayerB"/> (Requirement 1.7).</summary>
        public StartingBase BaseB { get; }

        /// <summary>
        /// Creates a descriptor for a started match. The two player ids must be distinct so
        /// each player is uniquely identifiable within the match (Requirement 1.7); each
        /// starting base must be owned by the player it is assigned to.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown when the two player ids are equal, or when a base's owner does not match
        /// the player it is assigned to.
        /// </exception>
        public MatchDescriptor(PlayerId playerA, PlayerId playerB, StartingBase baseA, StartingBase baseB)
        {
            if (playerA == playerB)
            {
                throw new ArgumentException(
                    "A match requires two distinct player identifiers (Requirement 1.7).");
            }

            if (baseA.Owner != playerA)
            {
                throw new ArgumentException(
                    "Starting base A must be owned by player A (Requirement 1.7).", nameof(baseA));
            }

            if (baseB.Owner != playerB)
            {
                throw new ArgumentException(
                    "Starting base B must be owned by player B (Requirement 1.7).", nameof(baseB));
            }

            PlayerA = playerA;
            PlayerB = playerB;
            BaseA = baseA;
            BaseB = baseB;
        }
    }
}
