# Requirements Document

## Introduction

This feature defines a 2D top-down real-time strategy (RTS) game built in Unity, supporting 1v1 online multiplayer. Unlike traditional RTS games, the player does not directly command units. Instead, units spawn and travel automatically along predefined paths. The player's strategic role is to construct and place structures (for example, unit spawners, defensive buildings, and resource or economy buildings) that influence the flow of the match. The first iteration targets a single 1v1 match type with basic placeholder graphics, since final art will be produced separately by the user.

This document captures the functional and quality requirements for the first playable version. Rendering fidelity is intentionally minimal; the focus is on correct game logic, deterministic pathing behavior, structure placement, and networked match flow.

## Glossary

- **Game_Client**: The Unity application instance run by a single player, responsible for rendering, input handling, and local simulation presentation.
- **Match_Server**: The authoritative networked component (dedicated server or host client) that owns the canonical game state for a match and resolves all game logic.
- **Match**: A single competitive session between exactly two players (1v1) from start to victory or defeat.
- **Player**: A human participant in a Match, controlling one side and identified by a unique player identifier.
- **Unit**: An automatically controlled game entity that spawns from a structure and travels along a Path without direct player command.
- **Path**: A predefined sequence of waypoints along which a Unit travels from its spawn point toward a target.
- **Structure**: A player-placed building occupying a location on the Map. Includes Spawner_Structure, Defensive_Structure, and Economy_Structure types. These expand the Build_Area.
- **Spawner_Structure**: A Structure that periodically produces Units.
- **Defensive_Structure**: A Structure that attacks enemy Units within range.
- **Economy_Structure**: A Structure that generates Resource over time.
- **Resource**: The in-match currency a Player spends to place Structures.
- **Map**: The 2D top-down playfield containing buildable areas, Paths, and each Player's base.
- **Base**: The location a Player must defend; its destruction determines Match outcome.
- **Build_Area**: A region of the Map where a Player is permitted to place Structures. This is dynamic.
- **Lobby**: The pre-match state where two Players are matched together before a Match begins.

## Requirements

### Requirement 1: Match Lobby and Pairing

**User Story:** As a player, I want to be matched with one opponent before a game starts, so that I can play a 1v1 match.

#### Acceptance Criteria

1. WHEN a Player who is not already assigned to a Lobby or Match requests to join a Match, THE Match_Server SHALL place the Player into a Lobby that contains at most two Players.
2. IF a Player who is already assigned to a Lobby or Match requests to join a Match, THEN THE Match_Server SHALL reject the request and preserve that Player's existing assignment.
3. WHEN exactly two Players are present in a Lobby, THE Match_Server SHALL start a Match for those two Players within 2 seconds.
4. WHILE a Lobby contains fewer than two Players and fewer than 120 seconds have elapsed since the waiting Player joined, THE Match_Server SHALL keep the waiting Player in the Lobby.
5. IF 120 seconds elapse while a Lobby still contains fewer than two Players, THEN THE Match_Server SHALL remove the waiting Player from the Lobby and signal that no opponent was found.
6. IF a Player disconnects from a Lobby before a Match starts, THEN THE Match_Server SHALL remove that Player from the Lobby within 5 seconds and retain any remaining waiting Player in the Lobby.
7. WHEN a Match starts, THE Match_Server SHALL assign each Player a player identifier that is unique within the Match and assign each Player a starting Base.

### Requirement 2: Authoritative Match State

**User Story:** As a player, I want the game outcome to be consistent for both participants, so that the match is fair.

#### Acceptance Criteria

1. THE Match_Server SHALL maintain the canonical state of all Units, Structures, Resource totals, and Base health for a Match as the single source of truth used to resolve the Match outcome.
2. WHEN a Game_Client submits a player action, THE Match_Server SHALL validate the action against the canonical state within 100 milliseconds of receipt before applying it.
3. WHEN a submitted player action passes validation, THE Match_Server SHALL apply the action to the canonical state exactly once.
4. IF a submitted player action is invalid against the canonical state, THEN THE Match_Server SHALL reject the action, leave the canonical state unchanged, and send the submitting Game_Client a response indicating the action was rejected.
5. WHEN the canonical state changes, THE Match_Server SHALL send the updated state to each connected Game_Client within 200 milliseconds of the change.
6. IF a Game_Client is not reachable when the Match_Server attempts to send updated state, THEN THE Match_Server SHALL retain the canonical state unchanged and resend the current canonical state to that Game_Client upon its next successful reconnection.

### Requirement 3: Resource Economy

**User Story:** As a player, I want to earn and spend resources, so that I can decide which structures to build.

#### Acceptance Criteria

