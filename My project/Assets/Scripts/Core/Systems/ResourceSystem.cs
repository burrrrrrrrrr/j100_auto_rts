using System.Collections.Generic;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// Canonical resource economy over a <see cref="MatchState"/> (Requirement 3): sets
    /// starting totals, accrues generation from active <c>Economy_Structure</c>s per
    /// 1-second interval (clamped at the cap), and resolves affordability and spending.
    /// Pure C# with no Unity or Netcode dependency so it can be property-tested against an
    /// in-memory state. Every operation is total over its valid domain (no-ops rather than
    /// throwing for unknown players) so a single bad entity cannot abort a tick.
    /// </summary>
    public sealed class ResourceSystem : IResourceSystem
    {
        private readonly MatchState _state;

        /// <summary>
        /// Creates a resource system bound to <paramref name="state"/>, the canonical match
        /// state it reads from and mutates in place.
        /// </summary>
        public ResourceSystem(MatchState state)
        {
            _state = state;
        }

        /// <inheritdoc />
        public void StartMatch(PlayerId a, PlayerId b)
        {
            SetStartingTotal(a);
            SetStartingTotal(b);
        }

        /// <inheritdoc />
        public void AccrueTick(float deltaSeconds)
        {
            // Non-positive steps advance no time; nothing accrues (Requirement 3.2).
            if (deltaSeconds <= 0f)
            {
                return;
            }

            foreach (KeyValuePair<StructureId, Structure> entry in _state.Structures)
            {
                Structure structure = entry.Value;

                // Only active economy structures generate resources (Requirements 3.2, 5.4).
                if (structure == null
                    || structure.Type != StructureType.Economy
                    || !structure.IsActive)
                {
                    continue;
                }

                AccrueStructure(structure, deltaSeconds);
            }
        }

        /// <inheritdoc />
        public bool CanAfford(PlayerId id, int cost)
        {
            // A non-positive cost is always affordable; a zero cost spends nothing.
            if (cost <= 0)
            {
                return true;
            }

            return TryGetPlayer(id, out PlayerState player) && player.ResourceTotal >= cost;
        }

        /// <inheritdoc />
        public bool TrySpend(PlayerId id, int cost)
        {
            if (!TryGetPlayer(id, out PlayerState player))
            {
                return false;
            }

            // Reject unaffordable spends with the total left unchanged (Requirement 3.4).
            if (cost > 0 && player.ResourceTotal < cost)
            {
                return false;
            }

            // Decrement by exactly the cost on success (Requirement 3.3). A non-positive
            // cost leaves the total unchanged.
            if (cost > 0)
            {
                player.ResourceTotal -= cost;
            }

            return true;
        }

        /// <inheritdoc />
        public int GetTotal(PlayerId id)
        {
            return TryGetPlayer(id, out PlayerState player) ? player.ResourceTotal : 0;
        }

        /// <summary>
        /// Sets a known player's resource total to the defined starting amount
        /// (Requirement 3.1); a no-op when the player is not present in the match.
        /// </summary>
        private void SetStartingTotal(PlayerId id)
        {
            if (TryGetPlayer(id, out PlayerState player))
            {
                player.ResourceTotal = SimulationConstants.StartingResources;
            }
        }

        /// <summary>
        /// Advances one economy structure's generation timer and adds its rate to its
        /// owner's total for each whole 1-second interval that elapsed, clamping the owner's
        /// total at the cap (Requirement 3.2). The fractional remainder is retained on the
        /// timer so accrual stays accurate across ticks.
        /// </summary>
        private void AccrueStructure(Structure structure, float deltaSeconds)
        {
            float interval = SimulationConstants.EconomyGenerationIntervalSeconds;
            structure.GenerationTimer += deltaSeconds;

            // A non-positive interval is degenerate; keep the accumulated time and skip.
            if (interval <= 0f)
            {
                return;
            }

            int intervalsElapsed = 0;
            while (structure.GenerationTimer >= interval)
            {
                structure.GenerationTimer -= interval;
                intervalsElapsed++;
            }

            if (intervalsElapsed == 0 || structure.GenerationRate == 0)
            {
                return;
            }

            if (!TryGetPlayer(structure.Owner, out PlayerState owner))
            {
                return;
            }

            // Accumulate as a long so a large rate times many intervals cannot overflow
            // before the cap clamp is applied (Requirement 3.2).
            long generated = (long)structure.GenerationRate * intervalsElapsed;
            owner.ResourceTotal = SimulationConstants.ClampResources(
                owner.ResourceTotal + generated);
        }

        /// <summary>
        /// Resolves a player's canonical state by id, returning false when the match holds
        /// no such player so callers can treat the operation as a total no-op.
        /// </summary>
        private bool TryGetPlayer(PlayerId id, out PlayerState player)
        {
            return _state.Players.TryGetValue(id, out player) && player != null;
        }
    }
}
