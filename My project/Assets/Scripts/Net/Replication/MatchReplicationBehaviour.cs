using System.Collections.Generic;
using MultiplayerRts.Core;
using Unity.Netcode;

namespace MultiplayerRts.Net
{
    /// <summary>
    /// The thin, Netcode-for-GameObjects-specific half of canonical-state replication
    /// (design.md "State replication": <see cref="NetworkVariable{T}"/> for persistent
    /// canonical state so reconnecting/late clients converge; Requirements 2.5, 2.6, 3.5,
    /// 4.6, 8.7). It owns the <see cref="NetworkVariable{T}"/>s / <see cref="NetworkList{T}"/>s
    /// that mirror the enumerated replicated state — per-player resource totals and base
    /// health, unit positions, structures, and match phase/outcome — and does nothing more
    /// than assign them from the pure <see cref="MatchStateSnapshot"/> whenever the pure
    /// <see cref="MatchStateProjector"/> reports the canonical state changed.
    ///
    /// <para>
    /// <b>Why this split.</b> All projection and change-detection logic lives in the pure,
    /// Core-only <see cref="MatchStateProjector"/> (verifiable without a Unity runtime). This
    /// behaviour is deliberately mechanical: on each server tick it asks the projector for a
    /// changed snapshot and, if there is one, copies its fields onto the NetworkVariables.
    /// Because NetworkVariables synchronize persistently, a late-joining or reconnecting
    /// client is automatically brought current without any extra resend logic here
    /// (Requirements 2.6, 8.7). Each NetworkVariable's own equality check suppresses a write
    /// whose value did not actually change, so pushing a snapshot is cheap even if a field
    /// happened to be unchanged.
    /// </para>
    ///
    /// <para>
    /// <b>Authority.</b> Every replicated variable is server-write / everyone-read: the
    /// server owns the canonical state (Requirement 2.1) and clients never mutate it. Writes
    /// are gated on <see cref="NetworkBehaviour.IsServer"/> so a client instance of this
    /// behaviour is purely a read mirror.
    /// </para>
    ///
    /// <para>
    /// <b>Environment note.</b> This file depends on the <c>Unity.Netcode</c> assembly
    /// (com.unity.netcode.gameobjects). It compiles only after a Unity package restore brings
    /// that package in; the pure snapshot/projector it drives compiles and is tested
    /// independently of Netcode.
    /// </para>
    /// </summary>
    public sealed class MatchReplicationBehaviour : NetworkBehaviour
    {
        /// <summary>Server-owned permission for all replicated canonical state.</summary>
        private const NetworkVariableReadPermission EveryoneRead = NetworkVariableReadPermission.Everyone;
        private const NetworkVariableWritePermission ServerWrite = NetworkVariableWritePermission.Server;

        /// <summary>Replicated match lifecycle phase (Requirement 8).</summary>
        public readonly NetworkVariable<MatchPhase> Phase =
            new NetworkVariable<MatchPhase>(MatchPhase.Lobby, EveryoneRead, ServerWrite);

        /// <summary>
        /// Replicated frozen outcome, persisted so a client reconnecting after the match ended
        /// converges to the result (Requirements 8.5, 8.7). <see cref="NetMatchOutcome.HasOutcome"/>
        /// distinguishes "no outcome yet" from a decided result without a nullable payload.
        /// </summary>
        public readonly NetworkVariable<NetMatchOutcome> Outcome =
            new NetworkVariable<NetMatchOutcome>(default, EveryoneRead, ServerWrite);

        /// <summary>Replicated per-player resource totals and base health (Requirements 3.5, 2.5).</summary>
        public readonly NetworkList<NetPlayer> Players = new NetworkList<NetPlayer>(
            new List<NetPlayer>(), EveryoneRead, ServerWrite);

        /// <summary>Replicated unit positions that clients render (Requirements 2.5, 6.5).</summary>
        public readonly NetworkList<NetUnit> Units = new NetworkList<NetUnit>(
            new List<NetUnit>(), EveryoneRead, ServerWrite);

        /// <summary>Replicated structures with location and owner (Requirement 4.6).</summary>
        public readonly NetworkList<NetStructure> Structures = new NetworkList<NetStructure>(
            new List<NetStructure>(), EveryoneRead, ServerWrite);

        /// <summary>
        /// Pure change-detector/projector. Server-only: a client mirror never projects, it only
        /// reads the replicated variables the server pushed.
        /// </summary>
        private readonly MatchStateProjector _projector = new MatchStateProjector();

        /// <summary>
        /// The canonical state this behaviour replicates. Injected on the server after the
        /// match is created (the Net layer owns the <see cref="MatchServer"/> and its state).
        /// Null on client instances.
        /// </summary>
        private MatchState _state;

        /// <summary>
        /// Binds the server-side canonical state this behaviour mirrors and immediately pushes
        /// the current snapshot so already-connected clients converge at bind time
        /// (Requirement 2.6). No-op effect on non-server instances.
        /// </summary>
        public void BindState(MatchState state)
        {
            _state = state;
            if (IsServer)
            {
                _projector.Reset();
                PublishIfChanged();
            }
        }

        /// <summary>
        /// Server replication entry point, called once per fixed simulation tick after
        /// <see cref="MatchServer.Tick"/> has advanced (and marked) the canonical state. Asks
        /// the pure projector whether the state changed since the last push and, if so, copies
        /// the new snapshot onto the NetworkVariables (Requirements 2.5, 3.5). Safe to call
        /// every tick: when nothing changed the projector's fast revision check returns false
        /// and no writes happen.
        /// </summary>
        public void ServerReplicationTick()
        {
            if (!IsServer)
            {
                return;
            }

            PublishIfChanged();
        }

        private void PublishIfChanged()
        {
            if (_state == null)
            {
                return;
            }

            if (_projector.TryProjectChanged(_state, out MatchStateSnapshot snapshot))
            {
                Apply(snapshot);
            }
        }

        /// <summary>
        /// Copies <paramref name="snapshot"/> onto the replicated variables. Scalars are
        /// assigned directly (NetworkVariable equality suppresses no-op writes); the lists are
        /// rebuilt from the snapshot's deterministically ordered entries. Rebuilding the whole
        /// list each change is acceptable at this scope (two players, modest unit/structure
        /// counts) and keeps this method a straight, side-effect-free mirror of the snapshot.
        /// </summary>
        private void Apply(MatchStateSnapshot snapshot)
        {
            Phase.Value = snapshot.Phase;
            Outcome.Value = NetMatchOutcome.From(snapshot.Outcome);

            Players.Clear();
            for (int i = 0; i < snapshot.Players.Count; i++)
            {
                Players.Add(NetPlayer.From(snapshot.Players[i]));
            }

            Units.Clear();
            for (int i = 0; i < snapshot.Units.Count; i++)
            {
                Units.Add(NetUnit.From(snapshot.Units[i]));
            }

            Structures.Clear();
            for (int i = 0; i < snapshot.Structures.Count; i++)
            {
                Structures.Add(NetStructure.From(snapshot.Structures[i]));
            }
        }
    }
}
