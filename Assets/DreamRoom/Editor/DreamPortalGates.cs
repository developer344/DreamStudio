using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static DreamStudio.Editor.DreamBuildUtils;
using Bits = DreamStudio.Editor.DreamUnderstory.Bits;

namespace DreamStudio.Editor
{
    /// <summary>
    /// The portals, after the portal concept sheets: weathered stone gates on worn steps, overgrown with vines,
    /// carved with glowing glyphs in the colour of their nightmare. The gate itself is the frame: the portal's
    /// surface fills the shape inside its stones (<see cref="SpecFor"/>), and in the Portal Room the wall
    /// opening behind follows the gate's outline, with a closed alcove behind it (a dark backdrop, not open sky):
    ///   - Haunted Forest: a triangle of stacked stone blocks with rows of small violet triangles, and a large
    ///     violet triangle hanging in a triangular portal.
    ///   - Sunken Depths: a ring of dark boulders round an oval portal of deep ocean blue, a blue diamond glyph.
    ///   - Glass Garden: two pale columns and a cracked lintel with an amber square glyph, round a rectangular
    ///     portal of sunset amber marked with a square on a stem, on straight steps among fallen rubble.
    /// Inside the worlds, the portal home is a broken ring of stones, some floating free, glowing gold
    /// (<see cref="WayHome"/>).
    ///
    /// Gates are built in the portal's local space (origin at the centre of the portal's oval, +Z into the
    /// room) and parented to it; the alcove is built in the wall's local space.
    /// </summary>
    static class DreamPortalGates
    {
        const float AlcoveDepth = 0.45f;

        class Mats
        {
            public Material dark = Lit("Gate_DarkStone", new Color(0.22f, 0.23f, 0.25f), 0.25f, Color.black);
            public Material pale = Lit("Gate_PaleStone", new Color(0.82f, 0.8f, 0.75f), 0.2f, new Color(0.02f, 0.02f, 0.02f));
            public Material moss = Lit("Gate_Moss", new Color(0.28f, 0.42f, 0.22f), 0.05f, new Color(0.02f, 0.05f, 0.02f));
            public Material vine = Lit("Gate_Vine", new Color(0.22f, 0.32f, 0.18f), 0.1f, Color.black);
            // Glyph colours, one per nightmare: haunted violet, deep-sea blue, sunset amber, and the gold of home.
            public Material violet = Lit("Gate_Glyph_Violet", new Color(0.8f, 0.6f, 1f), 0.5f, new Color(1.5f, 0.7f, 2.4f));
            public Material blue = Lit("Gate_Glyph_Blue", new Color(0.4f, 0.75f, 1f), 0.5f, new Color(0.4f, 1.3f, 2.6f));
            public Material amber = Lit("Gate_Glyph_Amber", new Color(1f, 0.65f, 0.35f), 0.5f, new Color(2.4f, 1.15f, 0.4f));
            public Material yellow = Lit("Gate_Glyph_Yellow", new Color(1f, 0.85f, 0.35f), 0.5f, new Color(2.2f, 1.7f, 0.45f));
            public Mesh[] rocks;

            public Mats()
            {
                rocks = new Mesh[4];
                for (int i = 0; i < rocks.Length; i++)
                    rocks[i] = DreamUnderstory.RockMesh($"GateRock_{i}", 900 + i * 37);
            }
        }

        // =====================================================================
        // Shapes
        // =====================================================================

        /// <summary>A gate's measurements: its portal's shape and size, and the wall opening behind it.</summary>
        public class Spec
        {
            /// <summary>The portal surface: 0 an ellipse, 1 a rectangle, 2 a triangle pointing up.</summary>
            public int shape;
            public Vector2 half;
            /// <summary>Height of the portal's centre above the floor.</summary>
            public float center;
            /// <summary>Half the width of the wall opening behind the gate...</summary>
            public float openingHalf;
            /// <summary>...its top edge across that width (height above the floor at x)...</summary>
            public System.Func<float, float> openingTop;
            /// <summary>...and its bottom edge (0: down to the floor).</summary>
            public System.Func<float, float> openingBottom = x => 0f;
            /// <summary>Height of the world's name on the wall, clear of the gate.</summary>
            public float labelHeight;
        }

