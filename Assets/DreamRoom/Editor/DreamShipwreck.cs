using System.Collections.Generic;
using UnityEngine;
using static DreamStudio.Editor.DreamBuildUtils;

namespace DreamStudio.Editor
{
    /// <summary>
    /// A sunken wooden sailing ship, built as a real hull rather than a block: a planked shell lofted
    /// through cross-sections (a keel, a full bilge, sides that rise toward bow and stern, a bow that
    /// narrows to a curved stem, the stern broken away), lying rolled on its side and half-buried in the
    /// sand. A jagged hole is torn in the upturned side, showing the ribs inside; deck beams and some deck
    /// planks remain, a broken mast stands in the hull and algae grows on everything facing up. Around it
    /// lie the fallen mast and yard, barrels, crates, loose planks, an anchor and two cannons.
    ///
    /// The hull's local space: length along +Z (bow at +Z), beam along X, keel at Y = 0.
    /// </summary>
    public static class DreamShipwreck
    {
        const float Length = 11f;
        const float Beam = 3.6f;
        const float Depth = 2.4f;
        const float Shell = 0.08f;     // plank thickness
        const int Stations = 28;       // cross-sections along the hull
        const int Points = 9;          // points from keel to gunwale on each side

        public static void Build(Transform parent, Vector3 position, float yaw, System.Func<Vector3, float> groundHeight, int seed)
        {
            var rng = new System.Random(seed);
            float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);

            var planks = Lit("Wreck_Planks", new Color(0.95f, 0.93f, 0.9f), 0.15f, new Color(0.01f, 0.01f, 0.01f));
            var timber = Lit("Wreck_Timber", new Color(0.42f, 0.36f, 0.3f), 0.1f, Color.black);
            var iron = Lit("Wreck_Iron", new Color(0.36f, 0.32f, 0.29f), 0.35f, Color.black);
            var algae = Lit("Wreck_Algae", new Color(0.4f, 0.43f, 0.24f), 0.1f, new Color(0.01f, 0.012f, 0.005f));

            var wreck = new GameObject("Shipwreck");
            wreck.transform.SetParent(parent, false);
            wreck.transform.localPosition = position;
            wreck.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 World(Vector3 local) => position + Quaternion.Euler(0f, yaw, 0f) * local;
            Vector3 OnSand(Vector3 local, float lift) { var w = World(local); return local + Vector3.up * (groundHeight(w) - position.y + lift); }

            // --- The hull, rolled onto its port side so the torn starboard side faces up, and sunk into the sand.
            var roll = Quaternion.Euler(R(2f, 5f), 0f, R(18f, 24f));
            var hull = new GameObject("Hull");
            hull.transform.SetParent(wreck.transform, false);
            hull.transform.localPosition = new Vector3(0f, groundHeight(position) - position.y - 0.65f, 0f);
            hull.transform.localRotation = roll;

            var shell = MeshObject("Planking", hull.transform, HullMesh(roll, rng), planks);
            shell.GetComponent<MeshRenderer>().sharedMaterials = new[] { planks, algae };
            MeshObject("Ribs", hull.transform, RibsMesh(rng), timber);

            // Keel and stem.
            var keelBits = new DreamUnderstory.Bits(1);
            var stem = new List<Vector3>();
            for (int k = 0; k <= 8; k++)
            {
                float t = Mathf.Lerp(0.03f, 1f, k / 8f);
                stem.Add(new Vector3(0f, Bottom(t) - 0.08f, Z(t)));
            }
            stem.Add(new Vector3(0f, Top(1f) + 0.5f, Z(1f) + 0.25f));
            Tube(keelBits, stem, 0.14f, 6);
            MeshObject("Keel and Stem", hull.transform, keelBits.ToMesh("WreckKeel"), timber);

            // Deck beams across the hull, some fallen away, and planking left on the fore and aft decks.
            for (float t = 0.1f; t < 0.88f; t += 0.075f)
            {
                if (rng.NextDouble() < 0.3)
                    continue;
                float w = HalfWidth(t) - 0.12f;
                var beam = Box("Deck Beam", hull.transform, timber, new Vector3(0f, Top(t) - 0.3f, Z(t)), new Vector3(w * 2f, 0.16f, 0.16f), false);
                beam.transform.localRotation = Quaternion.Euler(0f, 0f, R(-3f, 3f));
            }
            foreach (var (from, to) in new[] { (0.06f, 0.24f), (0.72f, 0.86f) })
            {
                float tMid = (from + to) * 0.5f;
                float w = HalfWidth(to) - 0.15f;
                for (float x = -w; x <= w; x += 0.22f)
                {
                    if (rng.NextDouble() < 0.3)
                        continue;
                    float length = (Z(to) - Z(from)) * R(0.6f, 1f);
                    var plank = Box("Deck Plank", hull.transform, timber, new Vector3(x, Top(tMid) - 0.18f, Z(tMid) + R(-0.3f, 0.3f)), new Vector3(0.2f, 0.05f, length), false);
                    plank.transform.localRotation = Quaternion.Euler(R(-4f, 4f), R(-3f, 3f), R(-6f, 6f));
                }
            }

