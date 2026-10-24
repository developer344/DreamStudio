using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using static DreamStudio.Editor.DreamBuildUtils;
using Bits = DreamStudio.Editor.DreamUnderstory.Bits;

namespace DreamStudio.Editor
{
    /// <summary>
    /// The Start Room as a palace rotunda: a polished marble floor inlaid with gold rings and a sixteen-point
    /// star, tall walls ringed by turned marble columns, seven great arched windows with gold tracery and
    /// gathered velvet drapes opening onto the dream sky, gilded wall panels, candelabra sconces and marble
    /// urns, a grand framed doorway, a gilded cornice with dentils, and over it all a starry vaulted dome
    /// with gold ribs and rosettes, open to the sky at its oculus, with a crystal chandelier hanging on a
    /// long chain. Built in the room's local space: +Z toward the doorway, the floor at Y = 0.
    /// Turned and moulded pieces (columns, cornice, dome, urns, chandelier) come from <see cref="Lathe"/>.
    /// </summary>
    internal static class DreamPalace
    {
        public class Mats
        {
            public Material marble = Lit("Palace_Marble", new Color(0.96f, 0.95f, 0.93f), 0.85f, new Color(0.03f, 0.03f, 0.03f));
            public Material darkMarble = Lit("Palace_MarbleDark", new Color(0.2f, 0.22f, 0.25f), 0.85f, Color.black);
            public Material creamMarble = Lit("Palace_MarbleCream", new Color(0.93f, 0.88f, 0.8f), 0.8f, new Color(0.03f, 0.025f, 0.02f));
            public Material gold = Lit("Palace_Gold", new Color(1f, 0.8f, 0.42f), 0.88f, new Color(0.28f, 0.18f, 0.05f));
            public Material wall = Lit("Palace_Wall", new Color(0.93f, 0.88f, 0.95f), 0.3f, new Color(0.06f, 0.05f, 0.07f));
            public Material panel = Lit("Palace_Panel", new Color(0.78f, 0.7f, 0.86f), 0.35f, new Color(0.04f, 0.03f, 0.05f));
            public Material velvet = Lit("Palace_Velvet", new Color(0.32f, 0.14f, 0.42f), 0.35f, new Color(0.02f, 0.01f, 0.03f));
            public Material vault = Lit("Palace_Vault", new Color(0.12f, 0.15f, 0.4f), 0.4f, new Color(0.03f, 0.04f, 0.12f));
            public Material star = Lit("Palace_Star", new Color(1f, 0.88f, 0.55f), 0.6f, new Color(1.6f, 1.3f, 0.6f));
            public Material flame = Lit("Palace_Flame", new Color(1f, 0.85f, 0.5f), 0.5f, new Color(2.2f, 1.6f, 0.7f));
            public Material crystal = Lit("Palace_Crystal", new Color(0.9f, 0.95f, 1f), 0.95f, new Color(0.35f, 0.4f, 0.5f));
            public Material candle = Lit("Palace_Candle", new Color(0.98f, 0.95f, 0.88f), 0.3f, new Color(0.1f, 0.09f, 0.07f));
            public Material roof = Lit("Palace_Roof", new Color(0.4f, 0.62f, 0.58f), 0.5f, new Color(0.02f, 0.03f, 0.03f));
        }

        // Proportions (meters).
        public const float WallHeight = 7f;
        const float WallThickness = 0.3f;
        const int Bays = 16;
        const float WindowWidth = 2f;
        const float SillHeight = 0.6f;
        const float SpringHeight = 4.6f;       // where each window's arch begins
        const float CorniceTop = 7.85f;        // where the dome springs

        /// <summary>Bay centers (degrees from +Z, clockwise seen from above) that hold great windows.</summary>
        static readonly float[] WindowBays = { 45f, 135f, 157.5f, 180f, 202.5f, 225f, 315f };
        /// <summary>Solid bays that get a candelabra (with a light) or a marble urn.</summary>
        static readonly float[] SconceBays = { 67.5f, 112.5f, 247.5f, 337.5f };
        static readonly float[] UrnBays = { 22.5f };

        static Quaternion Yaw(float degrees) => Quaternion.Euler(0f, degrees, 0f);

        const float Oculus = 1.2f;

        /// <summary>Height of the dome's crown, where the oculus opens.</summary>
        static float Apex(float radius)
        {
            float r = radius - 0.15f;
            return CorniceTop + r * Mathf.Sin(Mathf.Acos(Oculus / r));
        }
        static Vector3 OnCircle(float degrees, float radius, float y = 0f) => Yaw(degrees) * new Vector3(0f, y, radius);

        public static void Build(Transform root, Mats m, float radius, float doorWidth, float doorHeight)
        {
            Floor(root, m, radius);
            Walls(root, m, radius, doorWidth, doorHeight);
            Columns(root, m, radius, doorWidth);
            Doorway(root, m, radius, doorWidth, doorHeight);
            foreach (float bay in WindowBays)
                Window(root, m, radius, bay);
            WallPanels(root, m, radius, doorWidth);
            foreach (float bay in SconceBays)
                Sconce(root, m, radius, bay);
            foreach (float bay in UrnBays)
                Urn(root, m, radius, bay);
            Cornice(root, m, radius);
            Dome(root, m, radius);
            Chandelier(root, m, radius);
        }

        // =====================================================================
        // Floor
        // =====================================================================

