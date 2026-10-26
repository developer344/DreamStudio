using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using static DreamStudio.Editor.DreamBuildUtils;

namespace DreamStudio.Editor
{
    public static partial class DreamWorldBuilder
    {
        // =====================================================================
        // 3. Glass Garden: claustrophobia. Push back the closing walls to find the garden beyond.
        // =====================================================================

        const float RoomHalf = 2.5f;
        const float RoomHeight = 3f;
        const float GlassRadius = 28f;
        const float GlassWallHeight = 7f;
        const float DomeRise = 13f;

        /// <summary>
        /// The player wakes in a small, dim room whose walls and ceiling slowly close in (<see cref="DreamClosingRoom"/>).
        /// Pushing a wall back far enough makes the room fall open, revealing that it stood all along in the middle
        /// of a vast garden under a glass dome at sunset: a paved plaza, gravel paths, flower beds, clipped hedges and
        /// topiary, rose arches, a fountain, a lily pond, potted palms, benches and blossoming trees.
        /// </summary>
        static void BuildGlassGarden(World world)
        {
            var ctx = SetupWorld(world, Vector3.zero, 0f, true);
            var root = ctx.root;
            // A low sun ahead and to the left, so the sunset glows through the glass and shadows stretch long.
            ctx.sun.transform.rotation = Quaternion.Euler(14f, 150f, 0f);

            var nightmare = ctx.nightmare;
            nightmare.tokenName = "Garden";
            nightmare.introMessage = "You wake in a small, airless room.\nThe walls are moving.\nThey're closing in...";
            nightmare.dreamMessage = "Sunlight pours in through the glass.\nThe garden was here all along.\nA portal home waits behind you.";
            ApplyAtmosphere(new Atmosphere
            {
                sky = Sky("Garden_Sky_Dream", new Color(0.48f, 0.46f, 0.7f), new Color(0.99f, 0.66f, 0.48f),
                    new Color(0.92f, 0.6f, 0.52f), new Color(1f, 0.86f, 0.72f), 25f),
                fogColor = new Color(0.98f, 0.8f, 0.68f),
                fogDensity = 0.005f,
                ambientSky = new Color(0.75f, 0.68f, 0.8f),
                ambientEquator = new Color(0.98f, 0.78f, 0.65f),
                ambientGround = new Color(0.55f, 0.5f, 0.45f),
                sunColor = new Color(1f, 0.72f, 0.5f),
                sunIntensity = 1.25f,
            }, ctx.sun);
            ctx.vision.nightmare = NightmareLook(new Color(0.36f, 0.34f, 0.5f));

            var lawn = Lit("Garden_Lawn", new Color(0.62f, 0.75f, 0.45f), 0.1f, new Color(0.01f, 0.012f, 0.008f));
            var stone = Lit("Garden_Stone", new Color(0.9f, 0.86f, 0.78f), 0.2f, new Color(0.02f, 0.02f, 0.02f));
            var gravel = Lit("Garden_Gravel", new Color(0.88f, 0.8f, 0.68f), 0.1f, Color.black);
            var hedge = Lit("Garden_Hedge", new Color(0.36f, 0.55f, 0.34f), 0.1f, Color.black);
            var iron = Lit("Garden_Iron", new Color(0.95f, 0.94f, 0.9f), 0.5f, new Color(0.03f, 0.03f, 0.03f));
            var terracotta = Lit("Garden_Pot", new Color(0.78f, 0.45f, 0.33f), 0.2f, Color.black);
            var water = Lit("Garden_Water", new Color(0.45f, 0.72f, 0.75f), 0.95f, new Color(0.04f, 0.08f, 0.09f));
            var rose = Lit("Garden_Rose", new Color(0.95f, 0.55f, 0.55f), 0.4f, new Color(0.08f, 0.03f, 0.03f));
            var bloom = Lit("Garden_Bloom", new Color(1f, 0.85f, 0.88f), 0.4f, new Color(0.06f, 0.04f, 0.04f));
            var wood = Lit("Garden_Bench", new Color(0.6f, 0.45f, 0.32f), 0.2f, Color.black);
            var lily = Lit("Garden_LilyPad", new Color(0.45f, 0.65f, 0.38f), 0.3f, Color.black);

            Ground(root, lawn, 120f);
            Boundary(root, GlassRadius - 2.5f, 48);

            Glasshouse(root, stone, iron);
            var sunDir = -ctx.sun.transform.forward;
            var sunDisc = CreatePrimitive(PrimitiveType.Sphere, "Setting Sun", root,
                Lit("Garden_SunDisc", new Color(1f, 0.6f, 0.42f), 0.5f, new Color(2.6f, 1.3f, 0.7f)), false);
            sunDisc.transform.localPosition = sunDir * 140f;
            sunDisc.transform.localRotation = Quaternion.LookRotation(-sunDir);
            sunDisc.transform.localScale = new Vector3(14f, 14f, 1f);
            sunDisc.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // --- Paving, paths and features.
            var paving = Group("Paths", root).transform;
            Part(PrimitiveType.Cylinder, "Plaza", paving, stone, new Vector3(0f, 0.02f, 0f), new Vector3(11.4f, 0.02f, 11.4f));
            MeshObject("Ring Path", paving, DreamPalace.Lathe("GardenRingPath", new[] { new Vector2(16.1f, 0.016f), new Vector2(13.9f, 0.016f) }, 96, true), gravel);
            for (int k = 0; k < 4; k++)
            {
                var dir = Quaternion.Euler(0f, k * 90f, 0f);
                var path = Box("Path", paving, gravel, dir * new Vector3(0f, 0.015f, 15f), new Vector3(2.2f, 0.01f, 19f), false);
                path.transform.localRotation = dir;
            }
            var fountainAt = new Vector3(0f, 0f, 15f);
            var pondAt = new Vector3(-15f, 0f, 0f);
            Part(PrimitiveType.Cylinder, "Fountain Plaza", paving, stone, fountainAt + Vector3.up * 0.022f, new Vector3(7f, 0.02f, 7f));
            Fountain(root, fountainAt, stone, water);
            Pond(root, pondAt, stone, water, lily, bloom);

            // Clipped hedges along the paths, with topiary balls in pots where they meet the ring path.
            var hedges = Group("Hedges", root).transform;
            for (int k = 0; k < 4; k++)
            {
                var dir = Quaternion.Euler(0f, k * 90f, 0f);
                foreach (int side in new[] { -1, 1 })
                    foreach (var (from, to) in new[] { (6.2f, 13.3f), (16.7f, 23.6f) })
                    {
                        var h = Box("Hedge", hedges, hedge, dir * new Vector3(side * 1.55f, 0.3f, (from + to) * 0.5f), new Vector3(0.55f, 0.6f, to - from), true);
                        h.transform.localRotation = dir;
                        foreach (float r in new[] { from - 0.4f, to + 0.4f })
                            if (r > 7f && r < 23f)
                                Topiary(hedges, dir * new Vector3(side * 1.55f, 0f, r), terracotta, hedge);
                    }
            }

            // Rose arches over the east path.
            var arches = Group("Rose Arches", root).transform;
            var leaves = DreamTrees.Foliage("Forest_Foliage_A", new Color(0.85f, 0.95f, 0.8f), false);
            foreach (float x in new[] { 7.5f, 9.5f, 11.5f, 18.5f, 20.5f, 22.5f })
                RoseArch(arches, new Vector3(x, 0f, 0f), iron, leaves, rose, bloom);

            // Benches on the ring path, looking inward.
            for (int k = 0; k < 4; k++)
                Bench(root, Quaternion.Euler(0f, 45f + k * 90f, 0f), 16.6f, wood, iron);

            // Trees: a ring of leafy and blossoming trees near the glass, and four blossom trees in the beds.
            var trees = Group("Trees", root).transform;
            var treeBark = Lit("Forest_TreeBark", new Color(0.95f, 0.92f, 0.88f), 0.08f, new Color(0.012f, 0.01f, 0.008f));
            var blossom = DreamTrees.Foliage("Forest_Foliage_Blossom", new Color(0.95f, 0.97f, 0.92f), true);
            var variants = DreamTrees.LeafyVariants("GardenTree", 4);
            var treeSpots = new List<Vector3>();
            for (int k = 0; k < 16; k++)
            {
                float angle = 11.25f + k * 22.5f;
                if (Mathf.Abs(Mathf.DeltaAngle(angle, Mathf.Round(angle / 90f) * 90f)) < 10f)
                    continue;
                var at = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * Random.Range(21.5f, 23.5f);
                treeSpots.Add(at);
                DreamTrees.Place(trees, variants[k % variants.Length], at, Random.Range(0f, 360f), Random.Range(0.8f, 0.95f), treeBark, k % 3 == 0 ? blossom : leaves);
            }
            foreach (float angle in new[] { 45f, 135f, 225f, 315f })
            {
                var at = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 10f;
                treeSpots.Add(at);
                DreamTrees.Place(trees, variants[0], at, Random.Range(0f, 360f), 0.7f, treeBark, blossom);
            }

            // --- Lawn, flower beds, potted palms and flowering shrubs.
            Plantings(root, treeSpots, fountainAt, pondAt, terracotta, blossom);

            // --- The closing room, in the middle of it all.
            ClosingRoom(root);

            // --- Dream-only: petals and golden motes drifting in the evening light.
            var dreamy = Group("Garden Air", root);
            Particles(dreamy.transform, "Petals", new Vector3(0f, 3f, 0f), new Vector3(40f, 5f, 40f), 40f,
                new Color(1f, 0.82f, 0.86f), new Color(1f, 0.95f, 0.9f), 0.04f, 0.08f, ctx.sparkle, 0.05f, 0.25f);
            Particles(dreamy.transform, "Golden Motes", new Vector3(0f, 2f, 0f), new Vector3(30f, 4f, 30f), 50f,
                new Color(1f, 0.85f, 0.5f), new Color(1f, 0.75f, 0.5f), 0.02f, 0.05f, ctx.sparkle);
            var home = CreateWayHome(root, new Vector3(0f, 0f, -9f), 0f, stone);

            nightmare.nightmareOnly = new GameObject[0];
            nightmare.dreamOnly = new[] { home, dreamy };
            foreach (var go in nightmare.dreamOnly)
                go.SetActive(false);
        }

