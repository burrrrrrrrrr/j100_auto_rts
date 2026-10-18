using System.Collections.Generic;
using MultiplayerRts.Client;
using MultiplayerRts.Net;
using MultiplayerRts.Core;
using UnityEngine;

namespace MultiplayerRts.Bootstrap
{
    /// <summary>
    /// Zero-setup primitive renderer: draws the replicated match as simple 3D shapes so you
    /// can see the simulation without authoring sprites/prefabs. Implements the Client layer's
    /// <see cref="IUnitRenderer"/> seam and is driven by the GameClient mirror each time new
    /// state replicates.
    ///   - Units      -> small spheres, colored per owner (via the pure UnitColorResolver).
    ///   - Structures -> cubes, colored per owner, sized to their footprint.
    ///   - Bases      -> tall thin markers at each player's base location.
    /// Objects are pooled by id and moved in place; stale ones are destroyed.
    /// </summary>
    public sealed class ShapeRenderer : MonoBehaviour, IUnitRenderer
    {
        private readonly UnitColorResolver _colors = new UnitColorResolver();

        private readonly Dictionary<UnitId, GameObject> _units = new Dictionary<UnitId, GameObject>();
        private readonly Dictionary<StructureId, GameObject> _structures = new Dictionary<StructureId, GameObject>();
        private readonly Dictionary<uint, GameObject> _bases = new Dictionary<uint, GameObject>();

        // Transient attack sparks: small bright shapes that fade out quickly.
        private readonly List<(GameObject go, float ttl)> _sparks = new List<(GameObject, float)>();
        private const float SparkLifetime = 0.18f;

        private readonly List<(LineRenderer lr, float ttl)> _tracers = new List<(LineRenderer, float)>();
        private const float TracerLifetime = 0.12f;

        private Material _sharedMat;

        /// <summary>Optional: returns true if a structure id is a ranged spawner (drawn as an oval cylinder).</summary>
        public System.Func<StructureId, bool> IsRangedSpawner;
        /// <summary>Optional: returns true if a unit id is ranged (drawn slightly distinct).</summary>
        public System.Func<UnitId, bool> IsRangedUnit;

        /// <summary>Optional: returns true if a structure id is a wall segment (drawn as a team-colored block).</summary>
        public System.Func<StructureId, bool> IsWall;

        private void Awake()
        {
            // URP-compatible unlit-ish material; falls back gracefully if shader missing.
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) { sh = Shader.Find("Standard"); }
            _sharedMat = sh != null ? new Material(sh) : null;
        }

        public void Render(ClientRenderView view)
        {
            if (view == null) { return; }

            RenderBases(view);
            RenderStructures(view);
            RenderUnits(view);
        }

        private void RenderBases(ClientRenderView view)
        {
            var seen = new HashSet<uint>();
            foreach (PlayerSnapshot p in view.Players)
            {
                seen.Add(p.Id.Value);
                GameObject go = GetOrCreate(_bases, p.Id.Value, PrimitiveType.Cube, $"Base_{p.Id.Value}");
                go.transform.position = new Vector3(/* x */ BaseX(view, p), 1f, /* z */ BaseZ(view, p));
                go.transform.localScale = new Vector3(3f, 2f, 3f);
                Tint(go, _colors.Resolve(p.Id));
            }
            RemoveStale(_bases, seen);
        }

        // Base positions come from PlayerSnapshot only indirectly; we read them from structures/units
        // fallback by using the player's base location if present. PlayerSnapshot carries base health
        // but not location, so we approximate: left player at -9, right at +9 (matches DemoConfig).
        private static float BaseX(ClientRenderView view, PlayerSnapshot p)
        {
            // Lowest player id -> left, highest -> right (demo convention).
            uint minId = uint.MaxValue, maxId = 0;
            foreach (PlayerSnapshot q in view.Players) { if (q.Id.Value < minId) minId = q.Id.Value; if (q.Id.Value > maxId) maxId = q.Id.Value; }
            if (p.Id.Value == minId) return -30f;
            if (p.Id.Value == maxId) return 30f;
            return 0f;
        }
        private static float BaseZ(ClientRenderView view, PlayerSnapshot p) => 0f;