        static void Floor(Transform root, Mats m, float radius)
        {
            var floor = CreatePrimitive(PrimitiveType.Cylinder, "Floor", root, m.marble, false);
            floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(radius * 2f + 0.6f, 0.05f, radius * 2f + 0.6f);
            floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            floor.AddComponent<TeleportationArea>();

            // Inlay: gold rings, a band of dark marble, and a sixteen-point star between the rug and the band.
            var inlay = new GameObject("Floor Inlay").transform;
            inlay.SetParent(root, false);
            Flat(inlay, "Inner Ring", FlatRing("PalaceRing_Inner", 2.4f, 2.5f, 0.004f), m.gold);
            Flat(inlay, "Star Ring", FlatRing("PalaceRing_Star", 4.4f, 4.5f, 0.004f), m.gold);
            Flat(inlay, "Dark Band", FlatRing("PalaceRing_Band", 5.05f, 5.75f, 0.003f), m.darkMarble);
            Flat(inlay, "Band Edge", FlatRing("PalaceRing_BandIn", 4.98f, 5.05f, 0.004f), m.gold);
            Flat(inlay, "Band Edge", FlatRing("PalaceRing_BandOut", 5.75f, 5.82f, 0.004f), m.gold);
            Flat(inlay, "Outer Ring", FlatRing("PalaceRing_Outer", radius - 0.85f, radius - 0.75f, 0.004f), m.gold);

            var star = new Bits(2);
            for (int k = 0; k < 32; k++)
            {
                float a = k * 360f / 32f;
                bool longRay = k % 2 == 0;
                float tip = longRay ? 4.4f : 3.6f;
                float half = 360f / 32f * 0.5f;
                var p0 = OnCircle(a - half, 2.5f, 0.005f);
                var p1 = OnCircle(a, tip, 0.005f);
                var p2 = OnCircle(a + half, 2.5f, 0.005f);
                int sub = longRay ? 0 : 1;
                int i0 = star.Vert(p0, Vector3.up, Vector2.zero), i1 = star.Vert(p1, Vector3.up, Vector2.zero), i2 = star.Vert(p2, Vector3.up, Vector2.zero);
                star.sub[sub].AddRange(new[] { i0, i1, i2 });
                // The ray's spine, a thin line of the other colour.
                var s0 = OnCircle(a - 0.3f, 2.5f, 0.006f); var s1 = OnCircle(a + 0.3f, 2.5f, 0.006f);
                int j0 = star.Vert(s0, Vector3.up, Vector2.zero), j1 = star.Vert(p1 + Vector3.up * 0.001f, Vector3.up, Vector2.zero), j2 = star.Vert(s1, Vector3.up, Vector2.zero);
                star.sub[1 - sub].AddRange(new[] { j0, j1, j2 });
            }
            var starGo = MeshObject("Floor Star", inlay, star.ToMesh("PalaceFloorStar"), m.gold);
            starGo.GetComponent<MeshRenderer>().sharedMaterials = new[] { m.gold, m.darkMarble };
            starGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Flat(Transform parent, string name, Mesh mesh, Material mat)
        {
            MeshObject(name, parent, mesh, mat).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static Mesh FlatRing(string name, float inner, float outer, float y) =>
            Lathe(name, new[] { new Vector2(outer, y), new Vector2(inner, y) }, 64, true);

        // =====================================================================
        // Walls, windows and doorway
        // =====================================================================

        static float DoorHalfAngle(float radius, float doorWidth) => Mathf.Asin(doorWidth * 0.5f / radius) * Mathf.Rad2Deg;

        /// <summary>
        /// The round wall, built from narrow upright segments so the window and door openings can follow
        /// their outlines: each segment is cut away below the window's arch (a semicircle on the springing line).
        /// </summary>
        static void Walls(Transform root, Mats m, float radius, float doorWidth, float doorHeight)
        {
            var walls = new GameObject("Walls").transform;
            walls.SetParent(root, false);
            const int segments = 112;
            float width = 2f * Mathf.PI * radius / segments * 1.04f;
            float doorHalf = DoorHalfAngle(radius, doorWidth);
            float windowHalf = Mathf.Asin(WindowWidth * 0.5f / radius) * Mathf.Rad2Deg;
            for (int i = 0; i < segments; i++)
            {
                float angle = (i + 0.5f) * 360f / segments;
                var panel = new GameObject($"Wall {i}").transform;
                panel.SetParent(walls, false);
                panel.localPosition = OnCircle(angle, radius);
                panel.localRotation = Yaw(angle + 180f);

                void Piece(string name, float y0, float y1)
                {
                    if (y1 - y0 > 0.01f)
                        Box(name, panel, m.wall, new Vector3(0f, (y0 + y1) * 0.5f, 0f), new Vector3(width, y1 - y0, WallThickness));
                }

                if (Mathf.Abs(Mathf.DeltaAngle(angle, 0f)) < doorHalf)
                {
                    Piece("Over Door", doorHeight, WallHeight);
                    continue;
                }
                bool opening = false;
                foreach (float bay in WindowBays)
                {
                    float delta = Mathf.DeltaAngle(angle, bay);
                    if (Mathf.Abs(delta) >= windowHalf)
                        continue;
                    float x = radius * Mathf.Sin(delta * Mathf.Deg2Rad);
                    float r = WindowWidth * 0.5f;
                    float archTop = SpringHeight + Mathf.Sqrt(Mathf.Max(0f, r * r - x * x));
                    Piece("Under Window", 0f, SillHeight);
                    Piece("Over Window", archTop, WallHeight);
                    opening = true;
                    break;
                }
                if (!opening)
                    Piece("Panel", 0f, WallHeight);
                // Marble skirting at the foot of the wall.
                if (!opening)
                    Box("Skirting", panel, m.darkMarble, new Vector3(0f, 0.12f, -0.17f), new Vector3(width, 0.24f, 0.05f), false);
            }
        }

        /// <summary>A great arched window: gold frame and tracery, an invisible pane, and velvet drapes tied back.</summary>
        static void Window(Transform root, Mats m, float radius, float bay)
        {
            var w = new GameObject($"Window {bay}").transform;
            w.SetParent(root, false);
            w.localPosition = OnCircle(bay, radius - WallThickness * 0.5f);
            w.localRotation = Yaw(bay + 180f); // local +Z faces into the room
            float half = WindowWidth * 0.5f;
            const float frame = 0.09f;

            // Frame: jambs, sill and the arch, as short straight pieces.
            foreach (int side in new[] { -1, 1 })
                Box("Jamb", w, m.gold, new Vector3(side * half, (SillHeight + SpringHeight) * 0.5f, 0.02f), new Vector3(frame, SpringHeight - SillHeight, 0.36f), false);
            Box("Sill", w, m.marble, new Vector3(0f, SillHeight, 0.12f), new Vector3(WindowWidth + 0.3f, 0.08f, 0.5f), false);
            Arch(w, m.gold, half, SpringHeight, 0.02f, frame, 0.36f, 12);

            // Tracery: a mullion, a transom at the springing line, glazing bars, and an inner arch.
            Box("Mullion", w, m.gold, new Vector3(0f, (SillHeight + SpringHeight + half) * 0.5f, 0f), new Vector3(0.05f, SpringHeight + half - SillHeight, 0.06f), false);
            Box("Transom", w, m.gold, new Vector3(0f, SpringHeight, 0f), new Vector3(WindowWidth, 0.05f, 0.06f), false);
            for (float y = SillHeight + 0.8f; y < SpringHeight - 0.2f; y += 0.8f)
                Box("Glazing Bar", w, m.gold, new Vector3(0f, y, 0f), new Vector3(WindowWidth, 0.025f, 0.04f), false);
            Arch(w, m.gold, half * 0.5f, SpringHeight, 0f, 0.035f, 0.05f, 8, -half * 0.5f);
            Arch(w, m.gold, half * 0.5f, SpringHeight, 0f, 0.035f, 0.05f, 8, half * 0.5f);

            var pane = new GameObject("Pane");
            pane.transform.SetParent(w, false);
            pane.transform.localPosition = new Vector3(0f, (SillHeight + SpringHeight + half) * 0.5f, 0f);
            pane.AddComponent<BoxCollider>().size = new Vector3(WindowWidth, SpringHeight + half - SillHeight, 0.3f);

            // Drapes hanging inside the window from a gold rod, gathered back to each jamb (clear of the columns).
            var curtain = CurtainMesh();
            float rodY = SpringHeight + half + 0.25f;
            Box("Curtain Rod", w, m.gold, new Vector3(0f, rodY, 0.2f), new Vector3(WindowWidth + 0.2f, 0.05f, 0.05f), false);
            foreach (int side in new[] { -1, 1 })
            {
                var finial = CreatePrimitive(PrimitiveType.Sphere, "Finial", w, m.gold, false);
                finial.transform.localPosition = new Vector3(side * (half + 0.1f), rodY, 0.2f);
                finial.transform.localScale = Vector3.one * 0.11f;
                var drape = MeshObject("Drape", w, curtain, m.velvet);
                drape.transform.localPosition = new Vector3(side * (half - 0.02f), rodY - 0.03f, 0.22f);
                drape.transform.localScale = new Vector3(side * 0.62f, 1f, 1f);
                var tie = CreatePrimitive(PrimitiveType.Sphere, "Tieback", w, m.gold, false);
                tie.transform.localPosition = new Vector3(side * (half - 0.12f), rodY - 0.03f - CurtainGather, 0.27f);
                tie.transform.localScale = new Vector3(0.1f, 0.07f, 0.1f);
            }
        }

        /// <summary>A semicircular arch of short pieces, centred at <paramref name="centerX"/> on the springing line.</summary>
        static void Arch(Transform parent, Material mat, float radius, float spring, float z, float thickness, float depth, int pieces, float centerX = 0f)
        {
            for (int k = 0; k < pieces; k++)
            {
                float a0 = Mathf.PI * k / pieces, a1 = Mathf.PI * (k + 1) / pieces, a = (a0 + a1) * 0.5f;
                float length = 2f * radius * Mathf.Sin((a1 - a0) * 0.5f) + thickness * 0.6f;
                var piece = Box("Arch", parent, mat, new Vector3(centerX + Mathf.Cos(a) * radius, spring + Mathf.Sin(a) * radius, z), new Vector3(length, thickness, depth), false);
                piece.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f);
            }
        }

        /// <summary>
        /// A velvet drape hanging from its top-left corner (local -X is the window side): deep vertical folds
        /// that are gathered in tight at the tie-back and flare out again below. Both faces are built.
        /// </summary>
        /// <summary>How far below its top a drape from <see cref="CurtainMesh"/> is gathered in.</summary>
        internal const float CurtainGather = 3.85f;

        internal static Mesh CurtainMesh()
        {
            const int cols = 14, rows = 18;
            const float width = 0.95f, height = 5.4f;
            var b = new Bits(1);
            for (int face = 0; face < 2; face++)
            {
                int start = b.v.Count;
                for (int r = 0; r <= rows; r++)
                {
                    float y = -height * r / rows;
                    float gather = 1f - 0.62f * Mathf.Exp(-Mathf.Pow((y + CurtainGather) / 0.7f, 2f));
                    float depth = 0.05f + 0.04f * (r / (float)rows);
                    for (int c = 0; c <= cols; c++)
                    {
                        float u = c / (float)cols;
                        float x = -u * width * gather;
                        float z = Mathf.Sin(u * Mathf.PI * 5f) * depth * gather;
                        float slope = Mathf.Cos(u * Mathf.PI * 5f) * depth * Mathf.PI * 5f / width;
                        var n = new Vector3(slope, 0f, 1f).normalized * (face == 0 ? 1f : -1f);
                        b.Vert(new Vector3(x, y, z + (face == 0 ? 0.004f : -0.004f)), n, Vector2.zero);
                    }
                }
                for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int a = start + r * (cols + 1) + c, d = a + cols + 1;
                    var n = b.n[a];
                    var cross = Vector3.Cross(b.v[a + 1] - b.v[a], b.v[d + 1] - b.v[a]);
                    b.sub[0].AddRange(Vector3.Dot(cross, n) >= 0f ? new[] { a, a + 1, d + 1, a, d + 1, d } : new[] { a, d + 1, a + 1, a, d, d + 1 });
                }
            }
            return b.ToMesh("PalaceCurtain");
        }

