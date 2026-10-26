using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static DreamStudio.Editor.DreamBuildUtils;

namespace DreamStudio.Editor
{
    /// <summary>
    /// Grows a natural forest floor: grass, grass gone to seed and wildflowers in the open glades; ferns,
    /// sprouting plants, moss and drifts of fallen leaves in the shade near trees; clover everywhere; shrubs
    /// at the edges of the shade; mushrooms at the feet of trees and along logs; mossy rocks and fallen logs.
    /// A trodden path of fallen leaves, kept clear of tall plants, follows <see cref="Settings.path"/>.
    ///
    /// Small plants are alpha-cut cards from one texture atlas (<see cref="DreamTextures.UnderstoryAtlas"/>),
    /// merged into one mesh per 20 m square of forest along with the shrubs and mushrooms, so thousands of
    /// plants cost a few draw calls per square and whole squares are culled when out of view. None of them
    /// cast shadows (too costly on Quest) and all of them can be walked through. Rocks and logs are solid
    /// objects of their own.
    /// </summary>
    public static class DreamUnderstory
    {
        public class Settings
        {
            public string prefix = "Forest";
            public System.Func<Vector3, float> groundHeight;
            public System.Func<Vector3, Vector3> groundNormal;
            public IList<Vector3> trees;
            public float radius = 76f;
            public List<Vector3> path = new List<Vector3>();
            public float pathWidth = 1.1f;
            /// <summary>Kept open: only low plants (grass, flowers, clover, moss), no ferns, shrubs, rocks or logs.</summary>
            public List<(Vector3 center, float radius)> open = new List<(Vector3, float)>();
            public Material bark;
            public Material shrubLeaves;
            public int seed = 1;
        }

        const float ChunkSize = 20f;

        internal class Bits
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int>[] sub;

            public Bits(int submeshes)
            {
                sub = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++)
                    sub[i] = new List<int>();
            }

            public int Vert(Vector3 p, Vector3 normal, Vector2 t)
            {
                v.Add(p);
                n.Add(normal);
                uv.Add(t);
                return v.Count - 1;
            }

