using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using static DreamStudio.Editor.DreamBuildUtils;

namespace DreamStudio.Editor
{
    /// <summary>
    /// Builds the Portal Room into BasicScene: an octagonal hall with a portal to each world on six walls,
    /// a great window on the seventh and, on the eighth, a corridor back to the Start Room, where the game
    /// begins. A progress counter stands at its heart, with a finale once every world is restored.
    /// Run from the menu: Dream Studio > Build Dream Room. Re-running replaces the previous build.
    /// </summary>
    public static class DreamRoomBuilder
    {
        const string RootName = "Dream Room";

        // Room dimensions (meters).
        const float Apothem = 10f;         // center to wall
        const float WallHeight = 6.5f;
        const float WallThickness = 0.3f;
        const float OpeningWidth = 3f;
        const float OpeningTop = 3.9f;
        const float WindowWidth = 4.4f;

        // Layout of the eight walls (0 = +Z, then clockwise in 45° steps).
        const int Sides = 8;
        const int WindowSide = 0;   // straight ahead as you come in
        const int EntranceSide = 4; // toward the corridor and the Start Room
        static readonly int[] WorldSides = { 1, 2, 3, 5, 6, 7 };

        const float CorridorWidth = DreamLobbyBuilder.DoorWidth;
        const float CorridorHeight = DreamLobbyBuilder.DoorHeight;
        const float CorridorLength = 12f;
        static readonly Vector3 StartRoomCenter = new Vector3(0f, 0f, -(Apothem + CorridorLength + DreamLobbyBuilder.Radius));
        const float PortalBase = 0.3f;

        [MenuItem("Dream Studio/Build Dream Room")]
        public static void Build()
        {
            if (!OpenHubScene())
                return;

            EnsureFolders();

            var existing = GameObject.Find(RootName);
            if (existing != null)
                Object.DestroyImmediate(existing);

            var oldPlane = GameObject.Find("Plane");
            if (oldPlane != null)
                oldPlane.SetActive(false);

            var root = new GameObject(RootName);

            var mats = CreateMaterials();
            var ringMesh = SaveMesh(CreateEllipticTorus(1f, 1f, 0.02f, 96, 8), "Ring");

            BuildFloor(root.transform, mats, ringMesh);
            BuildWalls(root.transform, mats);
            BuildCenterpiece(root.transform, mats);
            BuildClouds(root.transform, mats);
            BuildProgress(root.transform, mats);
            BuildCorridor(root.transform, mats);
            DreamLobbyBuilder.BuildInto(root.transform, StartRoomCenter);
            Particles(root.transform, "Dream Sparkles", new Vector3(0f, 3f, 0f), new Vector3(18f, 5f, 18f), 60f,
                new Color(1f, 0.8f, 0.95f), new Color(0.7f, 0.85f, 1f), 0.03f, 0.09f, mats.sparkle);
            SetupLighting(mats);

            var hud = new GameObject("Dream HUD");
            hud.transform.SetParent(root.transform, false);
            var dreamHud = hud.AddComponent<DreamHud>();
            dreamHud.fadeMaterial = ScreenFade();
            dreamHud.overlayTextShader = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
            var ambience = hud.AddComponent<DreamSoundEmitter>();
            ambience.sound = DreamSound.HubAmbience;
            ambience.spatial = false;
            ambience.volume = 0.45f;

            // The game begins in the Start Room, facing the doorway and the corridor beyond.
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin != null)
                origin.transform.SetPositionAndRotation(StartRoomCenter + DreamLobbyBuilder.SpawnOffset, Quaternion.identity);
            ConfigureLighting();
            AddContactShadows(root.transform, new HashSet<Transform>());

            Undo.RegisterCreatedObjectUndo(root, "Build Dream Room");
            Selection.activeGameObject = root;

            var scene = root.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[DreamRoomBuilder] Dream Room built and BasicScene saved.");
        }

