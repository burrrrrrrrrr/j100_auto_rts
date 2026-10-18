using System.Collections.Generic;

namespace MultiplayerRts.Core
{
    /// <summary>
    /// A uniform-grid A* pathfinder over the map. Wall-segment structure footprints mark grid
    /// cells blocked; units path around them. The grid is rebuilt from the current walls via
    /// <see cref="Rebuild"/> (cheap, call only when walls change). Pure C# (no Unity).
    ///
    /// 8-directional movement with diagonal corner-cutting disallowed (a diagonal step is only
    /// allowed when both orthogonal neighbours are open), so paths don't clip wall corners.
    /// </summary>
    public sealed class NavGrid
    {
        private readonly float _minX, _minY, _cell;
        private readonly int _w, _h;
        private readonly bool[] _blocked;

        public NavGrid(MapVector min, MapVector max, float cellSize)
        {
            _minX = min.X; _minY = min.Y; _cell = cellSize;
            _w = System.Math.Max(1, (int)System.Math.Ceiling((max.X - min.X) / cellSize));
            _h = System.Math.Max(1, (int)System.Math.Ceiling((max.Y - min.Y) / cellSize));
            _blocked = new bool[_w * _h];
        }

        public float CellSize => _cell;

        private int Idx(int cx, int cy) => cy * _w + cx;
        private bool InBounds(int cx, int cy) => cx >= 0 && cy >= 0 && cx < _w && cy < _h;

        private void WorldToCell(MapVector p, out int cx, out int cy)
        {
            cx = (int)System.Math.Floor((p.X - _minX) / _cell);
            cy = (int)System.Math.Floor((p.Y - _minY) / _cell);
            if (cx < 0) cx = 0; if (cy < 0) cy = 0;
            if (cx >= _w) cx = _w - 1; if (cy >= _h) cy = _h - 1;
        }

        private MapVector CellToWorld(int cx, int cy)
            => new MapVector(_minX + (cx + 0.5f) * _cell, _minY + (cy + 0.5f) * _cell);

        /// <summary>Clears all cells, then marks every living wall structure's footprint blocked.</summary>
        public void Rebuild(MatchState state, WallRegistry walls, StructureHealth hp, float pad)
        {
            for (int i = 0; i < _blocked.Length; i++) { _blocked[i] = false; }
            if (walls == null) { return; }

            foreach (var kv in state.Structures)
            {
                var s = kv.Value;
                if (s == null || !walls.IsWall(s.Id)) { continue; }
                if (hp != null && hp.IsDead(s.Id)) { continue; }

                float minx = s.Footprint.Origin.X - pad;
                float miny = s.Footprint.Origin.Y - pad;
                float maxx = s.Footprint.Origin.X + s.Footprint.Size.X + pad;
                float maxy = s.Footprint.Origin.Y + s.Footprint.Size.Y + pad;

                WorldToCell(new MapVector(minx, miny), out int x0, out int y0);
                WorldToCell(new MapVector(maxx, maxy), out int x1, out int y1);
                for (int cy = y0; cy <= y1; cy++)
                {
                    for (int cx = x0; cx <= x1; cx++)
                    {
                        if (InBounds(cx, cy)) { _blocked[Idx(cx, cy)] = true; }
                    }
                }
            }
        }

        private bool IsOpen(int cx, int cy) => InBounds(cx, cy) && !_blocked[Idx(cx, cy)];

