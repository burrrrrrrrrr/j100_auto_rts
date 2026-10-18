using MultiplayerRts.Client;
using MultiplayerRts.Core;
using MultiplayerRts.Net;
using Unity.Netcode;
using UnityEngine;

namespace MultiplayerRts.Bootstrap
{
    /// <summary>
    /// Server-authoritative demo bootstrap. On Host start it builds the Core simulation,
    /// seeds a 2-player Active match, auto-places one spawner per player (so units appear and
    /// march without manual placement), ticks the sim at 20 Hz, and drives a zero-setup
    /// presentation layer (primitive ShapeRenderer + runtime MatchHud) from the server state.
    ///
    /// This is demo glue, not spec behavior: it fabricates a match and visualizes it in a
    /// single process so you can press Play and watch it run.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public sealed class MatchBootstrap : MonoBehaviour
    {
        [Header("Net behaviours (assign in inspector)")]
        [SerializeField] private MatchReplicationBehaviour _replication;
        [SerializeField] private MatchRpcBehaviour _rpc;
        [SerializeField] private MatchReconnectionBehaviour _reconnection;

        [Header("Demo players")]
        [SerializeField] private uint _playerAId = 1;
        [SerializeField] private uint _playerBId = 2;

        private NetworkManager _netManager;

        private MatchState _state;
        private MatchServer _server;
        private IVictorySystem _victory;
        private IStructureIdAllocator _structureIds;

        // Systems driven directly by the demo tick (so we use MELEE movement/combat for units
        // instead of the spec's path-based MovementSystem). _server is kept for SubmitAction.
        private IResourceSystem _resources;
        private ISpawnSystem _spawn;
        private ICombatSystem _towerCombat;           // existing tower-vs-unit ranged combat
        private MeleeMovementSystem _meleeMovement;    // new: units seek nearest enemy, no overlap
        private MeleeCombatSystem _meleeCombat;        // new: unit-vs-unit melee damage
        private StructureHealth _structureHp;          // new: buildings are engageable (100 HP)
        private WallRegistry _walls;                   // new: team-owned attackable wall segments
        private NavGrid _nav;                           // new: A* grid for pathing around walls
        private UnitKinds _unitKinds;                  // new: melee vs ranged classification
        private readonly System.Collections.Generic.HashSet<StructureId> _rangedSpawners =
            new System.Collections.Generic.HashSet<StructureId>();

        // Presentation (single-process demo: fed from the server state each tick).
        private GameClientStateMirror _mirror;
        private ShapeRenderer _renderer;
        private MatchHud _hud;
        private PlacementUI _placementUI;

        // The player this local client places structures as (left/blue in the demo).
        private PlayerId _localPlayer;
        private uint _nextActionId;

        private float _accumulator;
        private bool _running;

        private void Awake()
        {
            _netManager = GetComponent<NetworkManager>();
        }

        private void OnEnable()  { _netManager.OnServerStarted += HandleServerStarted; }
        private void OnDisable()
        {
            if (_netManager != null) { _netManager.OnServerStarted -= HandleServerStarted; }
            // Despawn the networked Match object BEFORE Unity destroys its behaviours, so NGO's
            // OnNetworkBehaviourDestroyed does not run while they are still IsSpawned (which, in
            // NGO 2.13.x, dereferences a now-null NetworkManagerOwner and throws). Despawning
            // first clears IsSpawned so that teardown path is skipped.
            CleanupNetworkObject();
        }

        private void CleanupNetworkObject()
        {
            if (_replication == null) { return; }
            var no = _replication.GetComponent<Unity.Netcode.NetworkObject>();
            if (no != null && no.IsSpawned && _netManager != null && _netManager.IsServer)
            {
                try { no.Despawn(false); } catch { /* best-effort teardown */ }
            }
        }

        private void HandleServerStarted()
        {
            if (!_netManager.IsServer) { return; }
            BuildMatch();
            SetupPresentation();
            _running = true;
        }

        private void BuildMatch()
        {
            var playerA = new PlayerId(_playerAId);
            var playerB = new PlayerId(_playerBId);

            var baseProvider = new DemoStartingBaseProvider();
            baseProvider.AssignBases(playerA, playerB, out StartingBase ba, out StartingBase bb);

            _localPlayer = playerA; // this client places as the left/blue player

            _state = new MatchState(MatchPhase.Active);
            _state.Players[playerA] = NewPlayer(ba);
            _state.Players[playerB] = NewPlayer(bb);

            var resources = new ResourceSystem(_state);
            var movement = new MovementSystem(_state);
            var combat = new CombatSystem(_state);
            var spawn = new SpawnSystem(_state, new SequentialUnitIdAllocator(), new DemoPathProvider());
            _victory = new VictorySystem(_state);

            var bounds = new RectangularMapBounds(
                new Footprint(new MapVector(-60f, -38f), new MapVector(120f, 76f)));
            _structureIds = new SequentialStructureIdAllocator();
            var placement = new PlacementSystem(
                _state, resources, new DemoStructureCatalog(), bounds, _structureIds);

            resources.StartMatch(playerA, playerB);

            _server = new MatchServer(_state, placement, resources, spawn, movement, combat, _victory);

            // Keep references for the demo's custom tick (melee instead of path movement).
            _resources = resources;
            _spawn = spawn;
            _towerCombat = combat;
            _structureHp = new StructureHealth(100f); // every building has 100 HP
            _unitKinds = new UnitKinds();
            _meleeMovement = new MeleeMovementSystem(_state, _structureHp, _unitKinds);
            _meleeCombat = new MeleeCombatSystem(_state, _structureHp, _unitKinds);

            _walls = new WallRegistry();
            _meleeMovement.SetWalls(_walls);

            // Navigation grid over the map bounds (1-unit cells), for A* around walls.
            _nav = new NavGrid(new MapVector(-60f, -38f), new MapVector(60f, 38f), 1f);
            _meleeMovement.SetNavGrid(_nav);

            // Auto-place one active spawner per player near their base so units flow immediately.
            PlaceSpawner(playerA, new MapVector(ba.Location.X + 8f, ba.Location.Y));
            PlaceSpawner(playerB, new MapVector(bb.Location.X - 8f, bb.Location.Y));

            // A defensive tower per side, in front of the spawner (further toward midfield),
            // offset vertically: blue slightly up (+y), red slightly down (-y).
            PlaceTower(playerA, new MapVector(ba.Location.X + 20f, ba.Location.Y + 5f));
            PlaceTower(playerB, new MapVector(bb.Location.X - 20f, bb.Location.Y - 5f));

            // Corner (L-shaped) wall per team, on the enemy-facing side of each tower,
            // shielding it. Blue tower at base+20,+5 faces right (+x); its L wraps the right
            // and top. Red mirrors it (faces left, wraps left and bottom).
            BuildCornerWall(playerA, new MapVector(ba.Location.X + 20f, ba.Location.Y + 5f), +1f);
            BuildCornerWall(playerB, new MapVector(bb.Location.X - 20f, bb.Location.Y - 5f), -1f);
            RebuildNav();

            // A ranged-unit spawner per side: below the blue spawner, above the red one, and
            // slightly closer to each base. (Blue spawner at base+8; this sits at base+5, -y.)
            PlaceRangedSpawner(playerA, new MapVector(ba.Location.X + 5f, ba.Location.Y - 6f));
            PlaceRangedSpawner(playerB, new MapVector(bb.Location.X - 5f, bb.Location.Y + 6f));

            if (_replication != null) { _replication.BindState(_state); }
            if (_rpc != null)
            {
                _rpc.BindServer(_server);
                _rpc.BindClient(_netManager.LocalClientId, playerA);
            }
            // NOTE: reconnection is left unbound in the demo (needs the shared ClientIdPlayerMap).

            Debug.Log("[MatchBootstrap] Match built and running (2 players, phase=Active, spawners placed).");
        }

        /// <summary>Directly inserts an active spawner into canonical state (demo seed, bypasses placement cost).</summary>
        private void PlaceSpawner(PlayerId owner, MapVector spawnPoint)
        {
            var s = new Structure
            {
                Id = _structureIds.Next(),
                Owner = owner,
                Type = StructureType.Spawner,
                Footprint = new Footprint(new MapVector(spawnPoint.X - 0.5f, spawnPoint.Y - 0.5f), new MapVector(1f, 1f)),
                IsActive = true,
                Cost = 0,
                SpawnIntervalSeconds = 2f,  // one unit every 2s
                SpawnPoint = spawnPoint,
                SpawnTimer = 0f,
            };
            _state.Structures[s.Id] = s;
        }

        /// <summary>Directly inserts an active defensive tower into canonical state (demo seed).</summary>
        private void PlaceTower(PlayerId owner, MapVector at)
        {
            var s = new Structure
            {
                Id = _structureIds.Next(),
                Owner = owner,
                Type = StructureType.Defensive,
                Footprint = new Footprint(new MapVector(at.X - 0.5f, at.Y - 0.5f), new MapVector(1f, 1f)),
                IsActive = true,
                Cost = 0,
                Range = 8f,          // attack range in map units
                AttackInterval = 1f, // one shot per second
                AttackDamage = 15f,  // damage per shot
                AttackTimer = 0f,
            };
            _state.Structures[s.Id] = s;
        }

        /// <summary>Inserts an active spawner that produces RANGED units (tagged in _rangedSpawners).</summary>
        private void PlaceRangedSpawner(PlayerId owner, MapVector spawnPoint)
        {
            var s = new Structure
            {
                Id = _structureIds.Next(),
                Owner = owner,
                Type = StructureType.Spawner,
                Footprint = new Footprint(new MapVector(spawnPoint.X - 0.5f, spawnPoint.Y - 0.5f), new MapVector(1f, 1f)),
                IsActive = true,
                Cost = 0,
                SpawnIntervalSeconds = 2.5f, // ranged units spawn a bit slower
                SpawnPoint = spawnPoint,
                SpawnTimer = 0f,
            };
            _state.Structures[s.Id] = s;
            _rangedSpawners.Add(s.Id); // mark so its units are tagged ranged
        }

        /// <summary>Inserts one attackable, team-owned, solid wall segment (1x1) at a cell center.</summary>
        private void PlaceWall(PlayerId owner, MapVector cellCenter)
        {
            var s = new Structure
            {
                Id = _structureIds.Next(),
                Owner = owner,
                Type = StructureType.Economy, // wall kind: no spawn, no shoot; tagged below
                Footprint = new Footprint(new MapVector(cellCenter.X - 0.5f, cellCenter.Y - 0.5f), new MapVector(1f, 1f)),
                IsActive = true,
                Cost = 0,
            };
            _state.Structures[s.Id] = s;
            _walls.Mark(s.Id); // solid blocker + renders as a team block; HP via StructureHealth (100)
        }

        /// <summary>
        /// Builds an L-shaped corner wall shielding a tower. <paramref name="dir"/> is +1 for
        /// the team facing +x (blue) or -1 for -x (red); the L wraps the enemy-facing column
        /// and the near horizontal arm. Cells are spaced 1 unit (footprints just touch).
        /// </summary>
        private void BuildCornerWall(PlayerId owner, MapVector tower, float dir)
        {
            float x = tower.X + dir * 2f; // the vertical arm sits 2 units toward the enemy
            // Vertical arm: 5 cells spanning the tower's height band.
            for (int i = -2; i <= 2; i++)
            {
                PlaceWall(owner, new MapVector(x, tower.Y + i * 1f));
            }
            // Horizontal arm turns back toward the tower. Offsetting by -dir makes the two
            // teams' Ls point-symmetric (a 180-degree rotation of each other): blue (dir=+1)
            // wraps the bottom, red (dir=-1) wraps the top.
            float armY = tower.Y - dir * 2f;
            PlaceWall(owner, new MapVector(x - dir * 1f, armY));
            PlaceWall(owner, new MapVector(x - dir * 2f, armY));
        }

        /// <summary>Rebuilds the A* navigation grid from the current living wall segments.</summary>
        private void RebuildNav()
        {
            if (_nav != null) { _nav.Rebuild(_state, _walls, _structureHp, 0.1f); }
        }

        private PlayerState NewPlayer(StartingBase b)
        {
            return new PlayerState
            {
                Id = b.Owner,
                ResourceTotal = SimulationConstants.StartingResources,
                BaseHealth = b.MaxHealth,
                BaseMaxHealth = b.MaxHealth,
                BaseLocation = b.Location,
                BuildArea = DemoStartingBaseProvider.BuildAreaFor(b.Location),
                Connection = ConnectionState.Connected,
            };
        }

        private void SetupPresentation()
        {
            // Top-down camera over the play area.
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("DemoCamera");
                cam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }
            cam.transform.position = new Vector3(0f, 60f, -6f);
            cam.transform.rotation = Quaternion.Euler(80f, 0f, 0f);
            cam.orthographic = true;
            cam.orthographicSize = 32f; // half-height of the view; zoomed in more
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.11f);

