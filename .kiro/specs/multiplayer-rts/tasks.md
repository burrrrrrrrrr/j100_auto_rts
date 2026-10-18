# Implementation Plan: Multiplayer RTS

## Overview

This plan implements a server-authoritative 2D top-down 1v1 RTS in Unity using Netcode for GameObjects. Work begins with the pure, framework-independent data models and simulation core (economy, placement, spawning, movement, combat, victory) so game logic can be property-tested in isolation against an in-memory state and mocked transport. Networking (NetworkVariable replication + RPCs), the lobby, and the thin presentation/rendering layer are wired on top of the verified core. Property-based tests (Properties 1–38) and example/integration tests accompany the systems they validate so regressions surface early. Each step builds on the previous and ends by integrating into the match tick pipeline or network boundary.

## Tasks

- [x] 1. Set up project structure, core data models, and test harness
  - [x] 1.1 Create the solution/assembly structure and core enums and identity types
    - Create directory layout separating pure simulation logic (`Core`) from Unity/Netcode integration (`Net`) and presentation (`Client`)
    - Define `PlayerId`, `UnitId`, `StructureId` readonly structs and `StructureType`, `MatchPhase`, `MatchOutcomeKind`, `RejectionReason`, `ConnectionState` enums
    - Define geometry types `MapVector`, `Footprint`, and the `Path` class (ordered waypoints + target base)
    - _Requirements: 2.1_

  - [x] 1.2 Implement entity and state data models
    - Implement `Unit`, `Structure` (with spawner/defensive/economy fields), `PlayerState`, `Build_Area`, `MatchState`, `MatchOutcome`
    - Implement `PlacementAction`, `PlacementResult`, `PlayerAction`, and `ActionResult` types
    - Centralize economy/spawn/geometry constants (start 100, cap 999,999, gen interval 1s, spawn range 1–600, reach threshold 0.1, tick 20 Hz)
    - _Requirements: 2.1, 3.1, 3.2, 5.1, 6.1, 6.2_

  - [x] 1.3 Set up the property-based testing framework and in-memory fixtures
    - Add an established PBT library (CsCheck or FsCheck) and a unit-test project; configure minimum 100 iterations per property
    - Create a mocked transport abstraction and in-memory `MatchState` fixtures/builders for tests
    - Add custom generators for lobbies/join sequences, player/resource/structure states, build areas and footprints (overlap + boundary), spawn/attack schedules, paths/waypoints (incl. 0.1 boundary), unit sets with varied path progression, and connection-state sets
    - _Requirements: 2.1_

- [x] 2. Implement the resource economy system
  - [x] 2.1 Implement `ResourceSystem` accrual, affordability, and spending
    - Implement `StartMatch` (set 100 each), `AccrueTick` (per-1s generation, clamp at 999,999), `CanAfford`, `TrySpend` (decrement by exact cost), `GetTotal`
    - _Requirements: 3.1, 3.2, 3.3_

  - [ ]* 2.2 Write property test for economy accrual rate and cap
    - **Property 10: Economy accrual respects rate and cap**
    - **Validates: Requirements 3.2**

  - [ ]* 2.3 Write property test for affordable spending decrement
    - **Property 11: Affordable spending decrements exactly by cost**
    - **Validates: Requirements 3.3**

  - [ ]* 2.4 Write unit test for match-start resource initialization
    - Assert both players start at exactly 100 resources
    - _Requirements: 3.1_

- [x] 3. Implement the structure placement system
  - [x] 3.1 Implement `PlacementSystem.TryPlace` validation and application
    - Validate in order: within map bounds (OutOfBounds), within `Build_Area` (NotInBuildArea), no footprint overlap (FootprintOverlap), affordable (InsufficientResources)
    - On success create the `Structure`, assign owner, and spend the cost via `ResourceSystem`
    - Return typed `PlacementResult`/`RejectionReason` on rejection with match state unchanged
    - _Requirements: 4.2, 4.3, 4.4, 4.5, 3.4_

  - [ ]* 3.2 Write property test for unaffordable placement rejection
    - **Property 12: Unaffordable placement is rejected with state unchanged**
    - **Validates: Requirements 3.4, 4.4**

  - [ ]* 3.3 Write property test for out-of-build-area rejection
    - **Property 13: Out-of-build-area placement is rejected with state unchanged**
    - **Validates: Requirements 4.2**

  - [ ]* 3.4 Write property test for non-overlapping placement
    - **Property 14: Placed structures never overlap**
    - **Validates: Requirements 4.3**

  - [ ]* 3.5 Write property test for valid placement creating an owned, charged structure
    - **Property 15: Valid placement creates an owned structure and charges its cost**
    - **Validates: Requirements 4.5**

