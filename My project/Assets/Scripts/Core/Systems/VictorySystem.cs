using System;
using System.Collections.Generic;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// Default <see cref="IVictorySystem"/> implementation. Resolves base-health end
    /// conditions and disconnect-timeout resolution over the canonical
    /// <see cref="MatchState"/> (Requirements 8, 9), recording the frozen
    /// <see cref="MatchOutcome"/> and transitioning the match to
    /// <see cref="MatchPhase.Ended"/>. Pure C# with no Unity or Netcode dependency so the
    /// behavior can be property-tested against an in-memory state. Every operation is total
    /// over its valid domain (no-ops rather than throwing for degenerate player sets) so a
    /// single bad entity cannot abort a tick.
    /// </summary>
    public sealed class VictorySystem : IVictorySystem
    {
        private readonly MatchState _state;

        /// <summary>
        /// Creates a victory system bound to <paramref name="state"/>, the canonical match
        /// state it reads from and mutates in place.
        /// </summary>
        public VictorySystem(MatchState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <inheritdoc />
        public MatchOutcome Evaluate()
        {
            // An already-ended match keeps its frozen result unchanged (Requirement 8.6).
            if (_state.IsEnded)
            {
                return _state.Outcome;
            }

            // Partition the players into those whose base has reached zero health and those
            // still alive. A base is considered destroyed once its health is at or below
            // zero (Requirement 8.1).
            PlayerState zeroHealth = null;
            int zeroHealthCount = 0;
            PlayerState survivor = null;
            int survivorCount = 0;

            foreach (KeyValuePair<PlayerId, PlayerState> entry in _state.Players)
            {
                PlayerState player = entry.Value;
                if (player == null)
                {
                    continue;
                }

                if (player.BaseHealth <= 0f)
                {
                    zeroHealth = player;
                    zeroHealthCount++;
                }
                else
                {
                    survivor = player;
                    survivorCount++;
                }
            }

            // No base has reached zero: no end condition met, state unchanged.
            if (zeroHealthCount == 0)
            {
                return null;
            }

            MatchOutcome outcome;
            if (zeroHealthCount == 1 && survivorCount >= 1)
            {
                // Exactly one base at zero health: the survivor wins, the zero-health player
                // loses (Requirements 8.2, 8.3).
                outcome = MatchOutcome.ForWinner(survivor.Id, zeroHealth.Id);
            }
            else
            {
                // Both bases at zero health in the same evaluation (or no distinct survivor
                // to crown): a draw with no winner and no loser (Requirement 8.4).
                outcome = MatchOutcome.ForDraw();
            }

            End(outcome);
            return outcome;
        }

        /// <inheritdoc />
        public void OnDisconnectTimeout(PlayerId id)
        {
            // An already-ended match keeps its frozen result unchanged (Requirement 8.6).
            if (_state.IsEnded)
            {
                return;
            }

            // The player whose reconnection window expired is now fully disconnected
            // (Requirement 9.1).
            if (_state.Players.TryGetValue(id, out PlayerState timedOut) && timedOut != null)
            {
                timedOut.Connection = ConnectionState.Disconnected;
            }

            // Count the players still connected; a single remaining connected player is the
            // winner (Requirement 9.2), while no remaining connected player ends the match
            // with no declared winner (Requirement 9.5).
            PlayerState connected = null;
            int connectedCount = 0;

            foreach (KeyValuePair<PlayerId, PlayerState> entry in _state.Players)
            {
                PlayerState player = entry.Value;
                if (player == null)
                {
                    continue;
                }

                if (player.Connection == ConnectionState.Connected)
                {
                    connected = player;
                    connectedCount++;
                }
            }

            MatchOutcome outcome;
            if (connectedCount == 1)
            {
                // Exactly one player remains connected: declare them the winner and the
                // timed-out player the loser (Requirement 9.2).
                PlayerId loser = timedOut != null ? timedOut.Id : id;
                outcome = MatchOutcome.ForWinner(connected.Id, loser);
            }
            else
            {
                // Both players disconnected (or none distinctly connected): the match ends
                // with no declared winner (Requirement 9.5).
                outcome = MatchOutcome.ForNoContest();
            }

            End(outcome);
        }

        /// <summary>
        /// Records the frozen <paramref name="outcome"/> on the canonical state and
        /// transitions the match to <see cref="MatchPhase.Ended"/> (Requirements 8.1, 8.6).
        /// </summary>
        private void End(MatchOutcome outcome)
        {
            _state.Outcome = outcome;
            _state.Phase = MatchPhase.Ended;
        }
    }
}