        /// <summary>The doorway: a pair of columns, an entablature, and an arched pediment with a gold crest.</summary>
        static void Doorway(Transform root, Mats m, float radius, float doorWidth, float doorHeight)
        {
            var d = new GameObject("Grand Doorway").transform;
            d.SetParent(root, false);
            d.localPosition = new Vector3(0f, 0f, radius - 0.75f);
            d.localRotation = Yaw(180f); // local +Z faces into the room
            var column = ColumnMesh();
            float half = doorWidth * 0.5f;
            foreach (int side in new[] { -1, 1 })
            {
                Box("Door Plinth", d, m.darkMarble, new Vector3(side * (half + 0.4f), 0.2f, 0f), new Vector3(0.65f, 0.4f, 0.65f), false);
                var col = MeshObject("Door Column", d, column, m.creamMarble);
                col.transform.localPosition = new Vector3(side * (half + 0.4f), 0.4f, 0f);
                col.transform.localScale = new Vector3(0.8f, (doorHeight + 0.6f - 0.4f) / 6.9f, 0.8f);
            }
            float top = doorHeight + 0.6f;
            Box("Architrave", d, m.gold, new Vector3(0f, top + 0.1f, 0f), new Vector3(doorWidth + 1.6f, 0.2f, 0.7f), false);
            Box("Frieze", d, m.creamMarble, new Vector3(0f, top + 0.4f, 0.05f), new Vector3(doorWidth + 1.5f, 0.4f, 0.55f), false);
            Box("Cornice", d, m.gold, new Vector3(0f, top + 0.66f, 0.05f), new Vector3(doorWidth + 1.9f, 0.12f, 0.8f), false);
            Arch(d, m.gold, half + 0.35f, top + 0.72f, 0.1f, 0.14f, 0.5f, 12);
            Arch(d, m.gold, half - 0.1f, top + 0.72f, 0.12f, 0.06f, 0.3f, 10);
            var crest = CreatePrimitive(PrimitiveType.Sphere, "Crest", d, m.gold, false);
            crest.transform.localPosition = new Vector3(0f, top + 1.3f, 0.2f);
            crest.transform.localScale = new Vector3(0.5f, 0.6f, 0.15f);
            var crescent = CreatePrimitive(PrimitiveType.Sphere, "Crest Moon", d, m.star, false);
            crescent.transform.localPosition = new Vector3(0f, top + 1.32f, 0.28f);
            crescent.transform.localScale = new Vector3(0.22f, 0.22f, 0.05f);
            // The door's own frame inside the opening.
            foreach (int side in new[] { -1, 1 })
                Box("Door Jamb", d, m.gold, new Vector3(side * (half + 0.02f), doorHeight * 0.5f, -0.1f), new Vector3(0.08f, doorHeight, 0.5f), false);
            Box("Door Head", d, m.gold, new Vector3(0f, doorHeight + 0.04f, -0.1f), new Vector3(doorWidth + 0.1f, 0.08f, 0.5f), false);
        }

