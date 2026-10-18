using System.Collections.Generic;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// Melee movement for units (demo combat model, added alongside the spec's path-based
    /// <see cref="MovementSystem"/>). Each tick a unit:
    ///   1. finds the CLOSEST living enemy unit and moves toward it, stopping at
    ///      <see cref="EngageRange"/> so it does not walk through the enemy;
    ///   2. if there is no enemy unit on the map, advances toward the enemy BASE instead
    ///      (and applies its damage + is removed on arrival, so the match still resolves);
    ///   3. is then separated from nearby friendly/enemy units so units cannot overlap and
    ///      naturally "queue" behind one another when crowding a target.
    ///
    /// This is pure C# (no Unity dependency) so it is testable, and it mutates unit
    /// <see cref="Unit.Position"/> in place. It does NOT read the unit's AssignedPath; it
    /// drives movement purely by seeking, which is the melee behavior requested for the demo.
    /// </summary>
    public sealed class MeleeMovementSystem
    {
        /// <summary>Distance at which a unit stops approaching its enemy target to melee it.</summary>
        public const float EngageRange = 0.9f;

        /// <summary>Minimum center-to-center spacing; closer units are pushed apart (no overlap).</summary>
        public const float UnitSpacing = 0.7f;

        /// <summary>How close to the enemy base counts as "arrived" (base damage + removal).</summary>
        public const float BaseArriveRange = 1.5f;

        private readonly MatchState _state;
        private readonly StructureHealth _structureHp;
        private readonly UnitKinds _kinds;
        private WallRegistry _walls;

        public MeleeMovementSystem(MatchState state, StructureHealth structureHp, UnitKinds kinds)
        {
            _state = state;
            _structureHp = structureHp;
            _kinds = kinds;
        }

        /// <summary>Approximate unit body radius, used to keep units clear of wall footprints.</summary>
        private const float UnitRadius = 0.3f;

        /// <summary>Registry marking which structures are solid wall segments to block against.</summary>
        public void SetWalls(WallRegistry walls) { _walls = walls; }

        // --- Pathfinding (A*) -------------------------------------------------------------
        private NavGrid _nav;
        // Per-unit cached next waypoint + time until recompute (throttled so A* isn't run every
        // tick for every unit).
        private readonly Dictionary<UnitId, MapVector> _waypoint = new Dictionary<UnitId, MapVector>();
        private readonly Dictionary<UnitId, float> _repath = new Dictionary<UnitId, float>();
        private const float RepathInterval = 0.4f; // seconds between A* recomputes per unit

        /// <summary>Supplies the navigation grid used to path around walls.</summary>
        public void SetNavGrid(NavGrid nav) { _nav = nav; }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || _state.Units.Count == 0) { return; }

            // Snapshot the current units so seeking reads a stable set while we move them.
            var units = new List<Unit>(_state.Units.Count);
            foreach (var kv in _state.Units)
            {
                if (kv.Value != null && !kv.Value.IsHeld) { units.Add(kv.Value); }
            }

            List<UnitId> arrived = null;

            // --- 1 & 2: seek nearest enemy, else advance to enemy base ---
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                float speed = u.MovementSpeed;
                if (speed <= 0f) { continue; }

                Unit target = FindNearestEnemyUnit(u, units);
                if (target != null)
                {
                    MoveToward(u, target.Position, speed, deltaSeconds, _kinds != null ? _kinds.EngageRange(u.Id) : EngageRange);
                }
                else
                {
                    // No enemy units: go after the nearest enemy STRUCTURE (engageable), and
                    // only if there are none, advance on the enemy base.
                    MapVector structPos;
                    if (TryFindNearestEnemyStructure(u, out structPos))
                    {
                        MoveToward(u, structPos, speed, deltaSeconds, _kinds != null ? _kinds.EngageRange(u.Id) : EngageRange);
                    }
                    else
                    {
                        PlayerState opp = _state.GetOpponent(u.Owner);
                        if (opp != null)
                        {
                            MapVector basePos = opp.BaseLocation;
                            float d = MapVector.Distance(u.Position, basePos);
                            if (d <= BaseArriveRange)
                            {
                                float nh = opp.BaseHealth - u.Damage;
                                opp.BaseHealth = nh < 0f ? 0f : nh;
                                (arrived ??= new List<UnitId>()).Add(u.Id);
                            }
                            else
                            {
                                float stepLen = System.Math.Min(speed * deltaSeconds, d);
                                MapVector dir = (basePos - u.Position).Normalized();
                                u.Position = u.Position + dir * stepLen;
                            }
                        }
                    }
                }
            }

            // --- 3: separation pass so units cannot overlap (they queue behind each other) ---
            Separate(units);

            // --- 4: wall blocking: push units out of living wall-segment structure footprints.
            // Two passes settle a unit wedged between a wall and its neighbours.
            if (_walls != null && _walls.Count > 0)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int i = 0; i < units.Count; i++)
                    {
                        ResolveWalls(units[i]);
                    }
                }
            }

            if (arrived != null)
            {
                for (int i = 0; i < arrived.Count; i++) { _state.Units.Remove(arrived[i]); }
            }
        }

        private void MoveToward(Unit u, MapVector targetPos, float speed, float dt, float stopRange)
        {
            float d = MapVector.Distance(u.Position, targetPos);
            if (d <= stopRange) { return; } // already in range; hold.

            // Decide what point to steer toward: the final target if we can go straight (no
            // nav grid, or close enough for final approach), otherwise the next A* waypoint.
            MapVector steerTo = targetPos;
            if (_nav != null && d > stopRange + 1.5f)
            {
                float timer = _repath.TryGetValue(u.Id, out float t) ? t : 0f;
                timer -= dt;
                MapVector wp;
                bool haveWp = _waypoint.TryGetValue(u.Id, out wp);
                if (timer <= 0f || !haveWp)
                {
                    if (_nav.TryNextWaypoint(u.Position, targetPos, out MapVector next))
                    {
                        wp = next; haveWp = true; _waypoint[u.Id] = wp;
                    }
                    else
                    {
                        haveWp = false; _waypoint.Remove(u.Id); // clear path, steer straight
                    }
                    timer = RepathInterval;
                }
                _repath[u.Id] = timer;

                if (haveWp)
                {
                    // If we've essentially reached the cached waypoint, steer at the final
                    // target and let the next recompute advance the path.
                    steerTo = MapVector.Distance(u.Position, wp) <= 0.4f ? targetPos : wp;
                }
            }

            float segDist = MapVector.Distance(u.Position, steerTo);
            if (segDist <= 0.0001f) { return; }
            float stepLen = System.Math.Min(speed * dt, segDist);
            MapVector dir = (steerTo - u.Position).Normalized();
            u.Position = u.Position + dir * stepLen;
        }

        private bool TryFindNearestEnemyStructure(Unit self, out MapVector pos)
        {
            pos = default;
            float bestSqr = float.MaxValue;
            bool found = false;
            foreach (var kv in _state.Structures)
            {
                var s = kv.Value;
                if (s == null || s.Owner == self.Owner) { continue; }
                if (_structureHp != null && _structureHp.IsDead(s.Id)) { continue; }
                float cx = s.Footprint.Origin.X + s.Footprint.Size.X * 0.5f;
                float cy = s.Footprint.Origin.Y + s.Footprint.Size.Y * 0.5f;
                var c = new MapVector(cx, cy);
                float sq = MapVector.SqrDistance(self.Position, c);
                if (sq < bestSqr) { bestSqr = sq; pos = c; found = true; }
            }
            return found;
        }

        private Unit FindNearestEnemyUnit(Unit self, List<Unit> units)
        {
            Unit best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < units.Count; i++)
            {
                Unit o = units[i];
                if (o.Owner == self.Owner) { continue; }
                if (o.Health <= 0f) { continue; }
                float sq = MapVector.SqrDistance(self.Position, o.Position);
                if (sq < bestSqr) { bestSqr = sq; best = o; }
            }
            return best;
        }

        /// <summary>
        /// Simple one-pass local separation: any two units closer than <see cref="UnitSpacing"/>
        /// are pushed apart along their connecting axis. Repeated each tick this keeps units
        /// from overlapping and makes crowds queue up around a target. O(n^2), fine for the
        /// modest unit counts in this demo.
        /// </summary>
        /// <summary>
        /// If the unit is inside any living wall-segment footprint (expanded by the unit
        /// radius), push it out along the axis of least penetration. Walls are solid to all
        /// units (there is no pathfinding, so units jam against them until destroyed).
        /// </summary>
        private void ResolveWalls(Unit u)
        {
            foreach (var kv in _state.Structures)
            {
                var s = kv.Value;
                if (s == null || _walls == null || !_walls.IsWall(s.Id)) { continue; }
                if (_structureHp != null && _structureHp.IsDead(s.Id)) { continue; }

                float minX = s.Footprint.Origin.X - UnitRadius;
                float minY = s.Footprint.Origin.Y - UnitRadius;
                float maxX = s.Footprint.Origin.X + s.Footprint.Size.X + UnitRadius;
                float maxY = s.Footprint.Origin.Y + s.Footprint.Size.Y + UnitRadius;

                MapVector p = u.Position;
                if (p.X > minX && p.X < maxX && p.Y > minY && p.Y < maxY)
                {
                    // Least-penetration exit.
                    float left = p.X - minX, right = maxX - p.X;
                    float down = p.Y - minY, up = maxY - p.Y;
                    float minPen = System.Math.Min(System.Math.Min(left, right), System.Math.Min(down, up));
                    if (minPen == left)       { p = new MapVector(minX, p.Y); }
                    else if (minPen == right) { p = new MapVector(maxX, p.Y); }
                    else if (minPen == down)  { p = new MapVector(p.X, minY); }
                    else                      { p = new MapVector(p.X, maxY); }
                    u.Position = p;
                }
            }
        }

        private void Separate(List<Unit> units)
        {
            for (int i = 0; i < units.Count; i++)
            {
                for (int j = i + 1; j < units.Count; j++)
                {
                    Unit a = units[i];
                    Unit b = units[j];
                    MapVector delta = b.Position - a.Position;
                    float dist = delta.Magnitude;
                    if (dist >= UnitSpacing) { continue; }

                    float overlap = UnitSpacing - dist;
                    MapVector push;
                    if (dist > 0.0001f)
                    {
                        push = delta.Normalized() * (overlap * 0.5f);
                    }
                    else
                    {
                        // Exactly coincident: nudge apart deterministically by id.
                        float sign = a.Id.Value <= b.Id.Value ? 1f : -1f;
                        push = new MapVector(overlap * 0.5f * sign, 0f);
                    }
                    a.Position = a.Position - push;
                    b.Position = b.Position + push;
                }
            }
        }
    }
}







