using System;
using System.Collections.Generic;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// Validates and applies structure placement over the canonical <see cref="MatchState"/>
    /// (Requirement 4). Validation runs in a fixed order and the state is left completely
    /// unchanged on any rejection, so no resource is spent and no structure is created until
    /// every check passes (Requirements 4.2, 4.3, 4.4):
    /// <list type="number">
    ///   <item>within map bounds (<see cref="RejectionReason.OutOfBounds"/>);</item>
    ///   <item>within the placing player's <c>Build_Area</c>
    ///     (<see cref="RejectionReason.NotInBuildArea"/>);</item>
    ///   <item>no footprint overlap with an existing structure
    ///     (<see cref="RejectionReason.FootprintOverlap"/>);</item>
    ///   <item>affordable (<see cref="RejectionReason.InsufficientResources"/>).</item>
    /// </list>
    /// On success a <see cref="Structure"/> is created at the target location with a footprint
    /// derived from the target, owned by the placing player, assigned a freshly allocated id,
    /// and its cost is spent through the resource system (Requirement 4.5).
    ///
    /// Structure cost and footprint size come from an injected <see cref="IStructureCatalog"/>
    /// and map extent from an injected <see cref="IMapBounds"/>, keeping the system a pure C#
    /// type with no Unity dependency that can be property-tested against an in-memory state.
    /// Every operation is total over its valid domain (rejection rather than throwing for
    /// unknown players or structure types) so a single bad input cannot abort the operation.
    /// </summary>
    public sealed class PlacementSystem : IPlacementSystem
    {
        private readonly MatchState _state;
        private readonly IResourceSystem _resources;
        private readonly IStructureCatalog _catalog;
        private readonly IMapBounds _mapBounds;
        private readonly IStructureIdAllocator _idAllocator;

        /// <summary>
        /// Creates a placement system bound to <paramref name="state"/>, the canonical match
        /// state it reads from and (only on success) mutates in place. Cost/footprint come
        /// from <paramref name="catalog"/>, the map extent from <paramref name="mapBounds"/>,
        /// resource affordability and spending from <paramref name="resources"/>, and new
        /// structure ids from <paramref name="idAllocator"/>.
        /// </summary>
        public PlacementSystem(
            MatchState state,
            IResourceSystem resources,
            IStructureCatalog catalog,
            IMapBounds mapBounds,
            IStructureIdAllocator idAllocator)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _mapBounds = mapBounds ?? throw new ArgumentNullException(nameof(mapBounds));
            _idAllocator = idAllocator ?? throw new ArgumentNullException(nameof(idAllocator));
        }

        /// <inheritdoc />
        public PlacementResult TryPlace(PlacementAction action)
        {
            if (action == null)
            {
                // No action to validate; a total no-op rejection (nothing is mutated).
                return PlacementResult.Rejected(RejectionReason.OutOfBounds);
            }

            // An unknown structure type has no cost or footprint to validate against; treat
            // it as out of the valid placement domain and reject without mutating state.
            if (!_catalog.TryGetDefinition(action.Type, out StructureDefinition definition))
            {
                return PlacementResult.Rejected(RejectionReason.OutOfBounds);
            }

            // The placed footprint is positioned at the target location (its bottom-left
            // origin) and sized from the catalog definition.
            var footprint = new Footprint(action.TargetLocation, definition.FootprintSize);

            // 1. Within map bounds, else OutOfBounds (Requirement 4.1).
            if (!_mapBounds.Contains(footprint))
            {
                return PlacementResult.Rejected(RejectionReason.OutOfBounds);
            }

            // 2. Within the placing player's Build_Area, else NotInBuildArea (Requirement 4.2).
            //    An unknown player or a player with no build area cannot contain the
            //    footprint, so this rejects rather than throwing (totality).
            if (!TryGetPlayer(action.PlacingPlayer, out PlayerState player)
                || player.BuildArea == null
                || !player.BuildArea.Contains(footprint))
            {
                return PlacementResult.Rejected(RejectionReason.NotInBuildArea);
            }

            // 3. No footprint overlap with any existing structure, else FootprintOverlap
            //    (Requirement 4.3).
            if (OverlapsExistingStructure(footprint))
            {
                return PlacementResult.Rejected(RejectionReason.FootprintOverlap);
            }

            // 4. Affordable, else InsufficientResources (Requirements 3.4, 4.4). Checked
            //    before any mutation so the resource total is left unchanged on rejection.
            if (!_resources.CanAfford(action.PlacingPlayer, definition.Cost))
            {
                return PlacementResult.Rejected(RejectionReason.InsufficientResources);
            }

            // All checks passed. Spend the cost first; affordability was just confirmed, so
            // this succeeds. Guard defensively: if the spend is refused, do not create the
            // structure, keeping state consistent (no resource change, no structure).
            if (!_resources.TrySpend(action.PlacingPlayer, definition.Cost))
            {
                return PlacementResult.Rejected(RejectionReason.InsufficientResources);
            }

            // Create the structure, assign the placing player as owner, and add it to the
            // canonical state (Requirement 4.5).
            StructureId id = _idAllocator.Next();
            var structure = new Structure
            {
                Id = id,
                Owner = action.PlacingPlayer,
                Type = action.Type,
                Footprint = footprint,
                IsActive = true,
                Cost = definition.Cost,
            };

            _state.Structures[id] = structure;
            return PlacementResult.Accepted(id);
        }

        /// <summary>
        /// Returns true when <paramref name="footprint"/> intersects the footprint of any
        /// structure already present in the match (Requirement 4.3). Edge contact alone does
        /// not count as overlap (see <see cref="Footprint.Overlaps"/>).
        /// </summary>
        private bool OverlapsExistingStructure(Footprint footprint)
        {
            foreach (KeyValuePair<StructureId, Structure> entry in _state.Structures)
            {
                Structure existing = entry.Value;
                if (existing != null && footprint.Overlaps(existing.Footprint))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves a player's canonical state by id, returning false when the match holds
        /// no such player so placement can reject rather than throw (totality).
        /// </summary>
        private bool TryGetPlayer(PlayerId id, out PlayerState player)
        {
            return _state.Players.TryGetValue(id, out player) && player != null;
        }
    }
}