            // The mainmast, snapped off a few metres up.
            var stump = CreatePrimitive(PrimitiveType.Cylinder, "Mast Stump", hull.transform, timber, false);
            stump.transform.localPosition = new Vector3(0.05f, 2.4f, Z(0.5f));
            stump.transform.localRotation = Quaternion.Euler(R(-6f, 6f), 0f, R(-8f, 8f));
            stump.transform.localScale = new Vector3(0.32f, 2.2f, 0.32f);
            for (int b = 0; b < 2; b++)
            {
                var band = CreatePrimitive(PrimitiveType.Cylinder, "Mast Band", stump.transform, iron, false);
                band.transform.localPosition = new Vector3(0f, -0.3f + b * 0.45f, 0f);
                band.transform.localScale = new Vector3(1.08f, 0.02f, 1.08f);
            }

            // --- Wreckage scattered on the sand around it.
            var debris = new GameObject("Wreckage");
            debris.transform.SetParent(wreck.transform, false);
            var d = debris.transform;

            // The rest of the mast lying where it fell, with its yard.
            var fallen = CreatePrimitive(PrimitiveType.Cylinder, "Fallen Mast", d, timber, false);
            fallen.transform.localPosition = OnSand(new Vector3(4.2f, 0f, 1f), 0.08f);
            fallen.transform.localRotation = Quaternion.Euler(0f, R(15f, 30f), 90f);
            fallen.transform.localScale = new Vector3(0.28f, 3.2f, 0.28f);
            var yard = CreatePrimitive(PrimitiveType.Cylinder, "Yard", d, timber, false);
            yard.transform.localPosition = OnSand(new Vector3(5.2f, 0f, 2.4f), 0.05f);
            yard.transform.localRotation = Quaternion.Euler(R(-4f, 4f), R(-70f, -50f), 90f);
            yard.transform.localScale = new Vector3(0.16f, 2.4f, 0.16f);

            var barrel = BarrelMesh();
            for (int i = 0; i < 6; i++)
            {
                var at = new Vector3(R(-5f, 5.5f), 0f, R(-7f, 6f));
                if (Mathf.Abs(at.x) < 2.6f && Mathf.Abs(at.z) < 6f)
                    at.x = Mathf.Sign(at.x + 0.01f) * R(2.8f, 4.5f);
                bool onSide = rng.NextDouble() < 0.6;
                var go = MeshObject("Barrel", d, barrel, timber);
                go.transform.localPosition = OnSand(at, onSide ? 0.22f : -0.15f);
                go.transform.localRotation = onSide ? Quaternion.Euler(R(-10f, 10f), R(0f, 360f), 90f) : Quaternion.Euler(R(-12f, 12f), R(0f, 360f), R(-12f, 12f));
                for (int h = 0; h < 3; h++)
                {
                    var hoop = CreatePrimitive(PrimitiveType.Cylinder, "Hoop", go.transform, iron, false);
                    float y = 0.12f + h * 0.32f;
                    float r = 0.32f * (1f + 0.12f * Mathf.Sin(Mathf.PI * y / 0.9f)) * 2.06f;
                    hoop.transform.localPosition = new Vector3(0f, y, 0f);
                    hoop.transform.localScale = new Vector3(r, 0.025f, r);
                }
            }
            for (int i = 0; i < 4; i++)
            {
                var at = new Vector3(R(-4.5f, -2.8f) * (i % 2 == 0 ? 1f : -1f), 0f, R(-6f, 4f));
                float s = R(0.55f, 0.8f);
                var crate = Box("Crate", d, planks, OnSand(at, s * 0.2f), Vector3.one * s, false);
                crate.transform.localRotation = Quaternion.Euler(R(-15f, 15f), R(0f, 360f), R(-15f, 15f));
            }
            for (int i = 0; i < 12; i++)
            {
                var at = new Vector3(R(-6f, 6.5f), 0f, R(-8f, 7f));
                if (Mathf.Abs(at.x) < 2.4f && Mathf.Abs(at.z) < 5.8f)
                    continue;
                var plank = Box("Loose Plank", d, timber, OnSand(at, 0.03f), new Vector3(R(0.16f, 0.24f), 0.05f, R(1.2f, 3f)), false);
                plank.transform.localRotation = Quaternion.Euler(R(-6f, 6f), R(0f, 360f), R(-8f, 8f));
            }

