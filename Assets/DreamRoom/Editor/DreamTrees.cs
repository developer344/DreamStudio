using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static DreamStudio.Editor.DreamBuildUtils;

namespace DreamStudio.Editor
{
    /// <summary>
    /// Procedural trees with a natural build. The trunk leans and wanders, flares into buttress roots at
    /// its foot and tapers as it climbs; limbs leave it at staggered heights, fork into smaller branches and
    /// turn up toward the light; the crown is made of leaf cards (alpha-cut quads showing clusters of
    /// leaves) whose normals point out from the crown's middle, so the whole crown shades as one rounded mass
    /// instead of flat cards. Dead trees are bare, crooked and twiggy, with a few broken limbs.
    ///
    /// A handful of variants are generated once as mesh assets and every tree in a world reuses one, turned
    /// and scaled, so hundreds of trees stay cheap to draw on Quest. Each tree is three renderers: the trunk
    /// (the only part near the ground, with a capsule collider), the branches and the foliage.
    /// </summary>
    public static class DreamTrees
    {
        public class Variant
        {
            public Mesh trunk;
            public Mesh branches;
            public Mesh foliage;      // null for a dead tree
            public float trunkRadius; // around chest height, for the collider
            public bool dead;
        }

        class Shape
        {
            public float height, radius, lean, wobble, flare;
            public int limbs;
            public float limbStart, limbEnd;            // fraction of the height where limbs leave the trunk
            public float limbLength;                    // fraction of the height
            public float limbRiseMin, limbRiseMax;      // degrees above horizontal
            public float upturn;                        // how strongly limbs curve up toward their tips
            public float crooked;                       // random kinks along limbs
            public int depth;                           // levels of forking below the limbs
            public float leafSize;                      // leaf card size (m); 0 for no leaves
        }

        // =====================================================================
        // Variants
        // =====================================================================

        /// <summary>
        /// Leafy broadleaf trees of three builds, taken in turn: spreading (long, low-angled limbs and a wide
        /// crown), upright (steeper, shorter limbs and a taller crown) and lopsided (wandering limbs). Limbs
        /// leave the trunk around a third of the way up, so the crown is deep rather than a ball on a stick,
        /// while staying above head height.
        /// </summary>
        public static Variant[] LeafyVariants(string prefix, int count)
        {
            var variants = new Variant[count];
            for (int i = 0; i < count; i++)
            {
                var rng = new System.Random(7001 + i * 131);
                int kind = i % 3;
                var shape = new Shape
                {
                    height = R(rng, 6.4f, 7.6f),
                    radius = R(rng, 0.24f, 0.32f),
                    lean = R(rng, 0.15f, 0.45f),
                    wobble = R(rng, 0.25f, 0.4f),
                    flare = R(rng, 0.3f, 0.42f),
                    limbs = kind == 1 ? 7 : 6,
                    limbStart = kind == 1 ? 0.34f : 0.3f,
                    limbEnd = kind == 1 ? 0.86f : 0.82f,
                    limbLength = kind == 0 ? 0.55f : kind == 1 ? 0.38f : 0.5f,
                    limbRiseMin = kind == 0 ? 12f : kind == 1 ? 35f : 18f,
                    limbRiseMax = kind == 0 ? 35f : kind == 1 ? 55f : 42f,
                    upturn = kind == 1 ? 0.3f : 0.2f,
                    crooked = kind == 2 ? 0.1f : 0.05f,
                    depth = 2,
                    leafSize = R(rng, 1.15f, 1.45f),
                };
                variants[i] = Generate($"{prefix}_{i}", shape, rng, false);
            }
            return variants;
        }

        /// <summary>Bare, crooked dead trees with twiggy crowns and a few broken limbs.</summary>
        public static Variant[] DeadVariants(string prefix, int count)
        {
            var variants = new Variant[count];
            for (int i = 0; i < count; i++)
            {
                var rng = new System.Random(9001 + i * 173);
                var shape = new Shape
                {
                    height = R(rng, 5.5f, 7f),
                    radius = R(rng, 0.2f, 0.28f),
                    lean = R(rng, 0.3f, 0.7f),
                    wobble = R(rng, 0.4f, 0.6f),
                    flare = R(rng, 0.35f, 0.5f),
                    limbs = 5,
                    limbStart = 0.38f,
                    limbEnd = 0.9f,
                    limbLength = 0.4f,
                    limbRiseMin = 15f,
                    limbRiseMax = 55f,
                    upturn = 0.12f,
                    crooked = 0.16f,
                    depth = 3,
                    leafSize = 0f,
                };
                variants[i] = Generate($"{prefix}_{i}", shape, rng, true);
            }
            return variants;
        }

