using System.Collections.Generic;
using MultiplayerRts.Core;

namespace MultiplayerRts.Tests.Transport
{
    /// <summary>
    /// A deterministic, in-memory <see cref="ITransport"/> for property and example tests.
    /// It performs no real networking: every message is recorded so a test can assert what
    /// the canonical server would have sent, and recipients can be marked disconnected so
    /// tests can verify that notifications target only connected clients (Requirement 7.6).
    /// <para/>
    /// The mock is intentionally passive — it does not itself validate or apply actions.
    /// Later tasks wire a <c>MatchServer</c>/<c>GameClient</c> on top; here it provides the
    /// recorded-traffic surface those tests assert against.
    /// </summary>
    public sealed class MockTransport : ITransport
    {
        /// <summary>A single captured message, tagged by kind, for order-sensitive assertions.</summary>
        public enum MessageKind
        {
            SubmitAction,
            ActionResult,
            StateReplication,
            UnitRemoved,
            MatchResult
        }

        /// <summary>An immutable record of one message that crossed the transport.</summary>
        public sealed class Message
        {
            public MessageKind Kind { get; }
            public PlayerId Recipient { get; }
            public PlayerAction Action { get; }
            public uint ActionId { get; }
            public ActionResult Result { get; }
            public MatchState State { get; }
            public UnitId? Unit { get; }
            public MatchOutcome Outcome { get; }

            internal Message(
                MessageKind kind,
                PlayerId recipient,
                PlayerAction action = null,
                uint actionId = 0,
                ActionResult result = null,
                MatchState state = null,
                UnitId? unit = null,
                MatchOutcome outcome = null)
            {
                Kind = kind;
                Recipient = recipient;
                Action = action;
                ActionId = actionId;
                Result = result;
                State = state;
                Unit = unit;
                Outcome = outcome;
            }
        }

        private readonly List<Message> _messages = new List<Message>();
        private readonly HashSet<PlayerId> _disconnected = new HashSet<PlayerId>();

        /// <summary>Every message recorded so far, in send order.</summary>
        public IReadOnlyList<Message> Messages => _messages;

        /// <summary>Clears all recorded traffic (e.g. between generated iterations).</summary>
        public void Reset()
        {
            _messages.Clear();
            _disconnected.Clear();
        }

        /// <summary>
        /// Marks a recipient as disconnected. Messages addressed to a disconnected recipient
        /// are dropped rather than recorded, so tests can assert that server→client
        /// notifications reach only connected clients (Requirement 7.6).
        /// </summary>
        public void SetDisconnected(PlayerId player, bool disconnected)
        {
            if (disconnected)
            {
                _disconnected.Add(player);
            }
            else
            {
                _disconnected.Remove(player);
            }
        }

        /// <summary>True when the recipient is currently reachable.</summary>
        public bool IsConnected(PlayerId player) => !_disconnected.Contains(player);

        /// <summary>Returns the recorded messages of a given kind, in order.</summary>
        public IReadOnlyList<Message> MessagesOfKind(MessageKind kind)
        {
            var result = new List<Message>();
            for (int i = 0; i < _messages.Count; i++)
            {
                if (_messages[i].Kind == kind)
                {
                    result.Add(_messages[i]);
                }
            }

            return result;
        }

        /// <summary>The most recent state snapshot replicated to a recipient, or null if none.</summary>
        public MatchState LastStateFor(PlayerId recipient)
        {
            for (int i = _messages.Count - 1; i >= 0; i--)
            {
                Message m = _messages[i];
                if (m.Kind == MessageKind.StateReplication && m.Recipient == recipient)
                {
                    return m.State;
                }
            }

            return null;
        }

        // ITransport. Client→server messages are always recorded; server→client messages
        // to a disconnected recipient are dropped (never recorded), modeling Requirement 7.6.

        public void SubmitAction(PlayerAction action)
        {
            _messages.Add(new Message(MessageKind.SubmitAction, action.PlacingPlayer, action: action, actionId: action.ActionId));
        }

        public void SendActionResult(PlayerId recipient, uint actionId, ActionResult result)
        {
            if (!IsConnected(recipient))
            {
                return;
            }

            _messages.Add(new Message(MessageKind.ActionResult, recipient, actionId: actionId, result: result));
        }

        public void ReplicateState(PlayerId recipient, MatchState state)
        {
            if (!IsConnected(recipient))
            {
                return;
            }

            _messages.Add(new Message(MessageKind.StateReplication, recipient, state: state));
        }

        public void NotifyUnitRemoved(PlayerId recipient, UnitId unit)
        {
            if (!IsConnected(recipient))
            {
                return;
            }

            _messages.Add(new Message(MessageKind.UnitRemoved, recipient, unit: unit));
        }

        public void SendMatchResult(PlayerId recipient, MatchOutcome outcome)
        {
            if (!IsConnected(recipient))
            {
                return;
            }

            _messages.Add(new Message(MessageKind.MatchResult, recipient, outcome: outcome));
        }
    }
}