            // The anchor, near the bow, half in the sand.
            var anchor = MeshObject("Anchor", d, AnchorMesh(), iron);
            anchor.transform.localPosition = OnSand(new Vector3(-2.2f, 0f, 7.6f), 0.1f);
            anchor.transform.localRotation = Quaternion.Euler(80f, R(0f, 360f), 0f);

            // Two cannons spilled out through the hole.
            var cannon = CannonMesh();
            for (int i = 0; i < 2; i++)
            {
                var go = MeshObject("Cannon", d, cannon, iron);
                go.transform.localPosition = OnSand(new Vector3(R(3f, 4f), 0f, R(-1f, 1.5f) + i * 1.6f), 0.18f);
                go.transform.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f) * Quaternion.Euler(R(-6f, 6f), 0f, R(-8f, 8f));
            }
        }

        // =====================================================================
        // Hull shape
        // =====================================================================

        static float Z(float t) => (t - 0.5f) * Length;

        /// <summary>Half the beam at a station: full amidships, a little narrower at the stern, curving in to the bow.</summary>
        static float HalfWidth(float t)
        {
            float w = Beam * 0.5f;
            if (t < 0.12f)
                return w * Mathf.Lerp(0.8f, 1f, t / 0.12f);
            if (t > 0.6f)
                return w * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((t - 0.6f) / 0.4f, 2f)));
            return w;
        }

        /// <summary>Height of the keel: flat, then sweeping up into the stem at the bow.</summary>
        static float Bottom(float t) => t > 0.78f ? Mathf.Pow((t - 0.78f) / 0.22f, 2f) * 1.3f : 0f;

        /// <summary>Height of the gunwale, rising toward bow and stern (the sheer).</summary>
        static float Top(float t) => Depth + 0.6f * Mathf.Pow(2f * t - 1f, 2f) + (t > 0.85f ? (t - 0.85f) * 2f : 0f);

        /// <summary>
        /// A point on the hull at station t, s from keel (0) to gunwale (1), on side +1 (starboard) or -1
        /// (port): a full, round-bilged section. <paramref name="inset"/> moves it inward (for the inner skin and ribs).
        /// </summary>
        static Vector3 HullPoint(float t, float s, int side, float inset)
        {
            float a = s * Mathf.PI * 0.5f;
            float w = Mathf.Max(0f, HalfWidth(t) - inset);
            float bottom = Bottom(t) + inset;
            // A round bilge with a little deadrise at the keel, and the topsides drawing in near the rail.
            float x = w * Mathf.Pow(Mathf.Sin(a), 0.85f) * (1f - 0.07f * Mathf.Pow(Mathf.Max(0f, (s - 0.75f) / 0.25f), 2f));
            float y = bottom + (Top(t) - bottom) * (1f - Mathf.Pow(Mathf.Cos(a), 0.9f)) + 0.12f * (1f - s) * s * 4f * (1f - s);
            return new Vector3(side * x, y, Z(t));
        }

        /// <summary>The torn hole in the starboard side, with a ragged edge, plus a few sprung planks around it.</summary>
        static bool Torn(float t, float s, int side, float noiseSeed)
        {
            if (side < 0)
                return false;
            float e = Mathf.Pow((t - 0.47f) / 0.13f, 2f) + Mathf.Pow((s - 0.62f) / 0.34f, 2f);
            return e < 1f + 0.7f * (Mathf.PerlinNoise(t * 14f + noiseSeed, s * 5f) - 0.5f);
        }

        /// <summary>
        /// The planked shell: an outer skin and an inner skin a plank's thickness inside it, joined along the
        /// gunwales, open at the broken-off stern and torn through on the starboard side. Outer faces that end
        /// up facing the sky (after <paramref name="roll"/>) go in a second submesh for algae.
        /// </summary>
        static Mesh HullMesh(Quaternion roll, System.Random rng)
        {
            float noiseSeed = (float)rng.NextDouble() * 50f;
            var verts = new List<Vector3>();
            var wood = new List<int>();
            var green = new List<int>();
            int cols = Points * 2 - 1; // port gunwale -> keel -> starboard gunwale

            int Grid(float inset)
            {
                int start = verts.Count;
                for (int i = 0; i < Stations; i++)
                {
                    float t = Mathf.Lerp(0.02f, 1f, i / (Stations - 1f));
                    for (int j = 0; j < cols; j++)
                    {
                        int side = j < Points - 1 ? -1 : 1;
                        float s = Mathf.Abs(j - (Points - 1)) / (Points - 1f);
                        verts.Add(HullPoint(t, s, side, inset));
                    }
                }
                return start;
            }
            int outer = Grid(0f);
            int inner = Grid(Shell);

            for (int i = 0; i < Stations - 1; i++)
            {
                float t = Mathf.Lerp(0.02f, 1f, (i + 0.5f) / (Stations - 1f));
                var inside = new Vector3(0f, (Bottom(t) + Top(t)) * 0.5f, Z(t));
                for (int j = 0; j < cols - 1; j++)
                {
                    int side = j < Points - 1 ? -1 : 1;
                    float s = Mathf.Abs(j + 0.5f - (Points - 1)) / (Points - 1f);
                    if (Torn(t, s, side, noiseSeed))
                        continue;
                    int a = i * cols + j, b = a + 1, c = a + cols + 1, d = a + cols;
                    var mid = (verts[outer + a] + verts[outer + c]) * 0.5f;
                    var outward = mid - inside;
                    var n = Face(verts, outer + a, outer + b, outer + c, outer + d, outward, out var tris);
                    (Vector3.Dot(roll * n, Vector3.up) > 0.55f ? green : wood).AddRange(tris);
                    Face(verts, inner + a, inner + b, inner + c, inner + d, -outward, out var innerTris);
                    wood.AddRange(innerTris);
                }
            }

            // Gunwale caps joining the two skins along the top edges.
            foreach (int j in new[] { 0, cols - 1 })
                for (int i = 0; i < Stations - 1; i++)
                {
                    int a = i * cols + j, n = a + cols;
                    Face(verts, outer + a, outer + n, inner + n, inner + a, Vector3.up, out var tris);
                    green.AddRange(tris);
                }

            var mesh = new Mesh { name = "WreckHull" };
            mesh.SetVertices(verts);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(wood, 0);
            mesh.SetTriangles(green, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMesh(mesh, "WreckHull");
        }

        /// <summary>A quad as two triangles, wound so its face points along <paramref name="outward"/>.</summary>
        static Vector3 Face(List<Vector3> v, int a, int b, int c, int d, Vector3 outward, out int[] tris)
        {
            var n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(n, outward) >= 0f)
            {
                tris = new[] { a, b, c, a, c, d };
                return n.normalized;
            }
            tris = new[] { a, c, b, a, d, c };
            return -n.normalized;
        }

        /// <summary>The ribs (frames) curving up inside the hull from gunwale to gunwale; some are broken off.</summary>
        static Mesh RibsMesh(System.Random rng)
        {
            var bits = new DreamUnderstory.Bits(1);
            for (int i = 2; i < Stations - 3; i += 2)
            {
                float t = Mathf.Lerp(0.02f, 1f, i / (Stations - 1f));
                var points = new List<Vector3>();
                for (int j = 0; j < Points * 2 - 1; j++)
                {
                    int side = j < Points - 1 ? -1 : 1;
                    float s = Mathf.Abs(j - (Points - 1)) / (Points - 1f);
                    points.Add(HullPoint(t, s, side, Shell + 0.07f));
                }
                int keep = rng.NextDouble() < 0.25 ? points.Count - 2 - rng.Next(5) : points.Count;
                Tube(bits, points.GetRange(0, Mathf.Max(2, keep)), 0.075f, 4);
            }
            return bits.ToMesh("WreckRibs");
        }

        // =====================================================================
        // Wreckage
        // =====================================================================

        /// <summary>A barrel: staves bulging out between flat ends, standing 0.9 m tall on its local Y.</summary>
        static Mesh BarrelMesh()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            const int sides = 14, rings = 6;
            const float height = 0.9f, radius = 0.32f;
            for (int r = 0; r <= rings; r++)
            {
                float y = height * r / rings;
                float rad = radius * (1f + 0.12f * Mathf.Sin(Mathf.PI * y / height));
                for (int k = 0; k < sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    verts.Add(new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad));
                }
            }
            for (int r = 0; r < rings; r++)
            for (int k = 0; k < sides; k++)
            {
                int a = r * sides + k, b = r * sides + (k + 1) % sides, c = a + sides, d = b + sides;
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
            for (int end = 0; end < 2; end++)
            {
                int ring = end == 0 ? 0 : rings * sides;
                int center = verts.Count;
                verts.Add(new Vector3(0f, end == 0 ? 0.02f : height - 0.02f, 0f));
                for (int k = 0; k < sides; k++)
                {
                    int a = ring + k, b = ring + (k + 1) % sides;
                    tris.AddRange(end == 0 ? new[] { center, a, b } : new[] { center, b, a });
                }
            }
            var mesh = new Mesh { name = "WreckBarrel" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMesh(mesh, "WreckBarrel");
        }

        /// <summary>An old ship's anchor along its local Y: shank, curved arms with flukes, a stock and a ring.</summary>
        static Mesh AnchorMesh()
        {
            var bits = new DreamUnderstory.Bits(1);
            Tube(bits, new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(0f, 1.9f, 0f) }, 0.07f, 6);
            foreach (int side in new[] { -1, 1 })
            {
                var arm = new List<Vector3>();
                for (int k = 0; k <= 5; k++)
                {
                    float a = k / 5f * 70f * Mathf.Deg2Rad;
                    arm.Add(new Vector3(side * Mathf.Sin(a) * 0.65f, 0.65f - Mathf.Cos(a) * 0.65f, 0f));
                }
                Tube(bits, arm, 0.06f, 6);
                // A fluke: a flattened spade at the arm's tip.
                var tip = arm[arm.Count - 1];
                Tube(bits, new List<Vector3> { tip + new Vector3(-side * 0.05f, -0.1f, 0f), tip + new Vector3(side * 0.08f, 0.22f, 0f) }, 0.13f, 4);
            }
            Tube(bits, new List<Vector3> { new Vector3(0f, 1.75f, -0.75f), new Vector3(0f, 1.75f, 0.75f) }, 0.06f, 6);
            var ring = new List<Vector3>();
            for (int k = 0; k <= 12; k++)
            {
                float a = k / 12f * Mathf.PI * 2f;
                ring.Add(new Vector3(Mathf.Sin(a) * 0.16f, 2.05f + Mathf.Cos(a) * 0.16f, 0f));
            }
            Tube(bits, ring, 0.03f, 5);
            return bits.ToMesh("WreckAnchor");
        }

        /// <summary>A cannon barrel along its local Z: tapering toward the muzzle, with a swelling and a knob at the breech.</summary>
        static Mesh CannonMesh()
        {
            var bits = new DreamUnderstory.Bits(1);
            var spine = new List<Vector3>();
            var radii = new List<float>();
            float[] z = { -0.8f, -0.7f, -0.3f, 0.3f, 0.75f, 0.85f };
            float[] r = { 0.2f, 0.21f, 0.18f, 0.15f, 0.14f, 0.16f };
            for (int i = 0; i < z.Length; i++)
            {
                spine.Add(new Vector3(0f, 0f, z[i]));
                radii.Add(r[i]);
            }
            Tube(bits, spine, radii, 10);
            Tube(bits, new List<Vector3> { new Vector3(0f, 0f, -0.82f), new Vector3(0f, 0f, -1f) }, 0.07f, 6);
            return bits.ToMesh("WreckCannon");
        }

        static void Tube(DreamUnderstory.Bits b, List<Vector3> spine, float radius, int sides)
        {
            var radii = new List<float>();
            foreach (var _ in spine)
                radii.Add(radius);
            Tube(b, spine, radii, sides);
        }

        /// <summary>A tube through spine points, rings carried along without twisting, outward-facing, with capped ends.</summary>
        static void Tube(DreamUnderstory.Bits b, List<Vector3> spine, List<float> radii, int sides)
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
                b.sub[0].AddRange(new[] { a, a + 1, c, a + 1, c + 1, c });
            }
            // Flat caps at both ends.
            for (int end = 0; end < 2; end++)
            {
                int ring = end == 0 ? start : start + (spine.Count - 1) * stride;
                var center = spine[end == 0 ? 0 : spine.Count - 1];
                var axis = end == 0 ? -(spine[1] - spine[0]).normalized : (spine[spine.Count - 1] - spine[spine.Count - 2]).normalized;
                int c0 = b.Vert(center, axis, Vector2.zero);
                for (int k = 0; k < sides; k++)
                    b.sub[0].AddRange(end == 0 ? new[] { c0, ring + k + 1, ring + k } : new[] { c0, ring + k, ring + k + 1 });
            }
        }
    }
}