        private void RenderStructures(ClientRenderView view)
        {
            var seen = new HashSet<StructureId>();
            foreach (StructureSnapshot s in view.Structures)
            {
                seen.Add(s.Id);

                // Wall segment: team-colored cube sized to its footprint, then skip other shaping.
                if (IsWall != null && IsWall(s.Id))
                {
                    GameObject wgo = GetOrCreate(_structures, s.Id, PrimitiveType.Cube, $"Wall_{s.Id.Value}");
                    Footprint wf = s.Footprint;
                    float wcx = wf.Origin.X + wf.Size.X * 0.5f;
                    float wcy = wf.Origin.Y + wf.Size.Y * 0.5f;
                    wgo.transform.position = new Vector3(wcx, 0.75f, wcy);
                    wgo.transform.localScale = new Vector3(Mathf.Max(0.5f, wf.Size.X), 1.5f, Mathf.Max(0.5f, wf.Size.Y));
                    Tint(wgo, _colors.Resolve(s.Owner));
                    continue;
                }

                // Shape per type:
                //   Spawner   -> Cylinder
                //   Defensive -> cone/spike (cylinder tapered to a point = "triangle")
                //   Economy   -> Capsule
                // (Unity has no cone/triangle primitive, so the tower is a tapered cylinder.)
                PrimitiveType prim = s.Type == StructureType.Economy ? PrimitiveType.Capsule
                                   : PrimitiveType.Cylinder;
                GameObject go = GetOrCreate(_structures, s.Id, prim, $"Structure_{s.Type}_{s.Id.Value}");

                Footprint f = s.Footprint;
                float cx = f.Origin.X + f.Size.X * 0.5f;
                float cy = f.Origin.Y + f.Size.Y * 0.5f;

                if (s.Type == StructureType.Defensive)
                {
                    // Tower: tall spike. A cylinder squashed at the top edge reads as a
                    // triangle/cone from the camera angle.
                    go.transform.position = new Vector3(cx, 1.25f, cy);
                    go.transform.localScale = new Vector3(1.1f, 2.5f, 1.1f);
                    ApplyConeTaper(go); // taper the top vertices to a point
                }
                else
                {
                    go.transform.position = new Vector3(cx, 0.6f, cy);
                    bool rangedSpawner = s.Type == StructureType.Spawner
                                         && IsRangedSpawner != null && IsRangedSpawner(s.Id);
                    if (rangedSpawner)
                    {
                        // Oval cylinder: wider on x, thinner on z.
                        go.transform.localScale = new Vector3(2.0f, 1.2f, 0.9f);
                    }
                    else
                    {
                        // Regular (melee) spawner / economy cylinder.
                        go.transform.localScale = new Vector3(1.1f, 1.2f, 1.1f);
                    }
                }

                Tint(go, _colors.Resolve(s.Owner));
            }
            RemoveStale(_structures, seen);
        }

        private void RenderUnits(ClientRenderView view)
        {
            var seen = new HashSet<UnitId>();
            foreach (RenderUnit u in view.Units)
            {
                seen.Add(u.Id);
                GameObject go = GetOrCreate(_units, u.Id, PrimitiveType.Sphere, $"Unit_{u.Id.Value}");
                go.transform.position = new Vector3(u.Position.X, 0.4f, u.Position.Y);
                bool ranged = IsRangedUnit != null && IsRangedUnit(u.Id);
                go.transform.localScale = ranged ? new Vector3(0.45f, 0.45f, 0.45f) : Vector3.one * 0.6f;
                Tint(go, _colors.Resolve(u.Owner));
            }
            RemoveStale(_units, seen);
        }