        /// <summary>The measurements of a world's gate. Every surface sits just inside its stones.</summary>
        public static Spec SpecFor(string scene)
        {
            switch (scene)
            {
                case "HauntedForest": // a triangle, its inner edges from (±2.7, 0.3) up to (0, 4.9)
                    return new Spec { shape = 2, half = new Vector2(2.72f, 2.3f), center = 2.6f, openingHalf = 3.22f,
                        openingTop = x => Mathf.Max(0f, 5.49f - 1.704f * Mathf.Abs(x)), labelHeight = 5.95f };
                case "GlassGarden": // a rectangle between the columns and under the lintel; the opening hides behind the columns
                    return new Spec { shape = 1, half = new Vector2(1.36f, 1.95f), center = 2.25f, openingHalf = 1.75f,
                        openingTop = x => 4.25f, labelHeight = 5.15f };
                default: // an oval filling a ring of stones; the opening follows the ring all round
                    return new Spec { shape = 0, half = new Vector2(1.32f, 1.68f), center = 1.95f, openingHalf = 1.75f,
                        openingTop = x => 1.95f + 2.15f * Mathf.Sqrt(Mathf.Max(0f, 1f - x * x / (1.75f * 1.75f))),
                        openingBottom = x => Mathf.Max(0f, 1.95f - 2.15f * Mathf.Sqrt(Mathf.Max(0f, 1f - x * x / (1.75f * 1.75f)))),
                        labelHeight = 4.85f };
            }
        }

        // =====================================================================
        // Portal Room gates
        // =====================================================================

        public static void Build(Transform wall, Transform portal, int worldIndex, float wallThickness, Spec spec)
        {
            var world = Worlds[worldIndex];
            var m = new Mats();
            var rng = new System.Random(4111 + world.seed);

            // A closed alcove behind the opening, so it shows a dark backdrop rather than open sky.
            var alcove = new GameObject("Alcove").transform;
            alcove.SetParent(wall, false);
            Alcove(alcove, spec.openingHalf, spec.openingTop(0f), -wallThickness * 0.5f, 0.3f,
                Lit($"Gate_Back_{world.scene}", Color.Lerp(world.outer, Color.black, 0.75f), 0.4f, Color.Lerp(world.outer, Color.black, 0.6f)), m.dark);

            var frame = new GameObject("Gate").transform;
            frame.SetParent(portal, false);
            float floor = -spec.center;
            switch (world.scene)
            {
                case "HauntedForest": TriangleGate(frame, floor, m, rng); break;
                case "SunkenDepths": BoulderRing(frame, floor, m, rng); break;
                case "GlassGarden": ColumnGate(frame, floor, m, rng); break;
            }
            PointLight(frame, "Gate Light", new Vector3(0f, 0.3f, 1.2f), Color.Lerp(world.inner, world.outer, 0.5f), 4.5f, 0.9f);
        }

        /// <summary>
        /// The closed recess behind the opening: a solid back panel, side linings, a ceiling and a floor level
        /// with the portal's foot. Built in wall space (+Z into the room).
        /// </summary>
        static void Alcove(Transform root, float half, float top, float back, float stepTop, Material backing, Material lining)
        {
            float zMid = back - AlcoveDepth * 0.5f;
            float zPanel = back - AlcoveDepth - 0.05f;
            Box("Backing", root, backing, new Vector3(0f, top * 0.5f, zPanel), new Vector3(half * 2f + 0.2f, top + 0.1f, 0.1f));
            for (int side = -1; side <= 1; side += 2)
                Box("Alcove Side", root, lining, new Vector3(side * (half + 0.05f), top * 0.5f, zMid), new Vector3(0.1f, top, AlcoveDepth), false);
            Box("Alcove Ceiling", root, lining, new Vector3(0f, top + 0.05f, zMid), new Vector3(half * 2f + 0.2f, 0.1f, AlcoveDepth), false);
            Box("Alcove Floor", root, lining, new Vector3(0f, stepTop * 0.5f, zMid), new Vector3(half * 2f, stepTop, AlcoveDepth), false);
        }

