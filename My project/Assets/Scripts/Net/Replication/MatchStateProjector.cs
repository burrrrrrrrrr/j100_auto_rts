using System;
using System.Collections.Generic;
using MultiplayerRts.Core;

namespace MultiplayerRts.Net
{
    /// <summary>
    /// The pure, Netcode-free heart of canonical-state replication (Requirements 2.5, 2.6,
    /// 3.5, 4.6, 8.7). It does two things, both independently testable against Core:
    ///
    /// <list type="number">
    /// <item><description>
    /// <b>Projection.</b> <see cref="Project"/> maps a live <see cref="MatchState"/> to an
    /// immutable <see cref="MatchStateSnapshot"/> containing exactly the enumerated
    /// replicated fields — unit positions, structures, per-player resource totals and base
    /// health, and match phase/outcome — ordered deterministically so two projections of
    /// equal states are themselves equal.
    /// </description></item>
    /// <item><description>
    /// <b>Change detection.</b> <see cref="TryProjectChanged"/> consumes
    /// <see cref="MatchState.Revision"/> (bumped once per advanced tick by
    /// <see cref="MatchState.MarkChanged"/>) to decide whether a new snapshot must be
    /// pushed. It only re-projects when the revision advanced past the last published one,
    /// and then confirms with structural equality so a bumped-but-identical revision does
    /// not produce a redundant push. This is what lets the thin NetworkBehaviour "push
    /// updated resource totals and state changes on change" without per-field diffing.
    /// </description></item>
    /// </list>
    ///
    /// <para>
    /// The projector is a stateful change-detector but holds no Netcode handles: it tracks
    /// the last revision it published and the last snapshot it emitted. One instance is
    /// owned per replicating server behaviour. Total over its domain: a null state projects
    /// an empty snapshot rather than throwing.
    /// </para>
    /// </summary>
    public sealed class MatchStateProjector
    {
        /// <summary>
        /// Sentinel for "nothing published yet", so the very first observation of any state —
        /// including a brand-new state still at revision 0 — is treated as a change and
        /// pushed. A fresh <see cref="MatchState"/> starts at revision 0, so initializing the
        /// baseline to 0 would wrongly suppress the initial snapshot a late-joining client
        /// needs; -1 guarantees the first <see cref="TryProjectChanged"/> fires.
        /// </summary>
        private const long NoRevisionPublished = -1;

        private long _lastPublishedRevision = NoRevisionPublished;
        private MatchStateSnapshot _lastSnapshot;

        /// <summary>The most recently emitted snapshot, or null before the first projection.</summary>
        public MatchStateSnapshot LastSnapshot => _lastSnapshot;

        /// <summary>
        /// Projects <paramref name="state"/> to an immutable snapshot of the replicated
        /// fields. Pure and side-effect-free: it does not touch the projector's change-
        /// detection bookkeeping, so callers can snapshot on demand (e.g. to answer a
        /// reconnecting client, Requirement 2.6) without disturbing the push cadence.
        /// Entities are ordered by id and players by id so equal states project to equal
        /// snapshots.
        /// </summary>
        public static MatchStateSnapshot Project(MatchState state)
        {
            if (state == null)
            {
                return new MatchStateSnapshot(
                    MatchStateProjector.NoRevisionPublished,
                    MatchPhase.Lobby,
                    null,
                    Array.Empty<PlayerSnapshot>(),
                    Array.Empty<UnitSnapshot>(),
                    Array.Empty<StructureSnapshot>());
            }

            var players = new List<PlayerSnapshot>(state.Players.Count);
            foreach (KeyValuePair<PlayerId, PlayerState> entry in state.Players)
            {
                PlayerState p = entry.Value;
                if (p == null)
                {
                    continue;
                }

                players.Add(new PlayerSnapshot(
                    p.Id,
                    p.ResourceTotal,
                    p.BaseHealth,
                    p.BaseMaxHealth,
                    p.Connection));
            }

            players.Sort(static (a, b) => a.Id.Value.CompareTo(b.Id.Value));

            var units = new List<UnitSnapshot>(state.Units.Count);
            foreach (KeyValuePair<UnitId, Unit> entry in state.Units)
            {
                Unit u = entry.Value;
                if (u == null)
                {
                    continue;
                }

                units.Add(new UnitSnapshot(u.Id, u.Owner, u.Position, u.Health, u.IsHeld));
            }

            units.Sort(static (a, b) => a.Id.Value.CompareTo(b.Id.Value));

            var structures = new List<StructureSnapshot>(state.Structures.Count);
            foreach (KeyValuePair<StructureId, Structure> entry in state.Structures)
            {
                Structure s = entry.Value;
                if (s == null)
                {
                    continue;
                }

                structures.Add(new StructureSnapshot(s.Id, s.Owner, s.Type, s.Footprint, s.IsActive));
            }

            structures.Sort(static (a, b) => a.Id.Value.CompareTo(b.Id.Value));

            MatchOutcomeSnapshot? outcome = MatchOutcomeSnapshot.FromOutcome(state.Outcome);

            return new MatchStateSnapshot(
                state.Revision,
                state.Phase,
                outcome,
                players,
                units,
                structures);
        }