        /// <summary>
        /// Flashes a small bright spark at a map position (x,y)->world(x,z) for a brief
        /// moment. Called from the melee combat strike callback.
        /// </summary>
        public void Spark(MapVector at)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Spark";
            go.transform.SetParent(transform, false);
            var col = go.GetComponent<Collider>(); if (col != null) { Destroy(col); }
            go.transform.position = new Vector3(at.X, 0.8f, at.Y);
            go.transform.localScale = Vector3.one * 0.5f;
            TintRaw(go, new Color(1f, 0.95f, 0.3f)); // bright yellow
            _sparks.Add((go, SparkLifetime));
        }

        /// <summary>
        /// Draws a brief tracer line from a tower (map from) to its target (map to), both
        /// mapped (x,y)->world(x,z). Used by the tower fire callback.
        /// </summary>
        public void Tracer(MapVector from, MapVector to)
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, new Vector3(from.X, 0.9f, from.Y));
            lr.SetPosition(1, new Vector3(to.X, 0.7f, to.Y));
            lr.widthMultiplier = 0.12f;
            lr.numCapVertices = 2;
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) { sh = Shader.Find("Sprites/Default"); }
            if (sh != null)
            {
                var mat = new Material(sh);
                var cyan = new Color(0.4f, 0.9f, 1f);
                mat.SetColor("_BaseColor", cyan);
                mat.color = cyan;
                lr.material = mat;
            }
            lr.startColor = lr.endColor = new Color(0.4f, 0.9f, 1f);
            _tracers.Add((lr, TracerLifetime));
        }

        private void Update()
        {
            // Age and remove tracers.
            for (int i = _tracers.Count - 1; i >= 0; i--)
            {
                var (lr, ttl) = _tracers[i];
                ttl -= Time.deltaTime;
                if (ttl <= 0f || lr == null)
                {
                    if (lr != null) { Destroy(lr.gameObject); }
                    _tracers.RemoveAt(i);
                }
                else
                {
                    float t = ttl / TracerLifetime;
                    if (lr != null) { lr.widthMultiplier = 0.12f * t; }
                    _tracers[i] = (lr, ttl);
                }
            }

            // Age and remove sparks; shrink them as they fade.
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var (go, ttl) = _sparks[i];
                ttl -= Time.deltaTime;
                if (ttl <= 0f || go == null)
                {
                    if (go != null) { Destroy(go); }
                    _sparks.RemoveAt(i);
                }
                else
                {
                    float t = ttl / SparkLifetime;
                    if (go != null) { go.transform.localScale = Vector3.one * (0.5f * t + 0.1f); }
                    _sparks[i] = (go, ttl);
                }
            }
        }

        private void TintRaw(GameObject go, Color col)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) { return; }
            if (_sharedMat != null)
            {
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_BaseColor", col);
                mpb.SetColor("_Color", col);
                r.SetPropertyBlock(mpb);
                r.sharedMaterial = _sharedMat;
            }
            else { r.material.color = col; }
        }
        private GameObject GetOrCreate<TKey>(Dictionary<TKey, GameObject> pool, TKey key, PrimitiveType prim, string name)
        {
            if (pool.TryGetValue(key, out GameObject existing) && existing != null) { return existing; }
            GameObject go = GameObject.CreatePrimitive(prim);
            go.name = name;
            go.transform.SetParent(transform, worldPositionStays: false);
            // Primitives come with colliders we don't need for a visual-only demo.
            Collider col = go.GetComponent<Collider>();
            if (col != null) { Destroy(col); }
            pool[key] = go;
            return go;
        }

        private void Tint(GameObject go, RenderColor c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) { return; }
            var col = new Color(c.R, c.G, c.B, c.A);
            if (_sharedMat != null)
            {
                // Per-instance color without leaking materials.
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", col);
                mpb.SetColor("_Color", col);
                r.SetPropertyBlock(mpb);
                if (r.sharedMaterial == null) { r.sharedMaterial = _sharedMat; }
            }
            else
            {
                r.material.color = col;
            }
        }

        /// <summary>
        /// Converts a unit cylinder mesh into a cone by collapsing every top-ring vertex
        /// (those at the cylinder's max local-Y) to the central axis, giving a pointed
        /// "triangle" silhouette for towers. Operates on a per-instance mesh copy so other
        /// cylinders are unaffected.
        /// </summary>
        private static void ApplyConeTaper(GameObject go)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) { return; }

            // Copy the mesh so we don't mutate Unity's shared built-in cylinder.
            Mesh mesh = Object.Instantiate(mf.sharedMesh);
            Vector3[] verts = mesh.vertices;

            float maxY = float.MinValue;
            for (int i = 0; i < verts.Length; i++) { if (verts[i].y > maxY) { maxY = verts[i].y; } }

            const float eps = 0.001f;
            for (int i = 0; i < verts.Length; i++)
            {
                if (maxY - verts[i].y <= eps)
                {
                    verts[i].x = 0f; // collapse top ring to the axis -> apex
                    verts[i].z = 0f;
                }
            }
            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
        }
        private void RemoveStale<TKey>(Dictionary<TKey, GameObject> pool, HashSet<TKey> seen)
        {
            if (pool.Count == 0) { return; }
            var remove = new List<TKey>();
            foreach (var kvp in pool) { if (!seen.Contains(kvp.Key)) { remove.Add(kvp.Key); } }
            foreach (var k in remove)
            {
                if (pool[k] != null) { Destroy(pool[k]); }
                pool.Remove(k);
            }
        }
    }
}











