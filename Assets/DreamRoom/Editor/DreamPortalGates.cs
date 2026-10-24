using UnityEngine;
using UnityEngine.Rendering;
using static DreamStudio.Editor.DreamBuildUtils;

namespace DreamStudio.Editor
{
    /// <summary>
    /// Dresses each world's portal in the Portal Room.
    ///
    /// Behind the portal sits a shallow, closed alcove with a softly glowing back panel, so the opening
    /// shows a solid backdrop instead of open sky (built in the wall's local space: +Z faces into the room).
    ///
    /// The portal's border is made of the world inside, bursting out of the rim into the room: gnarled
    /// branches, coral and kelp, or little buildings. Each piece slowly
    /// pushes out and sinks back, and sparkles in the world's colours drift out of the swirl. The border is
    /// built in the portal's local space (origin at the oval's center, +Z into the room) and parented to it.
    /// Nothing grows across the bottom of the rim, where players step in.
    /// </summary>
    static class DreamPortalGates
    {
        /// <summary>Depth of the alcove behind the wall; the back panel sits just behind the portal's barrier.</summary>
        const float AlcoveDepth = 0.45f;

        class Gate
        {
            public Transform root;     // alcove pieces, in wall space
            public Transform border;   // rim pieces, in portal space
            public float half;         // half the opening's width
            public float top;          // top of the opening
            public float back;         // z of the wall's outer surface
            public float stepTop;      // height of the portal's step
            public float portalY;      // height of the portal's center
            public float backingFront; // z of the back panel's visible face
        }

        public static void Build(Transform wall, Transform portal, int worldIndex, float opening, float top, float wallThickness, float stepHeight, Material stepMat)
        {
            var root = new GameObject("Alcove");
            root.transform.SetParent(wall, false);
            var border = new GameObject("Border");
            border.transform.SetParent(portal, false);
            var g = new Gate
            {
                root = root.transform,
                border = border.transform,
                half = opening * 0.5f,
                top = top,
                back = -wallThickness * 0.5f,
                stepTop = stepHeight,
                portalY = stepHeight + PortalHalfHeight,
            };

            var world = Worlds[worldIndex];
            var state = Random.state;
            Random.InitState(4111 + (world.seed - 1000) * 7919);
            switch (world.scene)
            {
                case "HauntedForest": HauntedForest(g, stepMat); break;
                case "SunkenDepths": SunkenDepths(g, stepMat); break;
                case "UpsideDownCity": UpsideDownCity(g, stepMat); break;
                default:
                    Alcove(g, Backdrop($"Gate_Back_{world.scene}", world.outer), stepMat, stepMat);
                    break;
            }

            // Sparkles in the world's colours drifting out of the swirl into the room.
            Particles(g.border, "Spilling Out", Vector3.forward * 0.05f, new Vector3(PortalHalfWidth * 1.6f, PortalHalfHeight * 1.6f, 0.05f), 8f,
                world.inner, world.outer, 0.03f, 0.08f, Sparkle(), 0.12f, 0.3f);
            PointLight(g.border, "Border Light", new Vector3(0f, PortalHalfHeight * 0.7f, 0.9f), Color.Lerp(world.inner, world.outer, 0.3f), 4.5f, 0.9f);
            Random.state = state;
        }

        // =====================================================================
        // The alcove
        // =====================================================================

        /// <summary>
        /// The closed recess behind the opening: a back panel (solid, so nothing can walk through), side
        /// linings that close the gap between the wall and the panel, a ceiling and a floor level with the step.
        /// </summary>
        static void Alcove(Gate g, Material backing, Material lining, Material floor)
        {
            float zMid = g.back - AlcoveDepth * 0.5f;
            float zPanel = g.back - AlcoveDepth - 0.05f;
            float width = g.half * 2f;
            g.backingFront = zPanel + 0.05f;

            Box("Backing", g.root, backing, new Vector3(0f, g.top * 0.5f, zPanel), new Vector3(width + 0.2f, g.top + 0.1f, 0.1f));
            for (int side = -1; side <= 1; side += 2)
                Box("Alcove Side", g.root, lining, new Vector3(side * (g.half + 0.05f), g.top * 0.5f, zMid), new Vector3(0.1f, g.top, AlcoveDepth), false);
            Box("Alcove Ceiling", g.root, lining, new Vector3(0f, g.top + 0.05f, zMid), new Vector3(width + 0.2f, 0.1f, AlcoveDepth), false);
            Box("Alcove Floor", g.root, floor, new Vector3(0f, g.stepTop * 0.5f, zMid), new Vector3(width, g.stepTop, AlcoveDepth), false);
        }