        /// <summary>
        /// Change-detecting projection. Returns true and sets <paramref name="snapshot"/> to a
        /// freshly projected snapshot exactly when the canonical state has changed since the
        /// last pushed snapshot; returns false (with the last snapshot echoed) otherwise.
        ///
        /// <para>
        /// The revision check is the fast path: while <see cref="MatchState.Revision"/> has not
        /// advanced past the last published revision there is definitionally nothing new to
        /// push, so no projection work is done. When the revision has advanced, the state is
        /// projected and compared structurally against the last published snapshot; a snapshot
        /// that differs only because the revision counter moved (no replicated field changed)
        /// is not re-pushed, but the baseline revision is still advanced so the comparison is
        /// not repeated every tick. This yields "push on change" for resource totals and all
        /// other replicated fields (Requirements 2.5, 3.5, 4.6, 8.7).
        /// </para>
        /// </summary>
        /// <param name="state">The live canonical state to observe.</param>
        /// <param name="snapshot">The snapshot to push when this returns true; the last emitted
        /// snapshot when it returns false.</param>
        /// <returns>True when a new snapshot should be published.</returns>
        public bool TryProjectChanged(MatchState state, out MatchStateSnapshot snapshot)
        {
            long currentRevision = state?.Revision ?? NoRevisionPublished;

            // Fast path: the revision marker has not advanced, so nothing changed since the last
            // push. The first-ever call never takes this path because the baseline is -1.
            if (_lastSnapshot != null && currentRevision == _lastPublishedRevision)
            {
                snapshot = _lastSnapshot;
                return false;
            }

            MatchStateSnapshot projected = Project(state);

            // The revision advanced (or this is the first observation). Confirm an actual
            // replicated-field change before declaring a push, so a no-op tick that only bumped
            // the revision does not spam an identical snapshot. Advance the baseline either way
            // so we don't re-compare the same revision on the next tick.
            bool changed = _lastSnapshot == null || !projected.EqualsIgnoringRevision(_lastSnapshot);

            _lastPublishedRevision = currentRevision;

            if (changed)
            {
                _lastSnapshot = projected;
                snapshot = projected;
                return true;
            }

            snapshot = _lastSnapshot;
            return false;
        }

        /// <summary>
        /// Resets change-detection bookkeeping so the next <see cref="TryProjectChanged"/> is
        /// treated as a first observation and pushes unconditionally. Used when a client
        /// (re)connects and must be brought fully current regardless of the revision it last
        /// saw (Requirement 2.6).
        /// </summary>
        public void Reset()
        {
            _lastPublishedRevision = NoRevisionPublished;
            _lastSnapshot = null;
        }
    }
}