1. WHEN a Match starts, THE Match_Server SHALL set each Player's Resource total to the defined starting amount of 100 Resources.
2. WHILE an Economy_Structure owned by a Player is active, THE Match_Server SHALL increase that Player's Resource total by that structure's defined generation rate once per generation interval of 1 second, up to a maximum Resource total of 999,999.
3. WHEN a Player places a Structure and that Player's current Resource total is greater than or equal to that Structure's defined cost, THE Match_Server SHALL decrease that Player's Resource total by that Structure's defined cost.
4. IF a Player attempts to place a Structure whose cost exceeds that Player's current Resource total, THEN THE Match_Server SHALL reject the placement, leave that Player's Resource total unchanged, and return an indication that the placement was rejected due to insufficient Resources.
5. WHEN the Match_Server sets or updates a Player's Resource total, THE Match_Server SHALL send the updated Resource total to that Player's Game_Client within 200 milliseconds.

### Requirement 4: Structure Placement

**User Story:** As a player, I want to place structures on the map, so that I can shape how the match unfolds.

#### Acceptance Criteria

1. WHEN a Player selects a Structure type and a target location within the Map boundaries, THE Game_Client SHALL submit a placement action identifying the Structure type, target location, and placing Player to the Match_Server.
2. IF the target location is outside the placing Player's Build_Area, THEN THE Match_Server SHALL reject the placement, leave the Match state unchanged, and send a rejection response to the placing Player's Game_Client.
3. IF the target Structure's footprint intersects the footprint of an existing Structure, THEN THE Match_Server SHALL reject the placement, leave the Match state unchanged, and send a rejection response to the placing Player's Game_Client.
4. IF the placing Player's current Resource total is less than the selected Structure's defined cost, THEN THE Match_Server SHALL reject the placement, leave the Match state unchanged, and send a rejection response to the placing Player's Game_Client.
5. WHEN a placement action targets a location inside the placing Player's Build_Area, has no footprint overlap with an existing Structure, and the placing Player's Resource total is greater than or equal to the Structure's defined cost, THE Match_Server SHALL create the Structure at the target location, assign ownership to the placing Player, and decrease that Player's Resource total by the Structure's defined cost.
6. WHEN a Structure is created, THE Match_Server SHALL send the new Structure, including its location and owning Player, to each connected Game_Client within 1 second.

### Requirement 5: Automatic Unit Spawning

**User Story:** As a player, I want spawner structures to produce units automatically, so that I can build an offense without controlling units directly.

#### Acceptance Criteria

1. WHILE a Spawner_Structure owned by a Player is active, THE Match_Server SHALL create one Unit at that structure's spawn point at the start of each spawn interval defined for that structure, where the spawn interval is a value between 1 and 600 seconds.
2. WHEN a Unit is created, THE Match_Server SHALL assign the Unit the owning Player's identifier before the Unit becomes active in the Match.
3. WHEN a Unit is created, THE Match_Server SHALL assign the Unit a Path leading toward the opponent's Base before the Unit becomes active in the Match.
4. IF a Spawner_Structure becomes inactive, THEN THE Match_Server SHALL stop creating Units at that structure's spawn point and SHALL not create any Unit for any spawn interval that elapses while the structure is inactive.
5. IF the spawn point is occupied such that a new Unit cannot be placed at the start of a spawn interval, THEN THE Match_Server SHALL retain the pending Unit creation and SHALL create the Unit at the spawn point once the spawn point becomes unoccupied.
6. IF no Path toward the opponent's Base can be assigned to a created Unit, THEN THE Match_Server SHALL hold the Unit at the spawn point without movement until a Path becomes available and SHALL retain the Unit's owning Player identifier.

### Requirement 6: Automatic Unit Movement Along Paths

**User Story:** As a player, I want units to travel on their own along set paths, so that combat flows without me issuing move commands.

#### Acceptance Criteria

1. THE Match_Server SHALL move each Unit along its assigned Path toward the next waypoint at that Unit's defined movement speed, updating the Unit's position at least 20 times per second.
2. WHEN a Unit is within 0.1 map units of its current target waypoint, THE Match_Server SHALL treat the waypoint as reached and advance the Unit toward the next waypoint on its Path.
3. WHEN a Unit reaches the final waypoint of its Path at the opponent's Base, THE Match_Server SHALL apply that Unit's defined damage to the opponent's Base and remove the Unit from the Match.
4. IF a Unit has no further waypoints on its Path and is not located at the opponent's Base, THEN THE Match_Server SHALL stop moving the Unit and retain it at its current position without applying damage to any Base.
5. WHEN the Match_Server updates a Unit's position, THE Game_Client SHALL render that Unit at the reported position within 100 milliseconds.