        /// <summary>Haunted Forest: a triangle of stacked stone blocks, glyph-carved, over curved steps, hung with vines.</summary>
        static void TriangleGate(Transform f, float floor, Mats m, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float baseY = floor + 0.3f;
            var apex = new Vector2(0f, floor + 4.9f);
            const float thick = 0.45f, depth = 0.55f, z = 0.35f;

            foreach (int side in new[] { -1, 1 })
            {
                var foot = new Vector2(side * 2.7f, baseY);
                var along = (apex - foot).normalized;
                // Perpendicular to the side, pointing away from the triangle's middle.
                var outward = new Vector2(-along.y, along.x);
                if (Vector2.Dot(outward, foot - new Vector2(0f, baseY + 1.5f)) < 0f)
                    outward = -outward;
                float length = Vector2.Distance(foot, apex);
                const int blocks = 4;
                for (int b = 0; b < blocks; b++)
                {
                    float t0 = b / (float)blocks, t1 = (b + 1) / (float)blocks;
                    float span = (t1 - t0) * length - 0.05f;
                    var mid = foot + along * ((t0 + t1) * 0.5f * length) + outward * (thick * 0.5f + R(-0.02f, 0.03f));
                    float angle = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg + R(-2f, 2f);
                    var block = Box("Block", f, m.dark, new Vector3(mid.x, mid.y, z + R(-0.03f, 0.03f)), new Vector3(span, thick, depth), false);
                    block.transform.localRotation = Quaternion.Euler(R(-2f, 2f), R(-3f, 3f), angle);

                    // Rows of small glowing triangles on the upper blocks; a single one on the lowest.
                    var face = new Vector3(mid.x, mid.y, z + depth * 0.5f + 0.006f);
                    int glyphs = b == 0 ? 1 : b >= 2 ? 3 : 0;
                    for (int g = 0; g < glyphs; g++)
                    {
                        var offset = along * ((g - (glyphs - 1) * 0.5f) * 0.2f);
                        Triangle(f, m.violet, face + new Vector3(offset.x, offset.y, 0f), 0.11f, angle + (side > 0 ? 180f : 0f), 0.018f);
                    }
                }
                // The foot of each side: a heavy block half sunk in the grass.
                Box("Foot Block", f, m.dark, new Vector3(side * 2.75f, floor + 0.25f, z), new Vector3(0.8f, 0.5f, 0.75f), false)
                    .transform.localRotation = Quaternion.Euler(0f, R(-6f, 6f), R(-4f, 4f));
                Triangle(f, m.violet, new Vector3(side * 2.75f, floor + 0.3f, z + 0.38f), 0.1f, side > 0 ? 0f : 180f, 0.018f);
            }
            // A capstone at the apex, with an upward chevron.
            Box("Capstone", f, m.dark, new Vector3(0f, apex.y + 0.3f, z), new Vector3(0.5f, 0.45f, depth + 0.05f), false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Bar(f, m.violet, new Vector3(-0.09f, apex.y + 0.26f, z + 0.32f), new Vector3(0f, apex.y + 0.36f, z + 0.32f), 0.02f, 0.01f);
            Bar(f, m.violet, new Vector3(0.09f, apex.y + 0.26f, z + 0.32f), new Vector3(0f, apex.y + 0.36f, z + 0.32f), 0.02f, 0.01f);

            // The great teal triangle hanging in the portal.
            Triangle(f, m.violet, new Vector3(0f, 0.15f, 0.06f), 0.95f, 0f, 0.035f);

            CurvedSteps(f, m.dark, floor, 0.1f, 3f, 1.5f);
            for (int v = 0; v < 4; v++)
            {
                float t = R(0.35f, 0.95f);
                int side = v % 2 == 0 ? -1 : 1;
                var start = Vector2.Lerp(new Vector2(side * 2.95f, baseY), apex, t);
                Strand(f, m.vine, new Vector3(start.x, start.y, z + 0.3f), R(0.4f, 1.1f), rng);
            }
            Vine(f, m.vine, new Vector3(-0.3f, apex.y - 0.05f, z + 0.3f), new Vector3(-1.75f, floor + 2.6f, z + 0.32f), 0.25f, rng);
        }

        /// <summary>Sunken Depths: a ring of dark, moss-capped boulders round a teal rim, a diamond glyph at its heart.</summary>
        static void BoulderRing(Transform f, float floor, Mats m, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            for (int i = 0; i <= 10; i++)
            {
                float a = (-50f + i * 28f) * Mathf.Deg2Rad;
                var at = new Vector3(Mathf.Cos(a) * 1.62f, Mathf.Sin(a) * 2.02f, 0.3f);
                float s = R(0.85f, 1.05f);
                Rock(f, m, m.rocks[rng.Next(m.rocks.Length)], at, new Vector3(s * 1.05f, s, s * 0.9f), new Vector3(R(-20f, 20f), R(0f, 360f), R(-20f, 20f)));
            }
            // The diamond glyph, outlined twice.
            var d = new[] { new Vector3(0f, 0.55f, 0.06f), new Vector3(0.36f, 0f, 0.06f), new Vector3(0f, -0.55f, 0.06f), new Vector3(-0.36f, 0f, 0.06f) };
            for (int k = 0; k < 4; k++)
            {
                Bar(f, m.blue, d[k], d[(k + 1) % 4], 0.035f, 0.012f);
                Bar(f, m.blue, d[k] * 0.55f + Vector3.forward * 0.027f, d[(k + 1) % 4] * 0.55f + Vector3.forward * 0.027f, 0.02f, 0.012f);
            }

            CurvedSteps(f, m.dark, floor, 0.1f, 2.8f, 1.4f);
            for (int v = 0; v < 5; v++)
            {
                float a = R(10f, 170f) * Mathf.Deg2Rad;
                Strand(f, m.vine, new Vector3(Mathf.Cos(a) * 1.75f, Mathf.Sin(a) * 2.1f - 0.3f, 0.65f), R(0.4f, 1f), rng);
            }
            Vine(f, m.vine, new Vector3(-0.6f, 2.35f, 0.6f), new Vector3(0.8f, 2.25f, 0.62f), 0.3f, rng);
            Vine(f, m.vine, new Vector3(-1.75f, 0.4f, 0.62f), new Vector3(-1.45f, -1.1f, 0.65f), 0.15f, rng);
        }

        /// <summary>
        /// Glass Garden: two weathered pale columns and a cracked lintel bearing a cyan square, framing a cyan
        /// panel marked with a square on a stem; straight steps, fallen rubble, a vine climbing one column.
        /// </summary>
        static void ColumnGate(Transform f, float floor, Mats m, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float baseY = floor + 0.3f;
            float lintelY = floor + 4.25f;
            var shaft = DreamPalace.Lathe("GateColumnShaft", new[]
            {
                new Vector2(0.27f, 0f), new Vector2(0.27f, 0.08f), new Vector2(0.23f, 0.14f), new Vector2(0.22f, 1.8f),
                new Vector2(0.2f, 3.3f), new Vector2(0.24f, 3.38f), new Vector2(0.24f, 3.45f), new Vector2(0f, 3.45f),
            }, 16, true);
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * 1.6f;
                Box("Column Plinth", f, m.pale, new Vector3(x, baseY + 0.2f, 0.35f), new Vector3(0.7f, 0.4f, 0.7f), false);
                var col = MeshObject("Column", f, shaft, m.pale);
                col.transform.localPosition = new Vector3(x, baseY + 0.4f, 0.35f);
                col.transform.localRotation = Quaternion.Euler(R(-1f, 1f), R(0f, 360f), R(-1f, 1f));
                Box("Capital", f, m.pale, new Vector3(x, lintelY - 0.1f, 0.35f), new Vector3(0.66f, 0.2f, 0.66f), false);
                // Rubble at the column's foot.
                for (int r = 0; r < 3; r++)
                {
                    float s = R(0.35f, 0.75f);
                    Rock(f, m, m.rocks[rng.Next(m.rocks.Length)], new Vector3(x + side * R(0.2f, 0.9f), floor + s * 0.25f, R(0.4f, 1.3f)),
                        new Vector3(s, s * 0.7f, s), new Vector3(R(-15f, 15f), R(0f, 360f), R(-15f, 15f)), m.pale);
                }
            }
            var lintel = Box("Lintel", f, m.pale, new Vector3(0f, lintelY + 0.28f, 0.35f), new Vector3(4.1f, 0.56f, 0.72f), false);
            lintel.transform.localRotation = Quaternion.Euler(0f, 0f, R(-1.5f, 1.5f));
            float face = 0.35f + 0.36f + 0.006f;
            Square(f, m.amber, new Vector3(0f, lintelY + 0.28f, face), 0.24f, 0.025f);
            // Glowing cracks running out from the glyph.
            Bar(f, m.amber, new Vector3(-0.85f, lintelY + 0.4f, face), new Vector3(-1.1f, lintelY + 0.2f, face), 0.012f, 0.01f);
            Bar(f, m.amber, new Vector3(-1.1f, lintelY + 0.2f, face), new Vector3(-1.35f, lintelY + 0.36f, face), 0.012f, 0.01f);
            Bar(f, m.amber, new Vector3(1.2f, lintelY + 0.45f, face), new Vector3(1.45f, lintelY + 0.25f, face), 0.012f, 0.01f);

            // The portal fills the space between the columns; on it, a square on a stem with a dot below.
            Square(f, m.amber, new Vector3(0f, 0.3f, 0.06f), 0.62f, 0.035f);
            Bar(f, m.amber, new Vector3(0f, -0.01f, 0.06f), new Vector3(0f, -0.55f, 0.06f), 0.03f, 0.012f);
            var dot = CreatePrimitive(PrimitiveType.Sphere, "Glyph Dot", f, m.amber, false);
            dot.transform.localPosition = new Vector3(0f, -0.63f, 0.06f);
            dot.transform.localScale = new Vector3(0.1f, 0.1f, 0.03f);
            dot.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Straight, worn steps, one block fallen across them.
            for (int s = 0; s < 3; s++)
            {
                float w = 4.4f - s * 0.35f, front = 1.35f - s * 0.33f;
                Box("Step", f, m.pale, new Vector3(0f, floor + 0.05f + s * 0.1f, (front - 0.2f) * 0.5f), new Vector3(w, 0.1f, front + 0.2f), false);
            }
            Box("Fallen Block", f, m.pale, new Vector3(R(1.7f, 2.1f), floor + 0.25f, R(0.9f, 1.3f)), new Vector3(0.9f, 0.45f, 0.5f), false)
                .transform.localRotation = Quaternion.Euler(R(-8f, 8f), R(20f, 50f), R(-12f, 12f));

            // A vine winding up the left column, and a few strands from the lintel.
            var helix = new List<Vector3>();
            for (int k = 0; k <= 24; k++)
            {
                float t = k / 24f;
                float a = t * Mathf.PI * 5f;
                helix.Add(new Vector3(-1.6f + Mathf.Cos(a) * 0.27f, baseY + 0.4f + t * 3.3f, 0.35f + Mathf.Sin(a) * 0.27f));
            }
            Tube(f, m.vine, helix, 0.022f, 5);
            for (int v = 0; v < 3; v++)
                Strand(f, m.vine, new Vector3(R(-1.8f, 1.8f), lintelY, 0.75f), R(0.3f, 0.8f), rng);
        }