- [x] 4. Implement the canonical action pipeline (validate + apply)
  - [x] 4.1 Implement `MatchServer.SubmitAction` validation-before-application and idempotency
    - Validate every action against canonical state; apply valid actions exactly once (track action identifiers for de-dup)
    - Leave state unchanged and produce a rejection response for invalid actions; reject all actions while match is `Ended`
    - _Requirements: 2.3, 2.4, 8.6_

  - [ ]* 4.2 Write property test for exactly-once application of valid actions
    - **Property 7: Valid actions apply exactly once**
    - **Validates: Requirements 2.3**

  - [ ]* 4.3 Write property test for invalid actions leaving state unchanged
    - **Property 8: Invalid actions leave state unchanged**
    - **Validates: Requirements 2.4**

  - [ ]* 4.4 Write property test for ended matches freezing result and rejecting actions
    - **Property 32: Ended matches reject further actions and freeze the result**
    - **Validates: Requirements 8.6**

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implement the unit spawning system
  - [x] 6.1 Implement `SpawnSystem.Tick` interval emission and pending-hold logic
    - Emit one unit per elapsed spawn interval (1–600s) for each active spawner; stop emission while inactive
    - Assign owner id and a base-targeted `Path` before activation; hold pending unit when spawn point occupied, create once free; hold pathless unit at spawn point with owner retained until a path is available
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

  - [ ]* 6.2 Write property test for active spawner interval emission
    - **Property 16: Active spawners emit units on their interval**
    - **Validates: Requirements 5.1**

  - [ ]* 6.3 Write property test for created-unit owner and base-targeted path
    - **Property 17: Created units carry owner and a base-targeted path**
    - **Validates: Requirements 5.2, 5.3**

  - [ ]* 6.4 Write property test for inactive spawners producing no units
    - **Property 18: Inactive spawners produce no units**
    - **Validates: Requirements 5.4**

  - [ ]* 6.5 Write property test for conserved blocked spawns
    - **Property 19: Blocked spawns are conserved, not lost or duplicated**
    - **Validates: Requirements 5.5**

  - [ ]* 6.6 Write property test for pathless units held with owner retained
    - **Property 20: Pathless units are held with owner retained**
    - **Validates: Requirements 5.6**

- [x] 7. Implement the unit movement system
  - [x] 7.1 Implement `MovementSystem.Tick` waypoint advancement and base arrival
    - Move each active unit toward its target waypoint by speed × dt, clamped to not overshoot; advance index when within 0.1 map units
    - On reaching the final waypoint at the opponent base, apply unit damage to that base and remove the unit; halt (no damage) on dead-end paths
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [ ]* 7.2 Write property test for speed-times-time advancement with clamping
    - **Property 21: Units advance toward the next waypoint by speed times time**
    - **Validates: Requirements 6.1**

  - [ ]* 7.3 Write property test for waypoint-reached index advancement
    - **Property 22: Reaching a waypoint advances the path index**
    - **Validates: Requirements 6.2**

  - [ ]* 7.4 Write property test for base arrival damage and removal
    - **Property 23: Arrival at the opponent base damages it and removes the unit**
    - **Validates: Requirements 6.3**

  - [ ]* 7.5 Write property test for dead-end path halting without damage
    - **Property 24: Dead-end paths halt units without damage**
    - **Validates: Requirements 6.4**

  - [ ]* 7.6 Write unit test for the 0.1-map-unit waypoint boundary
    - Test waypoint-reach at exactly 0.1 map units
    - _Requirements: 6.2_

- [x] 8. Implement the combat system
  - [x] 8.1 Implement `CombatSystem.Tick` targeting, interval damage, and removal
    - Select the single furthest-progressed enemy unit in range as target; apply damage once per attack interval while the target stays in range
    - Apply no damage and retain timing when no enemy is in range; remove units at ≤0 health within one tick
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [ ]* 8.2 Write property test for furthest-progressed targeting
    - **Property 25: Defensive structures target the furthest-progressed enemy**
    - **Validates: Requirements 7.1**

  - [ ]* 8.3 Write property test for once-per-interval damage in range
    - **Property 26: Damage is applied once per attack interval while in range**
    - **Validates: Requirements 7.2**

  - [ ]* 8.4 Write property test for empty-range no-damage timing retention
    - **Property 27: Empty range applies no damage and preserves timing**
    - **Validates: Requirements 7.3**

  - [ ]* 8.5 Write property test for one-tick removal of zero-health units
    - **Property 28: Units at or below zero health are removed within one tick**
    - **Validates: Requirements 7.4**

