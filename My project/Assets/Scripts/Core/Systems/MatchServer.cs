using System;
using System.Collections.Generic;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// Default <see cref="IMatchServer"/> implementation and the canonical action pipeline
    /// (Requirements 2.3, 2.4, 8.6). Owns the authoritative <see cref="MatchState"/> and
    /// validates every submitted <see cref="PlayerAction"/> against it <em>before</em>
    /// applying, so an invalid action never mutates the canonical state
    /// (validation-before-application, Requirements 2.3, 2.4). Pure C# with no Unity or
    /// Netcode dependency, so the pipeline can be property-tested against an in-memory state
    /// with a mocked transport. Every operation is total over its valid domain (rejection or
    /// no-op rather than throwing for unknown action kinds or null input) so one bad action
    /// cannot abort the server.
    ///
    /// <para>
    /// <b>Idempotency / exactly-once.</b> Each applied action is recorded by a dedup key and
    /// a re-delivery of an already-applied action makes no additional state change, returning
    /// <see cref="ActionResult.Duplicate"/> (Requirement 2.3). The dedup key is scoped
    /// <em>per player</em> as the pair (<see cref="PlayerAction.PlacingPlayer"/>,
    /// <see cref="PlayerAction.ActionId"/>). <see cref="PlayerAction.ActionId"/> is assigned by
    /// the submitting client, so its uniqueness is only guaranteed within that one client's
    /// stream; two different players may independently reuse the same numeric id for unrelated
    /// actions. Scoping the key by player keeps those independent streams from colliding while
    /// still de-duplicating any re-delivery of the same player's action. Only actions that are
    /// actually applied (accepted) are recorded, so a rejected action may be legitimately
    /// retried after the player corrects it (e.g. once they can afford the structure).
    /// </para>
    ///
    /// <para>
    /// <b>Ended match.</b> While <see cref="MatchState.Phase"/> is <see cref="MatchPhase.Ended"/>
    /// every action is rejected with <see cref="RejectionReason.MatchEnded"/> and the frozen
    /// result and state are left unchanged (Requirement 8.6).
    /// </para>
    /// </summary>
    public sealed class MatchServer : IMatchServer
    {
        private readonly MatchState _state;
        private readonly IPlacementSystem _placement;
        private readonly IResourceSystem _resources;
        private readonly ISpawnSystem _spawn;
        private readonly IMovementSystem _movement;
        private readonly ICombatSystem _combat;
        private readonly IVictorySystem _victory;

        /// <summary>
        /// Per-player set of applied action ids. A key is present once that player's action
        /// id has been validated and applied exactly once (Requirement 2.3).
        /// </summary>
        private readonly HashSet<AppliedActionKey> _appliedActions = new HashSet<AppliedActionKey>();

        /// <summary>
        /// Creates a match server bound to <paramref name="state"/>, the canonical match state
        /// it owns and (only on a valid action or a simulation tick) mutates in place. Placement
        /// actions are routed to <paramref name="placement"/>, which performs its own
        /// validation-before-application and leaves state unchanged on rejection (Requirement 4).
        /// The remaining systems compose the fixed-tick simulation pipeline driven by
        /// <see cref="Tick"/> in the order Economy -> Spawn -> Movement -> Combat -> Victory
        /// (Requirements 2.1, 6.1): <paramref name="resources"/> accrues economy generation,
        /// <paramref name="spawn"/> emits units, <paramref name="movement"/> advances them,
        /// <paramref name="combat"/> resolves defensive fire, and <paramref name="victory"/>
        /// evaluates end conditions. All dependencies are required; a null argument is a
        /// construction-time programming error and throws rather than deferring a null-reference
        /// into the tick loop.
        /// </summary>
        public MatchServer(
            MatchState state,
            IPlacementSystem placement,
            IResourceSystem resources,
            ISpawnSystem spawn,
            IMovementSystem movement,
            ICombatSystem combat,
            IVictorySystem victory)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _placement = placement ?? throw new ArgumentNullException(nameof(placement));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _spawn = spawn ?? throw new ArgumentNullException(nameof(spawn));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _victory = victory ?? throw new ArgumentNullException(nameof(victory));
        }

        /// <inheritdoc />
        public MatchState State => _state;

        /// <inheritdoc />
        public ActionResult SubmitAction(PlayerAction action)
        {
            // A null action has nothing to validate or apply; reject totally without mutating
            // the canonical state (Requirement 2.4).
            if (action == null)
            {
                return ActionResult.Rejected(RejectionReason.MatchEnded);
            }

            // An ended match rejects all further gameplay actions and keeps its frozen result
            // and state unchanged (Requirement 8.6). Checked first so no idempotency bookkeeping
            // or routing happens after the match has concluded.
            if (_state.IsEnded)
            {
                return ActionResult.Rejected(RejectionReason.MatchEnded);
            }

            // Exactly-once: a re-delivery of an action this player has already had applied
            // makes no additional change to the canonical state (Requirement 2.3).
            var key = new AppliedActionKey(action.PlacingPlayer, action.ActionId);
            if (_appliedActions.Contains(key))
            {
                return ActionResult.Duplicate();
            }

            // Route to the matching validator. Each validator runs validation-before-application
            // and leaves canonical state unchanged on rejection (Requirements 2.4, 4.2-4.4).
            ActionResult result = Route(action);

            // Only record actions that were actually applied, so a rejected action may be
            // retried after the player corrects the condition (e.g. once affordable). An
            // accepted result means the canonical state changed exactly once (Requirement 2.3).
            if (result.Success && !result.WasDuplicate)
            {
                _appliedActions.Add(key);
            }

            return result;
        }

        /// <summary>
        /// Dispatches <paramref name="action"/> to the validator for its
        /// <see cref="PlayerAction.Kind"/>. Unknown kinds are rejected rather than throwing so
        /// the pipeline stays total (Requirement 2.4).
        /// </summary>
        private ActionResult Route(PlayerAction action)
        {
            switch (action.Kind)
            {
                case PlayerActionKind.Placement:
                    return ApplyPlacement(action as PlacementAction);
                default:
                    // No validator for this kind; reject without mutating state.
                    return ActionResult.Rejected(RejectionReason.MatchEnded);
            }
        }

        /// <summary>
        /// Routes a <see cref="PlacementAction"/> to <see cref="IPlacementSystem.TryPlace"/> and
        /// translates its <see cref="PlacementResult"/> into an <see cref="ActionResult"/>:
        /// accepted on success, otherwise rejected carrying the placement system's
        /// <see cref="RejectionReason"/> with state left unchanged (Requirements 2.4, 4.2-4.5).
        /// </summary>
        private ActionResult ApplyPlacement(PlacementAction placement)
        {
            // A Placement kind that is not a PlacementAction is a malformed action; reject
            // without mutating state (totality).
            if (placement == null)
            {
                return ActionResult.Rejected(RejectionReason.MatchEnded);
            }

            PlacementResult result = _placement.TryPlace(placement);
            if (result != null && result.Success)
            {
                return ActionResult.Accepted();
            }

            // Preserve the placement system's typed reason; default to OutOfBounds if (defensively)
            // a rejection arrived without one, so the client still receives a rejection response.
            RejectionReason reason = result?.Reason ?? RejectionReason.OutOfBounds;
            return ActionResult.Rejected(reason);
        }

        /// <summary>
        /// Advances the fixed-step simulation by one tick, driving the canonical pipeline in
        /// the order the design sequence diagram specifies — Economy (accrue) -> Spawn ->
        /// Movement -> Combat -> Victory — exactly once (Requirements 2.1, 6.1). Ordering is
        /// load-bearing: economy before anything that may spend, movement before combat so a
        /// unit is targeted at its post-move position, and combat before victory so a base
        /// destroyed this tick ends the match in the same tick.
        ///
        /// <para>
        /// The simulation only advances while the match is <see cref="MatchPhase.Active"/>: in
        /// <see cref="MatchPhase.Lobby"/> the match has not started, and <see cref="MatchPhase.Paused"/>
        /// (a disconnect reconnection window, Requirement 9.4) must freeze the world so a
        /// reconnecting player resumes from preserved state rather than a world that kept moving
        /// without them. An <see cref="MatchPhase.Ended"/> match is frozen for good
        /// (Requirement 8.6). Victory evaluation is intentionally still run while active so the
        /// match can transition to Ended within the tick that destroyed a base.
        /// </para>
        ///
        /// <para>
        /// <b>Totality.</b> Each step is total over its valid domain: the underlying systems
        /// clamp/hold/no-op rather than throw (per their contracts), so one entity in an
        /// unexpected state is skipped rather than aborting the tick for every other entity.
        /// The loop itself adds no per-entity logic that could throw, so a full tick always
        /// completes once started.
        /// </para>
        ///
        /// <para>
        /// <b>Replication marking.</b> After the pipeline runs, the canonical state is marked
        /// changed via <see cref="MatchState.MarkChanged"/> so the replication layer
        /// (task 13.1) can observe that a new snapshot is available and push it
        /// (Requirement 2.1). The marker is bumped once per advanced tick regardless of which
        /// systems mutated state this tick: at 20 Hz an active match effectively always has
        /// progressing timers/positions, and a coarse per-tick mark keeps this layer free of
        /// per-field change tracking while the Net layer's own equality checks suppress any
        /// genuinely redundant NetworkVariable writes.
        /// </para>
        /// </summary>
        /// <param name="deltaSeconds">Elapsed simulation time for this tick, in seconds
        /// (<see cref="SimulationConstants.TickIntervalSeconds"/> at the 20 Hz fixed cadence,
        /// Requirement 6.1).</param>
        public void Tick(float deltaSeconds)
        {
            // Only advance the simulation while the match is actively running. Lobby, Paused
            // (reconnection window, Requirement 9.4), and Ended (frozen result, Requirement 8.6)
            // all leave the canonical world untouched so nothing is replicated as "changed".
            if (_state.Phase != MatchPhase.Active)
            {
                return;
            }

            // The canonical simulation tick counter advances once per fixed tick. Bumped before
            // the systems run so any per-tick bookkeeping observes the tick this step belongs to.
            _state.Tick++;

            // Ordered pipeline, once per tick, matching the design sequence diagram. Each call
            // is total over its valid domain (clamp/hold/no-op rather than throw), so a single
            // bad entity inside any step cannot abort the remaining steps or the tick.

            // 1. Economy: accrue per-1s generation, clamped at the cap (Requirement 3.2).
            _resources.AccrueTick(deltaSeconds);

            // 2. Spawn: emit units on elapsed spawn intervals; hold pending/pathless (Requirement 5).
            _spawn.Tick(deltaSeconds);

            // 3. Movement: advance units along their paths, damaging the base on arrival
            //    (Requirement 6). Runs before combat so a unit is engaged at its post-move position.
            _movement.Tick(deltaSeconds);

            // 4. Combat: defensive structures target and damage the furthest-progressed enemy in
            //    range, removing zero-health units within the tick (Requirement 7).
            _combat.Tick(deltaSeconds);

            // 5. Victory: evaluate base-health end conditions last so a base destroyed by this
            //    tick's movement/combat ends the match in the same tick (Requirements 8.1-8.4).
            //    This is the one step allowed to transition the match out of Active (to Ended).
            _victory.Evaluate();

            // Mark the canonical state changed so the replication layer can publish the new
            // snapshot for this tick (Requirement 2.1).
            _state.MarkChanged();
        }

        /// <inheritdoc />
        public void OnClientConnected(PlayerId id)
        {
            // Pause/resume and the reconnection window live in the Net layer's pure
            // DisconnectionCoordinator (task 13.3), driven by the thin NGO connection-callback
            // behaviour: Core cannot reference the Net assembly, so the coordinator reads and
            // mutates this canonical state directly (set Connection=Connected, resume
            // Paused->Active) and resynchronizes via the replicated state / result resend
            // (Requirements 2.6, 9.4, 8.7). This Core entry point is retained for the
            // IMatchServer contract but intentionally delegates the behavior out of Core.
        }

        /// <inheritdoc />
        public void OnClientDisconnected(PlayerId id)
        {
            // See OnClientConnected: the Net layer's DisconnectionCoordinator (task 13.3) owns
            // entering the reconnection window (Connection=ReconnectWindow), pausing the match
            // (Active->Paused), running the 30s countdown, and resolving via
            // IVictorySystem.OnDisconnectTimeout on expiry (Requirement 9). It operates on this
            // canonical state directly from the Net assembly.
        }

        /// <summary>
        /// The per-player idempotency key used to de-duplicate re-delivered actions: the pair
        /// (<see cref="PlayerId"/>, action id). Equality over both components keeps independent
        /// per-client id streams from colliding (Requirement 2.3).
        /// </summary>
        private readonly struct AppliedActionKey : IEquatable<AppliedActionKey>
        {
            private readonly PlayerId _player;
            private readonly uint _actionId;

            public AppliedActionKey(PlayerId player, uint actionId)
            {
                _player = player;
                _actionId = actionId;
            }

            public bool Equals(AppliedActionKey other)
            {
                return _player == other._player && _actionId == other._actionId;
            }

            public override bool Equals(object obj) => obj is AppliedActionKey other && Equals(other);

            public override int GetHashCode()
            {
                // Combine both components; unchecked so arithmetic overflow wraps rather than throws.
                unchecked
                {
                    return (_player.GetHashCode() * 397) ^ _actionId.GetHashCode();
                }
            }
        }
    }
}
