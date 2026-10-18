using System;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// Pairs <c>Player</c>s into a 1v1 <c>Match</c> before it begins (Requirement 1).
    ///
    /// The lobby holds at most <see cref="SimulationConstants.LobbyCapacity"/> players.
    /// <see cref="RequestJoin"/> admits an unassigned player when there is room and rejects
    /// a player who is already assigned to this lobby or a started match, preserving that
    /// player's prior assignment (Requirements 1.1, 1.2). <see cref="TryStartMatch"/>
    /// succeeds only at exactly two players, assigning each a unique id and a starting base
    /// (Requirements 1.3, 1.7); it is invokable immediately once the lobby is full, so a
    /// match can start well within the 2-second start window (Requirement 1.3).
    ///
    /// <see cref="TickWaiting"/> accumulates waiting time for a lone player and, once
    /// <see cref="SimulationConstants.LobbyWaitTimeout"/> is reached while still short of
    /// two players, removes that player and raises the no-opponent signal; before the
    /// timeout the waiting player is retained (Requirements 1.4, 1.5). The wait clock runs
    /// only while exactly one player waits: it resets when a player joins or leaves, and
    /// does not accrue against a full or empty lobby. <see cref="RemovePlayer"/> removes
    /// only the departing player and retains any remaining waiting player (Requirement 1.6).
    ///
    /// A pure C# type with no Unity dependency; base assignment and the no-opponent signal
    /// are injected so the lobby can be property-tested against in-memory state. Every
    /// operation is total over its valid domain (no-ops rather than throwing).
    /// </summary>
    public sealed class LobbyManager : ILobbyManager
    {
        private readonly IStartingBaseProvider _baseProvider;
        private readonly Action<PlayerId> _onNoOpponent;

        // The lobby holds at most two players. Capacity is fixed at construction from
        // SimulationConstants.LobbyCapacity (Requirement 1.1); the fields below model the
        // 1v1 scope directly rather than a general collection.
        private bool _hasFirst;
        private PlayerId _first;
        private bool _hasSecond;
        private PlayerId _second;

        // Time, in seconds, the current lone waiting player has been in the lobby. Only
        // meaningful while exactly one player is present; reset to zero on every join or
        // removal that changes the count (Requirements 1.4, 1.5).
        private float _waitElapsed;

        // True once a match has been started from this lobby, so re-joins by the paired
        // players are rejected while their assignment persists (Requirement 1.2).
        private bool _matchStarted;
        private MatchDescriptor _startedMatch;

        /// <summary>
        /// Creates a lobby manager.
        /// </summary>
        /// <param name="baseProvider">
        /// Supplies the starting base for each player at match start (Requirement 1.7).
        /// </param>
        /// <param name="onNoOpponent">
        /// Optional callback invoked with the removed player's id when the wait timeout
        /// elapses with no opponent found (Requirement 1.5); may be null.
        /// </param>
        public LobbyManager(IStartingBaseProvider baseProvider, Action<PlayerId> onNoOpponent = null)
        {
            _baseProvider = baseProvider ?? throw new ArgumentNullException(nameof(baseProvider));
            _onNoOpponent = onNoOpponent;
        }

        /// <summary>The number of players currently in the lobby (0, 1, or 2).</summary>
        public int PlayerCount => (_hasFirst ? 1 : 0) + (_hasSecond ? 1 : 0);

        /// <summary>True when the lobby holds its full capacity of players (Requirement 1.1).</summary>
        public bool IsFull => PlayerCount >= SimulationConstants.LobbyCapacity;

        /// <inheritdoc />
        public JoinResult RequestJoin(PlayerId id)
        {
            // Reject a player already assigned to this lobby or to the started match,
            // leaving the existing assignment untouched (Requirement 1.2).
            if (IsAssigned(id))
            {
                return JoinResult.RejectedAlreadyAssigned;
            }

            // The lobby never exceeds capacity: once full, further joiners are rejected as
            // there is no room to assign them (Requirement 1.1). They remain unassigned.
            if (IsFull)
            {
                return JoinResult.RejectedAlreadyAssigned;
            }

            if (!_hasFirst)
            {
                _first = id;
                _hasFirst = true;
            }
            else
            {
                _second = id;
                _hasSecond = true;
            }

            // A change in player count restarts the wait clock: a newly lone player begins
            // its 120s window fresh, and reaching two players stops the clock entirely
            // (Requirements 1.4, 1.5).
            _waitElapsed = 0f;

            return JoinResult.Accepted;
        }

        /// <inheritdoc />
        public bool TryStartMatch(out MatchDescriptor match)
        {
            // A match starts only at exactly two players (Requirement 1.3). With fewer, the
            // lobby keeps waiting; a match is never started twice from one lobby.
            if (_matchStarted || PlayerCount != SimulationConstants.LobbyCapacity)
            {
                match = null;
                return false;
            }

            // Assign each player a starting base; ids are already distinct because a joiner
            // equal to a seated player is rejected by RequestJoin (Requirement 1.7). The
            // MatchDescriptor constructor re-validates distinctness as a safety net.
            _baseProvider.AssignBases(_first, _second, out StartingBase baseA, out StartingBase baseB);

            match = new MatchDescriptor(_first, _second, baseA, baseB);

            _matchStarted = true;
            _startedMatch = match;

            // The players are now assigned to the match; the wait clock no longer applies.
            _waitElapsed = 0f;

            return true;
        }

        /// <inheritdoc />
        public void TickWaiting(float deltaSeconds)
        {
            // Negative time never advances the simulation; treat as a no-op (totality).
            if (deltaSeconds < 0f)
            {
                return;
            }

            // The wait timeout governs only a lobby that still holds fewer than two
            // players (Requirement 1.4). A full lobby (two players, match pending or
            // started) and an empty lobby accrue no wait time.
            if (PlayerCount == 0 || PlayerCount >= SimulationConstants.LobbyCapacity)
            {
                return;
            }

            _waitElapsed += deltaSeconds;

            // Below the timeout the lone player is retained (Requirement 1.4). At or beyond
            // the timeout the player is removed and the no-opponent signal raised
            // (Requirement 1.5).
            if (_waitElapsed < SimulationConstants.LobbyWaitTimeout)
            {
                return;
            }

            PlayerId timedOut = _hasFirst ? _first : _second;

            ClearSeats();
            _waitElapsed = 0f;

            _onNoOpponent?.Invoke(timedOut);
        }

        /// <inheritdoc />
        public void RemovePlayer(PlayerId id)
        {
            // Remove only the departing player; any remaining player stays in the lobby
            // (Requirement 1.6). Removing an unknown player is a no-op (totality).
            bool removed = false;

            if (_hasFirst && _first == id)
            {
                _hasFirst = false;
                _first = default;
                removed = true;
            }
            else if (_hasSecond && _second == id)
            {
                _hasSecond = false;
                _second = default;
                removed = true;
            }

            if (!removed)
            {
                return;
            }

            NormalizeSeats();

            // A change in player count restarts the wait clock for whoever remains, and
            // stops it when the lobby empties (Requirements 1.4, 1.5).
            _waitElapsed = 0f;
        }

        /// <summary>
        /// Returns true when <paramref name="id"/> is already assigned to this lobby or to
        /// the match started from it, used to reject duplicate joins (Requirement 1.2).
        /// </summary>
        private bool IsAssigned(PlayerId id)
        {
            if (_hasFirst && _first == id)
            {
                return true;
            }

            if (_hasSecond && _second == id)
            {
                return true;
            }

            if (_matchStarted && _startedMatch != null
                && (_startedMatch.PlayerA == id || _startedMatch.PlayerB == id))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Collapses a lone remaining player into the first seat so a single waiting player
        /// is always represented in <see cref="_first"/>. Keeps seat bookkeeping simple
        /// after a removal (Requirement 1.6).
        /// </summary>
        private void NormalizeSeats()
        {
            if (!_hasFirst && _hasSecond)
            {
                _first = _second;
                _hasFirst = true;
                _second = default;
                _hasSecond = false;
            }
        }

        /// <summary>
        /// Empties both seats. Used when the wait timeout removes the lone player
        /// (Requirement 1.5).
        /// </summary>
        private void ClearSeats()
        {
            _hasFirst = false;
            _first = default;
            _hasSecond = false;
            _second = default;
        }
    }
}