        // =====================================================================
        // The portal home, inside the worlds
        // =====================================================================

        /// <summary>
        /// The way home: a broken ring of dark stones round the portal, the lower ones resting on worn steps and
        /// the upper ones floating free and drifting, joined by yellow glowing seams and trailing vines.
        /// Built under the portal (origin at the oval's centre); its foot is the oval spec's centre height below.
        /// </summary>
        public static void WayHome(Transform portal)
        {
            var m = new Mats();
            var rng = new System.Random(7123);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var f = new GameObject("Gate").transform;
            f.SetParent(portal, false);
            float floor = -SpecFor("WayHome").center;

            // Worn, oval steps all round (the portal stands free).
            for (int s = 0; s < 3; s++)
            {
                var slab = CreatePrimitive(PrimitiveType.Cylinder, "Step", f, m.dark, false);
                slab.transform.localPosition = new Vector3(0f, floor + 0.05f + s * 0.1f, 0f);
                slab.transform.localScale = new Vector3(4.6f - s * 0.7f, 0.05f, 3f - s * 0.5f);
            }

            var stones = new List<Vector3>();
            for (int i = 0; i <= 10; i++)
            {
                if (i == 4 || i == 7)
                    continue; // gaps where stones have fallen away
                float a = (-50f + i * 28f) * Mathf.Deg2Rad;
                bool floating = Mathf.Sin(a) > 0.2f;
                float lift = floating ? R(0.15f, 0.5f) : 0f;
                var at = new Vector3(Mathf.Cos(a) * (1.62f + lift), Mathf.Sin(a) * (2.02f + lift), R(-0.1f, 0.1f));
                float s = R(0.8f, 1.05f);
                var holder = new GameObject(floating ? "Floating Stone" : "Stone").transform;
                holder.SetParent(f, false);
                holder.localPosition = at;
                if (floating)
                {
                    var bob = holder.gameObject.AddComponent<DreamFloat>();
                    bob.bobHeight = R(0.05f, 0.1f);
                    bob.bobSpeed = R(0.12f, 0.22f);
                    bob.spin = new Vector3(0f, R(-4f, 4f), 0f);
                }
                Rock(holder, m, m.rocks[rng.Next(m.rocks.Length)], Vector3.zero, new Vector3(s * 1.05f, s, s * 0.9f), new Vector3(R(-20f, 20f), R(0f, 360f), R(-20f, 20f)));
                stones.Add(at);
                if (floating && rng.NextDouble() < 0.6)
                    Strand(holder, m.vine, new Vector3(0f, -0.35f, 0.2f), R(0.3f, 0.7f), rng);
            }
            // Yellow light glowing in the seams between neighbouring stones.
            for (int i = 0; i < stones.Count - 1; i++)
            {
                var a = stones[i];
                var b = stones[i + 1];
                if (Vector3.Distance(a, b) > 1.6f)
                    continue;
                var mid = (a + b) * 0.5f;
                var dir = (b - a).normalized;
                Bar(f, m.yellow, mid - dir * 0.18f + Vector3.forward * 0.3f, mid + dir * 0.18f + Vector3.forward * 0.3f, 0.05f, 0.04f);
            }
            PointLight(f, "Gate Light", new Vector3(0f, 0.3f, 1f), new Color(1f, 0.85f, 0.5f), 5f, 1f);
        }

