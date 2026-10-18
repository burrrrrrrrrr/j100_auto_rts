using CsCheck;
using MultiplayerRts.Core;
using MultiplayerRts.Tests.Fixtures;
using MultiplayerRts.Tests.Generators;
using MultiplayerRts.Tests.Transport;
using NUnit.Framework;

namespace MultiplayerRts.Tests
{
    /// <summary>
    /// Smoke tests that exercise the property-based testing harness itself — the CsCheck
    /// library, the custom generators, the in-memory fixtures, and the mocked transport —
    /// so later property tasks (Properties 1–38) build on a known-good foundation. These
    /// are NOT any of the numbered design properties; they verify the plumbing established
    /// by task 1.3. Every property-style check runs at least <see cref="Pbt.Iterations"/>
    /// (100) generated iterations, matching the design's Testing Strategy.
    /// </summary>
    [TestFixture]
    public class SampleHarnessTests
    {
        /// <summary>
        /// Trivial sample property proving CsCheck runs at the configured 100-iteration
        /// minimum against a custom generator. The property is a tautology about the
        /// resource-total generator staying within the valid [0, cap] range.
        /// </summary>
        [Test]
        public void SampleProperty_RunsAtLeast100Iterations()
        {
            Gens.Resources.Sample(
                r => r >= 0 && r <= SimulationConstants.ResourceCap,
                iter: Pbt.Iterations);
        }

        /// <summary>
        /// The resource-display generator spans the full formatter range [0, 999,999,999]
        /// (Requirement 10.3) — confirms the boundary-aware generator yields in-range values.
        /// </summary>
        [Test]
        public void ResourceDisplayGenerator_StaysInFormatterRange()
        {
            Gens.ResourceDisplayValue.Sample(
                v => v >= 0 && v <= 999_999_999,
                iter: Pbt.Iterations);
        }

        /// <summary>
        /// The overlapping-footprint generator always produces a genuinely overlapping pair,
        /// per <see cref="Footprint.Overlaps"/> (supports Requirement 4.3 property later).
        /// </summary>
        [Test]
        public void OverlappingFootprintGenerator_AlwaysOverlaps()
        {
            Gens.OverlappingFootprints.Sample(
                pair => pair.First.Overlaps(pair.Second),
                iter: Pbt.Iterations);
        }

        /// <summary>
        /// The edge-touching generator never produces an overlap: shared edges with zero
        /// area must not count (the boundary case Requirement 4.3 depends on).
        /// </summary>
        [Test]
        public void EdgeTouchingFootprintGenerator_NeverOverlaps()
        {
            Gens.EdgeTouchingFootprints.Sample(
                pair => !pair.First.Overlaps(pair.Second),
                iter: Pbt.Iterations);
        }

        /// <summary>
        /// The build-area-with-inside-point generator always yields a point inside the area;
        /// the with-outside-point generator never does (Requirement 4.2 boundary).
        /// </summary>
        [Test]
        public void BuildAreaPointGenerators_RespectContainment()
        {
            Gens.BuildAreaWithInsidePoint.Sample(
                t => t.Area.Contains(t.Inside),
                iter: Pbt.Iterations);

            Gens.BuildAreaWithOutsidePoint.Sample(
                t => !t.Area.Contains(t.Outside),
                iter: Pbt.Iterations);
        }

        /// <summary>
        /// Generated spawn intervals always lie within the permitted [1, 600] range
        /// (Requirement 5.1), including the endpoints the generator forces.
        /// </summary>
        [Test]
        public void SpawnIntervalGenerator_StaysInValidRange()
        {
            Gens.SpawnInterval.Sample(
                interval => SimulationConstants.IsValidSpawnInterval(interval),
                iter: Pbt.Iterations);
        }

        /// <summary>
        /// Generated paths are non-empty and report their final index correctly — the shape
        /// movement/arrival logic relies on (Requirements 6.2, 6.3).
        /// </summary>
        [Test]
        public void PathGenerator_ProducesWellFormedPaths()
        {
            Gens.Path.Sample(
                path => path.HasWaypoints && path.IsFinalIndex(path.WaypointCount - 1),
                iter: Pbt.Iterations);
        }

        /// <summary>
        /// Unit sets carry unique ids and a shared owner, with every waypoint index valid for
        /// its own path — the varied-progression precondition for targeting (Requirement 7.1).
        /// </summary>
        [Test]
        public void UnitSetGenerator_HasUniqueIdsAndValidIndices()
        {
            Gens.UnitSet.Sample(
                units =>
                {
                    var seen = new System.Collections.Generic.HashSet<uint>();
                    foreach (Unit u in units)
                    {
                        if (!seen.Add(u.Id.Value))
                        {
                            return false;
                        }

                        if (u.AssignedPath != null
                            && (u.WaypointIndex < 0 || u.WaypointIndex >= u.AssignedPath.WaypointCount))
                        {
                            return false;
                        }
                    }

                    return true;
                },
                iter: Pbt.Iterations);
        }

        /// <summary>
        /// The two-player match fixture is a valid baseline: Active phase, exactly two
        /// players, each starting at 100 resources (Requirements 1.3, 3.1).
        /// </summary>
        [Test]
        public void MatchStateFixture_ProducesValidTwoPlayerBaseline()
        {
            MatchState state = MatchStateBuilder.TwoPlayerActive().Build();

            Assert.AreEqual(MatchPhase.Active, state.Phase);
            Assert.AreEqual(2, state.Players.Count);
            foreach (PlayerState p in state.Players.Values)
            {
                Assert.AreEqual(SimulationConstants.StartingResources, p.ResourceTotal);
            }
        }

        /// <summary>
        /// The mocked transport drops server→client messages addressed to a disconnected
        /// recipient and records those to connected recipients — the behavior removal and
        /// match-result properties depend on (Requirements 7.6, 8.5).
        /// </summary>
        [Test]
        public void MockTransport_DropsMessagesToDisconnectedRecipients()
        {
            var transport = new MockTransport();
            var connected = new PlayerId(1);
            var dropped = new PlayerId(2);
            transport.SetDisconnected(dropped, true);

            transport.NotifyUnitRemoved(connected, new UnitId(10));
            transport.NotifyUnitRemoved(dropped, new UnitId(10));

            Assert.AreEqual(1, transport.MessagesOfKind(MockTransport.MessageKind.UnitRemoved).Count);
            Assert.AreEqual(connected, transport.MessagesOfKind(MockTransport.MessageKind.UnitRemoved)[0].Recipient);
        }
    }
}