### Requirement 7: Combat Resolution

**User Story:** As a player, I want defensive structures to engage enemy units, so that my placements can stop an incoming attack.

#### Acceptance Criteria

1. WHILE one or more enemy Units are within a Defensive_Structure's defined range, THE Match_Server SHALL select the single enemy Unit that has progressed furthest along its Path as the structure's target.
2. WHILE a target enemy Unit remains within a Defensive_Structure's defined range, THE Match_Server SHALL apply that structure's defined damage to the target Unit once per that structure's defined attack interval, measured from the previous application to that structure.
3. IF a Defensive_Structure's defined range contains no enemy Unit at the time an attack interval elapses, THEN THE Match_Server SHALL apply no damage and SHALL retain the structure's accumulated attack interval timing.
4. WHEN a Unit's health is reduced to zero or below by applied damage, THE Match_Server SHALL remove the Unit from the Match within one server tick of the health reaching zero.
5. WHEN a Unit is removed from the Match, THE Match_Server SHALL send a removal notification identifying that Unit to each connected Game_Client within one server tick of the removal.
6. IF a Game_Client is not connected at the time a Unit is removed, THEN THE Match_Server SHALL exclude that Game_Client from the removal notification and SHALL complete the removal without waiting for that Game_Client.

### Requirement 8: Victory and Defeat

**User Story:** As a player, I want the match to end when a base is destroyed, so that there is a clear winner.

#### Acceptance Criteria

1. WHEN a Player's Base health reaches zero, THE Match_Server SHALL end the Match within 1 second.
2. WHEN a Match ends because exactly one Player's Base reached zero health, THE Match_Server SHALL declare the Player whose Base health remains above zero as the winner.
3. WHEN a Match ends because exactly one Player's Base reached zero health, THE Match_Server SHALL declare the Player whose Base reached zero health as the loser.
4. IF both Players' Bases reach zero health within the same simulation update, THEN THE Match_Server SHALL end the Match and declare the result a draw with no winner and no loser.
5. WHEN a Match ends, THE Match_Server SHALL send the Match result, including winner, loser, or draw status, to each connected Game_Client within 2 seconds.
6. WHILE a Match is in the ended state, THE Match_Server SHALL reject all further gameplay actions from any Game_Client and SHALL keep the Match result unchanged.
7. IF a Game_Client is disconnected when the Match result is sent, THEN THE Match_Server SHALL retain the Match result and SHALL send it to that Game_Client upon its reconnection within the same Match session.

### Requirement 9: Player Disconnection During a Match

**User Story:** As a player, I want a clear outcome if my opponent leaves, so that an abandoned match resolves.

#### Acceptance Criteria

1. IF a Player loses connection during an active Match and does not reconnect within 30 seconds, THEN THE Match_Server SHALL end the Match.
2. WHEN a Match ends due to disconnection, THE Match_Server SHALL declare the remaining connected Player as the winner.
3. WHEN a Match ends due to disconnection, THE Match_Server SHALL send the Match result to the remaining connected Game_Client within 5 seconds of ending the Match.
4. WHILE a Player is within the 30-second reconnection window, THE Match_Server SHALL pause the Match and SHALL resume the Match if the Player reconnects before the window expires.
5. IF both Players are disconnected when the reconnection window expires, THEN THE Match_Server SHALL end the Match with no declared winner.

### Requirement 10: Placeholder Rendering

**User Story:** As a developer, I want the game to use placeholder visuals, so that gameplay can be built and tested before final art exists.

#### Acceptance Criteria

1. WHEN a Unit is present on the Map, THE Game_Client SHALL render that Unit using a placeholder sprite whose visual color is uniquely assigned per owning Player, such that two Units owned by different Players are rendered in distinct colors.
2. WHEN a Structure is present on the Map, THE Game_Client SHALL render that Structure using a placeholder sprite whose shape or icon is uniquely assigned per Structure type (Spawner_Structure, Defensive_Structure, Economy_Structure), such that two Structures of different types are visually distinguishable.
3. WHEN a Player's Resource total changes, THE Game_Client SHALL update the rendered Resource total for that Player within 500 milliseconds to match the current value, displayed as an integer in the range 0 to 999,999,999.
4. WHEN a Base's current health changes, THE Game_Client SHALL update the rendered health value for that Base within 500 milliseconds to match the current value, displayed as an integer in the range 0 to the Base's maximum health.
5. IF a Unit or Structure has no placeholder sprite assigned for its owning Player or type, THEN THE Game_Client SHALL render a default fallback placeholder sprite and continue rendering all other game objects without interruption.