        // =====================================================================
        // Pieces
        // =====================================================================

        static void Rock(Transform parent, Mats m, Mesh mesh, Vector3 at, Vector3 scale, Vector3 euler, Material stone = null)
        {
            var rock = MeshObject("Stone", parent, mesh, stone ?? m.dark);
            rock.GetComponent<MeshRenderer>().sharedMaterials = new[] { stone ?? m.dark, m.moss };
            rock.transform.localPosition = at;
            rock.transform.localRotation = Quaternion.Euler(euler);
            rock.transform.localScale = scale;
        }

        /// <summary>Three oval slabs stepping up to the portal's foot (their back halves hidden in the wall).</summary>
        static void CurvedSteps(Transform f, Material mat, float floor, float z, float width, float depth)
        {
            for (int s = 0; s < 3; s++)
            {
                var slab = CreatePrimitive(PrimitiveType.Cylinder, "Step", f, mat, false);
                slab.transform.localPosition = new Vector3(0f, floor + 0.05f + s * 0.1f, z);
                slab.transform.localScale = new Vector3((width - s * 0.4f) * 2f, 0.05f, (depth - s * 0.3f) * 2f);
            }
        }

        /// <summary>A glowing line from <paramref name="a"/> to <paramref name="b"/> (a thin bar in the X-Y plane).</summary>
        static void Bar(Transform parent, Material mat, Vector3 a, Vector3 b, float thickness, float depth)
        {
            var d = b - a;
            var bar = Box("Glyph", parent, mat, (a + b) * 0.5f, new Vector3(new Vector2(d.x, d.y).magnitude + thickness, thickness, depth + Mathf.Abs(d.z)), false);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            bar.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>An outlined equilateral triangle, point up at <paramref name="degrees"/> = 0.</summary>
        static void Triangle(Transform parent, Material mat, Vector3 center, float size, float degrees, float thickness)
        {
            var corners = new Vector3[3];
            for (int k = 0; k < 3; k++)
            {
                float a = (90f + degrees + k * 120f) * Mathf.Deg2Rad;
                corners[k] = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * size;
            }
            for (int k = 0; k < 3; k++)
                Bar(parent, mat, corners[k], corners[(k + 1) % 3], thickness, 0.012f);
        }

        static void Square(Transform parent, Material mat, Vector3 center, float size, float thickness)
        {
            float h = size * 0.5f;
            var c = new[] { center + new Vector3(-h, -h, 0f), center + new Vector3(h, -h, 0f), center + new Vector3(h, h, 0f), center + new Vector3(-h, h, 0f) };
            for (int k = 0; k < 4; k++)
                Bar(parent, mat, c[k], c[(k + 1) % 4], thickness, 0.012f);
        }

        /// <summary>A vine draped from <paramref name="a"/> to <paramref name="b"/>, sagging in between, with a few leaves.</summary>
        static void Vine(Transform parent, Material mat, Vector3 a, Vector3 b, float sag, System.Random rng)
        {
            var points = new List<Vector3>();
            for (int k = 0; k <= 10; k++)
            {
                float t = k / 10f;
                points.Add(Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t));
            }
            Tube(parent, mat, points, 0.02f, 5);
            for (int l = 1; l < 10; l += 2)
                Leaf(parent, mat, points[l], rng);
        }