- [x] 9. Implement the victory system
  - [x] 9.1 Implement `VictorySystem.Evaluate` and disconnect resolution
    - End the match when a base reaches zero; determine winner/loser for single zero-health base and draw when both reach zero in the same update
    - Implement `OnDisconnectTimeout`: remaining connected player wins, both disconnected ends with no winner
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 9.1, 9.2, 9.5_

  - [ ]* 9.2 Write property test for a zero-health base ending the match
    - **Property 30: A base at zero health ends the match**
    - **Validates: Requirements 8.1**

  - [ ]* 9.3 Write property test for outcome determination by base health
    - **Property 31: Match outcome is determined by base health**
    - **Validates: Requirements 8.2, 8.3, 8.4**

  - [ ]* 9.4 Write property test for disconnection-timeout resolution
    - **Property 33: Disconnection timeout resolves the match**
    - **Validates: Requirements 9.1, 9.2, 9.5**

- [x] 10. Integrate the fixed-tick simulation pipeline
  - [x] 10.1 Implement `MatchServer.Tick` ordered pipeline
    - Drive Economy → Spawn → Movement → Combat → Victory in order each fixed tick at 20 Hz; mark changed canonical state for replication
    - Ensure each step is total over its valid domain (clamp/hold/no-op rather than throw) so one bad entity cannot abort a tick
    - _Requirements: 2.1, 6.1_

  - [ ]* 10.2 Write unit test for pipeline ordering and tick totality
    - Assert systems run in the specified order and a single entity in an unexpected state does not abort the tick
    - _Requirements: 2.1_

- [x] 11. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 12. Implement the lobby and pairing system
  - [x] 12.1 Implement `LobbyManager` pairing, timeout, and disconnect handling
    - `RequestJoin` places unassigned players into a ≤2-player lobby and rejects already-assigned players (preserving prior assignment)
    - `TryStartMatch` starts a match at exactly two players assigning unique ids + starting bases; `TickWaiting` enforces the 120s timeout/no-opponent signal and retains the player before it; `RemovePlayer` removes only the departing player on disconnect
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7_

  - [ ]* 12.2 Write property test for lobby capacity
    - **Property 1: Lobby never exceeds two players**
    - **Validates: Requirements 1.1**

  - [ ]* 12.3 Write property test for rejecting assigned players
    - **Property 2: Assigned players cannot re-join**
    - **Validates: Requirements 1.2**

  - [ ]* 12.4 Write property test for a full lobby starting a match
    - **Property 3: A full lobby starts a match**
    - **Validates: Requirements 1.3**

  - [ ]* 12.5 Write property test for lobby wait timeout
    - **Property 4: Lobby wait timeout removes the waiting player**
    - **Validates: Requirements 1.4, 1.5**

  - [ ]* 12.6 Write property test for lobby disconnect removing only the departing player
    - **Property 5: Lobby disconnect removes only the departing player**
    - **Validates: Requirements 1.6**

  - [ ]* 12.7 Write property test for match-start unique ids and bases
    - **Property 6: Match start assigns unique ids and bases**
    - **Validates: Requirements 1.7**

  - [ ]* 12.8 Write unit test for lobby wait boundary timing
    - Test lobby wait just below and just above 120 seconds
    - _Requirements: 1.4, 1.5_

