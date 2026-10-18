using System;
using System.Collections.Generic;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// The canonical match state: the single source of truth used to resolve the match
    /// outcome (Requirement 2.1). Holds the current <see cref="Phase"/>, the two
    /// <see cref="Players"/>, all live <see cref="Units"/> and <see cref="Structures"/>,
    /// the frozen <see cref="Outcome"/> once ended, and the simulation <see cref="Tick"/>
    /// counter. Mutable collections so the simulation can add and remove entities in place;
    /// a pure C# type with no Unity dependency so the full pipeline can be property-tested
    /// against an in-memory state.
    /// </summary>
    public sealed class MatchState
    {
        /// <summary>The current lifecycle phase of the match.</summary>
        public MatchPhase Phase { get; set; }

        /// <summary>The two players in the match, keyed by player id (exactly two once active).</summary>
        public IDictionary<PlayerId, PlayerState> Players { get; }

        /// <summary>All units currently present in the match, keyed by unit id.</summary>
        public IDictionary<UnitId, Unit> Units { get; }

        /// <summary>All structures currently placed in the match, keyed by structure id.</summary>
        public IDictionary<StructureId, Structure> Structures { get; }

        /// <summary>The frozen outcome once the match has ended; null while in progress (Requirement 8).</summary>
        public MatchOutcome Outcome { get; set; }

        /// <summary>The simulation tick counter, incremented once per fixed tick.</summary>
        public long Tick { get; set; }

        /// <summary>
        /// Monotonic "changed canonical state" marker for replication (Requirement 2.1).
        /// The simulation loop bumps this via <see cref="MarkChanged"/> at the end of any
        /// tick that advanced the canonical state, so the replication layer (task 13.1) can
        /// compare a last-published revision against this value to decide whether there is a
        /// new canonical snapshot to push. It is a coarse, whole-state dirty flag — not
        /// per-field change tracking — which is all the current scope needs: the Net layer
        /// mirrors canonical state onto NetworkVariables, whose own equality checks suppress
        /// no-op writes. A counter rather than a bool so a consumer can tell "nothing changed"
        /// from "changed again" without the loop having to clear a flag the consumer owns.
        /// Starts at 0; the first mutated tick makes it 1.
        /// </summary>
        public long Revision { get; private set; }

        /// <summary>
        /// Marks the canonical state as changed since the last published <see cref="Revision"/>
        /// by incrementing the revision counter (Requirement 2.1). Called once by the
        /// simulation loop at the end of a tick that advanced canonical state so the
        /// replication layer can observe that a new snapshot is available. Total and
        /// side-effect-free beyond the increment, so it can never abort a tick.
        /// </summary>
        public void MarkChanged()
        {
            // unchecked so the counter wraps rather than throwing on overflow after an
            // astronomically long match; consumers compare for inequality, not ordering past wrap.
            unchecked
            {
                Revision++;
            }
        }

        public MatchState()
            : this(MatchPhase.Lobby)
        {
        }

        public MatchState(MatchPhase phase)
        {
            Phase = phase;
            Players = new Dictionary<PlayerId, PlayerState>();
            Units = new Dictionary<UnitId, Unit>();
            Structures = new Dictionary<StructureId, Structure>();
            Outcome = null;
            Tick = 0;
        }

        /// <summary>True once the match has concluded and its outcome is frozen (Requirement 8.6).</summary>
        public bool IsEnded => Phase == MatchPhase.Ended;

        /// <summary>
        /// Returns the state of the opponent of <paramref name="playerId"/> in a two-player
        /// match, or null when no distinct opponent is present.
        /// </summary>
        public PlayerState GetOpponent(PlayerId playerId)
        {
            foreach (KeyValuePair<PlayerId, PlayerState> entry in Players)
            {
                if (entry.Key != playerId)
                {
                    return entry.Value;
                }
            }

            return null;
        }
    }
}