        // =====================================================================
        // Columns, panels, sconces and urns
        // =====================================================================

        /// <summary>Turned marble columns standing just off the wall at every bay boundary, except beside the doorway.</summary>
        static void Columns(Transform root, Mats m, float radius, float doorWidth)
        {
            var group = new GameObject("Columns").transform;
            group.SetParent(root, false);
            var mesh = ColumnMesh();
            float doorHalf = DoorHalfAngle(radius, doorWidth);
            for (int k = 0; k < Bays; k++)
            {
                float angle = (k + 0.5f) * 360f / Bays;
                if (Mathf.Abs(Mathf.DeltaAngle(angle, 0f)) < doorHalf + 3f)
                    continue;
                var at = OnCircle(angle, radius - 0.5f);
                var plinth = Box("Plinth", group, m.darkMarble, at + Vector3.up * 0.175f, new Vector3(0.75f, 0.35f, 0.75f));
                plinth.transform.localRotation = Yaw(angle);
                var col = MeshObject("Column", group, mesh, m.creamMarble);
                col.transform.localPosition = at + Vector3.up * 0.35f;
                col.transform.localRotation = Yaw(angle);
                var abacus = Box("Abacus", group, m.gold, at + Vector3.up * (0.35f + 6.85f - 0.08f), new Vector3(0.82f, 0.16f, 0.82f), false);
                abacus.transform.localRotation = Yaw(angle);
            }
        }