        /// <summary>An alpha-cut, double-sided leaf material showing the generated leaf clusters.</summary>
        public static Material Foliage(string name, Color tint, bool blossom) => Cutout(name, tint, DreamTextures.LeafCards(blossom));

        /// <summary>An alpha-cut, double-sided Lit material for plants drawn on cards.</summary>
        public static Material Cutout(string name, Color tint, Texture2D texture)
        {
            var mat = Lit(name, tint, 0.15f, Color.black);
            mat.SetTexture("_BaseMap", texture);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.45f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_AlphaToMask", 1f); // smoother leaf edges with MSAA on Quest
            mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.doubleSidedGI = true;
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)RenderQueue.AlphaTest;
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>One tree: a variant turned to <paramref name="yaw"/> and scaled, standing at <paramref name="position"/>.</summary>
        public static GameObject Place(Transform parent, Variant variant, Vector3 position, float yaw, float scale, Material bark, Material foliage)
        {
            var tree = new GameObject(variant.dead ? "Dead Tree" : "Tree");
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = position;
            tree.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            tree.transform.localScale = Vector3.one * scale;

            var trunk = MeshObject("Trunk", tree.transform, variant.trunk, bark);
            var collider = trunk.AddComponent<CapsuleCollider>();
            collider.radius = variant.trunkRadius;
            collider.height = 3.2f;
            collider.center = new Vector3(0f, 1.6f, 0f);
            MeshObject("Branches", tree.transform, variant.branches, bark);
            if (variant.foliage != null && foliage != null)
                MeshObject("Foliage", tree.transform, variant.foliage, foliage);
            return tree;
        }

        // =====================================================================
        // Generation
        // =====================================================================