        /// <summary>
        /// The glasshouse: a low stone plinth, glass walls and a glass dome on a white-painted iron frame of
        /// posts, rings and ribs (their shadows fall long across the garden at sunset), with a lantern on top.
        /// </summary>
        static void Glasshouse(Transform root, Material stone, Material iron)
        {
            var house = Group("Glasshouse", root).transform;
            var glass = LoadOrCreateMaterial("Garden_Glass", "Universal Render Pipeline/Lit");
            glass.SetColor("_BaseColor", new Color(0.86f, 0.93f, 0.95f, 0.12f));
            glass.SetFloat("_Smoothness", 0.95f);
            glass.SetFloat("_Surface", 1f);
            glass.SetFloat("_Blend", 0f);
            glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_ZWrite", 0f);
            glass.SetFloat("_Cull", (float)CullMode.Off);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.renderQueue = (int)RenderQueue.Transparent;
            glass.SetOverrideTag("RenderType", "Transparent");
            UnityEditor.EditorUtility.SetDirty(glass);

            float R = GlassRadius;
            MeshObject("Plinth", house, DreamPalace.Lathe("GardenPlinth", new[] { new Vector2(R, 0f), new Vector2(R, 0.8f), new Vector2(R + 0.35f, 0.8f) }, 96, false), stone);
            var wall = MeshObject("Glass Wall", house, DreamPalace.Lathe("GardenGlassWall", new[] { new Vector2(R, 0.8f), new Vector2(R, GlassWallHeight) }, 96, false), glass);
            wall.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var domeProfile = new List<Vector2>();
            for (int i = 0; i <= 24; i++)
            {
                float phi = i / 24f * Mathf.PI * 0.5f * 0.96f;
                domeProfile.Add(new Vector2(R * Mathf.Cos(phi), GlassWallHeight + DomeRise * Mathf.Sin(phi)));
            }
            var dome = MeshObject("Glass Dome", house, DreamPalace.Lathe("GardenDome", domeProfile, 96, false), glass);
            dome.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Iron frame: wall posts, rings round the wall, ribs up the dome and rings across it.
            for (int k = 0; k < 36; k++)
            {
                var dir = Quaternion.Euler(0f, k * 10f, 0f) * Vector3.forward;
                Strut(house, iron, dir * (R - 0.05f) + Vector3.up * 0.8f, dir * (R - 0.05f) + Vector3.up * GlassWallHeight, 0.07f);
            }
            foreach (float y in new[] { 0.8f, 3.9f, GlassWallHeight })
                MeshObject("Ring", house, DreamPalace.Lathe($"GardenRing_{y}", new[]
                {
                    new Vector2(R + 0.02f, y - 0.07f), new Vector2(R - 0.08f, y - 0.05f), new Vector2(R - 0.08f, y + 0.05f), new Vector2(R + 0.02f, y + 0.07f),
                }, 96, false), iron);
            for (int k = 0; k < 24; k++)
            {
                var dir = Quaternion.Euler(0f, k * 15f, 0f) * Vector3.forward;
                var rib = new List<Vector3>();
                foreach (var p in domeProfile)
                    rib.Add(dir * (p.x - 0.08f) + Vector3.up * (p.y - 0.05f));
                for (int i = 1; i < rib.Count; i++)
                    Strut(house, iron, rib[i - 1], rib[i], 0.06f);
            }
            foreach (int i in new[] { 8, 15, 20 })
            {
                var p = domeProfile[i];
                MeshObject("Dome Ring", house, DreamPalace.Lathe($"GardenDomeRing_{i}", new[]
                {
                    new Vector2(p.x + 0.02f, p.y - 0.07f), new Vector2(p.x - 0.1f, p.y - 0.05f), new Vector2(p.x - 0.1f, p.y + 0.05f), new Vector2(p.x + 0.02f, p.y + 0.07f),
                }, 64, false), iron);
            }
            var top = domeProfile[domeProfile.Count - 1];
            MeshObject("Lantern", house, DreamPalace.Lathe("GardenLantern", new[]
            {
                new Vector2(top.x + 0.2f, top.y - 0.1f), new Vector2(top.x + 0.2f, top.y + 0.8f), new Vector2(0.3f, top.y + 1.5f), new Vector2(0f, top.y + 1.9f),
            }, 24, true), iron);
        }