        /// <summary>A turned column 6.9 m tall from its foot: moulded base, a gently swelling shaft, a flared capital.</summary>
        static Mesh ColumnMesh()
        {
            var profile = new List<Vector2>
            {
                new Vector2(0.36f, 0f), new Vector2(0.36f, 0.06f), new Vector2(0.38f, 0.1f), new Vector2(0.36f, 0.15f),
                new Vector2(0.31f, 0.18f), new Vector2(0.31f, 0.24f), new Vector2(0.34f, 0.28f), new Vector2(0.29f, 0.33f),
                new Vector2(0.27f, 0.38f),
            };
            for (int i = 1; i <= 10; i++)
            {
                float t = i / 10f;
                profile.Add(new Vector2(0.27f - 0.045f * t + 0.02f * Mathf.Sin(Mathf.PI * t), 0.38f + t * (6.05f - 0.38f)));
            }
            profile.AddRange(new[]
            {
                new Vector2(0.25f, 6.1f), new Vector2(0.28f, 6.14f), new Vector2(0.25f, 6.18f),        // astragal
                new Vector2(0.255f, 6.25f), new Vector2(0.3f, 6.45f), new Vector2(0.37f, 6.6f),       // bell
                new Vector2(0.42f, 6.68f), new Vector2(0.42f, 6.72f), new Vector2(0f, 6.72f),
            });
            return Lathe("PalaceColumn", profile, 24, true);
        }

        /// <summary>Gilded moulding frames on the plain bays: a low dado panel and a tall upper panel in each.</summary>
        static void WallPanels(Transform root, Mats m, float radius, float doorWidth)
        {
            var group = new GameObject("Wall Panels").transform;
            group.SetParent(root, false);
            float doorHalf = DoorHalfAngle(radius, doorWidth);
            for (int k = 0; k < Bays; k++)
            {
                float bay = k * 360f / Bays;
                if (Mathf.Abs(Mathf.DeltaAngle(bay, 0f)) < doorHalf + 5f || System.Array.IndexOf(WindowBays, bay) >= 0)
                    continue;
                var frame = new GameObject($"Panels {bay}").transform;
                frame.SetParent(group, false);
                frame.localPosition = OnCircle(bay, radius - WallThickness * 0.5f - 0.02f);
                frame.localRotation = Yaw(bay + 180f);
                Moulding(frame, m, 1.5f, 0.35f, 1.15f);
                Moulding(frame, m, 1.5f, 1.5f, 6.2f);
                // A gold rosette at the top of the upper panel.
                var rosette = CreatePrimitive(PrimitiveType.Sphere, "Rosette", frame, m.gold, false);
                rosette.transform.localPosition = new Vector3(0f, 5.75f, 0.03f);
                rosette.transform.localScale = new Vector3(0.22f, 0.22f, 0.06f);
            }
        }

        /// <summary>A rectangle of gold moulding with a tinted inset, on the wall face (local +Z faces the room).</summary>
        static void Moulding(Transform frame, Mats m, float width, float y0, float y1)
        {
            const float bar = 0.06f;
            Box("Inset", frame, m.panel, new Vector3(0f, (y0 + y1) * 0.5f, -0.005f), new Vector3(width, y1 - y0, 0.02f), false);
            Box("Moulding", frame, m.gold, new Vector3(0f, y0, 0.01f), new Vector3(width + bar, bar, 0.04f), false);
            Box("Moulding", frame, m.gold, new Vector3(0f, y1, 0.01f), new Vector3(width + bar, bar, 0.04f), false);
            foreach (int side in new[] { -1, 1 })
                Box("Moulding", frame, m.gold, new Vector3(side * width * 0.5f, (y0 + y1) * 0.5f, 0.01f), new Vector3(bar, y1 - y0, 0.04f), false);
        }

