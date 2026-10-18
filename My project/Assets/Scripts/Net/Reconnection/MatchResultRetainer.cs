using System;
using MultiplayerRts.Core;

namespace MultiplayerRts.Net
{
    /// <summary>
    /// The pure, Netcode-free half of match-result delivery across a reconnection
    /// (Requirements 8.7, 9.3). Once a match has concluded, the server broadcasts the result
    /// to the clients connected at that moment (task 13.2 <c>BroadcastMatchResult</c>), but a
    /// client that was absent — mid-disconnect at send time — must still learn the outcome the
    /// moment it reconnects within the same match session (Requirement 8.7). This retainer is
    /// the Core-only memory of "the match ended, and here is the frozen result to hand to
    /// anyone who shows up late", so the retain-and-resend decision is verifiable by the
    /// out-of-tree dotnet smoke check without a Unity runtime. The thin NGO behaviour owns one
    /// instance, offers the current outcome to it each time it observes an ended match, and —
    /// when a reconnect produces a payload — fires the targeted result <c>ClientRpc</c>.
    ///
    /// <para>
    /// <b>Why a retainer rather than relying only on the broadcast.</b> The one-off broadcast
    /// is a low-latency nudge for present clients; it is not delivered to an absent one. The
    /// durable source of truth for an absent client is the persistent replicated outcome
    /// (task 13.1 <see cref="MatchReplicationBehaviour.Outcome"/>), which NGO brings current on
    /// reconnect automatically. This retainer complements that with an explicit, immediate
    /// targeted resend so the reconnecting client receives the same <see cref="MatchResultEvent"/>
    /// the connected clients got, satisfying both "retain and resend on reconnection within the
    /// session" (8.7) and the disconnect-resolution result delivery (9.3) through a single code
    /// path.
    /// </para>
    ///
    /// <para>
    /// <b>Totality.</b> <see cref="Capture"/> ignores a null outcome (an in-progress match has
    /// nothing to retain) and is idempotent — the first non-null outcome it sees is frozen and
    /// later offers do not overwrite it, mirroring the canonical result being frozen once set
    /// (Requirement 8.6). <see cref="TryBuildResend"/> returns false whenever there is no
    /// retained result to deliver, so a reconnect before the match ended simply yields nothing
    /// to resend.
    /// </para>
    /// </summary>
    public sealed class MatchResultRetainer
    {
        private bool _hasResult;
        private MatchResultEvent _result;

        /// <summary>True once a match result has been retained and is available to resend.</summary>
        public bool HasResult => _hasResult;

        /// <summary>
        /// The retained result payload. Meaningful only when <see cref="HasResult"/> is true;
        /// a default value otherwise.
        /// </summary>
        public MatchResultEvent Result => _result;

        /// <summary>
        /// Offers the current canonical <paramref name="outcome"/> to the retainer. The first
        /// non-null outcome is captured and frozen as the result to resend (Requirement 8.5/8.7);
        /// a null outcome (in-progress match) is ignored, and once a result is retained, later
        /// offers are ignored so the frozen result cannot be overwritten (Requirement 8.6).
        /// Returns true only on the call that first captures a result, so the caller can perform
        /// any one-time "match just ended" work.
        /// </summary>
        public bool Capture(MatchOutcome outcome)
        {
            if (_hasResult || outcome == null)
            {
                return false;
            }

            _result = MatchResultEvent.From(outcome);
            _hasResult = true;
            return true;
        }

        /// <summary>
        /// Captures the result directly from <paramref name="state"/> when its match has ended,
        /// a convenience for callers that hold the canonical state rather than a bare outcome.
        /// A no-op (returns false) for a null state, a match that has not ended, or an ended
        /// match whose outcome is somehow absent.
        /// </summary>
        public bool CaptureFrom(MatchState state)
        {
            if (state == null || !state.IsEnded)
            {
                return false;
            }

            return Capture(state.Outcome);
        }

        /// <summary>
        /// Builds the result payload to resend to a client that (re)connected within the match
        /// session (Requirements 8.7, 9.3). Returns true and sets <paramref name="payload"/> to
        /// the retained result when one exists; returns false (with a default payload) when the
        /// match has not yet produced a result, so the caller sends nothing. Non-destructive: the
        /// retained result stays available so it can be resent again to any further reconnect
        /// within the session.
        /// </summary>
        public bool TryBuildResend(out MatchResultEvent payload)
        {
            if (!_hasResult)
            {
                payload = default;
                return false;
            }

            payload = _result;
            return true;
        }

        /// <summary>
        /// Clears the retained result. Used when a match session ends for good (the server tears
        /// down the match), so a subsequent session starts with nothing to resend.
        /// </summary>
        public void Reset()
        {
            _hasResult = false;
            _result = default;
        }
    }
}
