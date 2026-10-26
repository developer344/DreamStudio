using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static DreamStudio.Editor.DreamBuildUtils;
using Bits = DreamStudio.Editor.DreamUnderstory.Bits;
using Context = DreamStudio.Editor.DreamUnderstory.Context;

namespace DreamStudio.Editor
{
    /// <summary>
    /// Grows a natural sea bottom on top of a rippled sand seabed. Large, soft patches of the floor take on
    /// different characters: kelp forests (giant kelp swaying from boulders), reefs (branching, brain and
    /// table corals, tube sponges, sea fans and sea whips over shell rubble) and open sand (seagrass
    /// meadows). Boulders capped with algae, tufts of seaweed, patches of turf algae, starfish and sea
    /// urchins are scattered through all of them.
    ///
    /// Like <see cref="DreamUnderstory"/>, the small things are cards from one texture atlas
    /// (<see cref="DreamTextures.ReefAtlas"/>) merged into one mesh per 20 m square; boulders and corals are
    /// solid objects reusing a few generated mesh variants; each kelp strand sways on its own.
    /// </summary>
    public static class DreamReef
    {
        class Solid
        {
            public Vector3 position;
            public float radius;
        }

        /// <summary>Builds the sea bottom and returns where its big pieces stand, so other things can keep clear.</summary>
        public static List<Vector3> Build(Transform parent, DreamUnderstory.Settings settings, Material rockMat)
        {
            var kelpSpots = new List<Vector3>();
            settings.trees = kelpSpots; // "shade" on the sea floor is under the kelp
            var c = DreamUnderstory.NewContext(settings);
            var solids = new List<Solid>();
            var root = new GameObject("Sea Floor");
            root.transform.SetParent(parent, false);

            var plants = DreamTrees.Cutout("Reef_Plants", new Color(0.95f, 0.97f, 0.95f), DreamTextures.ReefAtlas());
            var algae = Lit("Reef_Algae", new Color(0.42f, 0.44f, 0.24f), 0.1f, new Color(0.01f, 0.012f, 0.005f));
            var corals = new[]
            {
                Lit("Reef_Coral_Ochre", new Color(0.82f, 0.66f, 0.45f), 0.2f, Color.black),
                Lit("Reef_Coral_Violet", new Color(0.62f, 0.46f, 0.66f), 0.2f, Color.black),
                Lit("Reef_Coral_Rose", new Color(0.86f, 0.58f, 0.56f), 0.2f, Color.black),
                Lit("Reef_Coral_Olive", new Color(0.62f, 0.62f, 0.42f), 0.2f, Color.black),
            };
            var brains = new[]
            {
                Lit("Reef_Brain_Tan", new Color(0.78f, 0.68f, 0.5f), 0.25f, Color.black),
                Lit("Reef_Brain_Green", new Color(0.55f, 0.6f, 0.42f), 0.25f, Color.black),
            };
            var sponges = new[]
            {
                Lit("Reef_Sponge_Orange", new Color(0.8f, 0.45f, 0.2f), 0.1f, Color.black),
                Lit("Reef_Sponge_Purple", new Color(0.5f, 0.3f, 0.5f), 0.1f, Color.black),
                Lit("Reef_Sponge_Yellow", new Color(0.8f, 0.7f, 0.3f), 0.1f, Color.black),
            };
            var spongeInside = Lit("Reef_Sponge_Inside", new Color(0.2f, 0.12f, 0.1f), 0.05f, Color.black);
            var starfish = Lit("Reef_Starfish", new Color(0.85f, 0.42f, 0.22f), 0.2f, Color.black);
            var urchin = Lit("Reef_Urchin", new Color(0.16f, 0.09f, 0.2f), 0.3f, Color.black);

            float Kelpy(Vector3 p) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.PerlinNoise(p.x / 24f + 71.3f, p.z / 24f + 5.1f) - 0.48f) / 0.16f));
            float Reefy(Vector3 p) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.PerlinNoise(p.x / 16f + 13.7f, p.z / 16f + 88.2f) - 0.5f) / 0.16f)) * (1f - 0.7f * Kelpy(p));
            bool Clear(Vector3 p, float radius)
            {
                foreach (var s in solids)
                    if (new Vector2(p.x - s.position.x, p.z - s.position.z).magnitude < s.radius + radius)
                        return false;
                return true;
            }
            GameObject Place(Transform group, string name, Mesh mesh, Material[] mats, Vector3 p, Quaternion rot, Vector3 scale, float footprint)
            {
                var go = MeshObject(name, group, mesh, mats[0]);
                go.GetComponent<MeshRenderer>().sharedMaterials = mats;
                go.transform.localPosition = p;
                go.transform.localRotation = rot;
                go.transform.localScale = scale;
                solids.Add(new Solid { position = p, radius = footprint });
                return go;
            }

            // --- Boulders, crowded where kelp holds fast and in the reefs, capped with algae.
            var rockMeshes = new Mesh[5];
            for (int i = 0; i < rockMeshes.Length; i++)
                rockMeshes[i] = DreamUnderstory.RockMesh($"ReefRock_{i}", 500 + i * 23);
            var boulders = Group("Boulders", root.transform);
            for (int attempt = 0; attempt < 260; attempt++)
            {
                var p = c.RandomSpot();
                float size = c.Chance(0.7f) ? c.R(0.6f, 1.4f) : c.R(1.6f, 2.8f);
                if (c.InOpen(p, size * 0.6f) || !Clear(p, size * 0.6f) || !c.Chance(0.2f + 0.5f * Mathf.Max(Kelpy(p), Reefy(p))))
                    continue;
                Place(boulders, "Boulder", rockMeshes[c.rng.Next(rockMeshes.Length)], new[] { rockMat, algae }, p + Vector3.down * size * 0.25f,
                    Quaternion.FromToRotation(Vector3.up, c.N(p)) * Quaternion.Euler(c.R(-8f, 8f), c.R(0f, 360f), c.R(-8f, 8f)),
                    new Vector3(size * c.R(0.9f, 1.4f), size * c.R(0.55f, 0.85f), size * c.R(0.9f, 1.4f)), size * 0.55f);
            }

            // --- Giant kelp forests, each strand swaying slowly from its holdfast.
            var kelpMeshes = new Mesh[8];
            for (int i = 0; i < kelpMeshes.Length; i++)
                kelpMeshes[i] = KelpMesh($"ReefKelp_{i}", Mathf.Lerp(3.5f, 8.5f, i / (kelpMeshes.Length - 1f)), new System.Random(800 + i * 31));
            var kelpForest = Group("Kelp Forest", root.transform);
            for (int attempt = 0; attempt < 1600; attempt++)
            {
                var p = c.RandomSpot();
                if (c.InOpen(p, 1f) || !c.Chance(Kelpy(p) * 0.5f) || !Clear(p, 0.2f))
                    continue;
                bool crowded = false;
                foreach (var k in kelpSpots)
                    if (new Vector2(p.x - k.x, p.z - k.z).sqrMagnitude < 1.4f)
                    {
                        crowded = true;
                        break;
                    }
                if (crowded)
                    continue;
                var holdfast = Group("Giant Kelp", kelpForest);
                holdfast.localPosition = p + Vector3.down * 0.05f;
                holdfast.localRotation = Quaternion.Euler(0f, c.R(0f, 360f), 0f);
                var sway = holdfast.gameObject.AddComponent<DreamSway>();
                sway.angle = c.R(4f, 8f);
                sway.speed = c.R(0.07f, 0.14f);
                // Tall kelp casts long, swaying shadows over the sand.
                MeshObject("Strand", holdfast, kelpMeshes[c.rng.Next(kelpMeshes.Length)], plants);
                kelpSpots.Add(p);
            }

            // --- Reef corals and sponges.
            var coralGroup = Group("Corals", root.transform);
            var branching = new Mesh[6];
            for (int i = 0; i < branching.Length; i++)
                branching[i] = BranchingCoralMesh($"ReefStaghorn_{i}", new System.Random(900 + i * 41));
            var tables = new Mesh[3];
            for (int i = 0; i < tables.Length; i++)
                tables[i] = TableCoralMesh($"ReefTable_{i}", new System.Random(950 + i * 43));
            var spongeMeshes = new Mesh[4];
            for (int i = 0; i < spongeMeshes.Length; i++)
                spongeMeshes[i] = SpongeMesh($"ReefSponge_{i}", new System.Random(990 + i * 47));
            var brainMeshes = new Mesh[3];
            for (int i = 0; i < brainMeshes.Length; i++)
                brainMeshes[i] = DreamUnderstory.RockMesh($"ReefBrain_{i}", 700 + i * 29);

            for (int attempt = 0; attempt < 1400; attempt++)
            {
                var p = c.RandomSpot();
                float reef = Reefy(p);
                if (c.InOpen(p, 0.8f) || !c.Chance(reef * 0.4f))
                    continue;
                var yaw = Quaternion.Euler(0f, c.R(0f, 360f), 0f);
                double kind = c.rng.NextDouble();
                if (kind < 0.4)
                {
                    float s = c.R(0.6f, 1.3f);
                    if (Clear(p, s * 0.35f))
                        Place(coralGroup, "Staghorn Coral", branching[c.rng.Next(branching.Length)], new[] { corals[c.rng.Next(corals.Length)] },
                            p + Vector3.down * 0.03f, yaw, Vector3.one * s, s * 0.35f);
                }
                else if (kind < 0.65)
                {
                    float s = c.R(0.5f, 1.2f);
                    if (Clear(p, s * 0.5f))
                    {
                        var mat = brains[c.rng.Next(brains.Length)];
                        Place(coralGroup, "Brain Coral", brainMeshes[c.rng.Next(brainMeshes.Length)], new[] { mat, mat }, p + Vector3.down * s * 0.12f,
                            Quaternion.FromToRotation(Vector3.up, c.N(p)) * yaw, new Vector3(s, s * c.R(0.5f, 0.7f), s * c.R(0.85f, 1.1f)), s * 0.5f);
                    }
                }
                else if (kind < 0.78)
                {
                    float s = c.R(0.7f, 1.3f);
                    if (Clear(p, s * 0.6f))
                        Place(coralGroup, "Table Coral", tables[c.rng.Next(tables.Length)], new[] { corals[c.rng.Next(corals.Length)] }, p, yaw, Vector3.one * s, s * 0.35f);
                }
                else
                {
                    float s = c.R(0.7f, 1.3f);
                    if (Clear(p, s * 0.25f))
                        Place(coralGroup, "Tube Sponges", spongeMeshes[c.rng.Next(spongeMeshes.Length)], new[] { sponges[c.rng.Next(sponges.Length)], spongeInside },
                            p + Vector3.down * 0.03f, yaw, Vector3.one * s, s * 0.25f);
                }
            }

            // --- Small things, merged per 20 m square.
            const float currentYaw = 30f; // sea fans grow across the current
            DreamUnderstory.Scatter(c, 13000, p => c.InOpen(p) ? 0.05f : (0.08f + 0.8f * c.Meadow(p)) * (1f - Kelpy(p)) * (1f - Reefy(p)), p =>
                DreamUnderstory.Crossed(c, c.At(p).Layer("Plants"), p, c.R(0.3f, 0.5f), c.R(0.35f, 0.75f), 3, DreamTextures.Reef.Seagrass));
            DreamUnderstory.Scatter(c, 3200, p => c.InOpen(p) ? 0f : 0.12f + 0.45f * Mathf.Max(Kelpy(p), Reefy(p)), p =>
                DreamUnderstory.Crossed(c, c.At(p).Layer("Plants"), p, c.R(0.3f, 0.5f), c.R(0.25f, 0.45f), 2,
                    c.Chance(0.5f) ? DreamTextures.Reef.RedWeed : DreamTextures.Reef.Wrack));
            DreamUnderstory.Scatter(c, 2200, p => c.InOpen(p) ? 0f : Reefy(p) * 0.45f, p =>
                DreamUnderstory.Crossed(c, c.At(p).Layer("Plants"), p, c.R(0.3f, 0.45f), c.R(0.5f, 0.9f), 2, DreamTextures.Reef.SeaWhip));
            DreamUnderstory.Scatter(c, 1800, p => c.InOpen(p) || !Clear(p, 0.1f) ? 0f : Reefy(p) * 0.35f, p =>
            {
                float s = c.R(0.5f, 1.2f);
                Upright(c, c.At(p).Layer("Plants"), p, s, s, currentYaw + c.R(-25f, 25f), DreamTextures.Reef.SeaFan);
            });
            DreamUnderstory.Scatter(c, 1100, p => c.InOpen(p) ? 0f : 0.12f + 0.4f * Mathf.Max(Kelpy(p), Reefy(p)), p =>
                DreamUnderstory.Flat(c, c.At(p).Layer("Plants"), p, c.R(0.9f, 2.2f), c.R(0f, 360f), 0.012f + c.R(0f, 0.004f), DreamTextures.Reef.Algae, 3));
            DreamUnderstory.Scatter(c, 1000, p => c.InOpen(p) ? 0f : 0.12f + 0.45f * Reefy(p), p =>
                DreamUnderstory.Flat(c, c.At(p).Layer("Plants"), p, c.R(0.9f, 1.8f), c.R(0f, 360f), 0.018f + c.R(0f, 0.004f), DreamTextures.Reef.Rubble, 3));
            DreamUnderstory.Scatter(c, 700, p => c.InOpen(p) ? 0f : 0.1f + 0.25f * Reefy(p), p => Starfish(c, c.At(p).Layer("Critters", 2), p));
            DreamUnderstory.Scatter(c, 900, p => c.InOpen(p) ? 0f : 0.04f + 0.45f * Mathf.Max(Kelpy(p), Reefy(p)), p =>
            {
                int count = 1 + c.rng.Next(4);
                for (int u = 0; u < count; u++)
                    Urchin(c, c.At(p).Layer("Critters", 2), c.Ground(p + new Vector3(c.R(-0.3f, 0.3f), 0f, c.R(-0.3f, 0.3f))));
            });

            DreamUnderstory.EmitChunks(c, root.transform, new Dictionary<string, Material[]>
            {
                ["Plants"] = new[] { plants },
                ["Critters"] = new[] { starfish, urchin },
            });

            var spots = new List<Vector3>(kelpSpots);
            foreach (var s in solids)
                spots.Add(s.position);
            return spots;
        }

        static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // =====================================================================
        // Cards
        // =====================================================================

        /// <summary>One upright, double-sided card facing along <paramref name="yaw"/> (a sea fan across the current).</summary>
        static void Upright(Context c, Bits b, Vector3 p, float width, float height, float yaw, Rect uv)
        {
            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * (width * 0.5f);
            var normal = (Vector3.up + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 0.5f).normalized;
            var b0 = c.Ground(p - dir) + Vector3.down * 0.03f;
            var b1 = c.Ground(p + dir) + Vector3.down * 0.03f;
            var lean = new Vector3(c.R(-0.05f, 0.05f), 0f, c.R(-0.05f, 0.05f)) * height;
            int i0 = b.Vert(b0, normal, new Vector2(uv.xMin, uv.yMin));
            int i1 = b.Vert(b1, normal, new Vector2(uv.xMax, uv.yMin));
            int i2 = b.Vert(b1 + Vector3.up * height + lean, normal, new Vector2(uv.xMax, uv.yMax));
            int i3 = b.Vert(b0 + Vector3.up * height + lean, normal, new Vector2(uv.xMin, uv.yMax));
            b.Quad(0, i0, i3, i2, i1);
        }

        /// <summary>A starfish lying on the sand: five tapering arms, slightly domed.</summary>
        static void Starfish(Context c, Bits b, Vector3 p)
        {
            float radius = c.R(0.07f, 0.14f);
            float spin = c.R(0f, Mathf.PI * 2f);
            var up = c.N(p);
            int center = b.Vert(p + up * 0.025f, up, Vector2.zero);
            int first = b.v.Count;
            for (int k = 0; k < 10; k++)
            {
                bool tip = k % 2 == 0;
                float a = spin + k * Mathf.PI / 5f;
                float r = tip ? radius * c.R(0.85f, 1.1f) : radius * 0.32f;
                var at = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                at.y = c.H(at) + (tip ? 0.006f : 0.016f);
                b.Vert(at, up, Vector2.zero);
            }
            for (int k = 0; k < 10; k++)
                b.sub[0].AddRange(new[] { center, first + (k + 1) % 10, first + k });
        }

        /// <summary>A sea urchin: a dark, low ball bristling with long spines.</summary>
        static void Urchin(Context c, Bits b, Vector3 p)
        {
            float r = c.R(0.04f, 0.07f);
            var center = p + Vector3.up * r * 0.6f;
            // The body: an octahedron, squashed.
            var dirs = new[] { Vector3.up, Vector3.right, Vector3.forward, Vector3.left, Vector3.back, Vector3.down };
            int start = b.v.Count;
            foreach (var d in dirs)
                b.Vert(center + Vector3.Scale(d, new Vector3(r, r * 0.75f, r)), d, Vector2.zero);
            int[] ring = { 1, 2, 3, 4 };
            for (int k = 0; k < 4; k++)
            {
                int a = start + ring[k], n = start + ring[(k + 1) % 4];
                b.sub[1].AddRange(new[] { start, n, a });
                b.sub[1].AddRange(new[] { start + 5, a, n });
            }
            // Spines: thin three-sided spikes radiating out of the upper body.
            for (int s = 0; s < 18; s++)
            {
                var dir = new Vector3(c.R(-1f, 1f), c.R(-0.2f, 1f), c.R(-1f, 1f)).normalized;
                var root = center + dir * r * 0.8f;
                var tip = center + dir * (r + c.R(0.06f, 0.11f));
                var side = Vector3.Cross(dir, Vector3.up).normalized;
                if (side.sqrMagnitude < 0.01f)
                    side = Vector3.right;
                var other = Vector3.Cross(dir, side);
                const float w = 0.006f;
                int t = b.Vert(tip, dir, Vector2.zero);
                int a0 = b.Vert(root + side * w, side, Vector2.zero);
                int a1 = b.Vert(root + (-side * 0.5f + other * 0.87f) * w, other, Vector2.zero);
                int a2 = b.Vert(root + (-side * 0.5f - other * 0.87f) * w, -other, Vector2.zero);
                b.sub[1].AddRange(new[] { t, a0, a1, t, a1, a2, t, a2, a0 });
            }
        }

        // =====================================================================
        // Meshes
        // =====================================================================

        /// <summary>
        /// A giant kelp strand <paramref name="height"/> tall: a thin, wandering stipe with blades (each with a
        /// gas bladder at its base) springing up and out along it, and a canopy of drooping blades at the top.
        /// All one alpha-cut card mesh; its origin is the holdfast.
        /// </summary>
        static Mesh KelpMesh(string name, float height, System.Random rng)
        {
            float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);
            var b = new Bits(1);
            var stipe = DreamTextures.Reef.KelpStipe;
            var blade = DreamTextures.Reef.KelpBlade;

            const int segments = 10;
            var spine = new Vector3[segments + 1];
            float phase = R(0f, 10f);
            for (int k = 0; k <= segments; k++)
            {
                float t = k / (float)segments;
                spine[k] = new Vector3(Mathf.Sin(t * 3f + phase) * 0.25f * t, t * height, Mathf.Cos(t * 2.3f + phase) * 0.25f * t);
            }
            Vector3 SpineAt(float t)
            {
                float x = Mathf.Clamp01(t) * segments;
                int i = Mathf.Min(Mathf.FloorToInt(x), segments - 1);
                return Vector3.Lerp(spine[i], spine[i + 1], x - i);
            }

            // The stipe: two crossed thin strips.
            for (int s = 0; s < 2; s++)
            {
                var across = (s == 0 ? Vector3.right : Vector3.forward) * 0.025f;
                int start = b.v.Count;
                for (int k = 0; k <= segments; k++)
                {
                    float v = Mathf.Lerp(stipe.yMin, stipe.yMax, k % 2);
                    b.Vert(spine[k] - across, Vector3.up, new Vector2(stipe.xMin, v));
                    b.Vert(spine[k] + across, Vector3.up, new Vector2(stipe.xMax, v));
                }
                for (int k = 0; k < segments; k++)
                {
                    int a = start + k * 2;
                    b.Quad(0, a, a + 2, a + 3, a + 1);
                }
            }

            void Blade(Vector3 at, Vector3 dir, float length, float droop)
            {
                var flat = Vector3.Cross(dir, Vector3.up);
                if (flat.sqrMagnitude < 1e-3f)
                    flat = Vector3.right;
                var widthAxis = Quaternion.AngleAxis(R(-50f, 50f), dir) * flat.normalized * (length * 0.14f);
                var mid = at + dir * length * 0.5f;
                var tip = mid + (dir + Vector3.down * droop).normalized * length * 0.5f;
                var normal = (Vector3.up * 0.6f + new Vector3(dir.x, 0f, dir.z) * 0.4f).normalized;
                var rows = new[] { (at, 0f), (mid, 0.5f), (tip, 1f) };
                int start = b.v.Count;
                foreach (var (point, t) in rows)
                {
                    float v = Mathf.Lerp(blade.yMin, blade.yMax, t);
                    float w = t == 0f ? 0.4f : 1f;
                    b.Vert(point - widthAxis * w, normal, new Vector2(blade.xMin, v));
                    b.Vert(point + widthAxis * w, normal, new Vector2(blade.xMax, v));
                }
                for (int k = 0; k < 2; k++)
                {
                    int a = start + k * 2;
                    b.Quad(0, a, a + 2, a + 3, a + 1);
                }
            }

            int blades = Mathf.RoundToInt(height * 4.5f);
            float yaw = R(0f, 360f);
            for (int j = 0; j < blades; j++)
            {
                float t = 0.08f + 0.85f * j / blades;
                yaw += 137.5f + R(-20f, 20f);
                var outward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                var dir = (outward * 0.7f + Vector3.up * R(0.6f, 0.9f)).normalized;
                Blade(SpineAt(t), dir, R(0.5f, 0.85f), R(0.2f, 0.45f));
            }
            for (int j = 0; j < 9; j++)
            {
                var outward = Quaternion.Euler(0f, j * 40f + R(-15f, 15f), 0f) * Vector3.forward;
                Blade(spine[segments], (outward + Vector3.up * R(0.1f, 0.4f)).normalized, R(0.7f, 1.05f), R(0.5f, 0.9f));
            }
            return b.ToMesh(name);
        }

        /// <summary>A tube between spine points, rings carried along without twisting, with a rounded tip.</summary>
        static void Tube(Bits b, int sub, IList<Vector3> spine, IList<float> radii, int sides)
        {
            int start = b.v.Count;
            var t0 = (spine[1] - spine[0]).normalized;
            var normal = Vector3.Cross(t0, Mathf.Abs(t0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            for (int i = 0; i < spine.Count; i++)
            {
                var tangent = (i == 0 ? spine[1] - spine[0] : i == spine.Count - 1 ? spine[i] - spine[i - 1] : spine[i + 1] - spine[i - 1]).normalized;
                normal = Vector3.ProjectOnPlane(normal, tangent).normalized;
                var binormal = Vector3.Cross(tangent, normal);
                for (int k = 0; k <= sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    var outward = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    b.Vert(spine[i] + outward * radii[i], outward, Vector2.zero);
                }
            }
            int stride = sides + 1;
            for (int i = 0; i < spine.Count - 1; i++)
            for (int k = 0; k < sides; k++)
            {
                int a = start + i * stride + k, c = a + stride;
                b.sub[sub].AddRange(new[] { a, a + 1, c, a + 1, c + 1, c });
            }
            // Round off the tip.
            var end = spine[spine.Count - 1];
            var dir = (end - spine[spine.Count - 2]).normalized;
            int tip = b.Vert(end + dir * radii[radii.Count - 1], dir, Vector2.zero);
            int last = start + (spine.Count - 1) * stride;
            for (int k = 0; k < sides; k++)
                b.sub[sub].AddRange(new[] { last + k, last + k + 1, tip });
        }

        /// <summary>Staghorn coral: a few stout stems from one base, forking twice into upturned, blunt-tipped branches.</summary>
        static Mesh BranchingCoralMesh(string name, System.Random rng)
        {
            float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);
            var b = new Bits(1);
            void Branch(Vector3 from, Vector3 dir, float length, float radius, int depth)
            {
                var mid = from + dir * length * 0.5f + new Vector3(R(-0.02f, 0.02f), 0f, R(-0.02f, 0.02f));
                var upturned = (dir + Vector3.up * 0.25f).normalized;
                var end = mid + upturned * length * 0.5f;
                Tube(b, 0, new[] { from, mid, end }, new[] { radius, radius * 0.9f, radius * 0.8f }, 5);
                if (depth == 0)
                    return;
                int forks = 2;
                for (int f = 0; f < forks; f++)
                {
                    var axis = Vector3.Cross(upturned, new Vector3(R(-1f, 1f), R(-1f, 1f), R(-1f, 1f))).normalized;
                    var child = Quaternion.AngleAxis(R(22f, 40f), axis) * upturned;
                    Branch(end - upturned * radius * 0.5f, child, length * R(0.6f, 0.8f), radius * 0.8f, depth - 1);
                }
            }
            int stems = 3 + rng.Next(3);
            float yaw = R(0f, 360f);
            for (int s = 0; s < stems; s++)
            {
                yaw += 360f / stems + R(-25f, 25f);
                var dir = Quaternion.Euler(-R(35f, 70f), yaw, 0f) * Vector3.forward;
                Branch(new Vector3(0f, -0.04f, 0f), dir, R(0.18f, 0.3f), R(0.035f, 0.05f), 2);
            }
            return b.ToMesh(name);
        }

        /// <summary>Table coral: a short stalk carrying a broad, flat plate with a wavy, slightly lifted rim.</summary>
        static Mesh TableCoralMesh(string name, System.Random rng)
        {
            float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);
            var b = new Bits(1);
            float stalk = R(0.22f, 0.38f);
            Tube(b, 0, new[] { new Vector3(0f, -0.05f, 0f), new Vector3(0f, stalk * 0.5f, 0f), new Vector3(0f, stalk, 0f) }, new[] { 0.1f, 0.08f, 0.09f }, 7);

            const int sides = 18;
            float plate = R(0.45f, 0.65f);
            int waves = 4 + rng.Next(4);
            float wavePhase = R(0f, 6f);
            var centerTop = new Vector3(0f, stalk + 0.03f, 0f);
            float[] rings = { 0.35f, 0.75f, 1f };
            var firsts = new int[2];
            for (int face = 0; face < 2; face++)
            {
                float lift = face == 0 ? 0.02f : -0.02f;
                var normal = face == 0 ? Vector3.up : Vector3.down;
                int center = b.Vert(centerTop + Vector3.up * lift, normal, Vector2.zero);
                int first = b.v.Count;
                firsts[face] = first;
                foreach (float ring in rings)
                    for (int k = 0; k < sides; k++)
                    {
                        float a = k * Mathf.PI * 2f / sides;
                        float r = plate * ring * (1f + 0.1f * ring * Mathf.Sin(a * waves + wavePhase));
                        b.Vert(centerTop + new Vector3(Mathf.Cos(a) * r, lift + ring * ring * 0.08f, Mathf.Sin(a) * r), normal, Vector2.zero);
                    }
                for (int k = 0; k < sides; k++)
                {
                    int a = first + k, n = first + (k + 1) % sides;
                    b.sub[0].AddRange(face == 0 ? new[] { center, n, a } : new[] { center, a, n });
                }
                for (int ring = 0; ring < rings.Length - 1; ring++)
                for (int k = 0; k < sides; k++)
                {
                    int a = first + ring * sides + k, n = first + ring * sides + (k + 1) % sides;
                    int a2 = a + sides, n2 = n + sides;
                    b.sub[0].AddRange(face == 0 ? new[] { a, n, n2, a, n2, a2 } : new[] { a, n2, n, a, a2, n2 });
                }
            }
            // The plate's edge, joining the top face to the bottom.
            int topRim = firsts[0] + (rings.Length - 1) * sides, bottomRim = firsts[1] + (rings.Length - 1) * sides;
            for (int k = 0; k < sides; k++)
            {
                int ta = topRim + k, tn = topRim + (k + 1) % sides, ba = bottomRim + k, bn = bottomRim + (k + 1) % sides;
                b.sub[0].AddRange(new[] { ta, bn, ba, ta, tn, bn });
            }
            return b.ToMesh(name);
        }

        /// <summary>A clump of three to five tube sponges: open-topped tubes, flaring a little, dark inside.</summary>
        static Mesh SpongeMesh(string name, System.Random rng)
        {
            float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);
            var b = new Bits(2);
            int tubes = 3 + rng.Next(3);
            const int sides = 9;
            for (int t = 0; t < tubes; t++)
            {
                var foot = new Vector3(R(-0.15f, 0.15f), -0.05f, R(-0.15f, 0.15f));
                float h = R(0.3f, 0.8f);
                float r = R(0.06f, 0.1f);
                var axis = (Vector3.up + new Vector3(foot.x, 0f, foot.z) * 1.5f).normalized;
                var basis = Quaternion.FromToRotation(Vector3.up, axis);
                float[] heights = { 0f, 0.5f, 1f };
                float[] widths = { 1f, 0.92f, 1.12f };
                int outer = b.v.Count;
                foreach (var (y, w) in new[] { (heights[0], widths[0]), (heights[1], widths[1]), (heights[2], widths[2]) })
                    for (int k = 0; k <= sides; k++)
                    {
                        float a = k * Mathf.PI * 2f / sides;
                        var dir = basis * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        b.Vert(foot + axis * (y * h) + dir * r * w, dir, Vector2.zero);
                    }
                int inner = b.v.Count;
                foreach (var (y, w) in new[] { (0.15f, 0.8f), (0.6f, 0.75f), (1f, 0.95f) })
                    for (int k = 0; k <= sides; k++)
                    {
                        float a = k * Mathf.PI * 2f / sides;
                        var dir = basis * new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        b.Vert(foot + axis * (y * h) + dir * r * w, -dir, Vector2.zero);
                    }
                int stride = sides + 1;
                for (int i = 0; i < 2; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a = outer + i * stride + k, c = a + stride;
                    b.sub[0].AddRange(new[] { a, c, a + 1, a + 1, c, c + 1 });
                    int ia = inner + i * stride + k, ic = ia + stride;
                    b.sub[1].AddRange(new[] { ia, ia + 1, ic, ia + 1, ic + 1, ic });
                }
                // The lip joining outside to inside.
                int outerTop = outer + 2 * stride, innerTop = inner + 2 * stride;
                for (int k = 0; k < sides; k++)
                    b.sub[0].AddRange(new[] { outerTop + k, innerTop + k, outerTop + k + 1, outerTop + k + 1, innerTop + k, innerTop + k + 1 });
                // A dark floor deep inside.
                int floorCenter = b.Vert(foot + axis * (0.15f * h), axis, Vector2.zero);
                for (int k = 0; k < sides; k++)
                    b.sub[1].AddRange(new[] { floorCenter, inner + k + 1, inner + k });
            }
            return b.ToMesh(name);
        }
    }
}