            public void Quad(int s, int a, int b, int c, int d) => sub[s].AddRange(new[] { a, b, c, a, c, d });

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (v.Count > 65000)
                    mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(v);
                mesh.SetNormals(n);
                mesh.SetUVs(0, uv);
                mesh.subMeshCount = sub.Length;
                for (int i = 0; i < sub.Length; i++)
                    mesh.SetTriangles(sub[i], i);
                mesh.RecalculateBounds();
                return SaveMesh(mesh, name);
            }
        }

        /// <summary>One 20 m square of ground: a mesh per layer (plants, shrubs, mushrooms...), each with its own materials.</summary>
        internal class Chunk
        {
            public readonly Dictionary<string, Bits> layers = new Dictionary<string, Bits>();

            public Bits Layer(string name, int submeshes = 1)
            {
                if (!layers.TryGetValue(name, out var bits))
                    layers[name] = bits = new Bits(submeshes);
                return bits;
            }
        }

        internal class Context
        {
            public Settings s;
            public System.Random rng;
            public Dictionary<(int, int), Chunk> chunks = new Dictionary<(int, int), Chunk>();
            public List<(Vector3 a, Vector3 b)> logs = new List<(Vector3, Vector3)>();

            public float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);
            public bool Chance(float p) => rng.NextDouble() < p;
            public float H(Vector3 p) => s.groundHeight(p);
            public Vector3 N(Vector3 p) => s.groundNormal(p);
            public Vector3 Ground(Vector3 p) => new Vector3(p.x, s.groundHeight(p), p.z);

            public Chunk At(Vector3 p)
            {
                var key = (Mathf.FloorToInt(p.x / ChunkSize), Mathf.FloorToInt(p.z / ChunkSize));
                if (!chunks.TryGetValue(key, out var chunk))
                    chunks[key] = chunk = new Chunk();
                return chunk;
            }

            public Vector3 RandomSpot()
            {
                float a = R(0f, Mathf.PI * 2f);
                float r = Mathf.Sqrt(R(0f, 1f)) * s.radius;
                return Ground(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }

            public float TreeDistance(Vector3 p)
            {
                float best = float.MaxValue;
                foreach (var t in s.trees)
                {
                    float d = (t.x - p.x) * (t.x - p.x) + (t.z - p.z) * (t.z - p.z);
                    if (d < best)
                        best = d;
                }
                return Mathf.Sqrt(best);
            }

            public float PathDistance(Vector3 p)
            {
                float best = float.MaxValue;
                var q = new Vector2(p.x, p.z);
                for (int i = 1; i < s.path.Count; i++)
                {
                    var a = new Vector2(s.path[i - 1].x, s.path[i - 1].z);
                    var b = new Vector2(s.path[i].x, s.path[i].z);
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                    best = Mathf.Min(best, Vector2.Distance(q, a + ab * t));
                }
                return best;
            }

            public bool InOpen(Vector3 p, float margin = 0f)
            {
                foreach (var (center, radius) in s.open)
                    if (new Vector2(p.x - center.x, p.z - center.z).magnitude < radius + margin)
                        return true;
                return false;
            }

            public float LogDistance(Vector3 p)
            {
                float best = float.MaxValue;
                var q = new Vector2(p.x, p.z);
                foreach (var (a3, b3) in logs)
                {
                    var a = new Vector2(a3.x, a3.z);
                    var ab = new Vector2(b3.x, b3.z) - a;
                    float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                    best = Mathf.Min(best, Vector2.Distance(q, a + ab * t));
                }
                return best;
            }

            /// <summary>0..1: how much of a grassy glade a spot is, from large soft patches of noise.</summary>
            public float Meadow(Vector3 p) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.PerlinNoise(p.x / 18f + 31.7f, p.z / 18f + 12.3f) - 0.35f) / 0.35f));

            /// <summary>0..1: how deep in a tree's shade a spot is.</summary>
            public float Shade(Vector3 p) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - (TreeDistance(p) - 0.8f) / 3.2f));
        }

        internal static Context NewContext(Settings settings) => new Context { s = settings, rng = new System.Random(settings.seed) };

        public static void Build(Transform parent, Settings settings)
        {
            var c = NewContext(settings);
            var floor = new GameObject("Forest Floor");
            floor.transform.SetParent(parent, false);

            var plantsMat = DreamTrees.Cutout($"{settings.prefix}_Understory", new Color(0.92f, 0.95f, 0.88f), DreamTextures.UnderstoryAtlas());
            var capMat = Lit($"{settings.prefix}_MushroomCap", new Color(0.5f, 0.34f, 0.2f), 0.35f, Color.black);
            var stemMat = Lit($"{settings.prefix}_MushroomStem", new Color(0.86f, 0.8f, 0.7f), 0.2f, Color.black);
            var rockMat = Lit($"{settings.prefix}_Rock", new Color(0.62f, 0.6f, 0.56f), 0.2f, Color.black);
            var mossMat = Lit($"{settings.prefix}_Moss", new Color(0.36f, 0.48f, 0.2f), 0.05f, new Color(0.01f, 0.015f, 0.005f));
            var endMat = Lit($"{settings.prefix}_LogEnd", new Color(0.62f, 0.5f, 0.36f), 0.1f, Color.black);

            // Big things first, so small plants can gather around them.
            Logs(c, floor.transform, settings.bark ?? rockMat, mossMat, endMat);
            Rocks(c, floor.transform, rockMat, mossMat);

            // The trodden path: a trail of fallen leaves.
            for (int i = 1; i < settings.path.Count; i++)
            {
                float length = Vector3.Distance(settings.path[i - 1], settings.path[i]);
                for (float d = 0f; d < length; d += 0.9f)
                {
                    var p = c.Ground(Vector3.Lerp(settings.path[i - 1], settings.path[i], d / length)
                        + new Vector3(c.R(-0.3f, 0.3f), 0f, c.R(-0.3f, 0.3f)));
                    Flat(c, c.At(p).Layer("Plants"), p, c.R(1.1f, 1.6f), c.R(0f, 360f), 0.02f + c.R(0f, 0.004f), DreamTextures.Understory.Litter, 3);
                }
            }

            Scatter(c, 14000, p =>
            {
                float path = c.PathDistance(p);
                return Mathf.Clamp(0.12f + 0.75f * c.Meadow(p) - 0.45f * c.Shade(p), 0.04f, 0.92f) * (path < 0.6f ? 0.15f : 1f);
            }, p =>
            {
                float scale = Mathf.Lerp(1f, 0.65f, c.Shade(p)) * (c.InOpen(p) ? 0.8f : 1f);
                Crossed(c, c.At(p).Layer("Plants"), p, c.R(0.35f, 0.6f) * scale, c.R(0.25f, 0.5f) * scale, 3, DreamTextures.Understory.Grass);
            });
            Scatter(c, 2200, p => c.InOpen(p) || c.PathDistance(p) < 1f ? 0f : 0.55f * c.Meadow(p) * (1f - c.Shade(p)), p =>
                Crossed(c, c.At(p).Layer("Plants"), p, c.R(0.3f, 0.45f), c.R(0.55f, 0.85f), 2, DreamTextures.Understory.SeedGrass));
            Scatter(c, 2600, p => c.PathDistance(p) < 0.7f ? 0f : 0.08f + 0.5f * c.Meadow(p) * (1f - c.Shade(p)) + (c.InOpen(p) ? 0.3f : 0f), p =>
                Crossed(c, c.At(p).Layer("Plants"), p, c.R(0.22f, 0.32f), c.R(0.3f, 0.5f), 2, DreamTextures.Understory.Flowers));
            Scatter(c, 3500, p => c.PathDistance(p) < 0.5f ? 0.1f : 0.35f, p =>
                Flat(c, c.At(p).Layer("Plants"), p, c.R(0.25f, 0.45f), c.R(0f, 360f), 0.03f, DreamTextures.Understory.Clover, 1, c.R(5f, 20f)));
            Scatter(c, 2600, p => c.InOpen(p) || c.PathDistance(p) < 0.8f ? 0f : 0.15f + 0.4f * c.Shade(p), p =>
                Crossed(c, c.At(p).Layer("Plants"), p, c.R(0.22f, 0.32f), c.R(0.2f, 0.32f), 2, DreamTextures.Understory.Sprout));
            Scatter(c, 2400, p =>
            {
                if (c.InOpen(p, 0.5f) || c.PathDistance(p) < 1.3f || c.TreeDistance(p) < 0.7f)
                    return 0f;
                return 0.5f * c.Shade(p) + 0.12f * (1f - c.Meadow(p));
            }, p => Fern(c, c.At(p).Layer("Ferns"), p, c.R(0.7f, 1.05f) * Mathf.Lerp(0.8f, 1.15f, c.Shade(p)), DreamTextures.Understory.Fern));
            Scatter(c, 1100, p => c.PathDistance(p) < 1f ? 0f : 0.6f * c.Shade(p) + 0.05f, p =>
                Flat(c, c.At(p).Layer("Plants"), p, c.R(1f, 2.4f), c.R(0f, 360f), 0.012f + c.R(0f, 0.004f), DreamTextures.Understory.Moss, 3));
            Scatter(c, 1400, p => c.PathDistance(p) < 1f ? 0f : 0.2f + 0.45f * c.Shade(p), p =>
                Flat(c, c.At(p).Layer("Plants"), p, c.R(1f, 1.8f), c.R(0f, 360f), 0.018f + c.R(0f, 0.004f), DreamTextures.Understory.Litter, 3));
            Scatter(c, 900, p =>
            {
                float tree = c.TreeDistance(p);
                if (c.InOpen(p, 1f) || c.PathDistance(p) < 1.6f || tree < 1.3f)
                    return 0f;
                return 0.1f + 0.35f * (1f - c.Meadow(p)) * (tree < 6f ? 1f : 0.4f);
            }, p => Shrub(c, c.At(p).Layer("Shrubs"), p, c.R(0.6f, 1.3f)));
            Scatter(c, 900, p =>
            {
                if (c.InOpen(p) || c.PathDistance(p) < 0.8f)
                    return 0f;
                return c.TreeDistance(p) < 1.6f || c.LogDistance(p) < 0.7f ? 0.45f : 0.01f;
            }, p => Mushrooms(c, c.At(p).Layer("Mushrooms", 2), p));

            var layers = new Dictionary<string, Material[]>
            {
                ["Plants"] = new[] { plantsMat },
                ["Ferns"] = new[] { plantsMat },
                ["Mushrooms"] = new[] { capMat, stemMat },
            };
            if (settings.shrubLeaves != null)
                layers["Shrubs"] = new[] { settings.shrubLeaves };
            EmitChunks(c, floor.transform, layers);
        }

        /// <summary>One renderer per layer per 20 m square, with that layer's materials.</summary>
        internal static void EmitChunks(Context c, Transform parent, Dictionary<string, Material[]> layers)
        {
            foreach (var pair in c.chunks)
            {
                string key = $"{c.s.prefix}Floor_{pair.Key.Item1}_{pair.Key.Item2}";
                foreach (var layer in pair.Value.layers)
                    if (layers.TryGetValue(layer.Key, out var mats))
                        AddChunk(parent, layer.Key, layer.Value, $"{key}_{layer.Key}", mats);
            }
        }

        /// <summary>Layers tall enough to cast a worthwhile shadow; low ground cover doesn't (too costly on Quest).</summary>
        static readonly HashSet<string> ShadowCasters = new HashSet<string> { "Shrubs", "Ferns" };

        static void AddChunk(Transform parent, string name, Bits bits, string meshName, params Material[] mats)
        {
            if (bits.v.Count == 0)
                return;
            var go = MeshObject(name, parent, bits.ToMesh(meshName), mats[0]);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = mats;
            renderer.shadowCastingMode = ShadowCasters.Contains(name) ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        /// <summary>Tries <paramref name="attempts"/> random spots, keeping each with probability <paramref name="density"/>(spot).</summary>
        internal static void Scatter(Context c, int attempts, System.Func<Vector3, float> density, System.Action<Vector3> place)
        {
            for (int i = 0; i < attempts; i++)
            {
                var p = c.RandomSpot();
                if (c.Chance(density(p)))
                    place(p);
            }
        }

        // =====================================================================
        // Small plants (cards)
        // =====================================================================

        /// <summary>
        /// Upright cards crossed at even angles around a spot (a clump of grass, a flower patch), leaning a
        /// little. Normals point up, so the clump is lit like the ground it grows from.
        /// </summary>
        internal static void Crossed(Context c, Bits b, Vector3 p, float width, float height, int quads, Rect uv)
        {
            float yaw = c.R(0f, 180f);
            var lean = new Vector3(c.R(-0.1f, 0.1f), 0f, c.R(-0.1f, 0.1f)) * height;
            var normal = (c.N(p) + Vector3.up).normalized;
            for (int q = 0; q < quads; q++)
            {
                var dir = Quaternion.Euler(0f, yaw + q * 180f / quads, 0f) * Vector3.right * (width * 0.5f);
                var b0 = c.Ground(p - dir) + Vector3.down * 0.03f;
                var b1 = c.Ground(p + dir) + Vector3.down * 0.03f;
                bool flip = c.Chance(0.5f);
                float u0 = flip ? uv.xMax : uv.xMin, u1 = flip ? uv.xMin : uv.xMax;
                int i0 = b.Vert(b0, normal, new Vector2(u0, uv.yMin));
                int i1 = b.Vert(b1, normal, new Vector2(u1, uv.yMin));
                int i2 = b.Vert(b1 + Vector3.up * height + lean, normal, new Vector2(u1, uv.yMax));
                int i3 = b.Vert(b0 + Vector3.up * height + lean, normal, new Vector2(u0, uv.yMax));
                b.Quad(0, i0, i3, i2, i1);
            }
        }

        /// <summary>A card lying on the ground, following its slope on a small grid (moss, fallen leaves, clover).</summary>
        internal static void Flat(Context c, Bits b, Vector3 p, float size, float yaw, float lift, Rect uv, int grid, float tilt = 0f)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var tiltRot = Quaternion.AngleAxis(tilt, rot * Vector3.right);
            int start = b.v.Count;
            for (int z = 0; z <= grid; z++)
            for (int x = 0; x <= grid; x++)
            {
                var local = rot * new Vector3((x / (float)grid - 0.5f) * size, 0f, (z / (float)grid - 0.5f) * size);
                var world = p + local;
                world.y = c.H(world) + lift;
                if (tilt > 0f)
                    world = p + Vector3.up * lift + tiltRot * (world - p - Vector3.up * lift);
                b.Vert(world, c.N(world), new Vector2(Mathf.Lerp(uv.xMin, uv.xMax, x / (float)grid), Mathf.Lerp(uv.yMin, uv.yMax, z / (float)grid)));
            }
            for (int z = 0; z < grid; z++)
            for (int x = 0; x < grid; x++)
            {
                int a = start + z * (grid + 1) + x;
                b.Quad(0, a, a + grid + 1, a + grid + 2, a + 1);
            }
        }

        /// <summary>
        /// A fern: seven to ten fronds springing up and out from one crown, each arching over and drooping at
        /// the tip, made of a bent strip of card showing the frond.
        /// </summary>
        internal static void Fern(Context c, Bits b, Vector3 p, float size, Rect uv)
        {
            int fronds = 7 + c.rng.Next(4);
            float yaw0 = c.R(0f, 360f);
            const int segments = 5;
            for (int f = 0; f < fronds; f++)
            {
                float yaw = yaw0 + f * 360f / fronds + c.R(-12f, 12f);
                var outward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                var side = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                float length = size * c.R(0.75f, 1.1f);
                float rise = c.R(50f, 72f);
                float droop = c.R(55f, 85f);
                float width = length * c.R(0.32f, 0.38f);
                var pos = p + Vector3.up * 0.02f;
                var dir = outward;
                int start = b.v.Count;
                for (int s = 0; s <= segments; s++)
                {
                    float t = s / (float)segments;
                    if (s > 0)
                        pos += dir * (length / segments);
                    float angle = (rise - droop * t * t) * Mathf.Deg2Rad;
                    dir = outward * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
                    pos.y = Mathf.Max(pos.y, c.H(pos) + 0.02f);
                    var normal = (Vector3.up * 0.75f + outward * 0.35f).normalized;
                    float half = width * 0.5f * (s == 0 ? 0.6f : 1f);
                    float v = Mathf.Lerp(uv.yMin, uv.yMax, t);
                    b.Vert(pos - side * half, normal, new Vector2(uv.xMin, v));
                    b.Vert(pos + side * half, normal, new Vector2(uv.xMax, v));
                }
                for (int s = 0; s < segments; s++)
                {
                    int a = start + s * 2;
                    b.Quad(0, a, a + 2, a + 3, a + 1);
                }
            }
        }

        /// <summary>A shrub: a low, rounded mass of leaf cards, shaded from its middle like a tree's crown.</summary>
        internal static void Shrub(Context c, Bits b, Vector3 p, float height)
        {
            var center = p + Vector3.up * height * 0.55f;
            var shadeCenter = center - Vector3.up * height * 0.3f;
            int cards = 9 + c.rng.Next(7);
            for (int k = 0; k < cards; k++)
            {
                var offset = new Vector3(c.R(-1f, 1f) * height * 0.55f, c.R(-1f, 1f) * height * 0.38f, c.R(-1f, 1f) * height * 0.55f);
                var at = center + offset;
                float size = Mathf.Clamp(c.R(0.45f, 0.75f) * height, 0.35f, 0.9f);
                at.y = Mathf.Max(at.y, c.H(at) + size * 0.35f);
                var facing = ((at - center).normalized * 0.6f + new Vector3(c.R(-1f, 1f), c.R(-1f, 1f), c.R(-1f, 1f)).normalized * 0.8f).normalized;
                var right = Vector3.Cross(facing, Mathf.Abs(facing.y) < 0.95f ? Vector3.up : Vector3.forward).normalized;
                var up = Vector3.Cross(right, facing);
                var spin = Quaternion.AngleAxis(c.R(0f, 360f), facing);
                right = spin * right * (size * 0.5f);
                up = spin * up * (size * 0.5f);
                var corners = new[] { at - right - up, at + right - up, at + right + up, at - right + up };
                var uvs = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
                int start = b.v.Count;
                for (int i = 0; i < 4; i++)
                    b.Vert(corners[i], ((corners[i] - shadeCenter).normalized + Vector3.up * 0.25f).normalized, uvs[i]);
                b.Quad(0, start, start + 1, start + 2, start + 3);
            }
        }

        /// <summary>A little cluster of brown-capped mushrooms on pale stems, some leaning.</summary>
        static void Mushrooms(Context c, Bits b, Vector3 p)
        {
            int count = 3 + c.rng.Next(5);
            for (int m = 0; m < count; m++)
            {
                var at = c.Ground(p + new Vector3(c.R(-0.18f, 0.18f), 0f, c.R(-0.18f, 0.18f)));
                float h = c.R(0.04f, 0.12f);
                float r = c.R(0.035f, 0.07f) * Mathf.Lerp(0.8f, 1.2f, h / 0.12f);
                var axis = (Vector3.up + new Vector3(c.R(-0.25f, 0.25f), 0f, c.R(-0.25f, 0.25f))).normalized;
                var basis = Quaternion.FromToRotation(Vector3.up, axis);
                var top = at + axis * h;
                const int sides = 7;

                // Stem.
                int stem = b.v.Count;
                for (int k = 0; k <= sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    var outward = basis * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    b.Vert(at - axis * 0.01f + outward * r * 0.28f, outward, Vector2.zero);
                    b.Vert(top + outward * r * 0.24f, outward, Vector2.zero);
                }
                for (int k = 0; k < sides; k++)
                {
                    int a = stem + k * 2;
                    b.Quad(1, a, a + 1, a + 3, a + 2);
                }

                // Cap: a squashed dome, with pale gills underneath.
                float[] rings = { 0f, 35f, 65f };
                int cap = b.v.Count;
                foreach (float e in rings)
                {
                    float er = e * Mathf.Deg2Rad;
                    for (int k = 0; k <= sides; k++)
                    {
                        float a = k * Mathf.PI * 2f / sides;
                        var local = new Vector3(Mathf.Cos(er) * Mathf.Cos(a) * r, Mathf.Sin(er) * r * 0.55f, Mathf.Cos(er) * Mathf.Sin(a) * r);
                        b.Vert(top + basis * local, basis * new Vector3(local.x, local.y * 2.5f, local.z).normalized, Vector2.zero);
                    }
                }
                int crown = b.Vert(top + axis * r * 0.55f, axis, Vector2.zero);
                for (int ring = 0; ring < rings.Length - 1; ring++)
                for (int k = 0; k < sides; k++)
                {
                    int a = cap + ring * (sides + 1) + k;
                    int up = a + sides + 1;
                    b.Quad(0, a, up, up + 1, a + 1);
                }
                int last = cap + (rings.Length - 1) * (sides + 1);
                for (int k = 0; k < sides; k++)
                    b.sub[0].AddRange(new[] { last + k, crown, last + k + 1 });
                int gills = b.Vert(top - axis * r * 0.05f, -axis, Vector2.zero);
                for (int k = 0; k < sides; k++)
                    b.sub[1].AddRange(new[] { cap + k + 1, gills, cap + k });
            }
        }

        // =====================================================================
        // Rocks and logs (solid)
        // =====================================================================

        static void Rocks(Context c, Transform parent, Material rock, Material moss)
        {
            var meshes = new Mesh[5];
            for (int i = 0; i < meshes.Length; i++)
                meshes[i] = RockMesh(c.s.prefix + "Rock_" + i, 300 + i * 17);

            var group = new GameObject("Rocks");
            group.transform.SetParent(parent, false);
            for (int attempt = 0; attempt < 140; attempt++)
            {
                var p = c.RandomSpot();
                if (c.InOpen(p, 1f) || c.PathDistance(p) < 1.4f || c.TreeDistance(p) < 1.4f || !c.Chance(0.5f))
                    continue;
                float size = c.Chance(0.75f) ? c.R(0.3f, 0.7f) : c.R(0.8f, 1.4f);
                var go = MeshObject("Mossy Rock", group.transform, meshes[c.rng.Next(meshes.Length)], rock);
                go.GetComponent<MeshRenderer>().sharedMaterials = new[] { rock, moss };
                go.transform.localPosition = p + Vector3.down * size * 0.25f;
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, c.N(p)) * Quaternion.Euler(c.R(-8f, 8f), c.R(0f, 360f), c.R(-8f, 8f));
                go.transform.localScale = new Vector3(size * c.R(0.9f, 1.3f), size * c.R(0.6f, 0.9f), size * c.R(0.9f, 1.3f));
            }
        }

        /// <summary>A lumpy boulder: a sphere pushed in and out by noise and flattened, with moss on its upper faces.</summary>
        internal static Mesh RockMesh(string name, int seed)
        {
            const int lon = 12, lat = 8;
            var verts = new List<Vector3> { Vector3.up };
            for (int j = 1; j < lat; j++)
            {
                float phi = Mathf.PI * j / lat;
                for (int i = 0; i < lon; i++)
                {
                    float theta = Mathf.PI * 2f * i / lon;
                    verts.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta)));
                }
            }
            verts.Add(Vector3.down);
            for (int i = 0; i < verts.Count; i++)
            {
                var d = verts[i];
                float noise = (Mathf.PerlinNoise(d.x * 1.3f + seed, d.y * 1.3f) + Mathf.PerlinNoise(d.y * 1.3f + seed, d.z * 1.3f)
                    + Mathf.PerlinNoise(d.z * 1.3f + seed, d.x * 1.3f)) / 3f - 0.5f;
                var p = d * (1f + noise * 0.7f) * 0.5f;
                p.y *= p.y < 0f ? 0.5f : 0.8f;
                verts[i] = p;
            }

            var tris = new List<int>();
            int bottom = verts.Count - 1;
            for (int i = 0; i < lon; i++)
                tris.AddRange(new[] { 0, 1 + (i + 1) % lon, 1 + i });
            for (int j = 0; j < lat - 2; j++)
            for (int i = 0; i < lon; i++)
            {
                int a = 1 + j * lon + i, b = 1 + j * lon + (i + 1) % lon;
                int c2 = a + lon, d2 = b + lon;
                tris.AddRange(new[] { a, b, d2, a, d2, c2 });
            }
            int lastRing = 1 + (lat - 2) * lon;
            for (int i = 0; i < lon; i++)
                tris.AddRange(new[] { bottom, lastRing + i, lastRing + (i + 1) % lon });

            var stone = new List<int>();
            var moss = new List<int>();
            for (int t = 0; t < tris.Count; t += 3)
            {
                var n = Vector3.Cross(verts[tris[t + 1]] - verts[tris[t]], verts[tris[t + 2]] - verts[tris[t]]).normalized;
                (n.y > 0.6f ? moss : stone).AddRange(new[] { tris[t], tris[t + 1], tris[t + 2] });
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(stone, 0);
            mesh.SetTriangles(moss, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMesh(mesh, name);
        }

        static void Logs(Context c, Transform parent, Material bark, Material moss, Material end)
        {
            var group = new GameObject("Fallen Logs");
            group.transform.SetParent(parent, false);
            var shadowMat = ContactShadowMaterial();
            int placed = 0;
            for (int attempt = 0; attempt < 200 && placed < 20; attempt++)
            {
                var p = c.RandomSpot();
                float length = c.R(2.5f, 5f);
                float radius = c.R(0.16f, 0.3f);
                float yaw = c.R(0f, 360f);
                var along = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                var a = c.Ground(p - along * length * 0.5f);
                var b = c.Ground(p + along * length * 0.5f);

                bool clear = !c.InOpen(p, length * 0.5f + 1f);
                for (float t = 0f; t <= 1f && clear; t += 0.25f)
                {
                    var q = Vector3.Lerp(a, b, t);
                    clear = c.PathDistance(q) > 1.8f && c.TreeDistance(q) > 0.9f && c.LogDistance(q) > 1.5f;
                }
                if (!clear)
                    continue;

                var mesh = LogMesh($"{c.s.prefix}Log_{placed}", length, radius, c.rng);
                var log = MeshObject("Fallen Log", group.transform, mesh, bark);
                log.GetComponent<MeshRenderer>().sharedMaterials = new[] { bark, moss, end };
                float pitch = Mathf.Atan2(b.y - a.y, length) * Mathf.Rad2Deg;
                log.transform.localPosition = (a + b) * 0.5f + Vector3.up * radius * 0.6f;
                log.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, pitch) * Quaternion.Euler(c.R(0f, 360f), 0f, 0f);

                // A long, soft shadow under the whole log (the round contact patches don't suit long things).
                var blob = CreatePrimitive(PrimitiveType.Quad, "Log Shadow", group.transform, shadowMat, false);
                var blobRenderer = blob.GetComponent<Renderer>();
                blobRenderer.shadowCastingMode = ShadowCastingMode.Off;
                blobRenderer.receiveShadows = false;
                var center = c.Ground(p);
                blob.transform.localPosition = center + Vector3.up * 0.03f;
                blob.transform.localRotation = Quaternion.FromToRotation(Vector3.up, c.N(center)) * Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f);
                blob.transform.localScale = new Vector3(length * 1.15f, radius * 5f, 1f);

                c.logs.Add((a, b));
                placed++;
            }
        }

        /// <summary>
        /// A fallen log along local X: a slightly sagging, uneven tube of bark (moss growing on its upper side),
        /// cut ends showing pale wood, and a couple of broken branch stubs.
        /// </summary>
        static Mesh LogMesh(string name, float length, float radius, System.Random rng)
        {
            float Rr(float min, float max) => min + (float)rng.NextDouble() * (max - min);
            const int sides = 10, rings = 7;
            var verts = new List<Vector3>();
            var barkTris = new List<int>();
            var mossTris = new List<int>();
            var endTris = new List<int>();
            float seed = Rr(0f, 50f);
            float mossSeed = Rr(0f, 50f);

            for (int j = 0; j < rings; j++)
            {
                float t = j / (rings - 1f);
                float x = (t - 0.5f) * length;
                float sag = -Mathf.Sin(t * Mathf.PI) * length * 0.015f;
                float r = radius * Mathf.Lerp(1f, 0.82f, t);
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    float bumpy = 1f + 0.08f * (Mathf.PerlinNoise(Mathf.Cos(a) + seed, Mathf.Sin(a) + x * 0.8f) * 2f - 1f);
                    verts.Add(new Vector3(x, sag + Mathf.Sin(a) * r * bumpy, Mathf.Cos(a) * r * bumpy));
                }
            }
            for (int j = 0; j < rings - 1; j++)
            for (int i = 0; i < sides; i++)
            {
                int a = j * sides + i, b = j * sides + (i + 1) % sides;
                int c = a + sides, d = b + sides;
                foreach (var tri in new[] { new[] { a, c, d }, new[] { a, d, b } })
                {
                    var n = Vector3.Cross(verts[tri[1]] - verts[tri[0]], verts[tri[2]] - verts[tri[0]]).normalized;
                    var mid = (verts[tri[0]] + verts[tri[1]] + verts[tri[2]]) / 3f;
                    bool mossy = n.y > 0.35f && Mathf.PerlinNoise(mid.x * 0.7f + mossSeed, 0.5f) > 0.35f;
                    (mossy ? mossTris : barkTris).AddRange(tri);
                }
            }

            // Cut ends, as fans of their own vertices so they get flat normals.
            for (int e = 0; e < 2; e++)
            {
                int ring = e == 0 ? 0 : rings - 1;
                var center = Vector3.zero;
                for (int i = 0; i < sides; i++)
                    center += verts[ring * sides + i];
                center /= sides;
                int c0 = verts.Count;
                verts.Add(center);
                for (int i = 0; i < sides; i++)
                    verts.Add(verts[ring * sides + i]);
                for (int i = 0; i < sides; i++)
                {
                    int a = c0 + 1 + i, b = c0 + 1 + (i + 1) % sides;
                    endTris.AddRange(e == 0 ? new[] { c0, a, b } : new[] { c0, b, a });
                }
            }

            // Broken branch stubs.
            int stubs = 1 + rng.Next(2);
            for (int s = 0; s < stubs; s++)
            {
                float x = Rr(-0.3f, 0.3f) * length;
                float a = Rr(0.2f, 1.2f) * (rng.NextDouble() < 0.5 ? 1f : -1f) + Mathf.PI * 0.5f;
                var outward = new Vector3(Rr(-0.3f, 0.3f), Mathf.Sin(a), Mathf.Cos(a)).normalized;
                var basePos = new Vector3(x, 0f, 0f);
                var tipPos = basePos + outward * (radius + Rr(0.15f, 0.35f));
                var side = Vector3.Cross(outward, Vector3.right).normalized;
                var up = Vector3.Cross(side, outward);
                float sr = radius * Rr(0.25f, 0.4f);
                int start = verts.Count;
                for (int ringIndex = 0; ringIndex < 2; ringIndex++)
                {
                    var at = ringIndex == 0 ? basePos : tipPos;
                    float r = ringIndex == 0 ? sr : sr * 0.7f;
                    for (int i = 0; i < 6; i++)
                    {
                        float b = i * Mathf.PI / 3f;
                        verts.Add(at + (side * Mathf.Cos(b) + up * Mathf.Sin(b)) * r);
                    }
                }
                for (int i = 0; i < 6; i++)
                {
                    int p0 = start + i, p1 = start + (i + 1) % 6;
                    barkTris.AddRange(new[] { p0, p0 + 6, p1 + 6, p0, p1 + 6, p1 });
                }
                int tipCenter = verts.Count;
                verts.Add(tipPos);
                for (int i = 0; i < 6; i++)
                    endTris.AddRange(new[] { tipCenter, start + 6 + (i + 1) % 6, start + 6 + i });
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(barkTris, 0);
            mesh.SetTriangles(mossTris, 1);
            mesh.SetTriangles(endTris, 2);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMesh(mesh, name);
        }
    }
}