        /// <summary>A straight round bar between two points.</summary>
        static void Strut(Transform parent, Material mat, Vector3 a, Vector3 b, float thickness)
        {
            var d = b - a;
            var bar = CreatePrimitive(PrimitiveType.Cylinder, "Strut", parent, mat, false);
            bar.transform.localPosition = (a + b) * 0.5f;
            bar.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
            bar.transform.localScale = new Vector3(thickness, d.magnitude * 0.5f, thickness);
        }

        /// <summary>A tiered stone fountain in a round basin, water spraying from its top bowl.</summary>
        static void Fountain(Transform root, Vector3 at, Material stone, Material water)
        {
            var f = Group("Fountain", root).transform;
            f.localPosition = at;
            MeshObject("Basin", f, DreamPalace.Lathe("GardenBasin", new[]
            {
                new Vector2(2.2f, 0f), new Vector2(2.2f, 0.55f), new Vector2(2.05f, 0.62f), new Vector2(1.92f, 0.62f), new Vector2(1.92f, 0.15f), new Vector2(0f, 0.15f),
            }, 48, true), stone);
            Part(PrimitiveType.Cylinder, "Water", f, water, new Vector3(0f, 0.48f, 0f), new Vector3(3.85f, 0.01f, 3.85f));
            MeshObject("Tiers", f, DreamPalace.Lathe("GardenFountainTiers", new[]
            {
                new Vector2(0.35f, 0.15f), new Vector2(0.28f, 0.9f), new Vector2(0.9f, 1.1f), new Vector2(0.95f, 1.2f), new Vector2(0.2f, 1.25f),
                new Vector2(0.16f, 1.9f), new Vector2(0.5f, 2.05f), new Vector2(0.52f, 2.12f), new Vector2(0.1f, 2.15f), new Vector2(0.08f, 2.5f), new Vector2(0f, 2.55f),
            }, 32, true), stone);
            Part(PrimitiveType.Cylinder, "Bowl Water", f, water, new Vector3(0f, 1.18f, 0f), new Vector3(1.75f, 0.01f, 1.75f));
            var spray = Particles(f, "Spray", new Vector3(0f, 2.55f, 0f), new Vector3(0.1f, 0.1f, 0.1f), 60f,
                new Color(0.85f, 0.95f, 1f), new Color(1f, 1f, 1f), 0.02f, 0.05f, Sparkle(), 1.4f, 2.2f);
            var main = spray.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.6f);
            main.gravityModifier = 0.9f;
            var shape = spray.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        /// <summary>A round lily pond with a stone rim, lily pads and a few pink blooms.</summary>
        static void Pond(Transform root, Vector3 at, Material stone, Material water, Material pad, Material bloom)
        {
            var p = Group("Lily Pond", root).transform;
            p.localPosition = at;
            MeshObject("Rim", p, DreamPalace.Lathe("GardenPondRim", new[]
            {
                new Vector2(3.4f, 0f), new Vector2(3.4f, 0.25f), new Vector2(3.02f, 0.28f), new Vector2(3.0f, 0.05f), new Vector2(0f, 0.05f),
            }, 48, true), stone);
            Part(PrimitiveType.Cylinder, "Water", p, water, new Vector3(0f, 0.16f, 0f), new Vector3(6f, 0.01f, 6f));
            for (int i = 0; i < 12; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Mathf.Sqrt(Random.value) * 2.4f;
                var at2 = new Vector3(Mathf.Cos(a) * r, 0.175f, Mathf.Sin(a) * r);
                float s = Random.Range(0.35f, 0.6f);
                Part(PrimitiveType.Cylinder, "Lily Pad", p, pad, at2, new Vector3(s, 0.004f, s));
                if (i % 3 == 0)
                    Part(PrimitiveType.Sphere, "Lily", p, bloom, at2 + Vector3.up * 0.05f, new Vector3(0.14f, 0.09f, 0.14f));
            }
        }