        /// <summary>A three-branched gold candelabra on the wall, with lit candles.</summary>
        static void Sconce(Transform root, Mats m, float radius, float bay)
        {
            var s = new GameObject($"Sconce {bay}").transform;
            s.SetParent(root, false);
            s.localPosition = OnCircle(bay, radius - WallThickness * 0.5f);
            s.localRotation = Yaw(bay + 180f);
            const float y = 2.9f;
            var plate = CreatePrimitive(PrimitiveType.Sphere, "Backplate", s, m.gold, false);
            plate.transform.localPosition = new Vector3(0f, y, 0.02f);
            plate.transform.localScale = new Vector3(0.3f, 0.45f, 0.06f);
            for (int branch = -1; branch <= 1; branch++)
            {
                var cup = new Vector3(branch * 0.28f, y + (branch == 0 ? 0.22f : 0.08f), 0.3f);
                Tube(s, "Branch", m.gold, new[] { new Vector3(0f, y - 0.05f, 0.04f), new Vector3(branch * 0.18f, y - 0.12f, 0.2f), cup - Vector3.up * 0.04f }, 0.018f);
                var bobeche = CreatePrimitive(PrimitiveType.Cylinder, "Cup", s, m.gold, false);
                bobeche.transform.localPosition = cup;
                bobeche.transform.localScale = new Vector3(0.09f, 0.015f, 0.09f);
                var candle = CreatePrimitive(PrimitiveType.Cylinder, "Candle", s, m.candle, false);
                candle.transform.localPosition = cup + Vector3.up * 0.1f;
                candle.transform.localScale = new Vector3(0.04f, 0.09f, 0.04f);
                var flame = CreatePrimitive(PrimitiveType.Sphere, "Flame", s, m.flame, false);
                flame.transform.localPosition = cup + Vector3.up * 0.23f;
                flame.transform.localScale = new Vector3(0.03f, 0.06f, 0.03f);
                flame.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            PointLight(s, "Candle Light", new Vector3(0f, y + 0.4f, 0.45f), new Color(1f, 0.78f, 0.5f), 6f, 0.9f);
        }

        /// <summary>A marble urn on a pedestal, standing in front of a plain bay.</summary>
        static void Urn(Transform root, Mats m, float radius, float bay)
        {
            var at = OnCircle(bay, radius - 1f);
            var pedestal = Box("Pedestal", root, m.darkMarble, at + Vector3.up * 0.5f, new Vector3(0.6f, 1f, 0.6f));
            pedestal.transform.localRotation = Yaw(bay);
            Box("Pedestal Cap", root, m.gold, at + Vector3.up * 1.03f, new Vector3(0.68f, 0.06f, 0.68f), false).transform.localRotation = Yaw(bay);
            var profile = new[]
            {
                new Vector2(0.16f, 0f), new Vector2(0.16f, 0.06f), new Vector2(0.08f, 0.1f), new Vector2(0.08f, 0.2f),
                new Vector2(0.22f, 0.3f), new Vector2(0.3f, 0.48f), new Vector2(0.28f, 0.66f), new Vector2(0.17f, 0.8f),
                new Vector2(0.15f, 0.88f), new Vector2(0.22f, 0.94f), new Vector2(0.2f, 0.98f), new Vector2(0f, 0.98f),
            };
            var urn = MeshObject("Urn", root, Lathe("PalaceUrn", profile, 24, true), m.marble);
            urn.transform.localPosition = at + Vector3.up * 1.06f;
            var rim = MeshObject("Urn Band", root, Lathe("PalaceUrnBand", new[] { new Vector2(0.305f, 0.45f), new Vector2(0.305f, 0.51f) }, 24, true), m.gold);
            rim.transform.localPosition = at + Vector3.up * 1.06f;
        }

        // =====================================================================
        // Cornice, dome and chandelier
        // =====================================================================

        /// <summary>The gilded cornice round the top of the wall: architrave, a frieze with dentils, and a projecting crown.</summary>
        static void Cornice(Transform root, Mats m, float radius)
        {
            float r = radius - WallThickness * 0.5f;
            var c = new GameObject("Cornice").transform;
            c.SetParent(root, false);
            MeshObject("Architrave", c, Lathe("PalaceArchitrave", new[]
            {
                new Vector2(r, WallHeight - 0.2f), new Vector2(r - 0.1f, WallHeight - 0.2f), new Vector2(r - 0.1f, WallHeight + 0.05f),
                new Vector2(r - 0.14f, WallHeight + 0.08f), new Vector2(r - 0.14f, WallHeight + 0.15f),
            }, 96, false), m.gold);
            MeshObject("Frieze", c, Lathe("PalaceFrieze", new[] { new Vector2(r - 0.06f, WallHeight + 0.15f), new Vector2(r - 0.06f, WallHeight + 0.55f) }, 96, false), m.wall);
            MeshObject("Crown", c, Lathe("PalaceCrown", new[]
            {
                new Vector2(r - 0.06f, WallHeight + 0.55f), new Vector2(r - 0.24f, WallHeight + 0.62f), new Vector2(r - 0.3f, WallHeight + 0.72f),
                new Vector2(r - 0.4f, WallHeight + 0.78f), new Vector2(r - 0.4f, CorniceTop), new Vector2(r + 0.2f, CorniceTop),
            }, 96, false), m.gold);
            for (int k = 0; k < 144; k++)
            {
                float a = k * 360f / 144f;
                var dentil = Box("Dentil", c, m.gold, OnCircle(a, r - 0.1f, WallHeight + 0.45f), new Vector3(0.07f, 0.12f, 0.08f), false);
                dentil.transform.localRotation = Yaw(a);
            }
        }

        /// <summary>
        /// The dome: a starry vault springing from the cornice, ribbed in gold with rosettes in each panel,
        /// open at the top (the oculus) to the sky, roofed outside in green copper.
        /// </summary>
        static void Dome(Transform root, Mats m, float radius)
        {
            var dome = new GameObject("Dome").transform;
            dome.SetParent(root, false);
            float R = radius - 0.15f;
            const float oculus = Oculus;
            float phiMax = Mathf.Acos(oculus / R);
            var inner = new List<Vector2>();
            var outer = new List<Vector2>();
            for (int i = 0; i <= 20; i++)
            {
                float phi = phiMax * i / 20f;
                inner.Add(new Vector2(R * Mathf.Cos(phi), CorniceTop + R * Mathf.Sin(phi)));
            }
            for (int i = 20; i >= 0; i--)
            {
                float phi = phiMax * i / 20f;
                outer.Add(new Vector2((R + 0.35f) * Mathf.Cos(phi), CorniceTop + 0.05f + (R + 0.35f) * Mathf.Sin(phi)));
            }
            MeshObject("Vault", dome, Lathe("PalaceVault", inner, 64, false), m.vault);
            var roof = new List<Vector2>(outer);
            roof.Reverse();
            MeshObject("Roof", dome, Lathe("PalaceRoof", roof, 64, true), m.roof);
            float apex = CorniceTop + R * Mathf.Sin(phiMax);
            MeshObject("Oculus Rim", dome, Lathe("PalaceOculus", new[]
            {
                new Vector2(oculus - 0.02f, apex - 0.2f), new Vector2(oculus - 0.12f, apex - 0.2f), new Vector2(oculus - 0.12f, apex + 0.35f),
                new Vector2(oculus + 0.45f, apex + 0.45f),
            }, 48, false), m.gold);
            // Two crossed gold beams across the oculus, from which the chandelier hangs.
            foreach (float a in new[] { 0f, 90f })
                Box("Oculus Beam", dome, m.gold, new Vector3(0f, apex - 0.05f, 0f), new Vector3(0.12f, 0.16f, oculus * 2f + 0.2f), false).transform.localRotation = Yaw(a);

            // Gold ribs: sixteen meridians and three rings.
            for (int k = 0; k < Bays; k++)
            {
                float a = (k + 0.5f) * 360f / Bays;
                var points = new List<Vector3>();
                for (int i = 0; i <= 16; i++)
                {
                    float phi = phiMax * i / 16f;
                    points.Add(OnCircle(a, (R - 0.06f) * Mathf.Cos(phi), CorniceTop + (R - 0.06f) * Mathf.Sin(phi)));
                }
                Tube(dome, "Rib", m.gold, points.ToArray(), 0.07f);
            }
            float[] ringPhis = { 0.3f, 0.6f, 0.85f };
            foreach (float f in ringPhis)
            {
                float phi = phiMax * f;
                float rr = (R - 0.05f) * Mathf.Cos(phi), y = CorniceTop + (R - 0.05f) * Mathf.Sin(phi);
                MeshObject("Ring Rib", dome, Lathe($"PalaceRingRib_{f}", new[]
                {
                    new Vector2(rr + 0.05f, y - 0.07f), new Vector2(rr - 0.05f, y - 0.05f), new Vector2(rr - 0.05f, y + 0.05f), new Vector2(rr + 0.05f, y + 0.07f),
                }, 64, false), m.gold);
            }

            // A gold rosette in the middle of each panel, and a scatter of gilded stars across the vault.
            float[] bandMids = { 0.15f, 0.45f, 0.72f };
            for (int k = 0; k < Bays; k++)
                foreach (float f in bandMids)
                {
                    float phi = phiMax * f;
                    var dir = OnCircle(k * 360f / Bays, Mathf.Cos(phi), Mathf.Sin(phi));
                    var rosette = CreatePrimitive(PrimitiveType.Sphere, "Rosette", dome, m.gold, false);
                    rosette.transform.localPosition = new Vector3(0f, CorniceTop, 0f) + dir * (R - 0.04f);
                    rosette.transform.localRotation = Quaternion.LookRotation(-dir);
                    rosette.transform.localScale = new Vector3(0.28f, 0.28f, 0.08f) * (1.1f - f * 0.5f);
                }
            var rng = new System.Random(77);
            for (int s = 0; s < 160; s++)
            {
                float phi = phiMax * (float)rng.NextDouble() * 0.95f;
                float a = (float)rng.NextDouble() * 360f;
                var dir = OnCircle(a, Mathf.Cos(phi), Mathf.Sin(phi));
                var star = CreatePrimitive(PrimitiveType.Sphere, "Vault Star", dome, m.star, false);
                star.transform.localPosition = new Vector3(0f, CorniceTop, 0f) + dir * (R - 0.02f);
                star.transform.localRotation = Quaternion.LookRotation(-dir);
                star.transform.localScale = new Vector3(1f, 1f, 0.3f) * (0.04f + 0.05f * (float)rng.NextDouble());
                star.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            // Light falling through the oculus, shown by drifting motes.
            var motes = Particles(dome, "Oculus Light", new Vector3(0f, apex - 0.5f, 0f), new Vector3(1.8f, 0.5f, 1.8f), 10f,
                new Color(1f, 0.95f, 0.85f, 0.7f), new Color(1f, 0.9f, 0.75f, 0.5f), 0.02f, 0.05f, Sparkle(), 0.15f, 0.35f);
            var shape = motes.shape;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var main = motes.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(20f, 30f);
        }

        /// <summary>A two-tier gold chandelier hung on a long chain from the oculus, with candles and crystal drops.</summary>
        static void Chandelier(Transform root, Mats m, float radius)
        {
            var ch = new GameObject("Chandelier").transform;
            ch.SetParent(root, false);
            ch.localPosition = new Vector3(0f, 5.2f, 0f);
            float chainTop = Apex(radius) - 0.12f - 5.2f;
            Tube(ch, "Chain", m.gold, new[] { new Vector3(0f, 0.9f, 0f), new Vector3(0f, chainTop, 0f) }, 0.025f);
            var body = new[]
            {
                new Vector2(0.02f, -0.75f), new Vector2(0.09f, -0.7f), new Vector2(0.05f, -0.55f), new Vector2(0.14f, -0.35f),
                new Vector2(0.08f, -0.15f), new Vector2(0.06f, 0.1f), new Vector2(0.12f, 0.3f), new Vector2(0.05f, 0.5f),
                new Vector2(0.04f, 0.8f), new Vector2(0.08f, 0.88f), new Vector2(0.02f, 0.95f),
            };
            MeshObject("Body", ch, Lathe("PalaceChandelierBody", body, 16, true), m.gold);
            var crystal = SaveMesh(CreateCrystal(0.03f, 0.12f), "PalaceDrop");
            foreach (var (count, reach, height) in new[] { (12, 0.95f, -0.25f), (8, 0.55f, 0.25f) })
            {
                MeshObject($"Ring {count}", ch, Lathe($"PalaceChandelierRing_{count}", new[]
                {
                    new Vector2(reach * 0.6f, height - 0.03f), new Vector2(reach * 0.6f + 0.03f, height), new Vector2(reach * 0.6f, height + 0.03f),
                }, 32, true), m.gold);
                for (int k = 0; k < count; k++)
                {
                    float a = k * 360f / count;
                    var cup = OnCircle(a, reach, height + 0.08f);
                    Tube(ch, "Arm", m.gold, new[] { new Vector3(0f, height - 0.15f, 0f), OnCircle(a, reach * 0.55f, height - 0.2f), OnCircle(a, reach * 0.9f, height - 0.05f), cup }, 0.016f);
                    var dish = CreatePrimitive(PrimitiveType.Cylinder, "Cup", ch, m.gold, false);
                    dish.transform.localPosition = cup;
                    dish.transform.localScale = new Vector3(0.08f, 0.012f, 0.08f);
                    var candle = CreatePrimitive(PrimitiveType.Cylinder, "Candle", ch, m.candle, false);
                    candle.transform.localPosition = cup + Vector3.up * 0.09f;
                    candle.transform.localScale = new Vector3(0.035f, 0.08f, 0.035f);
                    var flame = CreatePrimitive(PrimitiveType.Sphere, "Flame", ch, m.flame, false);
                    flame.transform.localPosition = cup + Vector3.up * 0.2f;
                    flame.transform.localScale = new Vector3(0.028f, 0.055f, 0.028f);
                    flame.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    var drop = MeshObject("Crystal Drop", ch, crystal, m.crystal);
                    drop.transform.localPosition = OnCircle(a + 180f / count, reach * 0.75f, height - 0.22f);
                    drop.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            PointLight(ch, "Chandelier Light", new Vector3(0f, -0.2f, 0f), new Color(1f, 0.85f, 0.62f), 14f, 2.2f);
        }

        // =====================================================================
        // Mesh helpers
        // =====================================================================

        /// <summary>
        /// A surface of revolution: <paramref name="profile"/> points (x = radius, y = height) swept around the
        /// Y axis. Each profile step gets its own ring of vertices, so mouldings keep crisp edges. Faces point
        /// away from the axis (<paramref name="outward"/>, for things seen from outside like columns) or toward
        /// it (for things seen from inside the room like the cornice and the vault), following the profile.
        /// </summary>
        public static Mesh Lathe(string name, IList<Vector2> profile, int segments, bool outward)
        {
            var b = new Bits(1);
            for (int i = 0; i < profile.Count - 1; i++)
            {
                var p0 = profile[i];
                var p1 = profile[i + 1];
                var d = p1 - p0;
                if (d.sqrMagnitude < 1e-8f)
                    continue;
                var n2 = (outward ? new Vector2(d.y, -d.x) : new Vector2(-d.y, d.x)).normalized;
                int start = b.v.Count;
                for (int k = 0; k <= segments; k++)
                {
                    float a = k * Mathf.PI * 2f / segments;
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    var n = (dir * n2.x + Vector3.up * n2.y).normalized;
                    b.Vert(dir * p0.x + Vector3.up * p0.y, n, Vector2.zero);
                    b.Vert(dir * p1.x + Vector3.up * p1.y, n, Vector2.zero);
                }
                for (int k = 0; k < segments; k++)
                {
                    int a0 = start + k * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                    var n = b.n[a0] + b.n[b0];
                    var cross = Vector3.Cross(b.v[a1] - b.v[a0], b.v[b1] - b.v[a0]);
                    if (cross.sqrMagnitude < 1e-12f)
                        cross = Vector3.Cross(b.v[b1] - b.v[a0], b.v[b0] - b.v[a0]) * -1f;
                    b.sub[0].AddRange(Vector3.Dot(cross, n) >= 0f ? new[] { a0, a1, b1, a0, b1, b0 } : new[] { a0, b1, a1, a0, b0, b1 });
                }
            }
            return b.ToMesh(name);
        }

        /// <summary>A thin round tube through <paramref name="points"/> (an arm, a rib, a chain), as its own object.</summary>
        static void Tube(Transform parent, string name, Material mat, Vector3[] points, float radius)
        {
            const int sides = 6;
            var b = new Bits(1);
            var t0 = (points[1] - points[0]).normalized;
            var normal = Vector3.Cross(t0, Mathf.Abs(t0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            for (int i = 0; i < points.Length; i++)
            {
                var tangent = (i == 0 ? points[1] - points[0] : i == points.Length - 1 ? points[i] - points[i - 1] : points[i + 1] - points[i - 1]).normalized;
                normal = Vector3.ProjectOnPlane(normal, tangent).normalized;
                var binormal = Vector3.Cross(tangent, normal);
                for (int k = 0; k <= sides; k++)
                {
                    float a = k * Mathf.PI * 2f / sides;
                    var outward = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    b.Vert(points[i] + outward * radius, outward, Vector2.zero);
                }
            }
            for (int i = 0; i < points.Length - 1; i++)
            for (int k = 0; k < sides; k++)
            {
                int a = i * (sides + 1) + k, c = a + sides + 1;
                b.sub[0].AddRange(new[] { a, a + 1, c, a + 1, c + 1, c });
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(b.v);
            mesh.SetNormals(b.n);
            mesh.SetTriangles(b.sub[0], 0);
            mesh.RecalculateBounds();
            MeshObject(name, parent, mesh, mat).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