        static bool OpenHubScene()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path == HubScenePath)
                return true;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;
            EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);
            return true;
        }

        // ---------- Materials ----------

        class Mats
        {
            public Material floor, wall, column, trim, cloud, moon, sparkle, sky;
            public Material[] portalRing;
        }

        static Mats CreateMaterials()
        {
            var m = new Mats
            {
                floor = Lit("Floor", new Color(0.36f, 0.32f, 0.62f), 0.85f, new Color(0.05f, 0.04f, 0.12f)),
                wall = Lit("Wall", new Color(0.78f, 0.72f, 0.95f), 0.35f, new Color(0.12f, 0.08f, 0.2f)),
                column = Lit("Column", new Color(0.96f, 0.93f, 1f), 0.7f, new Color(0.15f, 0.12f, 0.2f)),
                trim = Lit("Trim", new Color(1f, 0.85f, 0.55f), 0.8f, new Color(0.9f, 0.65f, 0.3f)),
                cloud = Lit("Cloud", new Color(1f, 0.95f, 0.98f), 0.1f, new Color(0.35f, 0.28f, 0.38f)),
                moon = Lit("Moon", new Color(1f, 0.97f, 0.85f), 0.5f, new Color(1.6f, 1.45f, 1.1f)),
                sparkle = Sparkle(),
                sky = Sky("DreamSky", new Color(0.12f, 0.08f, 0.35f), new Color(0.95f, 0.6f, 0.85f),
                    new Color(0.55f, 0.45f, 0.9f), new Color(0.5f, 0.8f, 1f), 120f),
                portalRing = new Material[Worlds.Length],
            };

            for (int i = 0; i < Worlds.Length; i++)
                m.portalRing[i] = PortalRing(Worlds[i].scene, Worlds[i].inner, Worlds[i].outer);

            return m;
        }

        // ---------- Room ----------

        static void BuildFloor(Transform root, Mats mats, Mesh ringMesh)
        {
            float vertexRadius = Apothem / Mathf.Cos(180f / Sides * Mathf.Deg2Rad);
            var floorMesh = SaveMesh(CreatePolygon(vertexRadius + 0.35f, Sides), "PortalRoomFloor");

            var floor = MeshObject("Floor", root, floorMesh, mats.floor);
            floor.AddComponent<MeshCollider>().sharedMesh = floorMesh;
            floor.AddComponent<TeleportationArea>();

            var deco = new GameObject("Floor Glow");
            deco.transform.SetParent(root, false);

            foreach (float radius in new[] { 2f, 4.5f, 8f })
            {
                var ring = MeshObject($"Glow Ring {radius}m", deco.transform, ringMesh, mats.trim);
                ring.transform.localPosition = new Vector3(0f, 0.01f, 0f);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ring.transform.localScale = new Vector3(radius, radius, 1.5f);
            }

            // Glowing paths from the center circle toward each portal and the corridor.
            for (int side = 0; side < Sides; side++)
            {
                int world = WorldOnSide(side);
                if (world < 0 && side != EntranceSide)
                    continue;
                var path = CreatePrimitive(PrimitiveType.Cube, world >= 0 ? $"Path to {Worlds[world].name}" : "Path to the Corridor",
                    deco.transform, world >= 0 ? mats.portalRing[world] : mats.trim, false);
                path.transform.localRotation = Quaternion.Euler(0f, side * 360f / Sides, 0f);
                path.transform.localPosition = path.transform.localRotation * new Vector3(0f, 0.005f, (2f + Apothem - 0.6f) * 0.5f);
                path.transform.localScale = new Vector3(0.06f, 0.01f, Apothem - 2.6f);
            }
        }

        /// <summary>Which world's portal stands on a wall (index into Worlds), or -1 for the entrance and the window.</summary>
        static int WorldOnSide(int side)
        {
            int index = System.Array.IndexOf(WorldSides, side);
            return index >= 0 && index < Worlds.Length ? index : -1;
        }

        static void BuildWalls(Transform root, Mats mats)
        {
            float sideLength = 2f * Apothem * Mathf.Tan(180f / Sides * Mathf.Deg2Rad);
            float vertexRadius = Apothem / Mathf.Cos(180f / Sides * Mathf.Deg2Rad);

            var walls = new GameObject("Walls");
            walls.transform.SetParent(root, false);
            var portals = new GameObject("Portals");
            portals.transform.SetParent(root, false);

            for (int i = 0; i < Sides; i++)
            {
                float angle = i * 360f / Sides;
                var dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                int worldIndex = WorldOnSide(i);

                // Each wall's local +Z faces into the room.
                var wall = new GameObject($"Wall {i + 1}");
                wall.transform.SetParent(walls.transform, false);
                wall.transform.localPosition = dir * Apothem;
                wall.transform.localRotation = Quaternion.Euler(0f, angle + 180f, 0f);

                float opening = i == EntranceSide ? CorridorWidth : i == WindowSide ? WindowWidth : OpeningWidth;
                float top = i == EntranceSide ? CorridorHeight : OpeningTop;
                float pillarWidth = (sideLength - opening) * 0.5f;
                float pillarX = opening * 0.5f + pillarWidth * 0.5f;
                Box("Left", wall.transform, mats.wall, new Vector3(-pillarX, WallHeight * 0.5f, 0f), new Vector3(pillarWidth, WallHeight, WallThickness));
                Box("Right", wall.transform, mats.wall, new Vector3(pillarX, WallHeight * 0.5f, 0f), new Vector3(pillarWidth, WallHeight, WallThickness));
                Box("Lintel", wall.transform, mats.wall, new Vector3(0f, (top + WallHeight) * 0.5f, 0f), new Vector3(opening, WallHeight - top, WallThickness));
                Box("Top Trim", wall.transform, mats.trim, new Vector3(0f, WallHeight + 0.06f, 0f), new Vector3(sideLength, 0.12f, WallThickness + 0.12f), false);

                if (i == WindowSide)
                {
                    // A great window onto the dream sky, with an invisible pane to lean on.
                    Box("Sill", wall.transform, mats.wall, new Vector3(0f, 0.5f, 0f), new Vector3(opening, 1f, WallThickness));
                    Box("Mullion", wall.transform, mats.trim, new Vector3(0f, (1f + top) * 0.5f, 0f), new Vector3(0.1f, top - 1f, WallThickness + 0.04f), false);
                    Box("Transom", wall.transform, mats.trim, new Vector3(0f, 2.6f, 0f), new Vector3(opening, 0.1f, WallThickness + 0.04f), false);
                    var pane = new GameObject("Pane");
                    pane.transform.SetParent(wall.transform, false);
                    pane.transform.localPosition = new Vector3(0f, (1f + top) * 0.5f, 0f);
                    pane.AddComponent<BoxCollider>().size = new Vector3(opening, top - 1f, WallThickness);
                }
                else if (worldIndex >= 0)
                {
                    var world = Worlds[worldIndex];
                    // No collider on the step so players can walk right up into the portal.
                    Box("Step", wall.transform, mats.column, new Vector3(0f, PortalBase * 0.5f, 0.1f), new Vector3(opening, PortalBase, WallThickness + 0.2f), false);
                    var labelPos = new Vector3(0f, OpeningTop - PortalBase - PortalHalfHeight + 0.4f, WallThickness * 0.5f + 0.02f);
                    var portal = CreatePortal(portals.transform, world.name, world.scene, world.scene, world.inner, world.outer, labelPos);
                    portal.transform.position = wall.transform.TransformPoint(new Vector3(0f, PortalBase + PortalHalfHeight, 0f));
                    portal.transform.rotation = wall.transform.rotation;
                    portal.arriveHere = true;

                    // A banner in the world's colours on each side of the portal.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Box("Banner", wall.transform, mats.portalRing[worldIndex], new Vector3(side * pillarX, 3.6f, WallThickness * 0.5f + 0.03f), new Vector3(1.3f, 3.4f, 0.04f), false);
                        Box("Banner Rod", wall.transform, mats.trim, new Vector3(side * pillarX, 5.35f, WallThickness * 0.5f + 0.06f), new Vector3(1.6f, 0.06f, 0.06f), false);
                    }
                }

                // A glowing sconce on each side of every opening.
                for (int side = -1; side <= 1; side += 2)
                {
                    var sconce = new Vector3(side * (opening * 0.5f + 0.45f), 2.6f, WallThickness * 0.5f + 0.2f);
                    var cup = CreatePrimitive(PrimitiveType.Cylinder, "Sconce", wall.transform, mats.trim, false);
                    cup.transform.localPosition = sconce;
                    cup.transform.localScale = new Vector3(0.22f, 0.08f, 0.22f);
                    var glow = CreatePrimitive(PrimitiveType.Sphere, "Sconce Glow", wall.transform, mats.moon, false);
                    glow.transform.localPosition = sconce + Vector3.up * 0.14f;
                    glow.transform.localScale = Vector3.one * 0.16f;
                    glow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    PointLight(glow.transform, "Light", Vector3.zero, new Color(1f, 0.82f, 0.6f), 5f, 0.9f);
                }

                // Corner column with a floating orb, at the vertex between this wall and the next.
                var cornerDir = Quaternion.Euler(0f, angle + 180f / Sides, 0f) * Vector3.forward;
                var column = CreatePrimitive(PrimitiveType.Cylinder, $"Column {i + 1}", walls.transform, mats.column, true);
                column.transform.localPosition = cornerDir * vertexRadius + Vector3.up * WallHeight * 0.5f;
                column.transform.localScale = new Vector3(0.6f, WallHeight * 0.5f, 0.6f);

                var orb = CreatePrimitive(PrimitiveType.Sphere, $"Column Orb {i + 1}", walls.transform, mats.moon, false);
                orb.transform.localPosition = cornerDir * vertexRadius + Vector3.up * (WallHeight + 0.6f);
                orb.transform.localScale = Vector3.one * 0.35f;
                var orbFloat = orb.AddComponent<DreamFloat>();
                orbFloat.bobHeight = 0.12f;
                orbFloat.bobSpeed = 0.3f;
            }
        }

        /// <summary>
        /// The corridor from the Portal Room's entrance to the Start Room: a vaulted hall with a carpet
        /// runner, ribbed arches, sconces, and a swirling painting of each world along the walls.
        /// </summary>
        static void BuildCorridor(Transform root, Mats mats)
        {
            const float overlap = 0.5f;
            float start = -Apothem + overlap;
            float end = -(Apothem + CorridorLength) - overlap;
            float length = start - end;
            float half = CorridorWidth * 0.5f;

            var corridor = new GameObject("Corridor");
            corridor.transform.SetParent(root, false);
            corridor.transform.localPosition = new Vector3(0f, 0f, (start + end) * 0.5f);
            var c = corridor.transform;

            var floor = Box("Floor", c, mats.floor, new Vector3(0f, -0.055f, 0f), new Vector3(CorridorWidth + 0.6f, 0.1f, length));
            floor.AddComponent<TeleportationArea>();
            Box("Runner", c, Lit("Lobby_Rug", new Color(0.95f, 0.6f, 0.75f), 0.1f, new Color(0.1f, 0.03f, 0.06f)),
                new Vector3(0f, 0.004f, 0f), new Vector3(1.3f, 0.008f, length - 1.2f), false);
            for (int side = -1; side <= 1; side += 2)
                Box("Wall", c, mats.wall, new Vector3(side * (half + 0.15f), CorridorHeight * 0.5f, 0f), new Vector3(0.3f, CorridorHeight, length));
            Box("Ceiling", c, mats.wall, new Vector3(0f, CorridorHeight + 0.15f, 0f), new Vector3(CorridorWidth + 0.6f, 0.3f, length));

            // Ribbed arches every two meters.
            for (float z = -length * 0.5f + 1.5f; z < length * 0.5f - 1f; z += 2f)
            {
                for (int side = -1; side <= 1; side += 2)
                    Box("Rib", c, mats.trim, new Vector3(side * (half - 0.05f), CorridorHeight * 0.5f, z), new Vector3(0.12f, CorridorHeight, 0.15f), false);
                Box("Rib", c, mats.trim, new Vector3(0f, CorridorHeight - 0.05f, z), new Vector3(CorridorWidth, 0.12f, 0.15f), false);
            }

            // Sconces alternating sides, and a swirling painting of each world between them.
            for (int i = 0; i < 4; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                float z = -length * 0.5f + 2.5f + i * 3.2f;
                var sconce = CreatePrimitive(PrimitiveType.Sphere, "Sconce Glow", c, mats.moon, false);
                sconce.transform.localPosition = new Vector3(side * (half - 0.15f), 2.5f, z);
                sconce.transform.localScale = Vector3.one * 0.16f;
                sconce.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Box("Sconce Cup", c, mats.trim, new Vector3(side * (half - 0.1f), 2.38f, z), new Vector3(0.18f, 0.06f, 0.18f), false);
                PointLight(sconce.transform, "Light", Vector3.zero, new Color(1f, 0.82f, 0.6f), 5f, 1f);
            }
            for (int w = 0; w < Worlds.Length; w++)
            {
                int side = w % 2 == 0 ? -1 : 1;
                float z = -length * 0.5f + 3.2f + (w / 2) * 3.4f;
                var world = Worlds[w];
                var painting = CreatePrimitive(PrimitiveType.Quad, $"Painting of {world.name}", c, PortalSurface(world.scene, world.inner, world.outer), false);
                painting.transform.localPosition = new Vector3(side * (half - 0.02f), 1.8f, z);
                painting.transform.localRotation = Quaternion.Euler(0f, side * 90f, 0f);
                painting.transform.localScale = new Vector3(1.1f, 0.8f, 1f);
                painting.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Box("Frame", c, mats.trim, new Vector3(side * (half - 0.01f), 1.8f, z), new Vector3(0.04f, 0.92f, 1.22f), false);
            }
        }

        static void BuildCenterpiece(Transform root, Mats mats)
        {
            var center = new GameObject("Dream Moon");
            center.transform.SetParent(root, false);
            center.transform.localPosition = new Vector3(0f, 5.2f, 0f);
            var centerFloat = center.AddComponent<DreamFloat>();
            centerFloat.bobHeight = 0.2f;
            centerFloat.bobSpeed = 0.15f;
            centerFloat.spin = Vector3.zero;

            var moon = CreatePrimitive(PrimitiveType.Sphere, "Moon", center.transform, mats.moon, false);
            moon.transform.localScale = Vector3.one * 1.3f;

            PointLight(center.transform, "Moon Light", Vector3.zero, new Color(1f, 0.92f, 0.8f), 16f, 1.3f);

            // Six small orbs, one per world, orbiting the moon.
            var orbit = new GameObject("Orbit");
            orbit.transform.SetParent(center.transform, false);
            orbit.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            var orbitSpin = orbit.AddComponent<DreamFloat>();
            orbitSpin.bobHeight = 0f;
            orbitSpin.spin = new Vector3(0f, 12f, 0f);

            for (int i = 0; i < Worlds.Length; i++)
            {
                var orb = CreatePrimitive(PrimitiveType.Sphere, $"Orb {Worlds[i].name}", orbit.transform, mats.portalRing[i], false);
                orb.transform.localPosition = Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward * 1.9f;
                orb.transform.localScale = Vector3.one * 0.18f;
            }
        }

        static void BuildClouds(Transform root, Mats mats)
        {
            var clouds = new GameObject("Clouds");
            clouds.transform.SetParent(root, false);

            var state = Random.state;
            Random.InitState(1337);

            // A sea of clouds below and around the room, plus a few drifting overhead.
            for (int i = 0; i < 36; i++)
            {
                float angle = Random.Range(0f, 360f);
                float dist = Random.Range(16f, 34f);
                float height = Random.Range(-8f, 12f);
                var pos = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * dist + Vector3.up * height;
                if (InsideStartWing(pos))
                    continue;
                CreateCloud(clouds.transform, $"Cloud {i + 1}", pos, Random.Range(1.5f, 3.5f), mats.cloud);
            }
            for (int i = 0; i < 4; i++)
            {
                var pos = new Vector3(Random.Range(-7f, 7f), Random.Range(8.5f, 11f), Random.Range(-7f, 7f));
                CreateCloud(clouds.transform, $"Overhead Cloud {i + 1}", pos, Random.Range(0.8f, 1.4f), mats.cloud);
            }

            Random.state = state;
        }

        /// <summary>
        /// Text readable from both sides of a thin board. TextMeshPro draws from both sides, so two faces
        /// back to back would show through each other mirrored; the board between keeps each side clean.
        /// </summary>
        static TextMeshPro[] TwoSidedText(Transform parent, string text, float size, Vector2 box, Color color, Material boardMat)
        {
            Box("Board", parent, boardMat, Vector3.zero, new Vector3(box.x + 0.2f, box.y + 0.15f, 0.04f), false);
            var faces = new TextMeshPro[2];
            for (int side = 0; side < 2; side++)
            {
                var face = new GameObject(side == 0 ? "Front" : "Back");
                face.transform.SetParent(parent, false);
                face.transform.localPosition = new Vector3(0f, 0f, side == 0 ? -0.03f : 0.03f);
                face.transform.localRotation = Quaternion.Euler(0f, side * 180f, 0f);
                var tmp = face.AddComponent<TextMeshPro>();
                tmp.text = text;
                tmp.fontSize = size;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMax = size;
                tmp.fontSizeMin = size * 0.5f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = color;
                tmp.rectTransform.sizeDelta = box;
                faces[side] = tmp;
            }
            return faces;
        }

        /// <summary>True inside (or close to) the corridor or the Start Room, where clouds would poke through.</summary>
        static bool InsideStartWing(Vector3 p)
        {
            var flat = new Vector2(p.x - StartRoomCenter.x, p.z - StartRoomCenter.z);
            bool nearRoom = flat.magnitude < DreamLobbyBuilder.Radius + 6f;
            bool nearCorridor = Mathf.Abs(p.x) < CorridorWidth + 4f && p.z < -Apothem + 2f && p.z > StartRoomCenter.z;
            return nearRoom || nearCorridor;
        }

        static void CreateCloud(Transform parent, string name, Vector3 position, float scale, Material mat)
        {
            var cloud = new GameObject(name);
            cloud.transform.SetParent(parent, false);
            cloud.transform.localPosition = position;
            cloud.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            cloud.transform.localScale = Vector3.one * scale;

            int puffs = Random.Range(5, 9);
            for (int p = 0; p < puffs; p++)
            {
                var puff = CreatePrimitive(PrimitiveType.Sphere, "Puff", cloud.transform, mat, false);
                puff.transform.localPosition = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-0.15f, 0.35f), Random.Range(-0.5f, 0.5f));
                float s = Random.Range(0.7f, 1.3f);
                puff.transform.localScale = new Vector3(s * 1.2f, s * 0.8f, s);
                puff.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            var drift = cloud.AddComponent<DreamFloat>();
            drift.bobHeight = 0.3f;
            drift.bobSpeed = Random.Range(0.03f, 0.08f);
            drift.spin = new Vector3(0f, Random.Range(-2f, 2f), 0f);
        }

        static void SetupLighting(Mats mats)
        {
            var sun = GameObject.Find("Directional Light");
            if (sun != null && sun.TryGetComponent<Light>(out var light))
            {
                light.shadows = LightShadows.Soft;
                light.color = new Color(0.82f, 0.76f, 1f);
                light.intensity = 0.6f;
                sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            }

            RenderSettings.skybox = mats.sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.45f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.75f, 0.5f, 0.75f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.2f, 0.45f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.85f, 0.65f, 0.9f);
            RenderSettings.fogDensity = 0.018f;
        }

        /// <summary>
        /// The plinth at the heart of the room with a slowly turning "Dreams restored" counter, and the
        /// finale (hidden until every world is restored): a golden halo, a rainbow burst and a message.
        /// </summary>
        static void BuildProgress(Transform root, Mats mats)
        {
            var plinth = CreatePrimitive(PrimitiveType.Cylinder, "Plinth", root, mats.column, true);
            plinth.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            plinth.transform.localScale = new Vector3(2.4f, 0.4f, 2.4f);
            var band = CreatePrimitive(PrimitiveType.Cylinder, "Plinth Trim", root, mats.trim, false);
            band.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            band.transform.localScale = new Vector3(2.45f, 0.03f, 2.45f);

            var sign = new GameObject("Progress Sign");
            sign.transform.SetParent(root, false);
            sign.transform.localPosition = new Vector3(0f, 1.9f, 0f);
            var turn = sign.AddComponent<DreamFloat>();
            turn.bobHeight = 0.05f;
            turn.bobSpeed = 0.2f;
            turn.spin = new Vector3(0f, 15f, 0f);
            var counters = TwoSidedText(sign.transform, "Dreams restored\n0 / 6", 3f, new Vector2(3f, 1.2f), new Color(1f, 0.92f, 0.75f), mats.column);

            var finale = new GameObject("Finale");
            finale.transform.SetParent(root, false);
            var halo = MeshObject("Golden Halo", finale.transform, SaveMesh(CreateEllipticTorus(1f, 1f, 0.02f, 96, 8), "Ring"), mats.trim);
            halo.transform.localPosition = new Vector3(0f, 5.2f, 0f);
            halo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            halo.transform.localScale = new Vector3(3.2f, 3.2f, 6f);
            var haloSpin = halo.AddComponent<DreamFloat>();
            haloSpin.bobHeight = 0.2f;
            haloSpin.spin = new Vector3(0f, 0f, 20f);
            Particles(finale.transform, "Rainbow Burst", new Vector3(0f, 5f, 0f), new Vector3(14f, 6f, 14f), 90f,
                new Color(1f, 0.5f, 0.8f), new Color(0.5f, 0.9f, 1f), 0.05f, 0.14f, mats.sparkle, 0.3f, 0.8f);
            PointLight(finale.transform, "Finale Light", new Vector3(0f, 5f, 0f), new Color(1f, 0.9f, 0.7f), 20f, 2f);
            var banner = new GameObject("Finale Message");
            banner.transform.SetParent(finale.transform, false);
            banner.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            var bannerSpin = banner.AddComponent<DreamFloat>();
            bannerSpin.bobHeight = 0.1f;
            bannerSpin.spin = new Vector3(0f, -15f, 0f);
            TwoSidedText(banner.transform, "Every dream is restored!", 4f, new Vector2(8f, 1.2f), new Color(1f, 0.85f, 0.5f), mats.trim);
            finale.SetActive(false);

            var progress = root.gameObject.AddComponent<DreamHubProgress>();
            progress.counters = counters;
            progress.finale = finale;
            progress.sparkle = mats.sparkle;
        }
    }
}