- [x] 13. Implement networking, replication, and reconnection
  - [x] 13.1 Implement NetworkVariable-based canonical state replication
    - Expose unit positions, structures, resource totals, base health, and match phase/outcome as NetworkVariables/networked objects so reconnecting/late clients converge to current state
    - Push updated resource totals and state changes on change
    - _Requirements: 2.5, 2.6, 3.5, 4.6, 8.7_

  - [x] 13.2 Implement client→server placement RPC and targeted rejection/event RPCs
    - Receive placement intent via ServerRpc carrying `{structureType, targetLocation, placingPlayerId}` and route to `SubmitAction`
    - Send targeted ClientRpc rejections to the submitting client; broadcast unit-removal and match-result ClientRpcs only to connected clients
    - _Requirements: 4.1, 2.4, 7.5, 7.6, 8.5_

  - [x] 13.3 Implement disconnection pause/resume and match-result delivery
    - On disconnect enter `Paused` with a 30s reconnection timer; resume from preserved state on timely reconnect; resolve via `VictorySystem.OnDisconnectTimeout` on expiry
    - Retain and resend match result to a client that reconnects within the match session
    - _Requirements: 9.3, 9.4, 8.7_

  - [ ]* 13.4 Write property test for reconnecting clients converging to canonical state
    - **Property 9: Reconnecting clients converge to canonical state**
    - **Validates: Requirements 2.6, 8.7**

  - [ ]* 13.5 Write property test for removal notifications targeting only connected clients
    - **Property 29: Removal notifications target only connected clients**
    - **Validates: Requirements 7.6**

  - [ ]* 13.6 Write property test for pause-then-reconnect restoring the match
    - **Property 34: Pause then timely reconnect restores the match**
    - **Validates: Requirements 9.4**

  - [ ]* 13.7 Write integration/timing tests for replication and latency criteria
    - Validate action validation ≤100 ms (2.2), state replication ≤200 ms (2.5, 3.5), structure creation replicated ≤1 s (4.6), removal dispatch within one tick (7.5), match-result ≤2 s (8.5) and disconnect result ≤5 s (9.3), and single-source-of-truth derivation (2.1)
    - _Requirements: 2.1, 2.2, 2.5, 3.5, 4.6, 7.5, 8.5, 9.3_

- [x] 14. Implement the client presentation and placeholder rendering layer
  - [x] 14.1 Implement `GameClient` state mirroring and placement submission
    - Implement `SubmitPlacement` (build the ServerRpc payload), `OnStateReplicated`, `OnActionRejected`, `OnMatchResult`
    - Render units at reported positions and update on replication
    - _Requirements: 4.1, 6.5, 2.4, 8.5, 9.3_

  - [x] 14.2 Implement placeholder sprite/color resolver and numeric displays with fallback
    - Assign unique per-owner unit colors and unique per-type structure sprites; render resource total and base health as integers within their ranges; return a default sprite when none is assigned without interrupting other rendering
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_

  - [ ]* 14.3 Write property test for unique unit render color per owner
    - **Property 35: Unit render color is unique per owner**
    - **Validates: Requirements 10.1**

  - [ ]* 14.4 Write property test for unique structure sprite per type
    - **Property 36: Structure render sprite is unique per type**
    - **Validates: Requirements 10.2**

  - [ ]* 14.5 Write property test for rendered numeric values equal to canonical integers
    - **Property 37: Rendered numeric values equal the canonical integer value**
    - **Validates: Requirements 10.3, 10.4**

  - [ ]* 14.6 Write property test for missing-sprite fallback without interruption
    - **Property 38: Missing sprites fall back without interrupting rendering**
    - **Validates: Requirements 10.5**

  - [ ]* 14.7 Write unit test for placement message construction and render latency
    - Assert the placement message carries type, location, and placing player (4.1); assert the client renders a reported unit position within 100 ms (6.5); assert rendered value latency within 500 ms (10.3, 10.4)
    - _Requirements: 4.1, 6.5, 10.3, 10.4_

- [x] 15. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP. Core implementation tasks are never optional.
- Each task references specific requirements (granular sub-requirements) for traceability.
- Property tests cover Properties 1–38 from the design; each property is its own sub-task placed close to the system it validates. Latency/single-source-of-truth criteria use integration/timing tests instead of properties.
- Checkpoints ensure incremental validation at natural boundaries (after the core action pipeline, after the full simulation pipeline, and at the end).
- The pure simulation core is built and verified before networking so logic can be property-tested against in-memory state and mocked transport.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["1.3", "2.1", "6.1", "7.1", "8.1", "9.1", "12.1"] },
    { "id": 3, "tasks": ["2.2", "2.3", "2.4", "3.1", "6.2", "6.3", "6.4", "6.5", "6.6", "7.2", "7.3", "7.4", "7.5", "7.6", "8.2", "8.3", "8.4", "8.5", "9.2", "9.3", "9.4", "12.2", "12.3", "12.4", "12.5", "12.6", "12.7", "12.8"] },
    { "id": 4, "tasks": ["3.2", "3.3", "3.4", "3.5", "4.1"] },
    { "id": 5, "tasks": ["4.2", "4.3", "4.4", "10.1"] },
    { "id": 6, "tasks": ["10.2", "13.1", "13.2", "13.3"] },
    { "id": 7, "tasks": ["13.4", "13.5", "13.6", "13.7", "14.1"] },
    { "id": 8, "tasks": ["14.2"] },
    { "id": 9, "tasks": ["14.3", "14.4", "14.5", "14.6", "14.7"] }
  ]
}
```