        /// <summary>A clipped ball of box hedge on a short stem, in a terracotta pot.</summary>
        static void Topiary(Transform parent, Vector3 at, Material pot, Material hedge)
        {
            MeshObject("Pot", parent, DreamPalace.Lathe("GardenTopiaryPot", new[]
            {
                new Vector2(0.2f, 0f), new Vector2(0.28f, 0.42f), new Vector2(0.32f, 0.45f), new Vector2(0.3f, 0.5f), new Vector2(0f, 0.5f),
            }, 16, true), pot).transform.localPosition = at;
            Part(PrimitiveType.Cylinder, "Stem", parent, hedge, at + Vector3.up * 0.7f, new Vector3(0.06f, 0.25f, 0.06f));
            Part(PrimitiveType.Sphere, "Topiary", parent, hedge, at + Vector3.up * 1.2f, Vector3.one * 0.75f);
        }

        /// <summary>An iron arch over the path, wound with leafy climbing roses.</summary>
        static void RoseArch(Transform parent, Vector3 at, Material iron, Material leaves, Material rose, Material bloom)
        {
            const float half = 1.3f, legs = 2.1f;
            var points = new List<Vector3>();
            foreach (int side in new[] { -1, 1 })
                Strut(parent, iron, at + new Vector3(0f, 0f, side * half), at + new Vector3(0f, legs, side * half), 0.06f);
            for (int k = 0; k <= 10; k++)
            {
                float a = Mathf.PI * k / 10f;
                points.Add(at + new Vector3(0f, legs + Mathf.Sin(a) * half, Mathf.Cos(a) * half));
            }
            for (int k = 1; k < points.Count; k++)
                Strut(parent, iron, points[k - 1], points[k], 0.06f);

            // Leafy cards and rose blooms climbing the legs and over the top.
            var bits = new DreamUnderstory.Bits(1);
            var climb = new List<Vector3>(points);
            foreach (int side in new[] { -1, 1 })
                for (float y = 0.4f; y < legs; y += 0.45f)
                    climb.Add(at + new Vector3(0f, y, side * half));
            foreach (var p in climb)
            {
                for (int c = 0; c < 2; c++)
                {
                    var facing = new Vector3(Random.Range(-1f, 1f), Random.Range(-0.5f, 1f), Random.Range(-1f, 1f)).normalized;
                    var right = Vector3.Cross(facing, Vector3.up).normalized;
                    if (right.sqrMagnitude < 0.01f)
                        right = Vector3.right;
                    var up = Vector3.Cross(right, facing);
                    float s = Random.Range(0.25f, 0.4f);
                    var center = p + Random.insideUnitSphere * 0.12f;
                    int start = bits.v.Count;
                    var n = (facing + Vector3.up * 0.3f).normalized;
                    bits.Vert(center - right * s - up * s, n, new Vector2(0f, 0f));
                    bits.Vert(center + right * s - up * s, n, new Vector2(1f, 0f));
                    bits.Vert(center + right * s + up * s, n, new Vector2(1f, 1f));
                    bits.Vert(center - right * s + up * s, n, new Vector2(0f, 1f));
                    bits.Quad(0, start, start + 1, start + 2, start + 3);
                }
                if (Random.value < 0.6f)
                    Part(PrimitiveType.Sphere, "Rose", parent, Random.value < 0.6f ? rose : bloom, p + Random.insideUnitSphere * 0.15f, Vector3.one * Random.Range(0.08f, 0.13f));
            }
            MeshObject("Climbing Roses", parent, bits.ToMesh($"GardenRoseArch_{Mathf.RoundToInt(at.x * 10f)}"), leaves);
        }