        /// <summary>
        /// Finds a path from <paramref name="from"/> to <paramref name="to"/> and returns the
        /// next world waypoint to steer toward (not the full path). Returns false when no path
        /// exists or the target is essentially reachable directly (caller should steer straight).
        /// A blocked start/goal cell falls back to the nearest open cell.
        /// </summary>
        public bool TryNextWaypoint(MapVector from, MapVector to, out MapVector next)
        {
            next = to;
            WorldToCell(from, out int sx, out int sy);
            WorldToCell(to, out int gx, out int gy);

            if (!IsOpen(sx, sy)) { if (!NearestOpen(ref sx, ref sy)) return false; }
            if (!IsOpen(gx, gy)) { if (!NearestOpen(ref gx, ref gy)) return false; }
            if (sx == gx && sy == gy) { return false; } // same cell: steer straight

            // A*.
            int n = _w * _h;
            var came = new int[n];
            var g = new float[n];
            for (int i = 0; i < n; i++) { came[i] = -1; g[i] = float.MaxValue; }
            var open = new SimpleMinHeap(n);
            int start = Idx(sx, sy), goal = Idx(gx, gy);
            g[start] = 0f;
            open.Push(start, Heur(sx, sy, gx, gy));

            int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
            int[] dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

            bool found = false;
            while (open.Count > 0)
            {
                int cur = open.Pop();
                if (cur == goal) { found = true; break; }
                int ccx = cur % _w, ccy = cur / _w;
                for (int k = 0; k < 8; k++)
                {
                    int nx = ccx + dx[k], ny = ccy + dy[k];
                    if (!IsOpen(nx, ny)) { continue; }
                    bool diag = k >= 4;
                    if (diag && (!IsOpen(ccx + dx[k], ccy) || !IsOpen(ccx, ccy + dy[k]))) { continue; } // no corner cut
                    float step = diag ? 1.41421356f : 1f;
                    int ni = Idx(nx, ny);
                    float ng = g[cur] + step;
                    if (ng < g[ni])
                    {
                        g[ni] = ng; came[ni] = cur;
                        open.Push(ni, ng + Heur(nx, ny, gx, gy));
                    }
                }
            }

            if (!found) { return false; }

            // Walk back from goal to the cell right after start; that is the next waypoint.
            int node = goal, prev = goal;
            while (node != start && came[node] != -1)
            {
                prev = node;
                node = came[node];
            }
            int wx = prev % _w, wy = prev / _w;
            next = CellToWorld(wx, wy);
            return true;
        }

        private static float Heur(int ax, int ay, int bx, int by)
        {
            int dx = System.Math.Abs(ax - bx), dy = System.Math.Abs(ay - by);
            int min = System.Math.Min(dx, dy), max = System.Math.Max(dx, dy);
            return 1.41421356f * min + (max - min); // octile distance
        }

        private bool NearestOpen(ref int cx, ref int cy)
        {
            for (int r = 1; r <= 4; r++)
            {
                for (int oy = -r; oy <= r; oy++)
                {
                    for (int ox = -r; ox <= r; ox++)
                    {
                        if (IsOpen(cx + ox, cy + oy)) { cx += ox; cy += oy; return true; }
                    }
                }
            }
            return false;
        }

        /// <summary>Minimal binary min-heap of cell indices keyed by float priority.</summary>
        private sealed class SimpleMinHeap
        {
            private readonly int[] _items;
            private readonly float[] _prio;
            public int Count;
            public SimpleMinHeap(int cap) { _items = new int[cap + 1]; _prio = new float[cap + 1]; Count = 0; }
            public void Push(int item, float prio)
            {
                Count++; _items[Count] = item; _prio[Count] = prio;
                int c = Count;
                while (c > 1 && _prio[c] < _prio[c / 2]) { Swap(c, c / 2); c /= 2; }
            }
            public int Pop()
            {
                int top = _items[1];
                _items[1] = _items[Count]; _prio[1] = _prio[Count]; Count--;
                int c = 1;
                while (true)
                {
                    int l = c * 2, r = c * 2 + 1, s = c;
                    if (l <= Count && _prio[l] < _prio[s]) s = l;
                    if (r <= Count && _prio[r] < _prio[s]) s = r;
                    if (s == c) break;
                    Swap(c, s); c = s;
                }
                return top;
            }
            private void Swap(int a, int b)
            {
                int ti = _items[a]; _items[a] = _items[b]; _items[b] = ti;
                float tp = _prio[a]; _prio[a] = _prio[b]; _prio[b] = tp;
            }
        }
    }
}