        /// <summary>A strand of vine hanging straight down from <paramref name="top"/>, curling slightly, with leaves.</summary>
        static void Strand(Transform parent, Material mat, Vector3 top, float length, System.Random rng)
        {
            var points = new List<Vector3>();
            float curl = (float)rng.NextDouble() * 0.1f - 0.05f;
            for (int k = 0; k <= 6; k++)
            {
                float t = k / 6f;
                points.Add(top + new Vector3(curl * t * t * 4f, -length * t, 0.02f * t));
            }
            Tube(parent, mat, points, 0.015f, 4);
            for (int l = 2; l <= 6; l += 2)
                Leaf(parent, mat, points[l], rng);
        }

        static void Leaf(Transform parent, Material mat, Vector3 at, System.Random rng)
        {
            var leaf = CreatePrimitive(PrimitiveType.Sphere, "Leaf", parent, mat, false);
            leaf.transform.localPosition = at;
            leaf.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, 0f);
            leaf.transform.localScale = new Vector3(0.1f, 0.025f, 0.06f);
        }

        /// <summary>A thin tube through points, as its own object.</summary>
        static void Tube(Transform parent, Material mat, List<Vector3> points, float radius, int sides)
        {
            var b = new Bits(1);
            var t0 = (points[1] - points[0]).normalized;
            var normal = Vector3.Cross(t0, Mathf.Abs(t0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            for (int i = 0; i < points.Count; i++)
            {
                var tangent = (i == 0 ? points[1] - points[0] : i == points.Count - 1 ? points[i] - points[i - 1] : points[i + 1] - points[i - 1]).normalized;
                normal = Vector3.ProjectOnPlane(normal, tangent).normalized;
                var binormal = Vector3.Cross(tangent, normal);
                for (int k = 0; k <= sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    var outward = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    b.Vert(points[i] + outward * radius, outward, Vector2.zero);
                }
            }
            for (int i = 0; i < points.Count - 1; i++)
            for (int k = 0; k < sides; k++)
            {
                int a = i * (sides + 1) + k, c = a + sides + 1;
                b.sub[0].AddRange(new[] { a, a + 1, c, a + 1, c + 1, c });
            }
            var mesh = new Mesh { name = "Vine" };
            mesh.SetVertices(b.v);
            mesh.SetNormals(b.n);
            mesh.SetTriangles(b.sub[0], 0);
            mesh.RecalculateBounds();
            MeshObject("Vine", parent, mesh, mat).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