        /// <summary>A slatted wooden bench on iron legs, at <paramref name="radius"/> along <paramref name="heading"/>, facing the middle.</summary>
        static void Bench(Transform root, Quaternion heading, float radius, Material wood, Material iron)
        {
            var b = Group("Bench", root).transform;
            b.localPosition = heading * Vector3.forward * radius;
            b.localRotation = heading * Quaternion.Euler(0f, 180f, 0f); // +Z faces the middle
            for (int s = 0; s < 4; s++)
                Box("Seat Slat", b, wood, new Vector3(0f, 0.45f, -0.18f + s * 0.12f), new Vector3(1.6f, 0.04f, 0.09f));
            for (int s = 0; s < 3; s++)
                Box("Back Slat", b, wood, new Vector3(0f, 0.62f + s * 0.13f, -0.3f), new Vector3(1.6f, 0.08f, 0.03f), false);
            foreach (int side in new[] { -1, 1 })
            {
                Box("Leg", b, iron, new Vector3(side * 0.7f, 0.22f, 0f), new Vector3(0.05f, 0.45f, 0.45f), false);
                Box("Back Post", b, iron, new Vector3(side * 0.7f, 0.65f, -0.3f), new Vector3(0.05f, 0.45f, 0.05f), false);
            }
        }

        /// <summary>
        /// The garden's planting: short lawn grass and clover; flower beds of mixed wildflowers and leafy
        /// plants in two bands round the middle; flowering shrubs in the beds; potted palms round the plaza.
        /// Merged into 20 m squares like the forest floor.
        /// </summary>
        static void Plantings(Transform root, List<Vector3> trees, Vector3 fountainAt, Vector3 pondAt, Material pot, Material blossom)
        {
            var settings = new DreamUnderstory.Settings { prefix = "Garden", groundHeight = GroundHeight, groundNormal = GroundNormal, trees = trees, radius = GlassRadius - 2.8f, seed = 6601 };
            var c = DreamUnderstory.NewContext(settings);
            var plants = DreamTrees.Cutout("Garden_Plants", new Color(0.95f, 0.97f, 0.92f), DreamTextures.UnderstoryAtlas());

            bool OnPath(Vector3 p)
            {
                float r = new Vector2(p.x, p.z).magnitude;
                return r < 6f || Mathf.Abs(p.x) < 1.9f || Mathf.Abs(p.z) < 1.9f || Mathf.Abs(r - 15f) < 1.4f
                    || FlatDistance(p, fountainAt) < 3.8f || FlatDistance(p, pondAt) < 3.8f;
            }
            bool InBed(Vector3 p)
            {
                float r = new Vector2(p.x, p.z).magnitude;
                return !OnPath(p) && ((r > 6.8f && r < 13.2f) || (r > 16.9f && r < 23.2f));
            }

            DreamUnderstory.Scatter(c, 11000, p => OnPath(p) ? 0f : InBed(p) ? 0.15f : 0.55f, p =>
                DreamUnderstory.Crossed(c, c.At(p).Layer("Plants"), p, Random.Range(0.25f, 0.4f), Random.Range(0.12f, 0.22f), 3, DreamTextures.Understory.Grass));
            DreamUnderstory.Scatter(c, 3000, p => OnPath(p) || InBed(p) ? 0f : 0.35f, p =>
                DreamUnderstory.Flat(c, c.At(p).Layer("Plants"), p, Random.Range(0.25f, 0.4f), Random.Range(0f, 360f), 0.03f, DreamTextures.Understory.Clover, 1));
            DreamUnderstory.Scatter(c, 9000, p => InBed(p) ? 0.85f : 0f, p =>
                DreamUnderstory.Crossed(c, c.At(p).Layer("Plants"), p, Random.Range(0.25f, 0.35f), Random.Range(0.35f, 0.6f), 2, DreamTextures.Understory.Flowers));
            DreamUnderstory.Scatter(c, 2500, p => InBed(p) ? 0.5f : 0f, p =>
                DreamUnderstory.Crossed(c, c.At(p).Layer("Plants"), p, Random.Range(0.25f, 0.35f), Random.Range(0.25f, 0.4f), 2, DreamTextures.Understory.Sprout));
            DreamUnderstory.Scatter(c, 400, p => InBed(p) && c.TreeDistance(p) > 1.5f ? 0.25f : 0f, p =>
                DreamUnderstory.Shrub(c, c.At(p).Layer("Shrubs"), p, Random.Range(0.6f, 1.1f)));

            // Potted palms round the plaza, between the paths.
            for (int k = 0; k < 8; k++)
            {
                var at = Quaternion.Euler(0f, 22.5f + k * 45f, 0f) * Vector3.forward * 6.3f;
                MeshObject("Palm Pot", root, DreamPalace.Lathe("GardenPalmPot", new[]
                {
                    new Vector2(0.3f, 0f), new Vector2(0.42f, 0.6f), new Vector2(0.48f, 0.65f), new Vector2(0.45f, 0.72f), new Vector2(0f, 0.72f),
                }, 20, true), pot).transform.localPosition = at;
                Part(PrimitiveType.Cylinder, "Palm Trunk", root, Lit("Forest_TreeBark", new Color(0.95f, 0.92f, 0.88f), 0.08f, new Color(0.012f, 0.01f, 0.008f)),
                    at + Vector3.up * 1.4f, new Vector3(0.14f, 0.7f, 0.14f));
                DreamUnderstory.Fern(c, c.At(at).Layer("Ferns"), at + Vector3.up * 2.05f, 1.6f, DreamTextures.Understory.Fern);
            }

            DreamUnderstory.EmitChunks(c, root, new Dictionary<string, Material[]>
            {
                ["Plants"] = new[] { plants },
                ["Ferns"] = new[] { plants },
                ["Shrubs"] = new[] { blossom },
            });
        }