        /// <summary>A deep, softly glowing tint of a world's colour for the back panel.</summary>
        static Material Backdrop(string name, Color tint)
        {
            return Lit(name, Color.Lerp(tint, Color.black, 0.7f), 0.4f, Color.Lerp(tint, Color.black, 0.6f));
        }

        /// <summary>Tiny glowing dots on the back panel, only where the portal's oval doesn't cover it.</summary>
        static void Specks(Gate g, Material[] mats, int count, float size)
        {
            int placed = 0;
            for (int tries = 0; placed < count && tries < count * 30; tries++)
            {
                float x = Random.Range(-g.half + 0.08f, g.half - 0.08f);
                float y = Random.Range(g.stepTop + 0.1f, g.top - 0.08f);
                float ex = x / (PortalHalfWidth + 0.12f);
                float ey = (y - g.portalY) / (PortalHalfHeight + 0.12f);
                if (ex * ex + ey * ey < 1f)
                    continue;
                float s = size * Random.Range(0.6f, 1.2f);
                var speck = Part(PrimitiveType.Sphere, "Speck", g.root, mats[placed % mats.Length],
                    new Vector3(x, y, g.backingFront + 0.01f), new Vector3(s, s, s * 0.3f));
                speck.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                placed++;
            }
        }

        // =====================================================================
        // The border
        // =====================================================================