        class Builder
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector2> uvs = new List<Vector2>();
            public readonly List<int> triangles = new List<int>();

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (vertices.Count > 65000)
                    mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return SaveMesh(mesh, name);
            }
        }

        static Variant Generate(string name, Shape s, System.Random rng, bool dead)
        {
            var trunkB = new Builder();
            var branchB = new Builder();
            var leafB = new Builder();
            var clusters = new List<Vector3>();
            float seed = R(rng, 0f, 100f);

            // --- The trunk's spine: rings packed close at the foot (for the flare), then evenly up to the top.
            var leanDir = Quaternion.Euler(0f, R(rng, 0f, 360f), 0f) * Vector3.forward;
            float top = s.height * (dead ? 0.8f : 0.92f);
            var heights = new List<float> { -0.4f, 0f, 0.12f, 0.3f, 0.6f, 1f, 1.6f };
            for (int k = 1; k <= 6; k++)
                heights.Add(Mathf.Lerp(1.6f, top, k / 6f));

            var spine = new List<Vector3>();
            var radii = new List<float>();
            foreach (float y in heights)
            {
                float t = Mathf.Clamp01(y / s.height);
                var offset = leanDir * (s.lean * Mathf.Pow(t, 1.5f))
                    + new Vector3(Mathf.PerlinNoise(seed, y * 0.3f) - 0.5f, 0f, Mathf.PerlinNoise(seed + 7f, y * 0.3f) - 0.5f) * (s.wobble * t * 2f);
                spine.Add(new Vector3(offset.x, y, offset.z));
                float taper = Mathf.Lerp(1f, dead ? 0.22f : 0.12f, Mathf.Pow(t, 0.85f));
                radii.Add(s.radius * taper * (1f + s.flare * Mathf.Exp(-Mathf.Max(y, 0f) / 0.35f)));
            }

            int lobes = 4 + rng.Next(3);
            float lobePhase = R(rng, 0f, Mathf.PI * 2f);
            Tube(trunkB, spine, radii, 10, (ring, a) =>
            {
                float y = Mathf.Max(spine[ring].y, 0f);
                float buttress = 0.4f * Mathf.Exp(-y / 0.6f) * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(lobes * a + lobePhase)), 3f);
                float bumps = 0.07f * (Mathf.PerlinNoise(Mathf.Cos(a) * 1.3f + seed, Mathf.Sin(a) * 1.3f + y * 0.8f) * 2f - 1f);
                return 1f + buttress + bumps;
            });

            // --- Limbs at staggered heights around the trunk, each forking into smaller branches.
            float azimuth = R(rng, 0f, 360f);
            for (int i = 0; i < s.limbs; i++)
            {
                float f = Mathf.Lerp(s.limbStart, s.limbEnd, (i + R(rng, 0.1f, 0.9f)) / s.limbs);
                float y = f * s.height;
                var start = SpineAt(spine, y);
                azimuth += 137.5f + R(rng, -25f, 25f);
                float rise = R(rng, s.limbRiseMin, s.limbRiseMax) + (f - 0.5f) * 20f;
                var dir = Quaternion.Euler(-rise, azimuth, 0f) * Vector3.forward;
                float length = s.height * s.limbLength * Mathf.Lerp(1.1f, 0.65f, (f - s.limbStart) / Mathf.Max(0.01f, s.limbEnd - s.limbStart)) * R(rng, 0.85f, 1.15f);
                float radius = RadiusAt(spine, radii, y) * R(rng, 0.5f, 0.62f);
                bool broken = dead && rng.NextDouble() < 0.25;
                Branch(branchB, clusters, rng, s, start, dir, broken ? length * 0.35f : length, radius, 0, broken ? 0 : s.depth);
            }

            // A leader at the top of a living tree, so the crown has a middle.
            if (!dead)
            {
                var crownTop = spine[spine.Count - 1];
                Branch(branchB, clusters, rng, s, crownTop - Vector3.up * 0.3f, (Vector3.up + leanDir * 0.2f).normalized, s.height * 0.18f, radii[radii.Count - 1] * 1.4f, 1, s.depth);
            }

            // --- The crown: leaf cards around every branch tip, shaded as one rounded mass.
            if (!dead && clusters.Count > 0)
            {
                var center = Vector3.zero;
                foreach (var c in clusters)
                    center += c;
                center /= clusters.Count;
                center -= Vector3.up * 0.8f; // tops of the crown catch more light than the underside
                foreach (var c in clusters)
                {
                    int cards = 2 + rng.Next(2);
                    for (int k = 0; k < cards; k++)
                    {
                        var at = c + new Vector3(R(rng, -0.3f, 0.3f), R(rng, -0.2f, 0.3f), R(rng, -0.3f, 0.3f));
                        var facing = (at - center).normalized * 0.6f + Random3(rng) * 0.8f;
                        LeafCard(leafB, at, facing.normalized, s.leafSize * R(rng, 0.8f, 1.2f), R(rng, 0f, 360f), center, rng.NextDouble() < 0.5);
                    }
                }
            }

            return new Variant
            {
                trunk = trunkB.ToMesh($"{name}_Trunk"),
                branches = branchB.ToMesh($"{name}_Branches"),
                foliage = dead || leafB.vertices.Count == 0 ? null : leafB.ToMesh($"{name}_Foliage"),
                trunkRadius = RadiusAt(spine, radii, 1.2f) * 1.05f,
                dead = dead,
            };
        }

        /// <summary>
        /// One branch: a tapering, gently curving tube that turns up toward its tip (or kinks, for a dead
        /// tree), sprouting smaller branches along its outer part until <paramref name="maxDepth"/>.
        /// Leafy branch tips are recorded in <paramref name="clusters"/> for the crown.
        /// </summary>
        static void Branch(Builder b, List<Vector3> clusters, System.Random rng, Shape s, Vector3 start, Vector3 dir, float length, float radius, int depth, int maxDepth)
        {
            int points = depth == 0 ? 6 : 4;
            var spine = new List<Vector3>();
            var radii = new List<float>();
            var p = start;
            var d = dir;
            float step = length / (points - 1);
            for (int i = 0; i < points; i++)
            {
                float f = i / (points - 1f);
                spine.Add(p);
                radii.Add(Mathf.Max(0.008f, radius * Mathf.Lerp(1f, 0.25f, f)));
                d = (d + Vector3.up * s.upturn * 0.25f + Random3(rng) * s.crooked * 2f).normalized;
                p += d * step;
            }
            Tube(b, spine, radii, depth == 0 ? 6 : depth == 1 ? 5 : 3, null);

            if (depth < maxDepth)
            {
                int children = depth == 0 ? 2 + rng.Next(2) : 1 + rng.Next(2);
                for (int c = 0; c < children; c++)
                {
                    float f = R(rng, 0.4f, 0.85f);
                    var from = PointAlong(spine, f);
                    var axis = Vector3.Cross(dir, Random3(rng)).normalized;
                    var childDir = (Quaternion.AngleAxis(R(rng, 28f, 50f), axis) * dir + Vector3.up * 0.15f).normalized;
                    float childRadius = Mathf.Max(0.01f, radius * Mathf.Lerp(1f, 0.25f, f) * 0.7f);
                    Branch(b, clusters, rng, s, from, childDir, length * R(rng, 0.45f, 0.62f), childRadius, depth + 1, maxDepth);
                }
            }

            if (s.leafSize > 0f && depth >= 1)
            {
                clusters.Add(spine[spine.Count - 1]);
                if (depth == 1)
                    clusters.Add(PointAlong(spine, 0.6f));
            }
        }

        /// <summary>A square leaf card facing <paramref name="facing"/>, its normals pointing out of the crown.</summary>
        static void LeafCard(Builder b, Vector3 at, Vector3 facing, float size, float roll, Vector3 crownCenter, bool flip)
        {
            var right = Vector3.Cross(facing, Mathf.Abs(facing.y) < 0.95f ? Vector3.up : Vector3.forward).normalized;
            var up = Vector3.Cross(right, facing);
            var spin = Quaternion.AngleAxis(roll, facing);
            right = spin * right * (size * 0.5f);
            up = spin * up * (size * 0.5f);

            int start = b.vertices.Count;
            var corners = new[] { at - right - up, at + right - up, at + right + up, at - right + up };
            var uv = flip
                ? new[] { new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) }
                : new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            for (int i = 0; i < 4; i++)
            {
                b.vertices.Add(corners[i]);
                b.normals.Add(((corners[i] - crownCenter).normalized + Vector3.up * 0.25f).normalized);
                b.uvs.Add(uv[i]);
            }
            b.triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
        }

        /// <summary>
        /// A tube along a spine: rings carried along the spine without twisting, outward-facing, with an
        /// optional per-vertex radius multiplier <paramref name="shape"/>(ring, angle) for flares and bumps.
        /// </summary>
        static void Tube(Builder b, List<Vector3> spine, List<float> radii, int sides, System.Func<int, float, float> shape)
        {
            int start = b.vertices.Count;
            var t0 = (spine[1] - spine[0]).normalized;
            var normal = Vector3.Cross(t0, Mathf.Abs(t0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            float along = 0f;
            float uvScale = 1f / Mathf.Max(0.05f, radii[0] * Mathf.PI * 2f);
            for (int i = 0; i < spine.Count; i++)
            {
                var tangent = i == 0 ? spine[1] - spine[0]
                    : i == spine.Count - 1 ? spine[i] - spine[i - 1]
                    : spine[i + 1] - spine[i - 1];
                tangent.Normalize();
                normal = Vector3.ProjectOnPlane(normal, tangent).normalized;
                var binormal = Vector3.Cross(tangent, normal);
                if (i > 0)
                    along += Vector3.Distance(spine[i], spine[i - 1]);
                for (int k = 0; k <= sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    var outward = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    float r = radii[i] * (shape != null ? shape(i, a) : 1f);
                    b.vertices.Add(spine[i] + outward * r);
                    b.normals.Add(outward);
                    b.uvs.Add(new Vector2(k / (float)sides, along * uvScale));
                }
            }
            int stride = sides + 1;
            for (int i = 0; i < spine.Count - 1; i++)
            for (int k = 0; k < sides; k++)
            {
                int a = start + i * stride + k;
                int c = a + stride;
                b.triangles.AddRange(new[] { a, a + 1, c, a + 1, c + 1, c });
            }
        }

        static Vector3 SpineAt(List<Vector3> spine, float y)
        {
            for (int i = 1; i < spine.Count; i++)
                if (spine[i].y >= y)
                    return Vector3.Lerp(spine[i - 1], spine[i], Mathf.InverseLerp(spine[i - 1].y, spine[i].y, y));
            return spine[spine.Count - 1];
        }

        static float RadiusAt(List<Vector3> spine, List<float> radii, float y)
        {
            for (int i = 1; i < spine.Count; i++)
                if (spine[i].y >= y)
                    return Mathf.Lerp(radii[i - 1], radii[i], Mathf.InverseLerp(spine[i - 1].y, spine[i].y, y));
            return radii[radii.Count - 1];
        }

        static Vector3 PointAlong(List<Vector3> spine, float f)
        {
            float x = f * (spine.Count - 1);
            int i = Mathf.Min(Mathf.FloorToInt(x), spine.Count - 2);
            return Vector3.Lerp(spine[i], spine[i + 1], x - i);
        }

        static float R(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        static Vector3 Random3(System.Random rng)
        {
            var v = new Vector3(R(rng, -1f, 1f), R(rng, -1f, 1f), R(rng, -1f, 1f));
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.up;
        }
    }
}