            _renderer = gameObject.AddComponent<ShapeRenderer>();
            _hud = gameObject.AddComponent<MatchHud>();

            // The mirror turns a snapshot into the render view; a no-op sender since this
            // demo visualizes the server directly and does not submit placements over the net.
            _mirror = new GameClientStateMirror(new NullPlacementSender(), _renderer);
            _hud.Bind(_mirror);

            _placementUI = gameObject.AddComponent<PlacementUI>();
            _placementUI.Bind(this, cam);

            // Flash a spark at each melee strike, and a tracer at each tower shot.
            if (_meleeCombat != null && _renderer != null)
            {
                _meleeCombat.OnStrike += pos => _renderer.Spark(pos);
            }
            if (_towerCombat is CombatSystem cs && _renderer != null)
            {
                cs.OnFire += (from, to) => _renderer.Tracer(from, to);
            }

            // Ranged unit visuals + shot tracer.
            if (_renderer != null)
            {
                _renderer.IsRangedSpawner = id => _rangedSpawners.Contains(id);
                _renderer.IsRangedUnit = id => _unitKinds != null && _unitKinds.IsRanged(id);
                _renderer.IsWall = id => _walls != null && _walls.IsWall(id);
            }
            if (_meleeCombat != null && _renderer != null)
            {
                _meleeCombat.OnRangedShot += (from, to) => _renderer.Tracer(from, to);
            }
            if (_meleeCombat != null)
            {
                _meleeCombat.OnStructureDestroyed += id => { if (_walls != null) { _walls.Remove(id); RebuildNav(); } };
            }
        }

        /// <summary>
        /// Places a structure of <paramref name="type"/> at the clicked world point for the
        /// local player, routed through the authoritative placement pipeline
        /// (PlacementSystem validation + cost + build-area). Returns the typed result so the UI
        /// can show a rejection reason. World point (x,z) maps to canonical (x,y).
        /// </summary>
        public ActionResult TryPlace(StructureType type, Vector3 worldPoint)
        {
            if (_server == null) { return ActionResult.Rejected(RejectionReason.MatchEnded); }

            var target = new MapVector(worldPoint.x, worldPoint.z);
            var action = new PlacementAction(_nextActionId++, _localPlayer, type, target);
            ActionResult result = _server.SubmitAction(action);

            // PlacementSystem is structure-type-agnostic: it sets owner/footprint/cost but NOT
            // spawner-specific fields, so a placed spawner's SpawnPoint would default to (0,0)
            // (map center). Configure the newly created spawner here so units emerge AT the
            // clicked location instead of the center.
            if (result != null && result.Success && type == StructureType.Spawner)
            {
                ConfigureNewestSpawner(target);
            }

            return result;
        }

        /// <summary>
        /// Places one attackable, team-owned wall segment at the clicked point for the local
        /// player, through the validated pipeline (Economy-typed under the hood), then tags it
        /// in the wall registry so it blocks movement, renders as a team block, and is
        /// destroyable (100 HP via StructureHealth).
        /// </summary>
        public ActionResult TryPlaceWall(Vector3 worldPoint)
        {
            if (_server == null) { return ActionResult.Rejected(RejectionReason.MatchEnded); }

            var target = new MapVector(worldPoint.x, worldPoint.z);
            var action = new PlacementAction(_nextActionId++, _localPlayer, StructureType.Economy, target);
            ActionResult result = _server.SubmitAction(action);

            if (result != null && result.Success)
            {
                // Tag the newly created Economy structure nearest the click as a wall segment.
                Structure best = null; float bestSqr = float.MaxValue;
                foreach (var kv in _state.Structures)
                {
                    var s = kv.Value;
                    if (s == null || s.Type != StructureType.Economy || s.Owner != _localPlayer) { continue; }
                    if (_walls.IsWall(s.Id)) { continue; } // skip already-tagged walls
                    float cx = s.Footprint.Origin.X + s.Footprint.Size.X * 0.5f;
                    float cy = s.Footprint.Origin.Y + s.Footprint.Size.Y * 0.5f;
                    float d = (cx - target.X) * (cx - target.X) + (cy - target.Y) * (cy - target.Y);
                    if (d < bestSqr) { bestSqr = d; best = s; }
                }
                if (best != null) { _walls.Mark(best.Id); RebuildNav(); }
            }

            return result;
        }

        /// <summary>
        /// Finds the just-placed spawner for the local player nearest <paramref name="at"/> and
        /// gives it a spawn point at that location plus a spawn interval, so it actually
        /// produces units from there.
        /// </summary>
        private Structure ConfigureNewestSpawner(MapVector at)
        {
            Structure best = null;
            float bestSqr = float.MaxValue;
            foreach (var kv in _state.Structures)
            {
                var s = kv.Value;
                if (s == null || s.Type != StructureType.Spawner || s.Owner != _localPlayer) { continue; }
                // Spawners whose SpawnPoint is still unset (0,0) and whose footprint sits at the
                // target are the freshly placed one; pick the closest footprint-center to 'at'.
                float cx = s.Footprint.Origin.X + s.Footprint.Size.X * 0.5f;
                float cy = s.Footprint.Origin.Y + s.Footprint.Size.Y * 0.5f;
                float d = (cx - at.X) * (cx - at.X) + (cy - at.Y) * (cy - at.Y);
                if (d < bestSqr) { bestSqr = d; best = s; }
            }
            if (best != null)
            {
                best.SpawnPoint = at;
                best.SpawnIntervalSeconds = 2f;
                best.SpawnTimer = 0f;
                best.IsActive = true;
            }
            return best;
        }

        /// <summary>
        /// Places a RANGED-unit spawner at the clicked point for the local player, through the
        /// same validated pipeline as the other placements, then tags it so its units are
        /// ranged and the renderer draws it as an oval cylinder.
        /// </summary>
        public ActionResult TryPlaceRangedSpawner(Vector3 worldPoint)
        {
            if (_server == null) { return ActionResult.Rejected(RejectionReason.MatchEnded); }

            var target = new MapVector(worldPoint.x, worldPoint.z);
            var action = new PlacementAction(_nextActionId++, _localPlayer, StructureType.Spawner, target);
            ActionResult result = _server.SubmitAction(action);

            if (result != null && result.Success)
            {
                Structure placed = ConfigureNewestSpawner(target);
                if (placed != null)
                {
                    placed.SpawnIntervalSeconds = 2.5f;   // ranged spawns a touch slower
                    _rangedSpawners.Add(placed.Id);       // mark as ranged spawner
                }
            }
            return result;
        }

        /// <summary>True when a newly spawned unit's position matches a ranged spawner's spawn point.</summary>
        private bool SpawnedAtRangedSpawner(MapVector pos)
        {
            foreach (var kv in _state.Structures)
            {
                var s = kv.Value;
                if (s == null || !_rangedSpawners.Contains(s.Id)) { continue; }
                if (MapVector.SqrDistance(pos, s.SpawnPoint) <= 0.25f) { return true; } // within 0.5
            }
            return false;
        }
        /// <summary>The local player's current resource total, for the UI to show affordability.</summary>
        public int LocalResources()
        {
            return _state != null && _state.Players.TryGetValue(_localPlayer, out PlayerState p) ? p.ResourceTotal : 0;
        }

        private void Update()
        {
            if (!_running || _server == null) { return; }

            float step = SimulationConstants.TickIntervalSeconds;
            _accumulator += Time.deltaTime;
            while (_accumulator >= step)
            {
                _accumulator -= step;

                if (_state.Phase == MatchPhase.Active)
                {
                    _state.Tick++;

                    // Stamp freshly spawned units with demo combat stats (Core spawns them at 0).
                    foreach (var kvp in _state.Units)
                    {
                        Unit u = kvp.Value;
                        if (u != null && u.MovementSpeed <= 0f)
                        {
                            // Is this unit from a ranged spawner? (spawned at its spawn point)
                            bool isRanged = SpawnedAtRangedSpawner(u.Position);
                            if (isRanged) { _unitKinds.MarkRanged(u.Id); }

                            u.MovementSpeed = isRanged ? 2.2f : 2.5f; // ranged a touch slower
                            u.Damage = isRanged ? 7f : 10f;           // ranged hits softer
                            u.Health = isRanged ? 35f : 50f;          // ranged more fragile

                            // Slight random offset so units don't emerge stacked on the exact
                            // spawn point - feels more organic and seeds the separation pass.
                            float ox = UnityEngine.Random.Range(-0.6f, 0.6f);
                            float oy = UnityEngine.Random.Range(-0.6f, 0.6f);
                            u.Position = new MapVector(u.Position.X + ox, u.Position.Y + oy);
                        }
                    }

                    // Custom demo pipeline: Economy -> Spawn -> MELEE movement -> MELEE combat
                    // -> tower combat -> Victory. We deliberately use the melee systems for
                    // units instead of the spec's path-based MovementSystem.
                    _resources.AccrueTick(step);
                    _spawn.Tick(step);
                    _meleeMovement.Tick(step);
                    _meleeCombat.Tick(step);
                    _towerCombat.Tick(step);
                    _victory.Evaluate();

                    _state.MarkChanged();
                }

                if (_replication != null) { _replication.ServerReplicationTick(); }

                // Drive the local presentation straight from the server state.
                MatchStateSnapshot snap = MatchStateProjector.Project(_state);
                _mirror.OnStateReplicated(snap);

                DiagnosticTick(step);
            }
        }

        private float _diagClock;
        private void DiagnosticTick(float step)
        {
            _diagClock += step;
            if (_diagClock < 1f) { return; }  // log once per second
            _diagClock = 0f;

            int spawners = 0, held = 0, moving = 0;
            float anyTimer = -1f; float anyInterval = -1f; bool anyActive = false; bool anyPath = false;
            foreach (var kv in _state.Structures)
            {
                var s = kv.Value;
                if (s != null && s.Type == StructureType.Spawner)
                {
                    spawners++;
                    anyTimer = s.SpawnTimer; anyInterval = s.SpawnIntervalSeconds; anyActive = s.IsActive;
                }
            }
            foreach (var kv in _state.Units)
            {
                var u = kv.Value;
                if (u == null) { continue; }
                if (u.IsHeld) { held++; }
                if (u.MovementSpeed > 0f && !u.IsHeld) { moving++; }
                if (u.AssignedPath != null) { anyPath = true; }
            }
            Debug.Log($"[Diag] spawners={spawners} active={anyActive} timer={anyTimer:F2}/{anyInterval:F2} " +
                      $"units={_state.Units.Count} held={held} moving={moving} anyUnitHasPath={anyPath}");
        }

        /// <summary>No-op placement sender for the single-process visualization.</summary>
        private sealed class NullPlacementSender : IPlacementSender
        {
            public void Send(PlacementIntent intent) { }
        }
    }
}










