        /// <summary>
        /// The small room the player wakes in: wallpapered walls with a locked door ahead and a painting of a
        /// sunlit garden on the left, a plank floor, a low ceiling with a bare bulb, and a thread of light under
        /// each wall that brightens as it is pushed back. Driven by <see cref="DreamClosingRoom"/>.
        /// </summary>
        static void ClosingRoom(Transform root)
        {
            var paper = Lit("Room_Wallpaper", new Color(0.55f, 0.5f, 0.6f), 0.2f, new Color(0.02f, 0.02f, 0.03f));
            var floorMat = Lit("Room_Floor", new Color(0.95f, 0.93f, 0.9f), 0.2f, Color.black);
            var ceilingMat = Lit("Room_Ceiling", new Color(0.7f, 0.68f, 0.66f), 0.2f, Color.black);
            var doorMat = Lit("Room_Door", new Color(0.45f, 0.3f, 0.25f), 0.3f, Color.black);
            var brass = Lit("Room_Brass", new Color(0.95f, 0.75f, 0.4f), 0.85f, new Color(0.2f, 0.12f, 0.04f));
            var leakMat = Lit("Room_Leak", new Color(1f, 0.8f, 0.55f), 0.5f, new Color(2.4f, 1.3f, 0.6f));
            var bulbMat = Lit("Room_Bulb", new Color(1f, 0.9f, 0.7f), 0.5f, new Color(2.2f, 1.8f, 1.1f));

            var room = Group("Closing Room", root);
            var closing = room.AddComponent<DreamClosingRoom>();
            closing.startHalfSize = RoomHalf;
            closing.startCeiling = RoomHeight;
            Box("Room Floor", room.transform, floorMat, new Vector3(0f, 0.025f, 0f), new Vector3(RoomHalf * 2f + 0.4f, 0.03f, RoomHalf * 2f + 0.4f), false);

            const float thickness = 0.2f;
            float width = RoomHalf * 2f + 1f;
            for (int i = 0; i < 4; i++)
            {
                var dir = Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward;
                var wall = Group(i == 0 ? "Wall (Door)" : $"Wall {i}", room.transform).transform;
                wall.localPosition = dir * RoomHalf;
                wall.localRotation = Quaternion.LookRotation(-dir); // +Z faces into the room
                Box("Room Wall", wall, paper, new Vector3(0f, RoomHeight * 0.5f, -thickness * 0.5f), new Vector3(width, RoomHeight, thickness));
                Box("Skirting", wall, doorMat, new Vector3(0f, 0.08f, 0.01f), new Vector3(width, 0.16f, 0.03f), false);
                Box("Picture Rail", wall, doorMat, new Vector3(0f, 2.3f, 0.01f), new Vector3(width, 0.04f, 0.03f), false);
                var leak = Box("Light Leak", wall, leakMat, new Vector3(0f, 0.012f, 0.05f), new Vector3(width - 0.4f, 0.02f, 0.06f), false);
                var leakRenderer = leak.GetComponent<Renderer>();
                leakRenderer.shadowCastingMode = ShadowCastingMode.Off;
                leakRenderer.enabled = false;
                closing.walls[i] = wall;
                closing.leaks[i] = leakRenderer;

                if (i == 0)
                {
                    // The locked door, straight ahead.
                    Box("Door Frame", wall, doorMat, new Vector3(-0.58f, 1.1f, 0.03f), new Vector3(0.08f, 2.25f, 0.06f), false);
                    Box("Door Frame", wall, doorMat, new Vector3(0.58f, 1.1f, 0.03f), new Vector3(0.08f, 2.25f, 0.06f), false);
                    Box("Door Frame", wall, doorMat, new Vector3(0f, 2.24f, 0.03f), new Vector3(1.24f, 0.08f, 0.06f), false);
                    var leaf = Box("Room Door", wall, doorMat, new Vector3(0f, 1.08f, 0.03f), new Vector3(1.06f, 2.12f, 0.05f), false);
                    Box("Door Panel", leaf.transform, paper, new Vector3(0f, 0.2f, 0.6f), new Vector3(0.7f, 0.35f, 0.3f), false);
                    var handle = Part(PrimitiveType.Sphere, "Door Handle", wall, brass, new Vector3(0.38f, 1.02f, 0.1f), Vector3.one * 0.08f);
                    handle.AddComponent<SphereCollider>().radius = 1.2f;
                    handle.AddComponent<XRSimpleInteractable>();
                    handle.AddComponent<DreamLockedDoor>().leaf = leaf.transform;
                }
                else if (i == 3)
                {
                    // On the left: a small painting of a garden in the sunset, in a gilt frame.
                    var frame = Group("Painting", wall).transform;
                    frame.localPosition = new Vector3(0f, 1.6f, 0.02f);
                    Box("Frame", frame, brass, Vector3.zero, new Vector3(0.9f, 0.7f, 0.04f), false);
                    Box("Sky", frame, Lit("Room_PaintedSky", new Color(0.99f, 0.7f, 0.55f), 0.2f, new Color(0.35f, 0.2f, 0.12f)), new Vector3(0f, 0.08f, 0.022f), new Vector3(0.78f, 0.42f, 0.01f), false);
                    Box("Lawn", frame, Lit("Room_PaintedLawn", new Color(0.56f, 0.7f, 0.45f), 0.2f, new Color(0.1f, 0.14f, 0.08f)), new Vector3(0f, -0.2f, 0.022f), new Vector3(0.78f, 0.16f, 0.01f), false);
                    Part(PrimitiveType.Sphere, "Painted Sun", frame, Lit("Room_PaintedSun", new Color(1f, 0.55f, 0.4f), 0.2f, new Color(0.8f, 0.35f, 0.2f)), new Vector3(0.2f, 0.05f, 0.03f), new Vector3(0.12f, 0.12f, 0.01f));
                    for (int k = -2; k <= 2; k++)
                        Box("Painted Glass", frame, brass, new Vector3(k * 0.16f, 0.12f, 0.03f), new Vector3(0.008f, 0.3f, 0.005f), false);
                    for (int k = 0; k < 9; k++)
                        Part(PrimitiveType.Sphere, "Painted Flower", frame, k % 2 == 0 ? Lit("Room_PaintedRose", new Color(0.95f, 0.55f, 0.6f), 0.2f, new Color(0.3f, 0.1f, 0.1f)) : bulbMat,
                            new Vector3(-0.34f + k * 0.085f, -0.17f + (k % 3) * 0.03f, 0.03f), new Vector3(0.04f, 0.04f, 0.01f));
                }
            }

            var ceiling = Group("Room Ceiling", room.transform).transform;
            ceiling.localPosition = new Vector3(0f, RoomHeight, 0f);
            Box("Ceiling", ceiling, ceilingMat, new Vector3(0f, 0.1f, 0f), new Vector3(width + 0.5f, 0.2f, width + 0.5f));
            Part(PrimitiveType.Cylinder, "Cord", ceiling, doorMat, new Vector3(0f, -0.25f, 0f), new Vector3(0.015f, 0.25f, 0.015f));
            var bulb = Part(PrimitiveType.Sphere, "Bare Bulb", ceiling, bulbMat, new Vector3(0f, -0.55f, 0f), new Vector3(0.1f, 0.13f, 0.1f));
            bulb.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            closing.bulb = PointLight(ceiling, "Bulb Light", new Vector3(0f, -0.65f, 0f), new Color(1f, 0.82f, 0.6f), 7f, 1.4f);
            closing.bulb.shadows = LightShadows.None;
            closing.ceiling = ceiling;
        }
    }
}
