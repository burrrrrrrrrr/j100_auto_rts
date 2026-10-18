using System.Collections.Generic;
using MultiplayerRts.Core;

namespace MultiplayerRts.Tests.Fixtures
{
    /// <summary>
    /// Fluent builder for an in-memory <see cref="MatchState"/> fixture — the canonical
    /// single source of truth the simulation operates on (Requirement 2.1). Property and
    /// example tests use it to assemble a state with only the entities they care about.
    /// <see cref="TwoPlayerActive"/> produces the common baseline: an Active two-player
    /// match with opposed bases and symmetric build areas, matching match-start assignment
    /// (Requirements 1.3, 1.7, 3.1).
    /// </summary>
    public sealed class MatchStateBuilder
    {
        private MatchPhase _phase = MatchPhase.Active;
        private readonly List<PlayerState> _players = new List<PlayerState>();
        private readonly List<Unit> _units = new List<Unit>();
        private readonly List<Structure> _structures = new List<Structure>();
        private MatchOutcome _outcome;
        private long _tick;

        public static MatchStateBuilder AMatch() => new MatchStateBuilder();

        /// <summary>
        /// A ready-to-use Active two-player match: players 1 and 2 at 100 resources, opposed
        /// bases, and a build area around each base. The canonical baseline most simulation
        /// tests start from.
        /// </summary>
        public static MatchStateBuilder TwoPlayerActive()
        {
            var baseA = new MapVector(0f, 0f);
            var baseB = new MapVector(100f, 0f);
            return AMatch()
                .WithPhase(MatchPhase.Active)
                .AddPlayer(PlayerStateBuilder.APlayer()
                    .WithId(new PlayerId(1))
                    .WithBaseLocation(baseA)
                    .WithBuildArea(new Footprint(new MapVector(-10f, -10f), new MapVector(20f, 20f)))
                    .Build())
                .AddPlayer(PlayerStateBuilder.APlayer()
                    .WithId(new PlayerId(2))
                    .WithBaseLocation(baseB)
                    .WithBuildArea(new Footprint(new MapVector(90f, -10f), new MapVector(20f, 20f)))
                    .Build());
        }

        public MatchStateBuilder WithPhase(MatchPhase phase)
        {
            _phase = phase;
            return this;
        }

        public MatchStateBuilder AddPlayer(PlayerState player)
        {
            _players.Add(player);
            return this;
        }

        public MatchStateBuilder AddUnit(Unit unit)
        {
            _units.Add(unit);
            return this;
        }

        public MatchStateBuilder AddUnits(IEnumerable<Unit> units)
        {
            _units.AddRange(units);
            return this;
        }

        public MatchStateBuilder AddStructure(Structure structure)
        {
            _structures.Add(structure);
            return this;
        }

        public MatchStateBuilder AddStructures(IEnumerable<Structure> structures)
        {
            _structures.AddRange(structures);
            return this;
        }

        public MatchStateBuilder WithOutcome(MatchOutcome outcome)
        {
            _outcome = outcome;
            return this;
        }

        public MatchStateBuilder WithTick(long tick)
        {
            _tick = tick;
            return this;
        }

        public MatchState Build()
        {
            var state = new MatchState(_phase)
            {
                Outcome = _outcome,
                Tick = _tick
            };

            foreach (PlayerState p in _players)
            {
                state.Players[p.Id] = p;
            }

            foreach (Unit u in _units)
            {
                state.Units[u.Id] = u;
            }

            foreach (Structure s in _structures)
            {
                state.Structures[s.Id] = s;
            }

            return state;
        }
    }
}
