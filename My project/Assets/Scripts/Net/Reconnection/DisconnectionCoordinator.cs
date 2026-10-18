using System;
using System.Collections.Generic;
using MultiplayerRts.Core;

namespace MultiplayerRts.Net
{
    /// <summary>
    /// The pure, Netcode-free pause/resume state machine and reconnection timer behind
    /// Requirement 9 (disconnection during an active match). It owns the transition
    /// <see cref="MatchPhase.Active"/> &#8596; <see cref="MatchPhase.Paused"/> and the
    /// 30-second countdown (<see cref="SimulationConstants.ReconnectWindowSeconds"/>), and
    /// resolves the match through the canonical <see cref="IVictorySystem.OnDisconnectTimeout"/>
    /// when the window expires. It references only Core, so the whole state machine — pause on
    /// disconnect, resume on timely reconnect with preserved state, timeout resolution, and the
    /// both-players-disconnected case — is verifiable by the out-of-tree dotnet smoke check
    /// without a Unity runtime. The thin NGO behaviour (task 13.3 wiring) owns one instance and
    /// drives it from the transport connection callbacks and the game loop's tick.
    ///
    /// <para>
    /// <b>What it mutates, and what it deliberately does not.</b> On a disconnect it sets the
    /// dropping player's <see cref="PlayerState.Connection"/> to
    /// <see cref="ConnectionState.ReconnectWindow"/> and, if the match was
    /// <see cref="MatchPhase.Active"/>, flips the phase to <see cref="MatchPhase.Paused"/>. It
    /// touches no units, structures, resources, or base health — pausing freezes the world
    /// exactly because <see cref="MatchServer.Tick"/> no-ops while the phase is not Active, so
    /// a resume returns to the preserved canonical state with no loss (Requirement 9.4). Resume
    /// is the mirror: connection back to <see cref="ConnectionState.Connected"/> and phase back
    /// to <see cref="MatchPhase.Active"/>.
    /// </para>
    ///
    /// <para>
    /// <b>Multiple outstanding disconnects.</b> Both players can be inside their own 30-second
    /// window at once (each dropped at a different moment). A per-player countdown map tracks
    /// each independently; the match stays paused while any window is open, resumes only when
    /// every window has been closed by a reconnect, and a window that expires resolves the
    /// match via <see cref="IVictorySystem.OnDisconnectTimeout"/> — which itself handles the
    /// one-remaining-connected (winner, Requirement 9.2) and both-disconnected (no winner,
    /// Requirement 9.5) cases. Once the victory system has ended the match the coordinator
    /// clears its timers and performs no further pause/resume work (the result is frozen,
    /// Requirement 8.6).
    /// </para>
    ///
    /// <para>
    /// <b>Totality.</b> Every entry point is a no-op for an unknown player id, a null state,
    /// or a match that is not in a phase where the action makes sense, so a stray callback can
    /// never throw or corrupt the canonical state.
    /// </para>
    /// </summary>
    public sealed class DisconnectionCoordinator
    {
        private readonly MatchState _state;
        private readonly IVictorySystem _victory;
        private readonly float _windowSeconds;

        /// <summary>
        /// Per-player remaining reconnection time, in seconds. A player is present in this map
        /// exactly while their reconnection window is open (i.e. they are in
        /// <see cref="ConnectionState.ReconnectWindow"/>); the value counts down each
        /// <see cref="Tick"/> and the entry is removed on reconnect or on timeout resolution.
        /// </summary>
        private readonly Dictionary<PlayerId, float> _remaining = new Dictionary<PlayerId, float>();

        /// <summary>
        /// Creates a coordinator over the canonical <paramref name="state"/> it pauses/resumes
        /// and the <paramref name="victory"/> system it resolves timeouts through. The window
        /// defaults to <see cref="SimulationConstants.ReconnectWindowSeconds"/> but is injectable
        /// so the smoke check can drive short windows deterministically. Both state and victory
        /// are required; a null is a construction-time programming error.
        /// </summary>
        public DisconnectionCoordinator(
            MatchState state,
            IVictorySystem victory,
            float windowSeconds = SimulationConstants.ReconnectWindowSeconds)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _victory = victory ?? throw new ArgumentNullException(nameof(victory));
            _windowSeconds = windowSeconds;
        }

        /// <summary>True while at least one player's reconnection window is open.</summary>
        public bool HasPendingReconnect => _remaining.Count > 0;

        /// <summary>
        /// Seconds remaining in <paramref name="id"/>'s reconnection window, or 0 when the
        /// player has no window open (never dropped, already reconnected, or already timed out).
        /// </summary>
        public float RemainingSeconds(PlayerId id)
        {
            return _remaining.TryGetValue(id, out float remaining) ? remaining : 0f;
        }