        /// <summary>
        /// Calls <paramref name="build"/> for <paramref name="count"/> spots spaced around the portal's rim,
        /// skipping the bottom where players step in. Each spot gets a "Piece" transform whose local +Y points
        /// out of the rim and toward the room (the way the piece bursts out), whose local +Z faces the room as
        /// much as it can, and whose scale varies a little. Build pieces from y = -0.15 (just inside the swirl)
        /// outward; about 0.9 m long at most.
        /// </summary>
        static void AroundRim(Gate g, int count, float phase, System.Action<Transform, int> build)
        {
            float a = PortalHalfWidth + 0.06f;
            float b = PortalHalfHeight + 0.06f;
            int index = 0;
            for (int i = 0; i < count; i++)
            {
                float deg = phase + i * 360f / count;
                if (Mathf.Abs(Mathf.DeltaAngle(deg, 270f)) < 50f)
                    continue;

                float t = deg * Mathf.Deg2Rad;
                var rim = new Vector3(Mathf.Cos(t) * a, Mathf.Sin(t) * b, 0.05f);
                var normal = new Vector3(Mathf.Cos(t) / a, Mathf.Sin(t) / b, 0f).normalized;
                var up = (normal * 0.6f + Vector3.forward * 0.8f).normalized;
                var right = Vector3.Cross(up, Vector3.forward).normalized;
                var forward = Vector3.Cross(right, up);

                var spot = new GameObject("Burst");
                spot.transform.SetParent(g.border, false);
                spot.transform.localPosition = rim;
                spot.transform.localRotation = Quaternion.LookRotation(forward, up);
                var emerge = spot.AddComponent<DreamEmerge>();
                emerge.distance = Random.Range(0.04f, 0.08f);
                emerge.speed = Random.Range(0.12f, 0.22f);

                var piece = new GameObject("Piece");
                piece.transform.SetParent(spot.transform, false);
                piece.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-12f, 12f));
                piece.transform.localScale = Vector3.one * Random.Range(0.85f, 1.15f);
                build(piece.transform, index++);
            }
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default)
        {
            var go = CreatePrimitive(type, name, parent, mat, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            return go;
        }

        /// <summary>A cylinder or box stretched between two points.</summary>
        static GameObject Limb(PrimitiveType type, string name, Transform parent, Material mat, Vector3 from, Vector3 to, float thickness)
        {
            var dir = to - from;
            float length = dir.magnitude;
            var go = CreatePrimitive(type, name, parent, mat, false);
            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            go.transform.localScale = new Vector3(thickness, type == PrimitiveType.Cylinder ? length * 0.5f : length, thickness);
            return go;
        }

        /// <summary>A chain of tapering cylinders through <paramref name="points"/>, with knots at the bends.</summary>
        static void Bent(Transform parent, string name, Material mat, float startThickness, float endThickness, params Vector3[] points)
        {
            for (int i = 1; i < points.Length; i++)
            {
                float k = (i - 1f) / Mathf.Max(1, points.Length - 2);
                float thickness = Mathf.Lerp(startThickness, endThickness, k);
                Limb(PrimitiveType.Cylinder, name, parent, mat, points[i - 1], points[i], thickness);
                if (i < points.Length - 1)
                    Part(PrimitiveType.Sphere, "Bend", parent, mat, points[i], Vector3.one * thickness);
            }
        }

        static GameObject Glow(Transform parent, string name, Material mat, Vector3 pos, float size)
        {
            var glow = Part(PrimitiveType.Sphere, name, parent, mat, pos, Vector3.one * size);
            glow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return glow;
        }

        /// <summary>A pivot that rocks gently, like weed in a current.</summary>
        static Transform Swaying(Transform parent, string name, Vector3 pivot, float angle, float speed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pivot;
            var sway = go.AddComponent<DreamSway>();
            sway.angle = angle;
            sway.speed = speed;
            return go.transform;
        }

        static Material Iron() => Lit("Gate_Iron", new Color(0.12f, 0.12f, 0.15f), 0.7f, Color.black);

        // =====================================================================
        // Haunted Forest: gnarled branches, clawing roots and ghost wisps reaching out of the portal
        // =====================================================================

        static void HauntedForest(Gate g, Material step)
        {
            var bark = Lit("Gate_Bark", new Color(0.24f, 0.17f, 0.12f), 0.15f, Color.black);
            var moss = Lit("Gate_Moss", new Color(0.28f, 0.42f, 0.22f), 0.05f, new Color(0.02f, 0.05f, 0.02f));
            var ghost = Lit("Gate_GhostGlow", new Color(0.7f, 1f, 0.75f), 0.5f, new Color(0.9f, 1.6f, 1f));
            var eyes = Lit("Gate_Eyes", new Color(0.9f, 1f, 0.5f), 0.5f, new Color(1.6f, 2f, 0.5f));
            Alcove(g, Lit("Gate_Back_HauntedForest", new Color(0.07f, 0.05f, 0.1f), 0.2f, new Color(0.06f, 0.03f, 0.11f)), bark, step);

            // Eyes watching from the dark corners behind the portal.
            foreach (var at in new[] { new Vector2(-1.25f, 3.65f), new Vector2(1.3f, 0.6f) })
                for (int e = -1; e <= 1; e += 2)
                {
                    var eye = Part(PrimitiveType.Sphere, "Eye", g.root, eyes, new Vector3(at.x + e * 0.06f, at.y, g.backingFront + 0.01f), new Vector3(0.07f, 0.045f, 0.02f));
                    eye.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }

            AroundRim(g, 18, 5f, (p, i) =>
            {
                float s = i % 2 == 0 ? 1f : -1f;
                switch (i % 3)
                {
                    case 0:
                        // A crooked branch with a forked twig and a mossy knot.
                        Bent(p, "Branch", bark, 0.16f, 0.07f,
                            new Vector3(0f, -0.15f, 0f), new Vector3(0.05f * s, 0.25f, 0.04f), new Vector3(-0.06f * s, 0.55f, 0.02f), new Vector3(0.08f * s, 0.85f, 0.07f));
                        Limb(PrimitiveType.Cylinder, "Twig", p, bark, new Vector3(-0.06f * s, 0.55f, 0.02f), new Vector3(0.2f * s, 0.72f, 0.04f), 0.05f);
                        Limb(PrimitiveType.Cylinder, "Twig", p, bark, new Vector3(0.05f * s, 0.25f, 0.04f), new Vector3(-0.18f * s, 0.42f, 0.06f), 0.045f);
                        Part(PrimitiveType.Sphere, "Moss", p, moss, new Vector3(0.05f * s, 0.25f, 0.06f), new Vector3(0.16f, 0.1f, 0.12f));
                        break;
                    case 1:
                        // A root clawing its way out, splitting into three fingers.
                        Bent(p, "Root", bark, 0.14f, 0.1f, new Vector3(0f, -0.15f, 0f), new Vector3(-0.03f * s, 0.18f, 0.02f), new Vector3(0f, 0.38f, 0.03f));
                        foreach (var tip in new[] { new Vector3(0.16f, 0.62f, 0.04f), new Vector3(-0.15f, 0.6f, 0.05f), new Vector3(0.01f, 0.7f, 0.1f) })
                            Bent(p, "Claw", bark, 0.06f, 0.03f, new Vector3(0f, 0.38f, 0.03f), Vector3.Lerp(new Vector3(0f, 0.38f, 0.03f), tip, 0.55f) + Vector3.forward * 0.04f, tip);
                        break;
                    default:
                        // A short twisted stub with a ghost wisp drifting off its tip.
                        Bent(p, "Stub", bark, 0.12f, 0.06f, new Vector3(0f, -0.15f, 0f), new Vector3(0.04f * s, 0.2f, 0.02f), new Vector3(-0.02f * s, 0.42f, 0.05f));
                        var wisp = Glow(p, "Ghost Wisp", ghost, new Vector3(0f, 0.6f, 0.08f), 0.13f);
                        var bob = wisp.AddComponent<DreamFloat>();
                        bob.bobHeight = 0.05f;
                        bob.bobSpeed = 0.4f;
                        bob.spin = Vector3.zero;
                        break;
                }
            });
        }

        // =====================================================================
        // Sunken Depths: coral, kelp and scallop shells growing out of the rim, with bubbles
        // =====================================================================

        static void SunkenDepths(Gate g, Material step)
        {
            var reef = Lit("Gate_Reef", new Color(0.36f, 0.42f, 0.48f), 0.3f, Color.black);
            var coralPink = Lit("Gate_Coral_Pink", new Color(1f, 0.45f, 0.6f), 0.4f, new Color(0.3f, 0.06f, 0.12f));
            var coralOrange = Lit("Gate_Coral_Orange", new Color(1f, 0.6f, 0.25f), 0.4f, new Color(0.3f, 0.12f, 0.03f));
            var kelp = Lit("Gate_Kelp", new Color(0.2f, 0.55f, 0.32f), 0.3f, new Color(0.02f, 0.1f, 0.04f));
            var shell = Lit("Gate_Shell", new Color(1f, 0.9f, 0.82f), 0.8f, new Color(0.18f, 0.14f, 0.12f));
            var pearl = Lit("Gate_Pearl", new Color(0.95f, 0.97f, 1f), 0.95f, new Color(0.9f, 1f, 1.2f));
            Alcove(g, Lit("Gate_Back_SunkenDepths", new Color(0.02f, 0.1f, 0.25f), 0.6f, new Color(0.02f, 0.14f, 0.32f)), reef, step);
            Specks(g, new[] { pearl }, 8, 0.06f);

            AroundRim(g, 18, 0f, (p, i) =>
            {
                switch (i % 4)
                {
                    case 0:
                    case 2:
                        Part(PrimitiveType.Sphere, "Reef Rock", p, reef, new Vector3(0f, -0.05f, 0f), new Vector3(0.22f, 0.16f, 0.2f));
                        Coral(p, new Vector3(0f, -0.1f, 0f), Vector3.up, Random.Range(0.6f, 0.8f), i % 4 == 0 ? coralPink : coralOrange);
                        break;
                    case 1:
                        // A kelp blade streaming out, swaying in the current.
                        var blade = Swaying(p, "Kelp", new Vector3(0f, -0.15f, 0f), 9f, 0.2f);
                        Part(PrimitiveType.Cube, "Stalk", blade, kelp, new Vector3(0f, 0.45f, 0f), new Vector3(0.06f, 0.9f, 0.015f));
                        for (float y = 0.2f; y < 0.85f; y += 0.2f)
                            Part(PrimitiveType.Sphere, "Leaf", blade, kelp, new Vector3(0.05f, y, 0f), new Vector3(0.09f, 0.2f, 0.02f), new Vector3(0f, 0f, -35f));
                        break;
                    default:
                        // A scallop shell opening toward the room, a pearl inside.
                        Limb(PrimitiveType.Cylinder, "Shell Stem", p, shell, new Vector3(0f, -0.15f, 0f), new Vector3(0f, 0.05f, 0f), 0.06f);
                        for (int r = 0; r < 8; r++)
                        {
                            float a = (-70f + r * 20f) * Mathf.Deg2Rad;
                            var end = new Vector3(Mathf.Sin(a) * 0.3f, 0.12f + Mathf.Cos(a) * 0.3f, 0f);
                            Limb(PrimitiveType.Cylinder, "Shell Rib", p, shell, new Vector3(0f, 0.05f, 0f), end, 0.06f);
                            Part(PrimitiveType.Sphere, "Shell Edge", p, shell, end, Vector3.one * 0.08f);
                        }
                        Part(PrimitiveType.Sphere, "Pearl", p, pearl, new Vector3(0f, 0.12f, 0.06f), Vector3.one * 0.1f);
                        break;
                }
            });

            // Bubbles rising off the rim.
            var bubbles = Particles(g.border, "Bubbles", new Vector3(0f, -0.4f, 0.4f), new Vector3(2.8f, 0.4f, 0.05f), 5f,
                new Color(0.75f, 0.95f, 1f), new Color(0.5f, 0.8f, 1f), 0.04f, 0.1f, Sparkle(), 0.25f, 0.45f);
            var main = bubbles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            var shape = bubbles.shape;
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        /// <summary>A branching coral: a stem with knobbed branches splaying off it.</summary>
        static void Coral(Transform parent, Vector3 basePos, Vector3 up, float size, Material mat)
        {
            up.Normalize();
            var tip = basePos + up * size;
            Limb(PrimitiveType.Cylinder, "Coral", parent, mat, basePos, tip, size * 0.16f);
            Part(PrimitiveType.Sphere, "Coral Tip", parent, mat, tip, Vector3.one * size * 0.2f);
            for (int b = 0; b < 3; b++)
            {
                var from = Vector3.Lerp(basePos, tip, 0.35f + b * 0.2f);
                var dir = Quaternion.Euler(Random.Range(-15f, 15f), 0f, (b % 2 == 0 ? 1f : -1f) * Random.Range(30f, 45f)) * up;
                var end = from + dir * size * 0.5f;
                Limb(PrimitiveType.Cylinder, "Coral Branch", parent, mat, from, end, size * 0.11f);
                Part(PrimitiveType.Sphere, "Coral Tip", parent, mat, end, Vector3.one * size * 0.14f);
            }
        }

        // =====================================================================
        // Upside-Down City: little lit-up buildings and street lamps jutting out of the rim every which way
        // =====================================================================

        static void UpsideDownCity(Gate g, Material step)
        {
            var brick = Lit("Gate_Brick", new Color(0.62f, 0.33f, 0.28f), 0.2f, new Color(0.04f, 0.015f, 0.01f));
            var concrete = Lit("Gate_Concrete", new Color(0.72f, 0.72f, 0.75f), 0.3f, new Color(0.04f, 0.04f, 0.05f));
            var iron = Iron();
            var window = Lit("Gate_Window", new Color(1f, 0.85f, 0.5f), 0.4f, new Color(1.6f, 1.2f, 0.5f));
            var neonPink = Lit("Gate_Neon_Pink", new Color(1f, 0.4f, 0.9f), 0.6f, new Color(1.8f, 0.5f, 1.5f));
            var neonTeal = Lit("Gate_Neon_Teal", new Color(0.3f, 1f, 0.85f), 0.6f, new Color(0.4f, 1.6f, 1.3f));
            Alcove(g, Lit("Gate_Back_UpsideDownCity", new Color(0.09f, 0.05f, 0.16f), 0.4f, new Color(0.1f, 0.04f, 0.18f)), brick, step);
            Specks(g, new[] { window, neonPink, neonTeal }, 12, 0.06f);

            AroundRim(g, 16, 0f, (p, i) =>
            {
                if (i % 3 == 2)
                {
                    // A street lamp sticking out of the rim, lit.
                    float s = i % 2 == 0 ? 1f : -1f;
                    Limb(PrimitiveType.Cylinder, "Lamp Post", p, iron, new Vector3(0f, -0.15f, 0f), new Vector3(0f, 0.6f, 0f), 0.035f);
                    Limb(PrimitiveType.Cylinder, "Lamp Arm", p, iron, new Vector3(0f, 0.6f, 0f), new Vector3(s * 0.16f, 0.66f, 0f), 0.025f);
                    Box("Lamp Hood", p, iron, new Vector3(s * 0.18f, 0.65f, 0f), new Vector3(0.1f, 0.03f, 0.1f), false);
                    Glow(p, "Lamp Bulb", window, new Vector3(s * 0.18f, 0.6f, 0f), 0.07f);
                    return;
                }

                // A little building jutting out roof-first, its front windows lit.
                float h = Random.Range(0.45f, 0.85f);
                float w = Random.Range(0.2f, 0.28f);
                const float d = 0.2f;
                Box("Building", p, i % 2 == 0 ? brick : concrete, new Vector3(0f, -0.15f + h * 0.5f, 0f), new Vector3(w, h, d), false);
                Box("Roof", p, concrete, new Vector3(0f, -0.15f + h + 0.02f, 0f), new Vector3(w + 0.03f, 0.04f, d + 0.03f), false);
                var lit = i % 4 == 0 ? neonPink : i % 4 == 1 ? neonTeal : window;
                int rows = Mathf.Min(4, Mathf.FloorToInt((h - 0.25f) / 0.12f));
                for (int r = 0; r < rows; r++)
                    for (int c = -1; c <= 1; c += 2)
                        Box("Window", p, Random.value < 0.7f ? lit : iron, new Vector3(c * w * 0.22f, h - 0.25f - r * 0.12f, d * 0.5f + 0.005f), new Vector3(0.05f, 0.06f, 0.01f), false);
                if (i % 2 == 1)
                    Limb(PrimitiveType.Cylinder, "Antenna", p, iron, new Vector3(0f, -0.15f + h, 0f), new Vector3(0f, -0.15f + h + 0.25f, 0f), 0.015f);
            });
        }
    }
}