        /// <summary>
        /// Handles a player disconnecting during the match (Requirements 9.1, 9.4). Marks the
        /// player as being within the reconnection window, starts (or restarts) their
        /// 30-second countdown, and pauses the match if it was active. A no-op once the match
        /// has ended (frozen result, Requirement 8.6), for an unknown player id, or while the
        /// match is still in the lobby (lobby disconnects are the lobby manager's concern,
        /// Requirement 1.6). Idempotent: a repeated disconnect for a player already in the
        /// window simply refreshes their countdown rather than stacking windows.
        /// </summary>
        public void OnDisconnected(PlayerId id)
        {
            if (_state.IsEnded)
            {
                return;
            }

            // Only an in-progress match (Active now, or already Paused by an earlier drop) has a
            // world to freeze. A lobby disconnect is handled elsewhere (Requirement 1.6).
            if (_state.Phase != MatchPhase.Active && _state.Phase != MatchPhase.Paused)
            {
                return;
            }

            if (!_state.Players.TryGetValue(id, out PlayerState player) || player == null)
            {
                return;
            }

            player.Connection = ConnectionState.ReconnectWindow;

            // (Re)start this player's countdown. Using an indexer keeps a duplicate disconnect
            // idempotent: the window is refreshed, not stacked.
            _remaining[id] = _windowSeconds;

            // Freeze the world so the simulation loop stops advancing it (Requirement 9.4). Only
            // an Active match transitions here; an already-Paused match stays paused.
            if (_state.Phase == MatchPhase.Active)
            {
                _state.Phase = MatchPhase.Paused;
            }
        }

        /// <summary>
        /// Handles a player reconnecting within their window (Requirement 9.4). Sets the player
        /// back to <see cref="ConnectionState.Connected"/>, closes their countdown, and — once
        /// no reconnection window remains open — resumes the match from its preserved canonical
        /// state (<see cref="MatchPhase.Paused"/> &#8594; <see cref="MatchPhase.Active"/>). A
        /// no-op once the match has ended, for an unknown player id, or for a player who had no
        /// open window (e.g. a spurious reconnect). Returns true when this reconnect resumed the
        /// match, so the caller can trigger a state resync/result resend.
        /// </summary>
        public bool OnReconnected(PlayerId id)
        {
            if (_state.IsEnded)
            {
                return false;
            }

            if (!_state.Players.TryGetValue(id, out PlayerState player) || player == null)
            {
                return false;
            }

            bool hadWindow = _remaining.Remove(id);
            player.Connection = ConnectionState.Connected;

            if (!hadWindow)
            {
                // The player reconnected without an open window (or was never dropped): nothing
                // to resume. Connection is now Connected either way.
                return false;
            }

            // Resume only once every outstanding window has closed and only from a paused match,
            // so a second still-disconnected player keeps the world frozen.
            if (_remaining.Count == 0 && _state.Phase == MatchPhase.Paused)
            {
                _state.Phase = MatchPhase.Active;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Advances every open reconnection window by <paramref name="deltaSeconds"/>, called by
        /// the game loop (even while the match is paused, since the paused simulation tick itself
        /// no-ops). Any window that reaches zero resolves the match through
        /// <see cref="IVictorySystem.OnDisconnectTimeout"/> (Requirements 9.1, 9.2, 9.5); the
        /// victory system crowns the remaining connected player or ends with no winner when both
        /// are gone, and sets <see cref="MatchPhase.Ended"/>. A no-op once the match has ended or
        /// when no window is open. Totally safe for a non-positive delta (treated as no elapsed
        /// time) so a degenerate tick cannot expire a window early.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (_state.IsEnded || _remaining.Count == 0)
            {
                return;
            }

            if (deltaSeconds <= 0f)
            {
                return;
            }

            // Collect expiries first so resolution does not mutate the map we are iterating, and
            // so the order of resolution is deterministic (by player id value).
            List<PlayerId> expired = null;
            var keys = new List<PlayerId>(_remaining.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                PlayerId id = keys[i];
                float remaining = _remaining[id] - deltaSeconds;
                if (remaining <= 0f)
                {
                    (expired ??= new List<PlayerId>()).Add(id);
                }
                else
                {
                    _remaining[id] = remaining;
                }
            }

            if (expired == null)
            {
                return;
            }

            expired.Sort(static (a, b) => a.Value.CompareTo(b.Value));

            for (int i = 0; i < expired.Count; i++)
            {
                PlayerId id = expired[i];

                // Close this player's window before resolving so the victory system sees a clean
                // post-window set and a later expiry in the same tick does not double-resolve.
                _remaining.Remove(id);

                // Resolve the match. The victory system marks the timed-out player Disconnected
                // and ends the match (winner / no-contest). Once ended, stop: the result is frozen
                // and any other outstanding windows are moot.
                _victory.OnDisconnectTimeout(id);

                if (_state.IsEnded)
                {
                    // The match is resolved; abandon any remaining windows so they cannot fire.
                    _remaining.Clear();
                    return;
                }
            }
        }
    }
}
