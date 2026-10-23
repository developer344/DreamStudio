using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using static DreamStudio.Editor.DreamBuildUtils;

namespace DreamStudio.Editor
{
    /// <summary>
    /// Builds one scene per hub portal into Assets/Scenes/Worlds and registers them in the build list.
    /// Each world starts as a nightmare; finding its token turns it into a dream and opens the way home.
    /// Run from the menu: Dream Studio > Build Dream Worlds (or Build Everything to rebuild the hub too).
    /// </summary>
    public static class DreamWorldBuilder
    {
        [MenuItem("Dream Studio/Build Everything")]
        public static void BuildEverything()
        {
            if (BuildWorlds())
                DreamRoomBuilder.Build();
        }

        [MenuItem("Dream Studio/Build Dream Worlds")]
        public static void BuildWorldsMenu() => BuildWorlds();

        public static bool BuildWorlds()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            EnsureFolders();
            ConfigureLighting();
            var state = Random.state;

            for (int i = 0; i < Worlds.Length; i++)
            {
                var world = Worlds[i];
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Random.InitState(1000 + i);
                s_Terrain = null;

                switch (world.scene)
                {
                    case "HauntedForest": BuildHauntedForest(world); break;
                    case "SunkenDepths": BuildSunkenDepths(world); break;
                    case "CandyForest": BuildCandyForest(world); break;
                    case "ClockworkMoon": BuildClockworkMoon(world); break;
                    case "CrystalCaverns": BuildCrystalCaverns(world); break;
                    case "UpsideDownCity": BuildUpsideDownCity(world); break;
                    default: Debug.LogError($"[DreamWorldBuilder] No builder for world '{world.scene}'."); break;
                }

                var worldRoot = GameObject.Find(world.name).transform;
                var nightmareWorld = Object.FindAnyObjectByType<NightmareWorld>();
                AddSolidColliders(worldRoot);
                AddContactShadows(worldRoot, ToggledRoots(nightmareWorld), GroundHeight, GroundNormal);
                MarkStaticScenery(worldRoot, nightmareWorld);
                EditorSceneManager.SaveScene(scene, ScenePath(world));
            }

            Random.state = state;
            s_Terrain = null;
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);
            Debug.Log($"[DreamWorldBuilder] Built {Worlds.Length} world scenes in {WorldSceneDir} and updated the build list.");
            return true;
        }

        static string ScenePath(World world) => $"{WorldSceneDir}/{world.scene}.unity";

        /// <summary>
        /// Gives the world's solid props colliders so the player can't walk, swim or teleport through them:
        /// trunks, rocks, candy, coral, crystals, artifacts, rooftop clutter, the hills at the edge. Left
        /// passable: anything scripted to move, sway or be touched (tokens, collectibles, air bubbles, hazards,
        /// portals, kelp, fish, floating lights), thin or flat things you'd snag on (stems, puddles, runes),
        /// glows and effects, and things too high overhead to bump into.
        /// </summary>
        static void AddSolidColliders(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponent<Collider>() != null || !r.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
                    continue;
                if (r.name == "Beacon Beam" || r.sharedMaterial == null)
                    continue;
                string shader = r.sharedMaterial.shader.name;
                if (shader != "Universal Render Pipeline/Lit" && shader != "DreamStudio/TriplanarLit")
                    continue;
                if (HasScriptedAncestor(r.transform, root))
                    continue;

                var b = TransformBounds(filter.sharedMesh.bounds, r.transform.localToWorldMatrix);
                float footprint = Mathf.Max(b.size.x, b.size.z);
                if (footprint < 0.15f || b.size.y < 0.08f || Mathf.Max(footprint, b.size.y) < 0.25f || b.min.y - GroundHeight(b.center) > 2.5f)
                    continue;
                r.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
        }

        /// <summary>True if anything from <paramref name="t"/> up to the world root runs a script that moves it or must be touched (artifacts excepted: they only change looks).</summary>
        static bool HasScriptedAncestor(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
                foreach (var behaviour in t.GetComponents<MonoBehaviour>())
                    if (behaviour != null && !(behaviour is DreamArtifact) && !(behaviour is TeleportationArea))
                        return true;
            return false;
        }

        /// <summary>The groups the nightmare shows, hides or shrinks.</summary>
        static HashSet<Transform> ToggledRoots(NightmareWorld world)
        {
            var roots = new HashSet<Transform>();
            if (world != null)
                foreach (var go in world.nightmareOnly.Concat(world.dreamOnly))
                    if (go != null)
                        roots.Add(go.transform);
            return roots;
        }

        /// <summary>
        /// Marks scenery that never moves as batching-static so the big worlds stay cheap to draw on Quest.
        /// Anything scripted (animated, interactive, collectible, artifacts) or toggled by the nightmare stays dynamic.
        /// </summary>
        static void MarkStaticScenery(Transform root, NightmareWorld world)
        {
            var dynamicRoots = ToggledRoots(world);

            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool isDynamic = false;
                for (var t = renderer.transform; t != null; t = t.parent)
                {
                    if (dynamicRoots.Contains(t) || t.GetComponent<MonoBehaviour>() != null)
                    {
                        isDynamic = true;
                        break;
                    }
                }
                if (!isDynamic)
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        static void UpdateBuildSettings()
        {
            // The game opens in the hub scene, where the Start Room leads down a corridor to the Portal Room.
            var wanted = new List<string> { HubScenePath };
            wanted.AddRange(Worlds.Select(ScenePath));

            var scenes = wanted.Select(p => new EditorBuildSettingsScene(p, true)).ToList();
            foreach (var existing in EditorBuildSettings.scenes)
                if (!wanted.Contains(existing.path) && existing.path != LobbyScenePath)
                    scenes.Add(existing);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // =====================================================================
        // Shared world setup
        // =====================================================================

        class WorldContext
        {
            public string scene;
            public Transform root;
            public Transform spawn;
            public Light sun;
            public NightmareWorld nightmare;
            public DreamVision vision;
            public Material sparkle;
            public Material mist;
        }

        static WorldContext SetupWorld(World world, Vector3 spawnPos, float spawnYaw, bool isNightmare)
        {
            var ctx = new WorldContext
            {
                scene = world.scene,
                root = new GameObject(world.name).transform,
                sparkle = Sparkle(),
                mist = Mist(),
            };

            var sunGo = new GameObject("Sun");
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            ctx.sun = sunGo.AddComponent<Light>();
            ctx.sun.type = LightType.Directional;
            ctx.sun.shadows = LightShadows.Soft;
            ctx.sun.shadowStrength = 0.85f;

            ctx.spawn = new GameObject("Spawn Point").transform;
            ctx.spawn.SetPositionAndRotation(spawnPos, Quaternion.Euler(0f, spawnYaw, 0f));
            CreateXRRig(spawnPos, spawnYaw);

            var hud = new GameObject("Dream HUD").AddComponent<DreamHud>();
            hud.fadeMaterial = ScreenFade();
            hud.overlayTextShader = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            var ambience = new GameObject("Ambience").AddComponent<DreamSoundEmitter>();
            ambience.sound = AmbienceFor(world.scene);
            ambience.spatial = false;
            ambience.volume = 0.5f;

            if (isNightmare)
            {
                ctx.nightmare = new GameObject("Nightmare World").AddComponent<NightmareWorld>();
                ctx.nightmare.spawnPoint = ctx.spawn;
                ctx.vision = ctx.nightmare.gameObject.AddComponent<DreamVision>();
                ctx.vision.filterMaterial = LoadOrCreateMaterial("VisionFilter", "DreamStudio/VisionFilter");
                ctx.vision.dream = DreamLook();
                ctx.nightmare.vision = ctx.vision;
            }
            return ctx;
        }

        /// <summary>
        /// A world's sky, fog and light. The world always looks like this; the nightmare lives only in
        /// the player's <see cref="DreamVision"/> filter.
        /// </summary>
        class Atmosphere
        {
            public Material sky;
            public Color fogColor;
            public float fogDensity;
            public Color ambientSky;
            public Color ambientEquator;
            public Color ambientGround;
            public Color sunColor;
            public float sunIntensity;
        }

        static DreamSound AmbienceFor(string scene)
        {
            switch (scene)
            {
                case "HauntedForest": return DreamSound.ForestAmbience;
                case "SunkenDepths": return DreamSound.OceanAmbience;
                case "CandyForest": return DreamSound.CandyAmbience;
                case "ClockworkMoon": return DreamSound.MoonAmbience;
                case "CrystalCaverns": return DreamSound.CaveAmbience;
                case "UpsideDownCity": return DreamSound.CityAmbience;
                default: return DreamSound.HubAmbience;
            }
        }

        static void ApplyAtmosphere(Atmosphere a, Light sun)
        {
            RenderSettings.skybox = a.sky;
            RenderSettings.fogColor = a.fogColor;
            RenderSettings.fogDensity = a.fogDensity;
            RenderSettings.ambientSkyColor = a.ambientSky;
            RenderSettings.ambientEquatorColor = a.ambientEquator;
            RenderSettings.ambientGroundColor = a.ambientGround;
            sun.color = a.sunColor;
            sun.intensity = a.sunIntensity;
        }

        /// <summary>
        /// The nightmare filter: the view multiplied down by <paramref name="tint"/>, falling to near
        /// black at the edges, with a slow, uneasy breathing pulse.
        /// </summary>
        static DreamVision.Look NightmareLook(Color tint)
        {
            return new DreamVision.Look
            {
                tint = tint,
                edgeTint = tint * 0.12f,
                glow = Color.clear,
                vignetteStart = 0.04f,
                vignetteEnd = 0.42f,
                pulse = 1f,
            };
        }

        /// <summary>The dream filter: the world as it is, with a faint warm glow at the edges of the view.</summary>
        static DreamVision.Look DreamLook()
        {
            return new DreamVision.Look
            {
                tint = Color.white,
                edgeTint = new Color(1f, 0.96f, 0.98f),
                glow = new Color(1f, 0.8f, 0.92f, 0.12f),
                vignetteStart = 0.25f,
                vignetteEnd = 0.9f,
                pulse = 0f,
            };
        }

        static GameObject Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        // ---------- Terrain ----------

        /// <summary>The rolling ground of the world being built. Null means flat (or no ground at all).</summary>
        class TerrainShape
        {
            public string name;
            public float amplitude;
            public float rimStart;
            public float rimHeight;
            public Vector2 seed;
            public (Vector3 center, float radius)[] flats;
        }

        static TerrainShape s_Terrain;

        /// <summary>
        /// Gives this world's <see cref="Ground"/> rolling hills of up to <paramref name="amplitude"/> meters.
        /// Inside each of <paramref name="flats"/> the ground stays level at height 0, so the spawn,
        /// buildings and landmarks sit flat. From <paramref name="rimStart"/> meters out the ground climbs
        /// into a lumpy rim about <paramref name="rimHeight"/> high: the hills and dunes at the world's edge.
        /// Call it before scattering anything: scattered things follow it.
        /// </summary>
        static void UseTerrain(string name, float amplitude, float rimStart, float rimHeight, params (Vector3 center, float radius)[] flats)
        {
            s_Terrain = new TerrainShape
            {
                name = name,
                amplitude = amplitude,
                rimStart = rimStart,
                rimHeight = rimHeight,
                seed = new Vector2(Random.Range(10f, 500f), Random.Range(10f, 500f)),
                flats = flats,
            };
        }

        static float GroundHeight(Vector3 p)
        {
            var t = s_Terrain;
            if (t == null)
                return 0f;
            float broad = Mathf.PerlinNoise(p.x / 38f + t.seed.x, p.z / 38f + t.seed.y) * 2f - 1f;
            float fine = Mathf.PerlinNoise(p.x / 14f + t.seed.y, p.z / 14f + t.seed.x) * 2f - 1f;
            float h = t.amplitude * (broad * 0.75f + fine * 0.25f);
            foreach (var (center, radius) in t.flats)
            {
                float d = new Vector2(p.x - center.x, p.z - center.z).magnitude;
                h *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - radius) / 8f));
            }

            float fromCenter = new Vector2(p.x, p.z).magnitude;
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((fromCenter - t.rimStart) / 16f));
            float lumps = 0.55f + 0.9f * Mathf.PerlinNoise(p.x / 20f + t.seed.x * 0.5f, p.z / 20f + t.seed.y * 0.5f);
            return h + t.rimHeight * rise * lumps;
        }

        static Vector3 GroundNormal(Vector3 p)
        {
            const float e = 0.5f;
            float dx = GroundHeight(p + Vector3.right * e) - GroundHeight(p - Vector3.right * e);
            float dz = GroundHeight(p + Vector3.forward * e) - GroundHeight(p - Vector3.forward * e);
            return new Vector3(-dx, 2f * e, -dz).normalized;
        }

        /// <summary><paramref name="p"/> moved onto the ground.</summary>
        static Vector3 OnGround(Vector3 p) => new Vector3(p.x, GroundHeight(p), p.z);

        /// <summary>The lowest ground under a footprint, so wide things settle into slopes instead of floating.</summary>
        static Vector3 Settled(Vector3 p, float radius)
        {
            float h = GroundHeight(p);
            for (int i = 0; i < 8; i++)
                h = Mathf.Min(h, GroundHeight(p + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * radius));
            return new Vector3(p.x, h, p.z);
        }

        /// <summary>Tilts something flat (a puddle, a crater, a rune) to lie along the ground.</summary>
        static Quaternion AlongGround(Vector3 p) => Quaternion.FromToRotation(Vector3.up, GroundNormal(p));

        static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>A walkable, teleportable square of ground following the current terrain.</summary>
        static GameObject Ground(Transform parent, Material mat, float size)
        {
            const int cells = 140;
            var verts = new Vector3[(cells + 1) * (cells + 1)];
            var uvs = new Vector2[verts.Length];
            for (int z = 0; z <= cells; z++)
            for (int x = 0; x <= cells; x++)
            {
                var p = new Vector3((x / (float)cells - 0.5f) * size, 0f, (z / (float)cells - 0.5f) * size);
                p.y = GroundHeight(p);
                verts[z * (cells + 1) + x] = p;
                uvs[z * (cells + 1) + x] = new Vector2(x / (float)cells, z / (float)cells);
            }
            var tris = new int[cells * cells * 6];
            int t = 0;
            for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                int a = z * (cells + 1) + x;
                int b = a + cells + 1;
                tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
            }
            var mesh = new Mesh { vertices = verts, uv = uvs, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh = SaveMesh(mesh, $"Ground_{s_Terrain?.name ?? parent.name}");

            var ground = MeshObject("Ground", parent, mesh, mat);
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;
            ground.AddComponent<TeleportationArea>();
            return ground;
        }

        /// <summary>
        /// Dresses the rim beyond the boundary so the edge of the world reads as hills, dunes, cliffs and
        /// caves rather than an invisible wall: big half-buried shapes around the ring (some in accent
        /// materials, or as <paramref name="accentMesh"/> such as giant crystals) and a few dark cave mouths.
        /// </summary>
        static void EdgeDressing(Transform parent, float radius, int count, Material mat, Material[] accents, bool jagged, Mesh accentMesh, int caves)
        {
            var edge = Group("World Edge", parent);
            for (int i = 0; i < count; i++)
            {
                var dir = Quaternion.Euler(0f, (i + Random.Range(-0.3f, 0.3f)) * 360f / count, 0f) * Vector3.forward;
                var pos = OnGround(dir * (radius + Random.Range(1f, 12f)));
                float size = Random.Range(4f, 10f);
                bool accent = accents != null && accents.Length > 0 && Random.value < 0.35f;
                var material = accent ? accents[Random.Range(0, accents.Length)] : mat;

                if (accent && accentMesh != null)
                {
                    var crystal = MeshObject("Edge Crystal", edge.transform, accentMesh, material);
                    crystal.transform.localPosition = pos;
                    crystal.transform.localRotation = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f));
                    crystal.transform.localScale = Vector3.one * size * 0.6f;
                    continue;
                }

                var shape = CreatePrimitive(jagged ? PrimitiveType.Cube : PrimitiveType.Sphere, "Edge Rock", edge.transform, material, false);
                shape.transform.localPosition = pos + Vector3.down * size * 0.2f;
                shape.transform.localRotation = jagged
                    ? Random.rotation
                    : Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
                shape.transform.localScale = new Vector3(size * Random.Range(0.8f, 1.6f), size * Random.Range(0.5f, 1.1f), size * Random.Range(0.8f, 1.4f));
            }
            for (int c = 0; c < caves; c++)
                CaveMouth(edge.transform, (c + 0.5f) * 360f / caves + Random.Range(-15f, 15f), radius + 8f, mat);
        }

        /// <summary>A rocky mound in the rim with a dark cave opening facing the middle of the world.</summary>
        static void CaveMouth(Transform parent, float angle, float radius, Material rock)
        {
            var dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            var dark = Lit("Cave_Mouth_Dark", new Color(0.01f, 0.01f, 0.012f), 0f, Color.black);
            var cave = Group("Cave Mouth", parent);
            cave.transform.localPosition = Settled(dir * radius, 5f);
            cave.transform.localRotation = Quaternion.LookRotation(-dir); // +Z faces the middle
            Part(PrimitiveType.Sphere, "Mound", cave.transform, rock, new Vector3(0f, 1.5f, -3f), new Vector3(15f, 11f, 10f));
            Part(PrimitiveType.Sphere, "Opening", cave.transform, dark, new Vector3(0f, 2.2f, 1.4f), new Vector3(4.5f, 5.5f, 2f));
            for (int s = 0; s < 7; s++)
            {
                float a = Mathf.Lerp(-100f, 100f, s / 6f) * Mathf.Deg2Rad;
                Part(PrimitiveType.Sphere, "Arch Stone", cave.transform, rock,
                    new Vector3(Mathf.Sin(a) * 2.7f, 2.2f + Mathf.Cos(a) * 3.1f, 1.8f), Vector3.one * Random.Range(1.2f, 1.8f));
            }
            Part(PrimitiveType.Sphere, "Fallen Rock", cave.transform, rock, new Vector3(-3.2f, 0.4f, 3f), new Vector3(1.8f, 1.2f, 1.6f));
            Part(PrimitiveType.Sphere, "Fallen Rock", cave.transform, rock, new Vector3(2.8f, 0.3f, 3.6f), new Vector3(1.2f, 0.8f, 1.1f));
        }

        /// <summary>A ring of invisible walls, following the ground, that keeps the player inside the playable area.</summary>
        static void Boundary(Transform parent, float radius, int segments)
        {
            var boundary = Group("Boundary", parent);
            float width = 2f * Mathf.PI * radius / segments * 1.1f;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 360f / segments;
                var wall = new GameObject($"Wall {i}");
                wall.transform.SetParent(boundary.transform, false);
                wall.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
                // Tall enough that teleport arcs can't sail over, thick enough that nothing slips through.
                var foot = wall.transform.localRotation * new Vector3(0f, 0f, radius + 1f);
                wall.transform.localPosition = foot + Vector3.up * (GroundHeight(foot) + 15f);
                wall.AddComponent<BoxCollider>().size = new Vector3(width, 40f, 2f);
            }

            // Walls stop the body; this also catches head-only movement (room-scale, simulator) and falls.
            boundary.AddComponent<DreamWorldBounds>().radius = radius - 0.75f;
        }

        static void UseMeshCollider(GameObject go)
        {
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        }

        /// <summary>Random points on the ground (following the terrain) in a ring, kept apart from each other and from avoid zones.</summary>
        static List<Vector3> Scatter(int count, float minRadius, float maxRadius, float spacing, List<Vector3> occupied,
            params (Vector3 center, float radius)[] avoid)
        {
            var result = new List<Vector3>();
            int attempts = count * 40;
            while (result.Count < count && attempts-- > 0)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Mathf.Sqrt(Random.Range(minRadius * minRadius, maxRadius * maxRadius));
                var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);

                bool ok = true;
                foreach (var zone in avoid)
                    if (FlatDistance(p, zone.center) < zone.radius) { ok = false; break; }
                if (ok)
                    foreach (var q in result)
                        if (FlatDistance(p, q) < spacing) { ok = false; break; }
                if (ok && occupied != null)
                    foreach (var q in occupied)
                        if (FlatDistance(p, q) < spacing) { ok = false; break; }
                if (ok)
                    result.Add(OnGround(p));
            }
            occupied?.AddRange(result);
            return result;
        }

        /// <summary>
        /// A grabbable token hovering at <paramref name="position"/>. The visual spins on a child so it
        /// doesn't fight the grab; the hover bob lives on the parent.
        /// </summary>
        static DreamToken CreateToken(Transform parent, string name, Vector3 position, Color glowColor, string hint, out Transform visualRoot)
        {
            var hover = Group($"{name} (Hover)", parent);
            hover.transform.localPosition = position;
            var bob = hover.AddComponent<DreamFloat>();
            bob.bobHeight = 0.08f;
            bob.bobSpeed = 0.4f;
            bob.spin = Vector3.zero;

            var token = new GameObject(name);
            token.transform.SetParent(hover.transform, false);
            token.AddComponent<SphereCollider>().radius = 0.22f;
            var body = token.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var grab = token.AddComponent<XRGrabInteractable>();
            grab.throwOnDetach = false;

            var visual = Group("Visual", token.transform);
            var spin = visual.AddComponent<DreamFloat>();
            spin.bobHeight = 0f;
            spin.spin = new Vector3(0f, 45f, 0f);
            visualRoot = visual.transform;

            var dreamToken = token.AddComponent<DreamToken>();
            dreamToken.nearbyHint = hint;
            dreamToken.glow = PointLight(token.transform, "Glow", Vector3.zero, glowColor, 9f, 3f);
            return dreamToken;
        }

        /// <summary>The portal back to the hub, on a little stone step. Facing its local +Z.</summary>
        static GameObject CreateWayHome(Transform parent, Vector3 position, float yaw, Material stepMat)
        {
            var home = Group("Way Home", parent);

            var inner = new Color(1f, 0.85f, 0.95f);
            var outer = new Color(0.55f, 0.45f, 0.95f);
            var portal = CreatePortal(home.transform, "Return to the Portal Room", HubSceneName, HubSceneName, inner, outer,
                new Vector3(0f, PortalHalfHeight + 0.5f, 0.05f));
            portal.transform.localPosition = position + Vector3.up * (0.3f + PortalHalfHeight);
            portal.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var step = Box("Step", home.transform, stepMat, Vector3.zero, new Vector3(PortalHalfWidth * 2f + 0.4f, 0.3f, 0.8f), false);
            step.transform.localPosition = position + Vector3.up * 0.15f;
            step.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // A solid stone gate around the back and sides, so nobody wanders behind the portal (or off a
            // rooftop behind it). Its back wall also hides the label from behind.
            var gate = Group("Gate", home.transform);
            gate.transform.localPosition = position;
            gate.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float gateWidth = PortalHalfWidth * 2f + 1.4f;
            float gateHeight = 0.3f + PortalHalfHeight * 2f + 1.6f;
            Box("Back Wall", gate.transform, stepMat, new Vector3(0f, gateHeight * 0.5f, -0.6f), new Vector3(gateWidth, gateHeight, 0.3f));
            for (int side = -1; side <= 1; side += 2)
                Box("Pillar", gate.transform, stepMat, new Vector3(side * (PortalHalfWidth + 0.45f), 1.8f, -0.1f), new Vector3(0.5f, 3.6f, 1.1f));
            Box("Lintel", gate.transform, stepMat, new Vector3(0f, 0.3f + PortalHalfHeight * 2f + 0.2f, -0.35f), new Vector3(gateWidth, 0.4f, 0.5f));
            var crown = CreatePrimitive(PrimitiveType.Sphere, "Crown", gate.transform, Lit("WayHome_Beam", new Color(1f, 0.9f, 1f), 0.5f, new Color(2.2f, 1.8f, 2.4f)), false);
            crown.transform.localPosition = new Vector3(0f, gateHeight + 0.25f, -0.6f);
            crown.transform.localScale = Vector3.one * 0.5f;

            // A tall beam of light above the portal, visible from anywhere, to lead the player back.
            var beam = CreatePrimitive(PrimitiveType.Cylinder, "Beacon Beam", home.transform,
                Lit("WayHome_Beam", new Color(1f, 0.9f, 1f), 0.5f, new Color(2.2f, 1.8f, 2.4f)), false);
            beam.transform.localPosition = position + Vector3.up * 25f;
            beam.transform.localScale = new Vector3(0.25f, 20f, 0.25f);
            beam.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var rising = Particles(home.transform, "Beacon Sparkles", position + Vector3.up * 5f, new Vector3(1.2f, 1.2f, 0.2f), 20f,
                new Color(1f, 0.85f, 0.95f), new Color(0.8f, 0.85f, 1f), 0.04f, 0.09f, Sparkle(), 1.5f, 3f);
            var shape = rising.shape;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            return home;
        }

        /// <summary>Pairs of glowing eyes watching from the dark.</summary>
        static GameObject WatchingEyes(Transform parent, Material mat, int pairs, float minR, float maxR)
        {
            var eyes = Group("Watching Eyes", parent);
            foreach (var p in Scatter(pairs, minR, maxR, 3f, null))
            {
                var pair = Group("Eyes", eyes.transform);
                pair.transform.localPosition = p + Vector3.up * Random.Range(1.0f, 2.4f);
                pair.transform.localRotation = Quaternion.LookRotation(-new Vector3(p.x, 0f, p.z), Vector3.up);
                for (int side = -1; side <= 1; side += 2)
                {
                    var eye = CreatePrimitive(PrimitiveType.Sphere, "Eye", pair.transform, mat, false);
                    eye.transform.localPosition = new Vector3(side * 0.07f, 0f, 0f);
                    eye.transform.localScale = Vector3.one * 0.05f;
                }
                var blink = pair.AddComponent<DreamFloat>();
                blink.bobHeight = 0.05f;
                blink.bobSpeed = 0.2f;
                blink.spin = Vector3.zero;
            }
            return eyes;
        }

        static DreamHazard Hazard(Transform parent, string name, Vector3 localPos, float radius, string message)
        {
            var go = Group(name, parent);
            go.transform.localPosition = localPos;
            var hazard = go.AddComponent<DreamHazard>();
            hazard.radius = radius;
            hazard.message = message;
            return hazard;
        }

        static Mesh RingMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshDir}/Ring.asset");
            if (mesh != null)
                return mesh;
            return SaveMesh(CreateEllipticTorus(1f, 1f, 0.02f, 96, 8), "Ring");
        }

        /// <summary>A cog lying in its local XZ plane: a disc with square teeth around the rim.</summary>
        static GameObject CreateGear(Transform parent, string name, float radius, float thickness, int teeth, Material mat)
        {
            var gear = Group(name, parent);
            var disc = CreatePrimitive(PrimitiveType.Cylinder, "Disc", gear.transform, mat, false);
            disc.transform.localScale = new Vector3(radius * 2f, thickness * 0.5f, radius * 2f);
            var hub = CreatePrimitive(PrimitiveType.Cylinder, "Hub", gear.transform, mat, false);
            hub.transform.localScale = new Vector3(radius * 0.5f, thickness * 0.7f, radius * 0.5f);

            float tooth = Mathf.PI * radius / teeth;
            for (int i = 0; i < teeth; i++)
            {
                var rot = Quaternion.Euler(0f, i * 360f / teeth, 0f);
                var t = Box("Tooth", gear.transform, mat, rot * new Vector3(0f, 0f, radius + tooth * 0.4f), new Vector3(tooth, thickness, tooth), false);
                t.transform.localRotation = rot;
            }
            return gear;
        }

        /// <summary>
        /// A spinning gear whose arms sweep low across the ground. Touching an arm wakes you up.
        /// Inside <paramref name="safeRadius"/> there is no arm, so something can sit safely at the center.
        /// </summary>
        static GameObject Sweeper(Transform parent, Vector3 position, float length, float degreesPerSecond, int arms, float safeRadius,
            Material gearMat, Material armMat, string message)
        {
            var sweeper = Group("Gear Sweeper", parent);
            sweeper.transform.localPosition = position;
            var spin = sweeper.AddComponent<DreamFloat>();
            spin.bobHeight = 0f;
            spin.spin = new Vector3(0f, degreesPerSecond, 0f);

            var gear = CreateGear(sweeper.transform, "Hub Gear", 0.8f, 0.4f, 10, gearMat);
            gear.transform.localPosition = new Vector3(0f, 0.2f, 0f);

            for (int a = 0; a < arms; a++)
            {
                var rot = Quaternion.Euler(0f, a * 360f / arms, 0f);
                float start = Mathf.Max(safeRadius, 0f);
                var arm = Box("Arm", sweeper.transform, armMat, rot * new Vector3(0f, 0.5f, (start + length) * 0.5f),
                    new Vector3(0.25f, 0.25f, length - start), false);
                arm.transform.localRotation = rot;
                var tip = CreatePrimitive(PrimitiveType.Sphere, "Tip", sweeper.transform, armMat, false);
                tip.transform.localPosition = rot * new Vector3(0f, 0.5f, length);
                tip.transform.localScale = Vector3.one * 0.5f;

                for (float d = Mathf.Max(start, 0.6f); d <= length + 0.01f; d += 0.8f)
                    Hazard(sweeper.transform, "Danger", rot * new Vector3(0f, 0.5f, d), 0.55f, message);
            }
            if (safeRadius <= 0f)
                Hazard(sweeper.transform, "Danger", Vector3.up * 0.5f, 0.9f, message);
            return sweeper;
        }

        /// <summary>A floating, spinning piece to collect. Its visual lives on a "Visual" child that hides once collected.</summary>
        static DreamCollectible CreateCollectible(Transform parent, string name, Vector3 position, Mesh mesh, Material mat, Color glow, Material sparkle)
        {
            var piece = Group(name, parent);
            piece.transform.localPosition = position;
            var collectible = piece.AddComponent<DreamCollectible>();

            var visual = Group("Visual", piece.transform);
            var bob = visual.AddComponent<DreamFloat>();
            bob.bobHeight = 0.1f;
            bob.bobSpeed = 0.35f;
            bob.spin = new Vector3(0f, 60f, 0f);
            var gem = MeshObject("Gem", visual.transform, mesh, mat);
            gem.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            PointLight(visual.transform, "Glow", Vector3.zero, glow, 5f, 1.5f);
            Particles(visual.transform, "Glimmer", Vector3.zero, Vector3.one * 0.6f, 6f, glow, Color.white, 0.02f, 0.05f, sparkle);

            collectible.hideOnCollect = visual;
            return collectible;
        }

        /// <summary>Hides <paramref name="reward"/>'s token until every collectible is found.</summary>
        static void CollectionGoal(WorldContext ctx, DreamToken reward, string itemName, int required, string collectMessage, string completeMessage)
        {
            var goal = ctx.nightmare.gameObject.AddComponent<DreamCollectionGoal>();
            goal.itemName = itemName;
            goal.required = required;
            goal.reward = reward.transform.parent.gameObject;
            goal.collectMessage = collectMessage;
            goal.completeMessage = completeMessage;
            goal.reward.SetActive(false);
        }

        // =====================================================================
        // Artifacts — always in the world, seen differently through each filter
        // =====================================================================

        class ArtifactMats
        {
            public Material bone, socket, iron, grave, marble, gold, moss, wood, tar, sparkle, mist, nightGlow, dreamGlow;
            public Material[] candy;
            public Mesh star, chainLink;
            public Color night, dream;
        }

        /// <summary>
        /// Places a world's artifacts. At each of <paramref name="spots"/> stands a paired artifact: one
        /// thing that the nightmare filter shows as something grim and the dream filter as something lovely.
        /// Along <paramref name="route"/> (start to goal) lie nightmare-only runes pointing toward the goal,
        /// and dream-only orbs that mark the way back to the start.
        /// </summary>
        static void PlaceArtifacts(WorldContext ctx, Color nightGlow, Color dreamGlow, IList<Vector3> route, IList<Vector3> spots)
        {
            var m = new ArtifactMats
            {
                bone = Lit("Artifact_Bone", new Color(0.85f, 0.82f, 0.72f), 0.3f, Color.black),
                socket = Lit("Artifact_Socket", Color.black, 0f, Color.black),
                iron = Lit("Artifact_Iron", new Color(0.3f, 0.28f, 0.27f), 0.6f, Color.black),
                grave = Lit("Artifact_Gravestone", new Color(0.42f, 0.42f, 0.45f), 0.2f, Color.black),
                marble = Lit("Artifact_Marble", new Color(0.95f, 0.93f, 1f), 0.6f, new Color(0.12f, 0.1f, 0.14f)),
                gold = Lit("Artifact_Gold", new Color(1f, 0.8f, 0.35f), 0.85f, new Color(0.4f, 0.25f, 0.05f)),
                moss = Lit("Artifact_Moss", new Color(0.25f, 0.4f, 0.15f), 0.1f, Color.black),
                wood = Lit("Artifact_Wood", new Color(0.4f, 0.28f, 0.18f), 0.15f, Color.black),
                tar = Lit("Artifact_Tar", new Color(0.04f, 0.02f, 0.05f), 0.95f, Color.black),
                sparkle = ctx.sparkle,
                mist = ctx.mist,
                nightGlow = Lit($"Artifact_NightGlow_{ctx.scene}", nightGlow, 0.5f, nightGlow * 2f),
                dreamGlow = Lit($"Artifact_DreamGlow_{ctx.scene}", dreamGlow, 0.5f, dreamGlow * 1.6f),
                candy = new[]
                {
                    Lit("Artifact_Pink", new Color(1f, 0.55f, 0.75f), 0.6f, new Color(0.25f, 0.08f, 0.15f)),
                    Lit("Artifact_Mint", new Color(0.5f, 1f, 0.8f), 0.6f, new Color(0.08f, 0.25f, 0.15f)),
                    Lit("Artifact_Sky", new Color(0.55f, 0.8f, 1f), 0.6f, new Color(0.08f, 0.15f, 0.3f)),
                    Lit("Artifact_Lemon", new Color(1f, 0.92f, 0.45f), 0.6f, new Color(0.25f, 0.2f, 0.05f)),
                },
                star = SaveMesh(CreateCrystal(0.14f, 0.3f), "ArtifactStar"),
                chainLink = SaveMesh(CreateEllipticTorus(0.035f, 0.06f, 0.012f, 16, 6), "ChainLink"),
                night = nightGlow,
                dream = dreamGlow,
            };

            var group = Group("Artifacts", ctx.root);
            for (int i = 0; i < spots.Count; i++)
            {
                switch (i % 6)
                {
                    case 0: GraveOrShrine(group.transform, Settled(spots[i], 1.2f), m); break;
                    case 1: CageOrLantern(group.transform, Settled(spots[i], 1.2f), m); break;
                    case 2: SkullsOrGifts(group.transform, Settled(spots[i], 1.2f), m); break;
                    case 3: SpikesOrRainbow(group.transform, Settled(spots[i], 1.2f), m); break;
                    case 4: WeepingOrAngel(group.transform, Settled(spots[i], 1.2f), m); break;
                    default: RottenOrWishingWell(group.transform, Settled(spots[i], 1.2f), m); break;
                }
            }

            for (int i = 1; i < route.Count - 1; i++)
            {
                float yaw = Quaternion.LookRotation(Vector3.ProjectOnPlane(route[i + 1] - route[i], Vector3.up)).eulerAngles.y;
                Rune(group.transform, OnGround(route[i]), yaw, m, i % 2 == 0);
                DreamOrb(group.transform, OnGround(route[i]), m, i % 3 == 0);
            }
        }

        /// <summary>A start-to-goal path that gently weaves, sampled roughly every <paramref name="spacing"/> meters.</summary>
        static List<Vector3> GroundRoute(Vector3 from, Vector3 to, float spacing)
        {
            var route = new List<Vector3>();
            int steps = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(from, to) / spacing));
            var side = Vector3.Cross(Vector3.up, (to - from).normalized);
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                route.Add(Vector3.Lerp(from, to, t) + side * Mathf.Sin(t * Mathf.PI * 2f) * 2f);
            }
            return route;
        }

        /// <summary>An artifact root with a "Nightmare Look" and/or "Dream Look" child to fill in.</summary>
        static DreamArtifact NewArtifact(Transform parent, string name, Vector3 position, float yaw, bool hasNightmare, bool hasDream)
        {
            var go = Group(name, parent);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var artifact = go.AddComponent<DreamArtifact>();
            if (hasNightmare)
                artifact.nightmareLook = Group("Nightmare Look", go.transform);
            if (hasDream)
            {
                artifact.dreamLook = Group("Dream Look", go.transform);
                artifact.dreamLook.SetActive(false);
            }
            return artifact;
        }

        // ---------- Small parts ----------

        static GameObject Part(PrimitiveType type, string name, Transform parent, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default)
        {
            var go = CreatePrimitive(type, name, parent, mat, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            return go;
        }

        static void FloatingGlow(Transform parent, Vector3 localPos, float size, Material mat, Color light, float range)
        {
            var orb = Part(PrimitiveType.Sphere, "Glow", parent, mat, localPos, Vector3.one * size);
            orb.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var bob = orb.AddComponent<DreamFloat>();
            bob.bobHeight = 0.1f;
            bob.bobSpeed = 0.3f;
            if (range > 0f)
                PointLight(orb.transform, "Light", Vector3.zero, light, range, 1.2f);
        }

        /// <summary>A small flickering flame: a short-lived particle plume rising from a point.</summary>
        static void Flame(Transform parent, Vector3 localPos, Color color, Material sparkle)
        {
            var flame = Particles(parent, "Flame", localPos, Vector3.one * 0.02f, 18f, color, Color.Lerp(color, Color.white, 0.5f), 0.04f, 0.08f, sparkle, 0.15f, 0.3f);
            var main = flame.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.maxParticles = 20;
            var shape = flame.shape;
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        static void Candle(Transform parent, Vector3 localPos, float height, Color flame, ArtifactMats m)
        {
            Part(PrimitiveType.Cylinder, "Candle", parent, m.bone, localPos + Vector3.up * height * 0.5f, new Vector3(0.06f, height * 0.5f, 0.06f));
            Part(PrimitiveType.Sphere, "Wax Drip", parent, m.bone, localPos + new Vector3(0.025f, height * 0.6f, 0f), new Vector3(0.03f, 0.06f, 0.03f));
            Flame(parent, localPos + Vector3.up * (height + 0.03f), flame, m.sparkle);
        }

        /// <summary>A hanging chain of interlocked links from <paramref name="top"/> straight down <paramref name="length"/> meters.</summary>
        static void Chain(Transform parent, Vector3 top, float length, ArtifactMats m)
        {
            int links = Mathf.Max(1, Mathf.RoundToInt(length / 0.1f));
            for (int i = 0; i < links; i++)
            {
                var link = MeshObject("Chain Link", parent, m.chainLink, m.iron);
                link.transform.localPosition = top + Vector3.down * (i * 0.1f + 0.05f);
                link.transform.localRotation = Quaternion.Euler(0f, i % 2 == 0 ? 0f : 90f, 0f);
            }
        }

        static void Moss(Transform parent, Vector3 localPos, Vector3 size, ArtifactMats m)
        {
            Part(PrimitiveType.Sphere, "Moss", parent, m.moss, localPos, size);
        }

        static void Skull(Transform parent, Vector3 localPos, Quaternion rotation, float size, ArtifactMats m)
        {
            var skull = Group("Skull", parent);
            skull.transform.localPosition = localPos;
            skull.transform.localRotation = rotation;
            Part(PrimitiveType.Sphere, "Cranium", skull.transform, m.bone, Vector3.zero, new Vector3(size, size * 0.95f, size));
            Part(PrimitiveType.Sphere, "Jaw", skull.transform, m.bone, new Vector3(0f, -size * 0.38f, size * 0.12f), new Vector3(size * 0.62f, size * 0.32f, size * 0.6f));
            for (int side = -1; side <= 1; side += 2)
                Part(PrimitiveType.Sphere, "Eye Socket", skull.transform, m.socket, new Vector3(side * size * 0.2f, size * 0.05f, size * 0.42f), Vector3.one * size * 0.25f);
            Part(PrimitiveType.Cube, "Nose", skull.transform, m.socket, new Vector3(0f, -size * 0.12f, size * 0.47f), new Vector3(size * 0.08f, size * 0.12f, size * 0.05f), new Vector3(0f, 0f, 45f));
        }

        static void Gift(Transform parent, Vector3 localPos, float size, float yaw, Material wrap, Material ribbon)
        {
            var gift = Box("Gift", parent, wrap, localPos, Vector3.one * size, false);
            gift.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box("Ribbon", gift.transform, ribbon, Vector3.zero, new Vector3(1.02f, 1.02f, 0.15f), false);
            Box("Ribbon", gift.transform, ribbon, Vector3.zero, new Vector3(0.15f, 1.02f, 1.02f), false);
            for (int side = -1; side <= 1; side += 2)
                Part(PrimitiveType.Sphere, "Bow", gift.transform, ribbon, new Vector3(side * 0.13f, 0.58f, 0f), new Vector3(0.28f, 0.14f, 0.12f), new Vector3(0f, 0f, side * 25f));
        }

        // ---------- The paired artifacts ----------

        /// <summary>
        /// Nightmare: a cracked, mossy gravestone on a fresh mound behind a broken fence, candles guttering.
        /// Dream: a domed marble shrine on stepped plinths, a flower at its heart, a garland and a floating light.
        /// </summary>
        static void GraveOrShrine(Transform parent, Vector3 position, ArtifactMats m)
        {
            var artifact = NewArtifact(parent, "Gravestone / Flower Shrine", position, Random.Range(0f, 360f), true, true);
            var n = artifact.nightmareLook.transform;
            var slab = Part(PrimitiveType.Cube, "Gravestone", n, m.grave, new Vector3(0f, 0.65f, 0f), new Vector3(0.8f, 1.3f, 0.2f),
                new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-6f, 6f)));
            Part(PrimitiveType.Sphere, "Rounded Top", slab.transform, m.grave, new Vector3(0f, 0.5f, 0f), new Vector3(1f, 0.3f, 1f));
            Moss(slab.transform, new Vector3(-0.25f, -0.38f, 0.5f), new Vector3(0.45f, 0.2f, 0.4f), m);
            Moss(slab.transform, new Vector3(0.3f, 0.42f, 0.5f), new Vector3(0.3f, 0.12f, 0.3f), m);
            Part(PrimitiveType.Cube, "Broken Corner", n, m.grave, new Vector3(0.45f, 0.06f, 0.3f), new Vector3(0.22f, 0.12f, 0.18f), new Vector3(10f, 35f, 20f));
            Part(PrimitiveType.Cube, "Cross", n, m.iron, new Vector3(0f, 1.6f, 0f), new Vector3(0.1f, 0.55f, 0.08f));
            Part(PrimitiveType.Cube, "Cross Bar", n, m.iron, new Vector3(0f, 1.68f, 0f), new Vector3(0.38f, 0.1f, 0.08f));
            Part(PrimitiveType.Sphere, "Mound", n, m.grave, new Vector3(0f, 0f, 0.9f), new Vector3(1f, 0.28f, 1.6f));
            Moss(n, new Vector3(0.2f, 0.08f, 1.1f), new Vector3(0.5f, 0.12f, 0.6f), m);
            for (int s = 0; s < 3; s++)
                Part(PrimitiveType.Sphere, "Pebble", n, m.grave, new Vector3(Random.Range(-0.5f, 0.5f), 0.04f, Random.Range(0.3f, 1.6f)), Vector3.one * Random.Range(0.08f, 0.16f));
            // A broken fence behind the grave: leaning posts, a couple still joined by a rail.
            var posts = new List<Vector3>();
            for (int p = 0; p < 5; p++)
            {
                var dir = Quaternion.Euler(0f, 120f + p * 30f, 0f) * Vector3.forward;
                float h = Random.Range(0.6f, 1.1f);
                var postPos = dir * 1.4f + Vector3.up * h * 0.5f;
                posts.Add(postPos);
                Part(PrimitiveType.Cube, "Fence Post", n, m.wood, postPos, new Vector3(0.08f, h, 0.08f), new Vector3(Random.Range(-12f, 12f), 0f, Random.Range(-12f, 12f)));
            }
            for (int p = 0; p < 2; p++)
            {
                var a = posts[p]; var b = posts[p + 1];
                var rail = Part(PrimitiveType.Cube, "Fence Rail", n, m.wood, (a + b) * 0.5f + Vector3.up * 0.15f, new Vector3(0.05f, 0.05f, Vector3.Distance(a, b)));
                rail.transform.localRotation = Quaternion.LookRotation(b - a);
            }
            Candle(n, new Vector3(-0.3f, 0f, 0.35f), 0.18f, m.night, m);
            Candle(n, new Vector3(0.28f, 0f, 0.4f), 0.12f, m.night, m);
            PointLight(n, "Candle Light", new Vector3(0f, 0.4f, 0.4f), m.night, 3.5f, 1f);

            var d = artifact.dreamLook.transform;
            Part(PrimitiveType.Cylinder, "Plinth", d, m.marble, new Vector3(0f, 0.08f, 0f), new Vector3(1.5f, 0.08f, 1.5f));
            Part(PrimitiveType.Cylinder, "Upper Plinth", d, m.marble, new Vector3(0f, 0.22f, 0f), new Vector3(1.15f, 0.06f, 1.15f));
            Part(PrimitiveType.Cylinder, "Gold Band", d, m.gold, new Vector3(0f, 0.29f, 0f), new Vector3(1.17f, 0.012f, 1.17f));
            for (int c = 0; c < 4; c++)
            {
                var at = Quaternion.Euler(0f, 45f + c * 90f, 0f) * Vector3.forward * 0.45f;
                Part(PrimitiveType.Cylinder, "Column", d, m.marble, at + Vector3.up * 0.95f, new Vector3(0.09f, 0.65f, 0.09f));
                Part(PrimitiveType.Cube, "Capital", d, m.gold, at + Vector3.up * 1.62f, new Vector3(0.14f, 0.05f, 0.14f));
            }
            Part(PrimitiveType.Sphere, "Dome", d, m.marble, new Vector3(0f, 1.72f, 0f), new Vector3(1.15f, 0.55f, 1.15f));
            Part(PrimitiveType.Cylinder, "Dome Rim", d, m.gold, new Vector3(0f, 1.66f, 0f), new Vector3(1.2f, 0.02f, 1.2f));
            Part(PrimitiveType.Sphere, "Finial", d, m.dreamGlow, new Vector3(0f, 2.05f, 0f), Vector3.one * 0.12f);
            var garland = MeshObject("Garland", d, RingMesh(), m.candy[0]);
            garland.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            garland.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            garland.transform.localScale = new Vector3(0.5f, 0.5f, 4f);
            for (int p = 0; p < 6; p++)
                Part(PrimitiveType.Sphere, "Petal", d, m.candy[p % m.candy.Length],
                    new Vector3(0f, 0.55f, 0f) + Quaternion.Euler(0f, p * 60f, 0f) * Vector3.forward * 0.13f, new Vector3(0.16f, 0.06f, 0.16f));
            Part(PrimitiveType.Cylinder, "Stem", d, m.moss, new Vector3(0f, 0.42f, 0f), new Vector3(0.03f, 0.12f, 0.03f));
            Part(PrimitiveType.Sphere, "Flower Heart", d, m.gold, new Vector3(0f, 0.57f, 0f), Vector3.one * 0.1f);
            FloatingGlow(d, new Vector3(0f, 1.05f, 0f), 0.18f, m.dreamGlow, m.dream, 5f);
            Particles(d, "Petal Drift", new Vector3(0f, 1.2f, 0f), new Vector3(1.2f, 1f, 1.2f), 6f, m.dream, Color.white, 0.03f, 0.06f, m.sparkle);
        }

        /// <summary>
        /// Nightmare: a rusted cage hung by a chain from a gallows post, a skull inside and bones below.
        /// Dream: an ornate golden lamp post with two lanterns, a ribbon and fireflies.
        /// </summary>
        static void CageOrLantern(Transform parent, Vector3 position, ArtifactMats m)
        {
            var artifact = NewArtifact(parent, "Hanging Cage / Lantern Post", position, Random.Range(0f, 360f), true, true);
            var n = artifact.nightmareLook.transform;
            Part(PrimitiveType.Cylinder, "Gallows Post", n, m.wood, new Vector3(0f, 1.5f, -0.9f), new Vector3(0.16f, 1.5f, 0.16f));
            Part(PrimitiveType.Cube, "Gallows Beam", n, m.wood, new Vector3(0f, 2.95f, -0.4f), new Vector3(0.12f, 0.12f, 1.2f));
            Part(PrimitiveType.Cube, "Brace", n, m.wood, new Vector3(0f, 2.6f, -0.65f), new Vector3(0.07f, 0.07f, 0.7f), new Vector3(-45f, 0f, 0f));
            Chain(n, new Vector3(0f, 2.89f, 0f), 0.9f, m);
            var cage = Group("Cage", n);
            cage.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            cage.transform.localRotation = Quaternion.Euler(4f, 12f, -3f);
            foreach (float y in new[] { 0f, 0.7f, 1.35f })
                Part(PrimitiveType.Cylinder, "Cage Ring", cage.transform, m.iron, new Vector3(0f, y, 0f), new Vector3(1f, y == 0f ? 0.04f : 0.015f, 1f));
            Part(PrimitiveType.Sphere, "Cage Dome", cage.transform, m.iron, new Vector3(0f, 1.35f, 0f), new Vector3(1f, 0.35f, 1f));
            for (int b = 0; b < 10; b++)
                Part(PrimitiveType.Cylinder, "Bar", cage.transform, m.iron, Quaternion.Euler(0f, b * 36f, 0f) * new Vector3(0f, 0.68f, 0.48f), new Vector3(0.035f, 0.68f, 0.035f));
            Skull(cage.transform, new Vector3(0.1f, 0.18f, 0f), Quaternion.Euler(-10f, 30f, 15f), 0.32f, m);
            Part(PrimitiveType.Capsule, "Bone", n, m.bone, new Vector3(0.4f, 0.04f, 0.5f), new Vector3(0.05f, 0.18f, 0.05f), new Vector3(90f, 40f, 0f));
            Part(PrimitiveType.Capsule, "Bone", n, m.bone, new Vector3(-0.3f, 0.04f, 0.45f), new Vector3(0.05f, 0.14f, 0.05f), new Vector3(90f, -70f, 0f));
            Skull(n, new Vector3(-0.5f, 0.12f, 0.1f), Quaternion.Euler(0f, -50f, 80f), 0.25f, m);
            PointLight(n, "Glow", new Vector3(0f, 1f, 0f), m.night, 3.5f, 1f);

            var d = artifact.dreamLook.transform;
            Part(PrimitiveType.Cylinder, "Base", d, m.gold, new Vector3(0f, 0.05f, 0f), new Vector3(0.6f, 0.05f, 0.6f));
            Part(PrimitiveType.Cylinder, "Base Tier", d, m.gold, new Vector3(0f, 0.2f, 0f), new Vector3(0.35f, 0.1f, 0.35f));
            Part(PrimitiveType.Cylinder, "Post", d, m.gold, new Vector3(0f, 1.25f, 0f), new Vector3(0.09f, 1f, 0.09f));
            foreach (float y in new[] { 0.7f, 1.4f, 2.0f })
                Part(PrimitiveType.Sphere, "Knot", d, m.gold, new Vector3(0f, y, 0f), Vector3.one * 0.15f);
            Part(PrimitiveType.Cube, "Crossbar", d, m.gold, new Vector3(0f, 2.15f, 0f), new Vector3(1.1f, 0.06f, 0.06f));
            Part(PrimitiveType.Sphere, "Finial", d, m.gold, new Vector3(0f, 2.35f, 0f), new Vector3(0.12f, 0.2f, 0.12f));
            for (int side = -1; side <= 1; side += 2)
            {
                var at = new Vector3(side * 0.52f, 1.92f, 0f);
                Part(PrimitiveType.Cylinder, "Hood", d, m.gold, at + Vector3.up * 0.17f, new Vector3(0.26f, 0.03f, 0.26f));
                Part(PrimitiveType.Cylinder, "Cap", d, m.gold, at + Vector3.down * 0.15f, new Vector3(0.14f, 0.02f, 0.14f));
                var lantern = Part(PrimitiveType.Sphere, "Lantern", d, m.dreamGlow, at, new Vector3(0.24f, 0.3f, 0.24f));
                lantern.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                PointLight(lantern.transform, "Light", Vector3.zero, m.dream, 5f, 1.1f);
            }
            Part(PrimitiveType.Cube, "Ribbon", d, m.candy[0], new Vector3(0.12f, 1.75f, 0.06f), new Vector3(0.05f, 0.55f, 0.01f), new Vector3(0f, 0f, 12f));
            Part(PrimitiveType.Cube, "Ribbon", d, m.candy[2], new Vector3(-0.1f, 1.78f, 0.06f), new Vector3(0.05f, 0.5f, 0.01f), new Vector3(0f, 0f, -10f));
            Particles(d, "Fireflies", new Vector3(0f, 1.6f, 0f), new Vector3(2f, 1.5f, 2f), 6f, new Color(1f, 0.95f, 0.5f), m.dream, 0.03f, 0.05f, m.sparkle);
        }

        /// <summary>
        /// Nightmare: a heap of skulls in a tar puddle, a rusted sword driven into the ground, candles burning.
        /// Dream: a pile of bowed presents with balloons bobbing above.
        /// </summary>
        static void SkullsOrGifts(Transform parent, Vector3 position, ArtifactMats m)
        {
            var artifact = NewArtifact(parent, "Skull Pile / Gift Pile", position, Random.Range(0f, 360f), true, true);
            var n = artifact.nightmareLook.transform;
            Part(PrimitiveType.Cylinder, "Tar", n, m.tar, new Vector3(0f, 0.005f, 0f), new Vector3(1.8f, 0.005f, 1.4f));
            for (int k = 0; k < 5; k++)
            {
                var dir = Quaternion.Euler(0f, k * 72f + 20f, 0f) * Vector3.forward;
                Skull(n, dir * 0.38f + Vector3.up * 0.15f, Quaternion.LookRotation(dir) * Quaternion.Euler(Random.Range(-15f, 15f), 0f, Random.Range(-20f, 20f)), 0.3f, m);
            }
            for (int k = 0; k < 3; k++)
            {
                var dir = Quaternion.Euler(0f, k * 120f + 65f, 0f) * Vector3.forward;
                Skull(n, dir * 0.17f + Vector3.up * 0.42f, Quaternion.LookRotation(dir), 0.28f, m);
            }
            Skull(n, Vector3.up * 0.68f, Quaternion.Euler(-10f, 0f, 0f), 0.3f, m);
            for (int b = 0; b < 3; b++)
                Part(PrimitiveType.Capsule, "Bone", n, m.bone, Quaternion.Euler(0f, b * 120f + 40f, 0f) * Vector3.forward * 0.75f + Vector3.up * 0.04f,
                    new Vector3(0.05f, 0.16f, 0.05f), new Vector3(90f, b * 70f, 0f));
            var sword = Group("Sword", n);
            sword.transform.localPosition = new Vector3(0.75f, 0f, -0.2f);
            sword.transform.localRotation = Quaternion.Euler(12f, 30f, -8f);
            Part(PrimitiveType.Cube, "Blade", sword.transform, m.iron, new Vector3(0f, 0.45f, 0f), new Vector3(0.07f, 0.9f, 0.015f));
            Part(PrimitiveType.Cube, "Guard", sword.transform, m.iron, new Vector3(0f, 0.92f, 0f), new Vector3(0.3f, 0.04f, 0.05f));
            Part(PrimitiveType.Cylinder, "Grip", sword.transform, m.wood, new Vector3(0f, 1.05f, 0f), new Vector3(0.035f, 0.1f, 0.035f));
            Part(PrimitiveType.Sphere, "Pommel", sword.transform, m.iron, new Vector3(0f, 1.17f, 0f), Vector3.one * 0.06f);
            Candle(n, new Vector3(-0.55f, 0f, 0.35f), 0.2f, m.night, m);
            Candle(n, new Vector3(0.1f, 0.82f, 0.05f), 0.1f, m.night, m);
            PointLight(n, "Candle Light", new Vector3(0f, 0.9f, 0f), m.night, 3.5f, 1f);

            var d = artifact.dreamLook.transform;
            Gift(d, new Vector3(0f, 0.3f, 0f), 0.6f, 0f, m.candy[0], m.gold);
            Gift(d, new Vector3(0.5f, 0.2f, 0.2f), 0.4f, 20f, m.candy[1], m.gold);
            Gift(d, new Vector3(-0.4f, 0.18f, -0.3f), 0.36f, -15f, m.candy[2], m.gold);
            Gift(d, new Vector3(0.05f, 0.8f, 0f), 0.4f, 35f, m.candy[3], m.gold);
            Gift(d, new Vector3(-0.15f, 0.12f, 0.5f), 0.24f, 50f, m.candy[1], m.candy[0]);
            for (int b = 0; b < 3; b++)
            {
                var balloon = Group("Balloon", d);
                balloon.transform.localPosition = new Vector3(-0.3f + b * 0.3f, 1.9f + b * 0.15f, -0.1f + (b % 2) * 0.2f);
                var bob = balloon.AddComponent<DreamFloat>();
                bob.bobHeight = 0.12f;
                bob.bobSpeed = 0.25f;
                bob.spin = Vector3.zero;
                Part(PrimitiveType.Sphere, "Balloon", balloon.transform, m.candy[(b + 1) % m.candy.Length], Vector3.zero, new Vector3(0.3f, 0.36f, 0.3f));
                Part(PrimitiveType.Cylinder, "String", balloon.transform, m.marble, new Vector3(0f, -0.55f, 0f), new Vector3(0.008f, 0.38f, 0.008f));
            }
        }

        /// <summary>
        /// Nightmare: a black totem bristling with spikes and wrapped in chains, a skull below a burning eye.
        /// Dream: a stacked rainbow totem ringed with flowers, ribbons streaming from a spinning star.
        /// </summary>
        static void SpikesOrRainbow(Transform parent, Vector3 position, ArtifactMats m)
        {
            var artifact = NewArtifact(parent, "Spike Totem / Rainbow Totem", position, Random.Range(0f, 360f), true, true);
            var n = artifact.nightmareLook.transform;
            Part(PrimitiveType.Cylinder, "Column", n, m.iron, new Vector3(0f, 1.1f, 0f), new Vector3(0.35f, 1.1f, 0.35f));
            for (int k = 0; k < 12; k++)
            {
                var dir = (Quaternion.Euler(0f, k * 30f + (k % 3) * 10f, 0f) * new Vector3(0f, 0.3f + (k % 3) * 0.2f, 1f)).normalized;
                var spike = Part(PrimitiveType.Cylinder, "Spike", n, m.iron, new Vector3(0f, 0.4f + (k % 4) * 0.45f, 0f) + dir * 0.38f, new Vector3(0.045f, 0.3f, 0.045f));
                spike.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            }
            foreach (float y in new[] { 0.6f, 1.25f })
            {
                var wrap = MeshObject("Chain Wrap", n, RingMesh(), m.iron);
                wrap.transform.localPosition = new Vector3(0f, y, 0f);
                wrap.transform.localRotation = Quaternion.Euler(90f + Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
                wrap.transform.localScale = new Vector3(0.2f, 0.2f, 5f);
            }
            for (int r = 0; r < 5; r++)
                Part(PrimitiveType.Cube, "Rubble", n, m.grave, Quaternion.Euler(0f, r * 72f, 0f) * Vector3.forward * 0.4f + Vector3.up * 0.07f,
                    Vector3.one * Random.Range(0.12f, 0.22f), new Vector3(Random.Range(0f, 40f), Random.Range(0f, 90f), Random.Range(0f, 40f)));
            Skull(n, new Vector3(0f, 2.05f, 0.2f), Quaternion.identity, 0.28f, m);
            var eye = Part(PrimitiveType.Sphere, "Eye", n, m.nightGlow, new Vector3(0f, 2.4f, 0f), Vector3.one * 0.25f);
            PointLight(eye.transform, "Light", Vector3.zero, m.night, 4f, 1.2f);
            Flame(n, new Vector3(0f, 2.55f, 0f), m.night, m.sparkle);

            var d = artifact.dreamLook.transform;
            for (int j = 0; j < 5; j++)
            {
                Part(PrimitiveType.Cylinder, "Rainbow Ring", d, m.candy[j % m.candy.Length], new Vector3(0f, 0.15f + j * 0.35f, 0f),
                    new Vector3(1f - j * 0.15f, 0.15f, 1f - j * 0.15f));
                Part(PrimitiveType.Cylinder, "Gold Seam", d, m.gold, new Vector3(0f, 0.31f + j * 0.35f, 0f),
                    new Vector3(1.01f - j * 0.15f, 0.01f, 1.01f - j * 0.15f));
            }
            for (int f = 0; f < 7; f++)
            {
                var at = Quaternion.Euler(0f, f * 51f, 0f) * Vector3.forward * 0.75f;
                Part(PrimitiveType.Cylinder, "Stem", d, m.moss, at + Vector3.up * 0.15f, new Vector3(0.025f, 0.15f, 0.025f));
                Part(PrimitiveType.Sphere, "Bloom", d, m.candy[f % m.candy.Length], at + Vector3.up * 0.32f, new Vector3(0.16f, 0.09f, 0.16f));
            }
            var starHolder = Group("Star", d);
            starHolder.transform.localPosition = new Vector3(0f, 2.1f, 0f);
            var spin = starHolder.AddComponent<DreamFloat>();
            spin.bobHeight = 0.08f;
            spin.bobSpeed = 0.3f;
            spin.spin = new Vector3(0f, 60f, 0f);
            MeshObject("Star", starHolder.transform, m.star, m.dreamGlow).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            PointLight(starHolder.transform, "Light", Vector3.zero, m.dream, 5f, 1.2f);
            for (int r = 0; r < 4; r++)
            {
                var dir = Quaternion.Euler(0f, r * 90f + 45f, 0f) * Vector3.forward;
                Part(PrimitiveType.Cube, "Streamer", starHolder.transform, m.candy[r % m.candy.Length], dir * 0.25f + Vector3.down * 0.35f,
                    new Vector3(0.05f, 0.6f, 0.01f), new Vector3(-20f, Quaternion.LookRotation(dir).eulerAngles.y, 0f));
            }
        }

        /// <summary>
        /// Nightmare: a hooded statue weeping glowing tears into its hands, on a cracked, mossy plinth.
        /// Dream: a winged marble angel with a golden halo, holding up a light.
        /// </summary>
        static void WeepingOrAngel(Transform parent, Vector3 position, ArtifactMats m)
        {
            var artifact = NewArtifact(parent, "Weeping Statue / Angel", position, Random.Range(0f, 360f), true, true);
            var n = artifact.nightmareLook.transform;
            Part(PrimitiveType.Cube, "Plinth", n, m.grave, new Vector3(0f, 0.3f, 0f), new Vector3(0.85f, 0.6f, 0.85f), new Vector3(0f, 0f, 2f));
            Part(PrimitiveType.Cube, "Plinth Chunk", n, m.grave, new Vector3(0.55f, 0.08f, 0.3f), new Vector3(0.25f, 0.16f, 0.3f), new Vector3(15f, 30f, 25f));
            Moss(n, new Vector3(-0.3f, 0.55f, 0.3f), new Vector3(0.35f, 0.12f, 0.3f), m);
            Part(PrimitiveType.Capsule, "Robe", n, m.grave, new Vector3(0f, 1.3f, 0f), new Vector3(0.55f, 0.72f, 0.42f), new Vector3(6f, 0f, 0f));
            Part(PrimitiveType.Sphere, "Hood", n, m.grave, new Vector3(0f, 2.02f, 0.05f), new Vector3(0.42f, 0.5f, 0.46f), new Vector3(20f, 0f, 0f));
            for (int side = -1; side <= 1; side += 2)
            {
                Part(PrimitiveType.Capsule, "Arm", n, m.grave, new Vector3(side * 0.18f, 1.75f, 0.2f), new Vector3(0.12f, 0.22f, 0.12f), new Vector3(-50f, 0f, side * 20f));
                Part(PrimitiveType.Sphere, "Hand", n, m.grave, new Vector3(side * 0.07f, 1.98f, 0.25f), new Vector3(0.12f, 0.15f, 0.08f));
                Part(PrimitiveType.Cube, "Tear", n, m.nightGlow, new Vector3(side * 0.1f, 1.62f, 0.26f), new Vector3(0.018f, 0.35f, 0.01f));
            }
            Moss(n, new Vector3(0.22f, 1.75f, -0.1f), new Vector3(0.25f, 0.12f, 0.25f), m);
            Part(PrimitiveType.Cylinder, "Tear Pool", n, m.nightGlow, new Vector3(0f, 0.61f, 0.3f), new Vector3(0.3f, 0.005f, 0.2f));
            PointLight(n, "Tear Glow", new Vector3(0f, 1.6f, 0.5f), m.night, 3f, 0.9f);

            var d = artifact.dreamLook.transform;
            Part(PrimitiveType.Cube, "Plinth", d, m.marble, new Vector3(0f, 0.3f, 0f), new Vector3(0.85f, 0.6f, 0.85f));
            Part(PrimitiveType.Cube, "Gold Trim", d, m.gold, new Vector3(0f, 0.58f, 0f), new Vector3(0.88f, 0.04f, 0.88f));
            Part(PrimitiveType.Capsule, "Robe", d, m.marble, new Vector3(0f, 1.3f, 0f), new Vector3(0.5f, 0.72f, 0.4f));
            Part(PrimitiveType.Sphere, "Head", d, m.marble, new Vector3(0f, 2.05f, 0f), Vector3.one * 0.32f);
            var halo = MeshObject("Halo", d, RingMesh(), m.gold);
            halo.transform.localPosition = new Vector3(0f, 2.35f, -0.05f);
            halo.transform.localRotation = Quaternion.Euler(75f, 0f, 0f);
            halo.transform.localScale = new Vector3(0.2f, 0.2f, 4f);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(PrimitiveType.Sphere, "Wing", d, m.marble, new Vector3(side * 0.42f, 1.75f, -0.25f), new Vector3(0.12f, 0.95f, 0.5f), new Vector3(-15f, side * 20f, side * -30f));
                Part(PrimitiveType.Sphere, "Wing Tip", d, m.marble, new Vector3(side * 0.7f, 2.2f, -0.35f), new Vector3(0.08f, 0.45f, 0.3f), new Vector3(-15f, side * 20f, side * -45f));
            }
            Part(PrimitiveType.Capsule, "Raised Arm", d, m.marble, new Vector3(0.25f, 2.05f, 0.12f), new Vector3(0.1f, 0.3f, 0.1f), new Vector3(0f, 0f, -25f));
            FloatingGlow(d, new Vector3(0.38f, 2.45f, 0.15f), 0.16f, m.dreamGlow, m.dream, 5f);
            Particles(d, "Feathers", new Vector3(0f, 1.8f, 0f), new Vector3(1.4f, 1.2f, 1.4f), 5f, Color.white, m.dream, 0.03f, 0.06f, m.sparkle);
        }

        /// <summary>
        /// Nightmare: a crumbling well of black water glowing from below, its roof collapsed and bucket tipped over.
        /// Dream: a candy-roofed wishing well with sparkling water, a hanging bucket and coins on the rim.
        /// </summary>
        static void RottenOrWishingWell(Transform parent, Vector3 position, ArtifactMats m)
        {
            var artifact = NewArtifact(parent, "Rotten Well / Wishing Well", position, Random.Range(0f, 360f), true, true);
            var n = artifact.nightmareLook.transform;
            for (int s = 0; s < 12; s++)
            {
                if (s == 3 || s == 8)
                    continue;
                var rot = Quaternion.Euler(0f, s * 30f, 0f);
                bool tumbled = s == 4;
                var pos = tumbled ? rot * new Vector3(0f, 0.12f, 1.15f) : rot * new Vector3(0f, Random.Range(0.3f, 0.4f), 0.8f);
                Part(PrimitiveType.Cube, "Well Stone", n, m.grave, pos, new Vector3(0.42f, tumbled ? 0.24f : 0.7f, 0.25f),
                    (rot * Quaternion.Euler(tumbled ? 80f : Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f))).eulerAngles);
            }
            Moss(n, new Vector3(0.5f, 0.68f, 0.5f), new Vector3(0.4f, 0.12f, 0.3f), m);
            Part(PrimitiveType.Cylinder, "Black Water", n, m.tar, new Vector3(0f, 0.35f, 0f), new Vector3(1.4f, 0.01f, 1.4f));
            PointLight(n, "Glow From Below", new Vector3(0f, 0.6f, 0f), m.night, 3.5f, 1.3f);
            Particles(n, "Rising Miasma", new Vector3(0f, 0.5f, 0f), new Vector3(1f, 0.2f, 1f), 4f, new Color(0.05f, 0.02f, 0.08f, 0.6f), m.night * 0.4f, 0.25f, 0.5f, m.mist);
            Part(PrimitiveType.Cylinder, "Post", n, m.wood, new Vector3(-0.9f, 0.9f, 0f), new Vector3(0.1f, 0.9f, 0.1f));
            Part(PrimitiveType.Cylinder, "Broken Post", n, m.wood, new Vector3(0.95f, 0.45f, 0.05f), new Vector3(0.1f, 0.45f, 0.1f), new Vector3(0f, 0f, -18f));
            Part(PrimitiveType.Cube, "Fallen Roof", n, m.wood, new Vector3(0.6f, 0.08f, 1.3f), new Vector3(1.4f, 0.06f, 0.7f), new Vector3(8f, 25f, 6f));
            Part(PrimitiveType.Cylinder, "Tipped Bucket", n, m.wood, new Vector3(-0.85f, 0.14f, 0.9f), new Vector3(0.28f, 0.16f, 0.28f), new Vector3(85f, 30f, 0f));
            Part(PrimitiveType.Cylinder, "Frayed Rope", n, m.bone, new Vector3(-0.9f, 1.4f, 0.12f), new Vector3(0.02f, 0.35f, 0.02f), new Vector3(10f, 0f, 8f));

            var d = artifact.dreamLook.transform;
            for (int s = 0; s < 12; s++)
            {
                var rot = Quaternion.Euler(0f, s * 30f, 0f);
                Part(PrimitiveType.Cube, "Well Stone", d, s % 2 == 0 ? m.marble : m.candy[(s / 2) % m.candy.Length],
                    rot * new Vector3(0f, 0.35f, 0.8f), new Vector3(0.42f, 0.7f, 0.25f), rot.eulerAngles);
            }
            var rim = MeshObject("Gold Rim", d, RingMesh(), m.gold);
            rim.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rim.transform.localScale = new Vector3(0.82f, 0.82f, 3f);
            var water = Part(PrimitiveType.Cylinder, "Wishing Water", d, m.dreamGlow, new Vector3(0f, 0.5f, 0f), new Vector3(1.4f, 0.01f, 1.4f));
            water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Particles(d, "Wish Sparkles", new Vector3(0f, 0.8f, 0f), new Vector3(1f, 0.5f, 1f), 10f, m.dream, Color.white, 0.03f, 0.06f, m.sparkle);
            PointLight(d, "Water Glow", new Vector3(0f, 0.8f, 0f), m.dream, 4f, 1f);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(PrimitiveType.Cylinder, "Post", d, m.wood, new Vector3(side * 0.9f, 1f, 0f), new Vector3(0.1f, 1f, 0.1f));
                Part(PrimitiveType.Cube, "Roof", d, m.candy[0], new Vector3(0f, 2.22f, side * 0.38f), new Vector3(2.1f, 0.06f, 0.95f), new Vector3(side * -35f, 0f, 0f));
            }
            Part(PrimitiveType.Cube, "Ridge", d, m.gold, new Vector3(0f, 2.5f, 0f), new Vector3(2.15f, 0.06f, 0.06f));
            Part(PrimitiveType.Cylinder, "Crank", d, m.wood, new Vector3(0f, 1.65f, 0f), new Vector3(0.08f, 0.9f, 0.08f), new Vector3(0f, 0f, 90f));
            Part(PrimitiveType.Cylinder, "Rope", d, m.bone, new Vector3(0f, 1.35f, 0f), new Vector3(0.02f, 0.3f, 0.02f));
            Part(PrimitiveType.Cylinder, "Bucket", d, m.wood, new Vector3(0f, 0.98f, 0f), new Vector3(0.26f, 0.14f, 0.26f));
            Part(PrimitiveType.Cylinder, "Bucket Band", d, m.gold, new Vector3(0f, 1.02f, 0f), new Vector3(0.27f, 0.015f, 0.27f));
            for (int c = 0; c < 6; c++)
                Part(PrimitiveType.Cylinder, "Coin", d, m.gold, Quaternion.Euler(0f, c * 55f + 10f, 0f) * new Vector3(0f, 0.72f, 0.8f), new Vector3(0.08f, 0.006f, 0.08f),
                    new Vector3(Random.Range(-15f, 15f), 0f, Random.Range(-15f, 15f)));
        }

        /// <summary>Nightmare-only: a glowing rune circle on the ground with an arrow toward the goal.</summary>
        static void Rune(Transform parent, Vector3 position, float yaw, ArtifactMats m, bool withLight)
        {
            var artifact = NewArtifact(parent, "Rune (Nightmare Only)", position, yaw, true, false);
            artifact.transform.localRotation = AlongGround(position) * artifact.transform.localRotation;
            var n = artifact.nightmareLook.transform;
            foreach (float r in new[] { 0.7f, 0.55f })
            {
                var ring = MeshObject("Circle", n, RingMesh(), m.nightGlow);
                ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ring.transform.localScale = new Vector3(r, r, 3f);
            }
            for (int g = 0; g < 6; g++)
                Part(PrimitiveType.Cube, "Glyph", n, m.nightGlow, Quaternion.Euler(0f, g * 60f + 30f, 0f) * Vector3.forward * 0.62f + Vector3.up * 0.03f,
                    new Vector3(0.1f, 0.02f, 0.04f), new Vector3(0f, g * 60f + 30f + (g % 2) * 45f, 0f));
            Part(PrimitiveType.Cube, "Arrow", n, m.nightGlow, new Vector3(0f, 0.03f, 0f), new Vector3(0.12f, 0.02f, 0.7f));
            for (int side = -1; side <= 1; side += 2)
                Part(PrimitiveType.Cube, "Arrow Head", n, m.nightGlow, new Vector3(side * 0.115f, 0.03f, 0.186f), new Vector3(0.08f, 0.02f, 0.4f), new Vector3(0f, -side * 35f, 0f));
            Particles(n, "Embers", new Vector3(0f, 0.2f, 0f), new Vector3(1f, 0.2f, 1f), 3f, m.night, Color.black, 0.02f, 0.04f, m.sparkle);
            if (withLight)
                PointLight(n, "Light", new Vector3(0f, 0.4f, 0f), m.night, 3f, 1f);
        }

        /// <summary>Dream-only: a floating orb of light with a slow halo, marking the way back to the start.</summary>
        static void DreamOrb(Transform parent, Vector3 position, ArtifactMats m, bool withLight)
        {
            var artifact = NewArtifact(parent, "Dream Orb (Dream Only)", position, 0f, false, true);
            var d = artifact.dreamLook.transform;
            FloatingGlow(d, new Vector3(0f, 2f, 0f), 0.3f, m.dreamGlow, m.dream, withLight ? 5f : 0f);
            var halo = Group("Halo", d);
            halo.transform.localPosition = new Vector3(0f, 2f, 0f);
            var spin = halo.AddComponent<DreamFloat>();
            spin.bobHeight = 0.1f;
            spin.bobSpeed = 0.3f;
            spin.spin = new Vector3(20f, 45f, 0f);
            var ring = MeshObject("Ring", halo.transform, RingMesh(), m.gold);
            ring.transform.localScale = new Vector3(0.3f, 0.3f, 3f);
            Particles(d, "Motes", new Vector3(0f, 2f, 0f), new Vector3(0.6f, 0.6f, 0.6f), 4f, m.dream, Color.white, 0.02f, 0.04f, m.sparkle);
        }

        // =====================================================================
        // 1. Haunted Forest — escape the stalker, find the Token of Fear
        // =====================================================================

        static void BuildHauntedForest(World world)
        {
            var ctx = SetupWorld(world, Vector3.zero, 0f, true);
            var root = ctx.root;

            var groundMat = Lit("Forest_Ground_Dream", new Color(0.42f, 0.72f, 0.42f), 0.2f, new Color(0.04f, 0.08f, 0.04f));
            var barkMat = Lit("Forest_Bark_Dream", new Color(0.5f, 0.34f, 0.25f), 0.1f, new Color(0.04f, 0.02f, 0.01f));
            var stoneMat = Lit("Forest_Stone_Dream", new Color(0.82f, 0.8f, 0.9f), 0.4f, new Color(0.1f, 0.08f, 0.14f));
            var leavesA = Lit("Forest_Leaves_A", new Color(0.45f, 0.85f, 0.5f), 0.2f, new Color(0.05f, 0.12f, 0.05f));
            var leavesB = Lit("Forest_Leaves_B", new Color(0.65f, 0.92f, 0.4f), 0.2f, new Color(0.06f, 0.1f, 0.03f));
            var blossom = Lit("Forest_Blossom", new Color(1f, 0.72f, 0.86f), 0.2f, new Color(0.2f, 0.08f, 0.12f));
            var eyesMat = Lit("Forest_Eyes", new Color(1f, 0.1f, 0.05f), 0.5f, new Color(3f, 0.25f, 0.1f));
            var shadowMat = Lit("Forest_Shadow", new Color(0.01f, 0.01f, 0.015f), 0f, Color.black);
            var wispMat = Lit("Forest_Wisp", new Color(0.6f, 1f, 1f), 0.5f, new Color(0.8f, 1.6f, 1.8f));
            var tokenMat = Lit("Forest_TokenOfFear", new Color(0.35f, 0.05f, 0.5f), 0.9f, new Color(1.2f, 0.2f, 1.6f));
            var flowerMats = new[]
            {
                Lit("Forest_Flower_Pink", new Color(1f, 0.6f, 0.8f), 0.3f, new Color(0.3f, 0.1f, 0.2f)),
                Lit("Forest_Flower_Yellow", new Color(1f, 0.92f, 0.45f), 0.3f, new Color(0.3f, 0.25f, 0.05f)),
                Lit("Forest_Flower_Blue", new Color(0.55f, 0.75f, 1f), 0.3f, new Color(0.1f, 0.15f, 0.3f)),
                Lit("Forest_Flower_White", new Color(1f, 1f, 1f), 0.3f, new Color(0.25f, 0.25f, 0.25f)),
            };

            var nightmare = ctx.nightmare;
            nightmare.tokenName = "Token of Fear";
            nightmare.introMessage = "You are lost in the Haunted Forest...\nSomething is following you.\nFind the Token of Fear!";
            nightmare.dreamMessage = "Your fear melts away.\nThe forest dreams again.\nA portal home now waits where you started.";
            var atmosphere = new Atmosphere
            {
                sky = Sky("Forest_Sky_Dream", new Color(0.3f, 0.55f, 0.95f), new Color(1f, 0.82f, 0.9f),
                    new Color(0.6f, 0.8f, 0.6f), new Color(1f, 0.9f, 1f), 80f),
                fogColor = new Color(0.95f, 0.85f, 0.92f),
                fogDensity = 0.018f,
                ambientSky = new Color(0.6f, 0.65f, 0.85f),
                ambientEquator = new Color(0.7f, 0.65f, 0.6f),
                ambientGround = new Color(0.3f, 0.35f, 0.25f),
                sunColor = new Color(1f, 0.93f, 0.8f),
                sunIntensity = 1.2f,
            };
            ApplyAtmosphere(atmosphere, ctx.sun);
            ctx.vision.nightmare = NightmareLook(new Color(0.32f, 0.3f, 0.5f));


            var tokenPos = new Vector3(-26f, 0f, 56f);
            var stalkerStart = new Vector3(0f, 0f, -16f);
            UseTerrain(world.scene, 2f, 78f, 9f, (Vector3.zero, 7f), (tokenPos, 5.5f));
            var groundGo = Ground(root, groundMat, 230f);
            Boundary(root, 80f, 40);
            EdgeDressing(root, 82f, 46, stoneMat, new[] { leavesA, leavesB }, false, null, 3);

            // --- Trees with full, blossoming canopies.
            var trees = Group("Trees", root);
            var canopies = Group("Canopies", root);
            var occupied = new List<Vector3>();
            foreach (var p in Scatter(380, 3.5f, 78f, 2.4f, occupied, (Vector3.zero, 4.5f), (tokenPos, 5.5f)))
            {
                float height = Random.Range(4.5f, 8f);
                float radius = Random.Range(0.22f, 0.45f);
                float yaw = Random.Range(0f, 360f);

                var trunk = CreatePrimitive(PrimitiveType.Cylinder, "Tree", trees.transform, barkMat, true);
                trunk.transform.localPosition = p + Vector3.up * height * 0.5f;
                trunk.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                trunk.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);

                var canopy = Group("Canopy", canopies.transform);
                canopy.transform.localPosition = p + Vector3.up * height;
                var leafMat = Random.value < 0.3f ? blossom : (Random.value < 0.5f ? leavesA : leavesB);
                int puffs = Random.Range(3, 6);
                for (int c = 0; c < puffs; c++)
                {
                    var puff = CreatePrimitive(PrimitiveType.Sphere, "Leaves", canopy.transform, leafMat, false);
                    float s = Random.Range(height * 0.3f, height * 0.45f);
                    puff.transform.localPosition = new Vector3(Random.Range(-1f, 1f), Random.Range(-0.6f, 0.6f), Random.Range(-1f, 1f));
                    puff.transform.localScale = new Vector3(s, s * 0.8f, s);
                }
            }

            // --- The clearing of standing stones where the Token of Fear waits.
            var clearing = Group("Clearing of Fear", root);
            clearing.transform.localPosition = tokenPos;
            for (int i = 0; i < 7; i++)
            {
                var rot = Quaternion.Euler(0f, i * 360f / 7f, 0f);
                float h = Random.Range(1.6f, 2.6f);
                var stone = Box("Standing Stone", clearing.transform, stoneMat, rot * new Vector3(0f, h * 0.5f, 3.4f), new Vector3(0.7f, h, 0.4f));
                stone.transform.localRotation = rot * Quaternion.Euler(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
            }
            var altar = CreatePrimitive(PrimitiveType.Cylinder, "Altar", clearing.transform, stoneMat, true);
            altar.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            altar.transform.localScale = new Vector3(1f, 0.45f, 1f);
            UseMeshCollider(altar);

            var token = CreateToken(clearing.transform, "Token of Fear", new Vector3(0f, 1.45f, 0f), new Color(0.7f, 0.2f, 1f),
                "The Token of Fear pulses nearby...\nReach out and grab it!", out var tokenVisual);
            var gem = MeshObject("Crystal", tokenVisual, SaveMesh(CreateCrystal(0.12f, 0.36f), "TokenCrystal"), tokenMat);
            gem.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Particles(clearing.transform, "Fear Aura", new Vector3(0f, 1.4f, 0f), new Vector3(1.2f, 1f, 1.2f), 12f,
                new Color(0.7f, 0.2f, 1f), new Color(1f, 0.3f, 0.6f), 0.03f, 0.08f, ctx.sparkle);
            token.hintDistance = 7f;

            // --- Will-o'-wisps: a faint trail of lights leading toward the clearing.
            var wisps = Group("Wisps", root);
            for (int i = 0; i < 12; i++)
            {
                float t = (i + 1) / 13f;
                var along = Vector3.Lerp(Vector3.zero, tokenPos, t);
                var side = Vector3.Cross(Vector3.up, tokenPos.normalized) * Mathf.Sin(t * Mathf.PI * 3f) * 2.5f;
                var wisp = CreatePrimitive(PrimitiveType.Sphere, $"Wisp {i + 1}", wisps.transform, wispMat, false);
                wisp.transform.localPosition = OnGround(along + side) + Vector3.up * Random.Range(1.5f, 2.1f);
                wisp.transform.localScale = Vector3.one * 0.12f;
                PointLight(wisp.transform, "Light", Vector3.zero, new Color(0.5f, 1f, 1f), 4f, 1.2f);
                var drift = wisp.AddComponent<DreamFloat>();
                drift.bobHeight = 0.2f;
                drift.bobSpeed = 0.25f;
            }

            // --- The stalker.
            var stalker = Group("The Stalker", root);
            stalker.transform.localPosition = OnGround(stalkerStart);
            var body = Group("Body", stalker.transform);
            var hover = body.AddComponent<DreamFloat>();
            hover.bobHeight = 0.08f;
            hover.bobSpeed = 0.3f;
            hover.spin = Vector3.zero;

            var torso = CreatePrimitive(PrimitiveType.Capsule, "Torso", body.transform, shadowMat, false);
            torso.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            torso.transform.localScale = new Vector3(0.8f, 1.15f, 0.55f);
            var head = CreatePrimitive(PrimitiveType.Sphere, "Head", body.transform, shadowMat, false);
            head.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            head.transform.localScale = new Vector3(0.45f, 0.55f, 0.45f);
            for (int side = -1; side <= 1; side += 2)
            {
                var arm = CreatePrimitive(PrimitiveType.Capsule, "Arm", body.transform, shadowMat, false);
                arm.transform.localPosition = new Vector3(side * 0.5f, 1.4f, 0.1f);
                arm.transform.localRotation = Quaternion.Euler(10f, 0f, side * 8f);
                arm.transform.localScale = new Vector3(0.15f, 0.8f, 0.15f);
                var eye = CreatePrimitive(PrimitiveType.Sphere, "Eye", body.transform, eyesMat, false);
                eye.transform.localPosition = new Vector3(side * 0.1f, 2.55f, 0.2f);
                eye.transform.localScale = Vector3.one * 0.07f;
            }
            var eyeLight = PointLight(body.transform, "Eye Glow", new Vector3(0f, 2.55f, 0.45f), new Color(1f, 0.1f, 0.05f), 3.5f, 1.5f);
            var trail = Particles(body.transform, "Shadow Trail", new Vector3(0f, 0.5f, 0f), new Vector3(0.8f, 0.6f, 0.8f), 15f,
                new Color(0f, 0f, 0f, 0.8f), new Color(0.05f, 0f, 0.05f, 0.6f), 0.4f, 0.9f, ctx.mist);
            var trailMain = trail.main;
            trailMain.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            trailMain.prewarm = false;
            var stalkerAI = stalker.AddComponent<ForestStalker>();
            stalkerAI.eyeLight = eyeLight;
            stalkerAI.ground = groundGo.GetComponent<Collider>();
            stalkerAI.goal = token.transform;

            var eyes = WatchingEyes(root, eyesMat, 30, 15f, 70f);

            // --- Forest life.
            var flowers = Group("Dream Flowers", root);
            foreach (var p in Scatter(450, 1.5f, 76f, 0.9f, null, (tokenPos, 1.5f)))
            {
                var flower = Group("Flower", flowers.transform);
                flower.transform.localPosition = p;
                var stem = CreatePrimitive(PrimitiveType.Cylinder, "Stem", flower.transform, leavesA, false);
                stem.transform.localPosition = new Vector3(0f, 0.2f, 0f);
                stem.transform.localScale = new Vector3(0.03f, 0.2f, 0.03f);
                var bloom = CreatePrimitive(PrimitiveType.Sphere, "Bloom", flower.transform, flowerMats[Random.Range(0, flowerMats.Length)], false);
                bloom.transform.localPosition = new Vector3(0f, 0.42f, 0f);
                bloom.transform.localScale = new Vector3(1f, 0.6f, 1f) * Random.Range(0.12f, 0.2f);
                bloom.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            var fireflies = Particles(root, "Fireflies", new Vector3(0f, 1.5f, 0f), new Vector3(90f, 3f, 90f), 80f,
                new Color(1f, 0.95f, 0.5f), new Color(0.7f, 1f, 0.6f), 0.04f, 0.08f, ctx.sparkle);
            var home = CreateWayHome(root, new Vector3(0f, 0f, 4f), 180f, stoneMat);

            PlaceArtifacts(ctx, new Color(1f, 0.15f, 0.1f), new Color(1f, 0.85f, 0.5f), GroundRoute(new Vector3(0f, 0f, 7f), tokenPos, 7f),
                Scatter(12, 8f, 72f, 2.5f, occupied, (Vector3.zero, 6f), (tokenPos, 6f)));
            nightmare.nightmareOnly = new[] { wisps, stalker, eyes };
            nightmare.dreamOnly = new[] { home };
            foreach (var go in nightmare.dreamOnly)
                go.SetActive(false);
        }

        // =====================================================================
        // 2. Sunken Depths — survive without air, find the Magic Shell
        // =====================================================================

        static void BuildSunkenDepths(World world)
        {
            var ctx = SetupWorld(world, Vector3.zero, 0f, true);
            var root = ctx.root;
            ctx.sun.transform.rotation = Quaternion.Euler(75f, -20f, 0f);

            var sandMat = Lit("Ocean_Sand_Dream", new Color(0.95f, 0.88f, 0.7f), 0.15f, new Color(0.08f, 0.07f, 0.05f));
            var rockMat = Lit("Ocean_Rock_Dream", new Color(0.5f, 0.55f, 0.68f), 0.3f, new Color(0.04f, 0.05f, 0.08f));
            var kelpMat = Lit("Ocean_Kelp_Dream", new Color(0.3f, 0.85f, 0.45f), 0.3f, new Color(0.03f, 0.12f, 0.06f));
            var templeMat = Lit("Ocean_Temple_Dream", new Color(0.85f, 0.88f, 0.95f), 0.5f, new Color(0.1f, 0.12f, 0.16f));
            var wreckMat = Lit("Ocean_Wreck_Dream", new Color(0.5f, 0.35f, 0.25f), 0.2f, new Color(0.04f, 0.02f, 0.01f));
            var bubbleMat = Lit("Ocean_Bubble", new Color(0.75f, 0.95f, 1f), 1f, new Color(0.4f, 0.7f, 0.9f));
            var shellMat = Lit("Ocean_MagicShell", new Color(1f, 0.85f, 0.8f), 0.9f, new Color(1.2f, 0.7f, 0.9f));
            var clamMat = Lit("Ocean_Clam", new Color(0.35f, 0.3f, 0.45f), 0.6f, new Color(0.08f, 0.05f, 0.12f));
            var lureMat = Lit("Ocean_Lure", new Color(0.9f, 1f, 0.4f), 0.5f, new Color(1.8f, 2f, 0.5f));
            var jellyMat = Lit("Ocean_Jelly", new Color(0.8f, 0.5f, 1f), 0.8f, new Color(0.8f, 0.4f, 1.2f));
            var coralMats = new[]
            {
                Lit("Ocean_Coral_Pink", new Color(1f, 0.45f, 0.6f), 0.3f, new Color(0.35f, 0.08f, 0.15f)),
                Lit("Ocean_Coral_Orange", new Color(1f, 0.6f, 0.3f), 0.3f, new Color(0.35f, 0.15f, 0.05f)),
                Lit("Ocean_Coral_Purple", new Color(0.7f, 0.45f, 1f), 0.3f, new Color(0.2f, 0.1f, 0.35f)),
                Lit("Ocean_Coral_Teal", new Color(0.3f, 1f, 0.85f), 0.3f, new Color(0.05f, 0.3f, 0.25f)),
            };
            var fishMats = new[]
            {
                Lit("Ocean_Fish_Gold", new Color(1f, 0.8f, 0.2f), 0.7f, new Color(0.2f, 0.15f, 0.02f)),
                Lit("Ocean_Fish_Blue", new Color(0.3f, 0.6f, 1f), 0.7f, new Color(0.05f, 0.1f, 0.25f)),
                Lit("Ocean_Fish_Coral", new Color(1f, 0.5f, 0.45f), 0.7f, new Color(0.2f, 0.08f, 0.06f)),
            };

            var nightmare = ctx.nightmare;
            nightmare.tokenName = "Magic Shell";
            nightmare.introMessage = "You are sinking in the Sunken Depths...\nYou can't breathe!\nSwim: pull your arms back, or push the stick where you look.\nFind the Magic Shell. Touch air bubbles to catch your breath.";
            nightmare.dreamMessage = "The shell sings and the water fills with light.\nYou can breathe!\nA portal home now waits where you started.";
            var atmosphere = new Atmosphere
            {
                sky = Sky("Ocean_Sky_Dream", new Color(0.55f, 0.9f, 1f), new Color(0.2f, 0.6f, 0.8f),
                    new Color(0.05f, 0.3f, 0.45f), new Color(0.8f, 1f, 1f), 0f),
                fogColor = new Color(0.25f, 0.65f, 0.8f),
                fogDensity = 0.03f,
                ambientSky = new Color(0.5f, 0.8f, 0.95f),
                ambientEquator = new Color(0.35f, 0.65f, 0.75f),
                ambientGround = new Color(0.3f, 0.35f, 0.35f),
                sunColor = new Color(0.85f, 1f, 1f),
                sunIntensity = 1.1f,
            };
            ApplyAtmosphere(atmosphere, ctx.sun);
            ctx.vision.nightmare = NightmareLook(new Color(0.22f, 0.38f, 0.5f));

            var breath = nightmare.gameObject.AddComponent<UnderwaterBreath>();
            breath.maxAir = 75f;

            // Swimming instead of walking, with bubbles streaming off the swimmer.
            var swimming = nightmare.gameObject.AddComponent<DreamSwimming>();
            var swimBubbles = Particles(nightmare.transform, "Swim Bubbles", Vector3.zero, new Vector3(0.25f, 0.25f, 0.05f), 4f,
                new Color(0.85f, 0.97f, 1f), new Color(0.6f, 0.9f, 1f), 0.02f, 0.06f, ctx.sparkle, 0.3f, 0.7f);
            var bubbleMain = swimBubbles.main;
            bubbleMain.prewarm = false;
            bubbleMain.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            bubbleMain.maxParticles = 150;
            var bubbleShape = swimBubbles.shape;
            bubbleShape.rotation = new Vector3(-90f, 0f, 0f);
            swimming.bubbles = swimBubbles;

            // Once the nightmare lifts, the dream filter shows the world through clear blue-green water.
            ctx.vision.dream = new DreamVision.Look
            {
                tint = new Color(0.82f, 0.95f, 1f),
                edgeTint = new Color(0.55f, 0.82f, 0.95f),
                glow = new Color(0.45f, 0.9f, 1f, 0.12f),
                vignetteStart = 0.2f,
                vignetteEnd = 0.9f,
                pulse = 0.15f,
            };


            var templePos = new Vector3(36f, 0f, 44f);
            var wreckPos = new Vector3(-30f, 0f, 14f);
            UseTerrain(world.scene, 2f, 78f, 7f, (Vector3.zero, 7f), (templePos, 6.5f), (wreckPos, 6.5f));
            Ground(root, sandMat, 230f);
            Boundary(root, 80f, 40);
            EdgeDressing(root, 82f, 40, rockMat, coralMats, false, null, 2);
            var occupied = new List<Vector3>();

            // --- Rocks and kelp.
            var rocks = Group("Rocks", root);
            foreach (var p in Scatter(160, 5f, 74f, 3f, occupied, (Vector3.zero, 4f), (templePos, 7f), (wreckPos, 6f)))
            {
                var rock = CreatePrimitive(PrimitiveType.Sphere, "Rock", rocks.transform, rockMat, true);
                var s = new Vector3(Random.Range(0.8f, 2.5f), Random.Range(0.5f, 1.5f), Random.Range(0.8f, 2.5f));
                rock.transform.localPosition = p + Vector3.up * s.y * 0.3f;
                rock.transform.localRotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
                rock.transform.localScale = s;
                UseMeshCollider(rock);
            }

            var kelpForest = Group("Kelp", root);
            foreach (var p in Scatter(400, 3f, 74f, 1.2f, occupied, (Vector3.zero, 3f), (templePos, 6f), (wreckPos, 5f)))
            {
                var strand = Group("Kelp Strand", kelpForest.transform);
                strand.transform.localPosition = p;
                strand.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                var sway = strand.AddComponent<DreamSway>();
                sway.angle = Random.Range(6f, 12f);
                sway.speed = Random.Range(0.15f, 0.3f);

                float h = Random.Range(2f, 6f);
                Box("Stalk", strand.transform, kelpMat, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.15f, h, 0.04f), false);
                int leaves = Random.Range(2, 5);
                for (int l = 0; l < leaves; l++)
                {
                    var leaf = Box("Leaf", strand.transform, kelpMat, new Vector3(0f, Random.Range(h * 0.3f, h * 0.9f), 0f), new Vector3(0.35f, 0.8f, 0.03f), false);
                    leaf.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 180f), (l % 2 == 0 ? 1f : -1f) * 35f);
                }
            }

            // --- A sunken shipwreck landmark.
            var wreck = Group("Shipwreck", root);
            wreck.transform.localPosition = wreckPos;
            wreck.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
            var hull = Box("Hull", wreck.transform, wreckMat, new Vector3(0f, 0.9f, 0f), new Vector3(3.2f, 2f, 9f));
            hull.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            var deck = Box("Broken Deck", wreck.transform, wreckMat, new Vector3(0.6f, 2f, -2f), new Vector3(2.6f, 0.15f, 4f));
            deck.transform.localRotation = Quaternion.Euler(8f, 0f, 22f);
            var mast = CreatePrimitive(PrimitiveType.Cylinder, "Mast", wreck.transform, wreckMat, true);
            mast.transform.localPosition = new Vector3(1.6f, 3f, 1f);
            mast.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            mast.transform.localScale = new Vector3(0.25f, 3f, 0.25f);

            // --- The sunken temple holding the Magic Shell.
            var temple = Group("Sunken Temple", root);
            temple.transform.localPosition = templePos;
            var platform = CreatePrimitive(PrimitiveType.Cylinder, "Platform", temple.transform, templeMat, true);
            platform.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            platform.transform.localScale = new Vector3(10f, 0.15f, 10f);
            UseMeshCollider(platform);
            for (int i = 0; i < 8; i++)
            {
                var rot = Quaternion.Euler(0f, i * 45f, 0f);
                bool fallen = i == 3 || i == 6;
                float h = fallen ? 3.5f : Random.Range(1.5f, 4.2f);
                var column = CreatePrimitive(PrimitiveType.Cylinder, "Column", temple.transform, templeMat, true);
                column.transform.localScale = new Vector3(0.7f, h * 0.5f, 0.7f);
                if (fallen)
                {
                    column.transform.localPosition = rot * new Vector3(1.2f, 0.65f, 5.6f);
                    column.transform.localRotation = rot * Quaternion.Euler(0f, 0f, 90f);
                }
                else
                {
                    column.transform.localPosition = rot * new Vector3(0f, 0.3f + h * 0.5f, 4.2f);
                    column.transform.localRotation = rot;
                }
            }
            Box("Altar", temple.transform, templeMat, new Vector3(0f, 0.8f, 0f), new Vector3(1.3f, 1f, 1.3f));

            var clamBottom = CreatePrimitive(PrimitiveType.Sphere, "Clam (Bottom)", temple.transform, clamMat, false);
            clamBottom.transform.localPosition = new Vector3(0f, 1.38f, 0f);
            clamBottom.transform.localScale = new Vector3(1.1f, 0.25f, 1.1f);
            var clamTop = CreatePrimitive(PrimitiveType.Sphere, "Clam (Top)", temple.transform, clamMat, false);
            clamTop.transform.localPosition = new Vector3(0f, 1.75f, -0.4f);
            clamTop.transform.localRotation = Quaternion.Euler(-55f, 0f, 0f);
            clamTop.transform.localScale = new Vector3(1.1f, 0.25f, 1.1f);

            var token = CreateToken(temple.transform, "Magic Shell", new Vector3(0f, 1.75f, 0.05f), new Color(1f, 0.7f, 0.85f),
                "The Magic Shell is singing softly nearby...\nReach out and take it!", out var shellVisual);
            token.hintDistance = 8f;
            // A spiral of shrinking spheres reads as a conch shell.
            for (int i = 0; i < 10; i++)
            {
                float a = i * 55f * Mathf.Deg2Rad;
                float r = 0.012f * (9 - i);
                var whorl = CreatePrimitive(PrimitiveType.Sphere, "Whorl", shellVisual, shellMat, false);
                whorl.transform.localPosition = new Vector3(Mathf.Cos(a) * r, (9 - i) * -0.012f + 0.05f, Mathf.Sin(a) * r);
                whorl.transform.localScale = Vector3.one * (0.04f + 0.018f * (9 - i));
                whorl.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            Particles(temple.transform, "Shell Glimmer", new Vector3(0f, 1.8f, 0f), new Vector3(1.2f, 1f, 1.2f), 12f,
                new Color(1f, 0.8f, 0.9f), new Color(0.8f, 0.9f, 1f), 0.03f, 0.07f, ctx.sparkle);

            // --- Air vents: rising bubbles with a pocket of air at head height. Some lead toward the temple.
            var vents = Group("Air Vents", root);
            var ventPoints = new List<Vector3>();
            for (int i = 1; i <= 5; i++)
                ventPoints.Add(OnGround(Vector3.Lerp(Vector3.zero, templePos, i / 6f) + new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f))));
            ventPoints.AddRange(Scatter(20, 8f, 70f, 9f, null, (Vector3.zero, 5f), (templePos, 7f)));
            foreach (var p in ventPoints)
            {
                var vent = Group("Air Vent", vents.transform);
                vent.transform.localPosition = p;
                for (int s = 0; s < 3; s++)
                {
                    var stone = CreatePrimitive(PrimitiveType.Sphere, "Vent Stone", vent.transform, rockMat, false);
                    stone.transform.localPosition = Quaternion.Euler(0f, s * 120f, 0f) * new Vector3(0f, 0.05f, 0.3f);
                    stone.transform.localScale = new Vector3(0.35f, 0.25f, 0.35f);
                }
                var stream = Particles(vent.transform, "Bubbles", new Vector3(0f, 0.1f, 0f), new Vector3(0.3f, 0.3f, 0.05f), 8f,
                    new Color(0.8f, 0.95f, 1f), new Color(0.6f, 0.85f, 1f), 0.04f, 0.12f, ctx.sparkle, 0.6f, 1f);
                var shape = stream.shape;
                shape.rotation = new Vector3(-90f, 0f, 0f);

                var pocket = Group("Air Pocket", vent.transform);
                pocket.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                var air = pocket.AddComponent<AirBubble>();
                var orb = CreatePrimitive(PrimitiveType.Sphere, "Bubble", pocket.transform, bubbleMat, false);
                orb.transform.localScale = Vector3.one * 0.35f;
                var orbFloat = orb.AddComponent<DreamFloat>();
                orbFloat.bobHeight = 0.08f;
                orbFloat.bobSpeed = 0.4f;
                PointLight(orb.transform, "Glow", Vector3.zero, new Color(0.5f, 0.9f, 1f), 3f, 1f);
                air.visual = orb;
            }

            Particles(root, "Marine Snow", new Vector3(0f, 3f, 0f), new Vector3(90f, 6f, 90f), 100f,
                new Color(0.8f, 0.9f, 0.9f, 0.6f), new Color(0.6f, 0.8f, 0.9f, 0.4f), 0.02f, 0.05f, ctx.sparkle);

            var eyes = WatchingEyes(root, lureMat, 24, 15f, 70f);

            // --- Reef life.
            var coral = Group("Coral Garden", root);
            foreach (var p in Scatter(200, 3f, 72f, 1.5f, null, (Vector3.zero, 3f), (templePos, 6f), (wreckPos, 5f)))
            {
                var cluster = Group("Coral", coral.transform);
                cluster.transform.localPosition = p;
                int pieces = Random.Range(3, 7);
                for (int c = 0; c < pieces; c++)
                {
                    var mat = coralMats[Random.Range(0, coralMats.Length)];
                    var offset = new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f));
                    if (Random.value < 0.6f)
                    {
                        float len = Random.Range(0.3f, 0.8f);
                        var rot = Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(0f, 360f), Random.Range(-30f, 30f));
                        var branch = CreatePrimitive(PrimitiveType.Cylinder, "Branch Coral", cluster.transform, mat, false);
                        branch.transform.localRotation = rot;
                        branch.transform.localPosition = offset + rot * Vector3.up * len * 0.5f;
                        branch.transform.localScale = new Vector3(0.08f, len * 0.5f, 0.08f);
                    }
                    else
                    {
                        float s = Random.Range(0.3f, 0.6f);
                        var brain = CreatePrimitive(PrimitiveType.Sphere, "Brain Coral", cluster.transform, mat, false);
                        brain.transform.localPosition = offset + Vector3.up * s * 0.25f;
                        brain.transform.localScale = new Vector3(s, s * 0.6f, s);
                    }
                }
            }

            var schools = Group("Fish Schools", root);
            for (int i = 0; i < 16; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(5f, 55f);
                var school = Group("School", schools.transform);
                school.transform.localPosition = new Vector3(Mathf.Cos(a) * dist, Random.Range(2f, 5f), Mathf.Sin(a) * dist);
                var swim = school.AddComponent<DreamFloat>();
                swim.bobHeight = 0.3f;
                swim.bobSpeed = 0.1f;
                float dir = Random.value < 0.5f ? 1f : -1f;
                swim.spin = new Vector3(0f, dir * Random.Range(20f, 35f), 0f);

                var mat = fishMats[Random.Range(0, fishMats.Length)];
                int count = Random.Range(6, 11);
                for (int f = 0; f < count; f++)
                {
                    float fa = Random.Range(0f, 360f);
                    float fr = Random.Range(2f, 4f);
                    var pos = Quaternion.Euler(0f, fa, 0f) * Vector3.forward * fr + Vector3.up * Random.Range(-0.5f, 0.5f);
                    // Swim direction is tangent to the circle, matching the school's spin.
                    var tangent = Vector3.Cross(Vector3.up, pos.normalized) * dir;
                    var fish = Group("Fish", school.transform);
                    fish.transform.localPosition = pos;
                    fish.transform.localRotation = Quaternion.LookRotation(tangent, Vector3.up);
                    var fishBody = CreatePrimitive(PrimitiveType.Capsule, "Body", fish.transform, mat, false);
                    fishBody.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    fishBody.transform.localScale = new Vector3(0.12f, 0.15f, 0.08f);
                    Box("Tail", fish.transform, mat, new Vector3(0f, 0f, -0.18f), new Vector3(0.02f, 0.12f, 0.08f), false);
                }
            }

            var jellies = Group("Jellyfish", root);
            foreach (var p in Scatter(24, 4f, 60f, 4f, null))
            {
                var jelly = Group("Jellyfish", jellies.transform);
                jelly.transform.localPosition = p + Vector3.up * Random.Range(2.5f, 5f);
                var drift = jelly.AddComponent<DreamFloat>();
                drift.bobHeight = 0.5f;
                drift.bobSpeed = Random.Range(0.08f, 0.15f);
                drift.spin = new Vector3(0f, 10f, 0f);
                var bell = CreatePrimitive(PrimitiveType.Sphere, "Bell", jelly.transform, jellyMat, false);
                bell.transform.localScale = new Vector3(0.5f, 0.35f, 0.5f);
                for (int t = 0; t < 4; t++)
                {
                    var tentacle = CreatePrimitive(PrimitiveType.Cylinder, "Tentacle", jelly.transform, jellyMat, false);
                    tentacle.transform.localPosition = Quaternion.Euler(0f, t * 90f, 0f) * new Vector3(0f, -0.45f, 0.12f);
                    tentacle.transform.localScale = new Vector3(0.02f, 0.3f, 0.02f);
                }
            }

            var home = CreateWayHome(root, new Vector3(0f, 0f, 4f), 180f, templeMat);

            PlaceArtifacts(ctx, new Color(0.2f, 1f, 0.6f), new Color(0.6f, 0.95f, 1f), GroundRoute(new Vector3(0f, 0f, 7f), templePos, 7f),
                Scatter(12, 8f, 70f, 3f, occupied, (Vector3.zero, 6f), (templePos, 8f), (wreckPos, 7f)));
            // --- Nightmare: ocean currents that drag swimmers off course, shown as streaming motes.
            var currents = Group("Currents", root);
            foreach (var p in Scatter(7, 12f, 64f, 14f, null, (Vector3.zero, 9f), (templePos, 9f)))
            {
                var current = Group("Current", currents.transform);
                current.transform.localPosition = p + Vector3.up * 2.5f;
                current.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                current.AddComponent<DreamCurrent>();
                var stream = Particles(current.transform, "Stream", new Vector3(0f, 0f, -6f), new Vector3(8f, 3f, 0.5f), 18f,
                    new Color(0.7f, 0.9f, 1f, 0.5f), new Color(0.5f, 0.8f, 1f, 0.3f), 0.03f, 0.08f, ctx.sparkle, 2f, 3.5f);
                var streamMain = stream.main;
                streamMain.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            }

            nightmare.nightmareOnly = new[] { eyes, currents };
            nightmare.dreamOnly = new[] { home };
            foreach (var go in nightmare.dreamOnly)
                go.SetActive(false);
        }

        // =====================================================================
        // 3. Candy Forest — avoid the sour slime, gather Sugar Stars, find the Golden Lollipop
        // =====================================================================

        static void BuildCandyForest(World world)
        {
            var ctx = SetupWorld(world, Vector3.zero, 0f, true);
            var root = ctx.root;

            var frosting = Lit("Candy_Frosting", new Color(1f, 0.8f, 0.88f), 0.5f, new Color(0.12f, 0.05f, 0.08f));
            var stick = Lit("Candy_Stick", new Color(1f, 0.97f, 0.92f), 0.6f, new Color(0.1f, 0.1f, 0.1f));
            var gingerbread = Lit("Candy_Gingerbread", new Color(0.72f, 0.42f, 0.2f), 0.2f, new Color(0.06f, 0.03f, 0.01f));
            var icing = Lit("Candy_Icing", Color.white, 0.7f, new Color(0.2f, 0.2f, 0.22f));
            var stripe = Lit("Candy_Stripe", new Color(1f, 0.15f, 0.2f), 0.7f, new Color(0.25f, 0.02f, 0.04f));
            var candyMats = new[]
            {
                Lit("Candy_Pink", new Color(1f, 0.45f, 0.7f), 0.8f, new Color(0.3f, 0.08f, 0.18f)),
                Lit("Candy_Mint", new Color(0.45f, 1f, 0.75f), 0.8f, new Color(0.08f, 0.3f, 0.18f)),
                Lit("Candy_Lemon", new Color(1f, 0.9f, 0.35f), 0.8f, new Color(0.3f, 0.25f, 0.05f)),
                Lit("Candy_Grape", new Color(0.7f, 0.45f, 1f), 0.8f, new Color(0.18f, 0.08f, 0.3f)),
                Lit("Candy_Orange", new Color(1f, 0.6f, 0.25f), 0.8f, new Color(0.3f, 0.14f, 0.04f)),
            };
            var slimeMat = Lit("Candy_SourSlime", new Color(0.4f, 0.9f, 0.1f), 0.9f, new Color(0.5f, 1.2f, 0.1f));
            var eyesMat = Lit("Candy_Eyes", new Color(0.7f, 1f, 0.2f), 0.5f, new Color(1.2f, 2f, 0.2f));
            var starMat = Lit("Candy_SugarStar", new Color(1f, 0.95f, 0.6f), 0.9f, new Color(1.8f, 1.5f, 0.6f));
            var goldMat = Lit("Candy_GoldenLollipop", new Color(1f, 0.8f, 0.3f), 0.95f, new Color(1.6f, 1.1f, 0.3f));
            var cottonMat = Lit("Candy_CottonCandy", new Color(1f, 0.8f, 0.95f), 0.1f, new Color(0.35f, 0.25f, 0.35f));

            var nightmare = ctx.nightmare;
            nightmare.tokenName = "Golden Lollipop";
            nightmare.introMessage = "The Candy Forest has gone sour...\nDon't step in the bubbling slime!\nCollect the Sugar Stars to wake the Golden Lollipop.";
            nightmare.dreamMessage = "Sweetness floods back into the forest.\nEverything is delicious again!\nA portal home now waits where you started.";
            var atmosphere = new Atmosphere
            {
                sky = Sky("Candy_Sky_Dream", new Color(0.45f, 0.7f, 1f), new Color(1f, 0.75f, 0.88f),
                    new Color(1f, 0.85f, 0.7f), new Color(1f, 0.6f, 0.9f), 30f),
                fogColor = new Color(1f, 0.85f, 0.92f),
                fogDensity = 0.016f,
                ambientSky = new Color(0.8f, 0.7f, 0.85f),
                ambientEquator = new Color(0.9f, 0.7f, 0.75f),
                ambientGround = new Color(0.5f, 0.4f, 0.4f),
                sunColor = new Color(1f, 0.95f, 0.88f),
                sunIntensity = 1.2f,
            };
            ApplyAtmosphere(atmosphere, ctx.sun);
            ctx.vision.nightmare = NightmareLook(new Color(0.45f, 0.48f, 0.26f));


            var housePos = new Vector3(10f, 0f, 58f);
            var tokenPos = housePos + new Vector3(0f, 0f, -5f);
            var homePos = new Vector3(0f, 0f, 4f);
            UseTerrain(world.scene, 2f, 78f, 8f, (Vector3.zero, 7f), (housePos, 7.5f), (tokenPos, 3f));
            Ground(root, frosting, 230f);
            Boundary(root, 80f, 40);
            EdgeDressing(root, 82f, 44, icing, candyMats, false, null, 0);
            var occupied = new List<Vector3>();
            var starPoints = Scatter(5, 18f, 70f, 20f, occupied, (Vector3.zero, 6f), (housePos, 8f));

            // --- Lollipop trees: a stick with a big swirled disc of candy.
            var ring = RingMesh();
            var trees = Group("Lollipop Trees", root);
            foreach (var p in Scatter(200, 4.5f, 78f, 3f, occupied, (Vector3.zero, 4.5f), (housePos, 7f), (tokenPos, 3f)))
            {
                var tree = Group("Lollipop Tree", trees.transform);
                tree.transform.localPosition = p;
                tree.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                float h = Random.Range(2.5f, 5f);
                float d = Random.Range(1.4f, 2.4f);

                var trunk = CreatePrimitive(PrimitiveType.Cylinder, "Stick", tree.transform, stick, true);
                trunk.transform.localPosition = Vector3.up * h * 0.5f;
                trunk.transform.localScale = new Vector3(0.14f, h * 0.5f, 0.14f);

                var candy = CreatePrimitive(PrimitiveType.Cylinder, "Candy", tree.transform, candyMats[Random.Range(0, candyMats.Length)], false);
                candy.transform.localPosition = Vector3.up * (h + d * 0.45f);
                candy.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                candy.transform.localScale = new Vector3(d, 0.06f, d);
                foreach (float r in new[] { 0.18f, 0.32f })
                {
                    var swirl = MeshObject("Swirl", tree.transform, ring, icing);
                    swirl.transform.localPosition = candy.transform.localPosition;
                    swirl.transform.localScale = new Vector3(d * r, d * r, 4.5f);
                }
            }

            // --- Candy canes.
            var canes = Group("Candy Canes", root);
            foreach (var p in Scatter(70, 5f, 76f, 3f, occupied, (Vector3.zero, 5f), (housePos, 7f), (tokenPos, 3f)))
            {
                var cane = Group("Candy Cane", canes.transform);
                cane.transform.localPosition = p;
                cane.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                float h = Random.Range(2f, 3.2f);

                var shaft = CreatePrimitive(PrimitiveType.Cylinder, "Shaft", cane.transform, stick, true);
                shaft.transform.localPosition = Vector3.up * h * 0.5f;
                shaft.transform.localScale = new Vector3(0.22f, h * 0.5f, 0.22f);
                for (float y = 0.3f; y < h; y += 0.45f)
                {
                    var band = CreatePrimitive(PrimitiveType.Cylinder, "Stripe", cane.transform, stripe, false);
                    band.transform.localPosition = Vector3.up * y;
                    band.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
                    band.transform.localScale = new Vector3(0.235f, 0.06f, 0.235f);
                }

                // The hook: short segments around a half circle.
                const float hookRadius = 0.4f;
                for (int s = 0; s <= 6; s++)
                {
                    float a = s / 6f * Mathf.PI;
                    var seg = CreatePrimitive(PrimitiveType.Cylinder, "Hook", cane.transform, s % 2 == 0 ? stick : stripe, false);
                    seg.transform.localPosition = new Vector3(hookRadius - Mathf.Cos(a) * hookRadius, h + Mathf.Sin(a) * hookRadius, 0f);
                    seg.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg);
                    seg.transform.localScale = new Vector3(0.22f, 0.17f, 0.22f);
                }
            }

            var gumdrops = Group("Gumdrops", root);
            foreach (var p in Scatter(230, 2f, 78f, 1.2f, null, (housePos, 6f), (tokenPos, 2f), (homePos, 2f)))
            {
                float s = Random.Range(0.3f, 0.7f);
                var drop = CreatePrimitive(PrimitiveType.Sphere, "Gumdrop", gumdrops.transform, candyMats[Random.Range(0, candyMats.Length)], false);
                drop.transform.localPosition = p;
                drop.transform.localScale = new Vector3(s, s * 1.1f, s);
            }

            // --- The gingerbread house, front door facing the spawn (-Z).
            var house = Group("Gingerbread House", root);
            house.transform.localPosition = housePos;
            Box("Walls", house.transform, gingerbread, new Vector3(0f, 1.75f, 0f), new Vector3(6f, 3.5f, 5f));
            Box("Door", house.transform, stripe, new Vector3(0f, 1.1f, -2.52f), new Vector3(1.3f, 2.2f, 0.06f), false);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Window", house.transform, candyMats[2], new Vector3(side * 1.8f, 2f, -2.52f), new Vector3(0.9f, 0.9f, 0.06f), false);
                var slab = Box("Roof", house.transform, icing, new Vector3(side * 1.5f, 4.55f, 0f), new Vector3(3.9f, 0.25f, 5.6f), false);
                slab.transform.localRotation = Quaternion.Euler(0f, 0f, -side * 35f);
            }
            Box("Chimney", house.transform, stripe, new Vector3(1.8f, 5.2f, 1.2f), new Vector3(0.6f, 1.4f, 0.6f), false);
            for (int i = 0; i < 6; i++)
            {
                var drop = CreatePrimitive(PrimitiveType.Sphere, "Roof Gumdrop", house.transform, candyMats[i % candyMats.Length], false);
                drop.transform.localPosition = new Vector3(0f, 5.65f, -2.4f + i * 0.96f);
                drop.transform.localScale = Vector3.one * 0.35f;
            }

            // --- The cake pedestal and the Golden Lollipop, which appears once every Sugar Star is found.
            var cake = Group("Cake Pedestal", root);
            cake.transform.localPosition = tokenPos;
            var tier = CreatePrimitive(PrimitiveType.Cylinder, "Tier", cake.transform, candyMats[0], true);
            tier.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            tier.transform.localScale = new Vector3(1.2f, 0.25f, 1.2f);
            var topTier = CreatePrimitive(PrimitiveType.Cylinder, "Top Tier", cake.transform, icing, false);
            topTier.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            topTier.transform.localScale = new Vector3(0.9f, 0.2f, 0.9f);

            var token = CreateToken(root, "Golden Lollipop", tokenPos + Vector3.up * 1.5f, new Color(1f, 0.8f, 0.3f),
                "The Golden Lollipop shimmers nearby...\nReach out and grab it!", out var lollipop);
            token.hintDistance = 7f;
            var lolliStick = CreatePrimitive(PrimitiveType.Cylinder, "Stick", lollipop, stick, false);
            lolliStick.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            lolliStick.transform.localScale = new Vector3(0.03f, 0.18f, 0.03f);
            var lolliCandy = CreatePrimitive(PrimitiveType.Cylinder, "Candy", lollipop, goldMat, false);
            lolliCandy.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            lolliCandy.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            lolliCandy.transform.localScale = new Vector3(0.3f, 0.03f, 0.3f);
            Particles(token.transform.parent, "Golden Glimmer", Vector3.zero, new Vector3(1f, 1f, 1f), 12f,
                new Color(1f, 0.85f, 0.4f), Color.white, 0.03f, 0.07f, ctx.sparkle);

            var starMesh = SaveMesh(CreateCrystal(0.16f, 0.22f), "SugarStar");
            var stars = Group("Sugar Stars", root);
            foreach (var p in starPoints)
                CreateCollectible(stars.transform, "Sugar Star", p + Vector3.up * 1.3f, starMesh, starMat, new Color(1f, 0.9f, 0.5f), ctx.sparkle);
            CollectionGoal(ctx, token, "SUGAR STARS", starPoints.Count, "A Sugar Star! Sweet!",
                "All the Sugar Stars glow together...\nThe Golden Lollipop has appeared by the gingerbread house!");

            // --- Nightmare: sour slime pools and watching eyes.
            const string slimeMessage = "You stepped in the sour slime...\nYou wake up where you started.";
            var slime = Group("Sour Slime", root);
            foreach (var p in Scatter(40, 6f, 74f, 4f, occupied, (Vector3.zero, 6f), (housePos, 7f), (tokenPos, 3f)))
            {
                float r = Random.Range(0.9f, 1.6f);
                var pool = Hazard(slime.transform, "Slime Pool", p, r + 0.1f, slimeMessage).gameObject;
                var creep = pool.AddComponent<DreamCreep>();
                creep.maxGrowth = 1.8f;
                creep.growTime = 200f;
                pool.transform.localRotation = AlongGround(p);
                var puddle = CreatePrimitive(PrimitiveType.Cylinder, "Puddle", pool.transform, slimeMat, false);
                puddle.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                puddle.transform.localScale = new Vector3(r * 2f, 0.02f, r * 2f);
                var bubbles = Particles(pool.transform, "Bubbles", Vector3.up * 0.1f, new Vector3(r * 1.4f, r * 1.4f, 0.1f), 5f,
                    new Color(0.6f, 1f, 0.2f), new Color(0.3f, 0.8f, 0.1f), 0.05f, 0.12f, ctx.sparkle, 0.1f, 0.3f);
                var shape = bubbles.shape;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                PointLight(pool.transform, "Glow", Vector3.up * 0.5f, new Color(0.5f, 1f, 0.2f), r * 3f, 0.8f);
            }

            var eyes = WatchingEyes(root, eyesMat, 28, 15f, 70f);

            // --- Cotton candy clouds and sprinkles.
            var clouds = Group("Cotton Candy Clouds", root);
            for (int i = 0; i < 36; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(5f, 70f);
                var cloud = Group("Cotton Candy", clouds.transform);
                cloud.transform.localPosition = new Vector3(Mathf.Cos(a) * dist, Random.Range(9f, 14f), Mathf.Sin(a) * dist);
                int puffs = Random.Range(4, 7);
                for (int c = 0; c < puffs; c++)
                {
                    var puff = CreatePrimitive(PrimitiveType.Sphere, "Puff", cloud.transform, cottonMat, false);
                    float s = Random.Range(1.2f, 2.2f);
                    puff.transform.localPosition = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-0.3f, 0.4f), Random.Range(-0.8f, 0.8f));
                    puff.transform.localScale = new Vector3(s * 1.2f, s * 0.8f, s);
                    puff.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                var drift = cloud.AddComponent<DreamFloat>();
                drift.bobHeight = 0.4f;
                drift.bobSpeed = Random.Range(0.04f, 0.08f);
                drift.spin = new Vector3(0f, Random.Range(-3f, 3f), 0f);
            }
            var sprinkles = Particles(root, "Sprinkles", new Vector3(0f, 2f, 0f), new Vector3(90f, 4f, 90f), 80f,
                new Color(1f, 0.5f, 0.8f), new Color(0.5f, 0.9f, 1f), 0.03f, 0.07f, ctx.sparkle);
            var home = CreateWayHome(root, homePos, 180f, icing);

            PlaceArtifacts(ctx, new Color(0.6f, 1f, 0.1f), new Color(1f, 0.6f, 0.85f), GroundRoute(new Vector3(0f, 0f, 7f), tokenPos, 7f),
                Scatter(12, 8f, 72f, 3f, occupied, (Vector3.zero, 6f), (housePos, 8f), (tokenPos, 4f)));
            nightmare.nightmareOnly = new[] { slime, eyes };
            nightmare.dreamOnly = new[] { home };
            foreach (var go in nightmare.dreamOnly)
                go.SetActive(false);
        }

        // =====================================================================
        // 4. Clockwork Moon — beat the midnight clock, dodge the gears, find the Moon Key
        // =====================================================================

        static void BuildClockworkMoon(World world)
        {
            var ctx = SetupWorld(world, Vector3.zero, 0f, true);
            var root = ctx.root;
            ctx.sun.transform.rotation = Quaternion.Euler(35f, -50f, 0f);

            var dust = Lit("Moon_Dust", new Color(0.78f, 0.76f, 0.9f), 0.15f, new Color(0.06f, 0.05f, 0.1f));
            var rock = Lit("Moon_Rock", new Color(0.6f, 0.58f, 0.72f), 0.2f, new Color(0.04f, 0.03f, 0.07f));
            var brass = Lit("Moon_Brass", new Color(1f, 0.78f, 0.35f), 0.85f, new Color(0.35f, 0.22f, 0.05f));
            var stone = Lit("Moon_Tower", new Color(0.95f, 0.92f, 1f), 0.5f, new Color(0.1f, 0.09f, 0.14f));
            var clockFace = Lit("Moon_ClockFace", new Color(1f, 0.97f, 0.85f), 0.6f, new Color(0.9f, 0.85f, 0.6f));
            var iron = Lit("Moon_Iron", new Color(0.08f, 0.08f, 0.1f), 0.7f, Color.black);
            var hotIron = Lit("Moon_HotIron", new Color(0.5f, 0.1f, 0.05f), 0.6f, new Color(1.6f, 0.3f, 0.05f));
            var eyesMat = Lit("Moon_Eyes", new Color(1f, 0.6f, 0.1f), 0.5f, new Color(2.5f, 1.2f, 0.1f));
            var keyMat = Lit("Moon_Key", new Color(0.85f, 0.9f, 1f), 0.95f, new Color(0.9f, 1.1f, 1.6f));
            var lanternMat = Lit("Moon_Lantern", new Color(1f, 0.9f, 0.6f), 0.5f, new Color(2f, 1.6f, 0.8f));
            var planetMat = Lit("Moon_Planet", new Color(0.3f, 0.55f, 1f), 0.6f, new Color(0.15f, 0.3f, 0.6f));

            var nightmare = ctx.nightmare;
            nightmare.tokenName = "Moon Key";
            nightmare.introMessage = "The Clockwork Moon is grinding toward midnight...\nDodge the sweeping gears.\nFind the Moon Key before the clock strikes twelve!";
            nightmare.dreamMessage = "The gears hum a lullaby.\nTime stands still in the moonlight.\nA portal home now waits where you started.";
            var atmosphere = new Atmosphere
            {
                sky = Sky("Moon_Sky_Dream", new Color(0.05f, 0.04f, 0.2f), new Color(0.45f, 0.35f, 0.75f),
                    new Color(0.2f, 0.15f, 0.4f), new Color(0.6f, 0.8f, 1f), 200f),
                fogColor = new Color(0.3f, 0.25f, 0.5f),
                fogDensity = 0.012f,
                ambientSky = new Color(0.55f, 0.5f, 0.8f),
                ambientEquator = new Color(0.5f, 0.45f, 0.65f),
                ambientGround = new Color(0.25f, 0.22f, 0.35f),
                sunColor = new Color(0.9f, 0.92f, 1f),
                sunIntensity = 1.1f,
            };
            ApplyAtmosphere(atmosphere, ctx.sun);
            ctx.vision.nightmare = NightmareLook(new Color(0.5f, 0.3f, 0.22f));

            var countdown = nightmare.gameObject.AddComponent<DreamCountdown>();
            countdown.seconds = 210f;


            var towerPos = new Vector3(0f, 0f, 62f);
            var keyPos = new Vector3(0f, 0f, 55f);
            var sweeperSpots = new[] { new Vector3(0f, 0f, 14f), new Vector3(-12f, 0f, 26f), new Vector3(12f, 0f, 26f), new Vector3(0f, 0f, 38f) };
            UseTerrain(world.scene, 2f, 78f, 10f, (Vector3.zero, 7f), (towerPos, 6f), (keyPos, 6f),
                (sweeperSpots[0], 6.5f), (sweeperSpots[1], 6f), (sweeperSpots[2], 6f), (sweeperSpots[3], 6.5f));
            Ground(root, dust, 230f);
            Boundary(root, 80f, 40);
            EdgeDressing(root, 82f, 50, rock, new[] { brass }, true, null, 2);
            var occupied = new List<Vector3>();

            // --- Craters and boulders, kept clear of the gear sweepers.
            var craters = Group("Craters", root);
            foreach (var p in Scatter(60, 6f, 76f, 7f, occupied, (Vector3.zero, 5f), (keyPos, 8f), (towerPos, 7f),
                         (sweeperSpots[0], 6f), (sweeperSpots[1], 6f), (sweeperSpots[2], 6f), (sweeperSpots[3], 6f)))
            {
                float r = Random.Range(1.5f, 4f);
                var crater = Group("Crater", craters.transform);
                crater.transform.localPosition = p;
                crater.transform.localRotation = AlongGround(p);
                var floor = CreatePrimitive(PrimitiveType.Cylinder, "Floor", crater.transform, rock, false);
                floor.transform.localPosition = new Vector3(0f, 0.01f, 0f);
                floor.transform.localScale = new Vector3(r * 2f, 0.01f, r * 2f);
                int stones = Mathf.RoundToInt(r * 4f);
                for (int i = 0; i < stones; i++)
                {
                    var rot = Quaternion.Euler(0f, i * 360f / stones, 0f);
                    var rim = CreatePrimitive(PrimitiveType.Sphere, "Rim", crater.transform, rock, false);
                    rim.transform.localPosition = rot * new Vector3(0f, 0.05f, r);
                    rim.transform.localRotation = rot;
                    rim.transform.localScale = new Vector3(r * 0.9f, 0.3f, 0.6f);
                }
            }

            var boulders = Group("Boulders", root);
            foreach (var p in Scatter(70, 6f, 76f, 4f, occupied, (Vector3.zero, 5f), (keyPos, 7f), (towerPos, 6f),
                         (sweeperSpots[0], 6f), (sweeperSpots[1], 6f), (sweeperSpots[2], 6f), (sweeperSpots[3], 6f)))
            {
                var boulder = CreatePrimitive(PrimitiveType.Sphere, "Boulder", boulders.transform, rock, true);
                var s = new Vector3(Random.Range(0.8f, 2.2f), Random.Range(0.6f, 1.4f), Random.Range(0.8f, 2.2f));
                boulder.transform.localPosition = p + Vector3.up * s.y * 0.3f;
                boulder.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                boulder.transform.localScale = s;
                UseMeshCollider(boulder);
            }

            // --- The clock tower, its face turned toward the spawn (-Z).
            var tower = Group("Clock Tower", root);
            tower.transform.localPosition = towerPos;
            var tick = tower.AddComponent<DreamSoundEmitter>();
            tick.sound = DreamSound.ClockTick;
            tick.volume = 0.6f;
            tick.maxDistance = 45f;
            Box("Base", tower.transform, stone, new Vector3(0f, 4f, 0f), new Vector3(6f, 8f, 6f));
            Box("Upper", tower.transform, stone, new Vector3(0f, 10f, 0f), new Vector3(4.5f, 4f, 4.5f));
            Box("Door", tower.transform, iron, new Vector3(0f, 1.5f, -3.02f), new Vector3(1.8f, 3f, 0.06f), false);
            var roof = Box("Roof", tower.transform, brass, new Vector3(0f, 12.8f, 0f), new Vector3(3.4f, 3.4f, 3.4f), false);
            roof.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            var spire = CreatePrimitive(PrimitiveType.Cylinder, "Spire", tower.transform, brass, false);
            spire.transform.localPosition = new Vector3(0f, 15.5f, 0f);
            spire.transform.localScale = new Vector3(0.12f, 1.2f, 0.12f);

            var face = CreatePrimitive(PrimitiveType.Cylinder, "Clock Face", tower.transform, clockFace, false);
            face.transform.localPosition = new Vector3(0f, 10f, -2.3f);
            face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            face.transform.localScale = new Vector3(3.6f, 0.05f, 3.6f);
            var faceRim = MeshObject("Face Rim", tower.transform, RingMesh(), brass);
            faceRim.transform.localPosition = new Vector3(0f, 10f, -2.33f);
            faceRim.transform.localScale = new Vector3(1.8f, 1.8f, 8f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                var mark = Box("Hour Mark", tower.transform, iron,
                    new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 1.5f, 10f + Mathf.Cos(a * Mathf.Deg2Rad) * 1.5f, -2.38f), new Vector3(0.1f, 0.28f, 0.04f), false);
                mark.transform.localRotation = Quaternion.Euler(0f, 0f, -a);
            }
            foreach (var (length, speed) in new[] { (0.9f, -0.5f), (1.35f, -6f) })
            {
                var pivot = Group("Clock Hand", tower.transform);
                pivot.transform.localPosition = new Vector3(0f, 10f, -2.42f);
                pivot.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                Box("Hand", pivot.transform, iron, new Vector3(0f, length * 0.5f, 0f), new Vector3(0.14f, length, 0.03f), false);
                var turn = pivot.AddComponent<DreamFloat>();
                turn.bobHeight = 0f;
                turn.spin = new Vector3(0f, 0f, speed);
            }

            // --- The Moon Key on a gear pedestal, guarded by a sweeper that circles it.
            var pedestal = CreateGear(root, "Key Pedestal", 0.6f, 1f, 12, brass);
            pedestal.transform.localPosition = keyPos + Vector3.up * 0.5f;
            var token = CreateToken(root, "Moon Key", keyPos + Vector3.up * 1.5f, new Color(0.7f, 0.85f, 1f),
                "The Moon Key is ringing softly nearby...\nTime your dash past the gears and grab it!", out var key);
            token.hintDistance = 8f;
            var keyRing = MeshObject("Bow", key, SaveMesh(CreateEllipticTorus(0.08f, 0.08f, 0.02f, 32, 8), "KeyRing"), keyMat);
            keyRing.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            Box("Shaft", key, keyMat, new Vector3(0f, -0.14f, 0f), new Vector3(0.03f, 0.3f, 0.03f), false);
            Box("Tooth", key, keyMat, new Vector3(0.04f, -0.27f, 0f), new Vector3(0.06f, 0.03f, 0.03f), false);
            Box("Tooth", key, keyMat, new Vector3(0.035f, -0.21f, 0f), new Vector3(0.05f, 0.03f, 0.03f), false);
            Particles(root, "Key Glimmer", keyPos + Vector3.up * 1.5f, new Vector3(1f, 1f, 1f), 12f,
                new Color(0.7f, 0.85f, 1f), Color.white, 0.03f, 0.07f, ctx.sparkle);

            const string gearMessage = "The gears swept you away...\nYou wake up where you started.";
            var sweepers = Group("Gear Sweepers", root);
            Sweeper(sweepers.transform, keyPos, 4.5f, 30f, 2, 1.3f, iron, hotIron, gearMessage);
            Sweeper(sweepers.transform, sweeperSpots[0], 5f, 22f, 2, 0f, iron, hotIron, gearMessage);
            Sweeper(sweepers.transform, sweeperSpots[1], 4.5f, -26f, 3, 0f, iron, hotIron, gearMessage);
            Sweeper(sweepers.transform, sweeperSpots[2], 4.5f, 26f, 3, 0f, iron, hotIron, gearMessage);
            Sweeper(sweepers.transform, sweeperSpots[3], 5f, -20f, 2, 0f, iron, hotIron, gearMessage);
            foreach (Transform sweeper in sweepers.transform)
            {
                var escalation = sweeper.gameObject.AddComponent<DreamEscalation>();
                escalation.goal = keyPos;
                escalation.followCountdown = true;
                var grind = sweeper.gameObject.AddComponent<DreamSoundEmitter>();
                grind.sound = DreamSound.GearGrind;
                grind.volume = 0.5f;
                grind.maxDistance = 12f;
            }

            // --- Great gears turning in the sky.
            var skyGears = Group("Sky Gears", root);
            for (int i = 0; i < 16; i++)
            {
                float a = (i * 22.5f + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
                float dist = Random.Range(40f, 72f);
                var flat = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var holder = Group("Sky Gear", skyGears.transform);
                holder.transform.localPosition = flat * dist + Vector3.up * Random.Range(10f, 24f);
                holder.transform.localRotation = Quaternion.LookRotation(-flat, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
                var gear = CreateGear(holder.transform, "Gear", Random.Range(2f, 4.5f), 0.5f, Random.Range(12, 20), brass);
                var turn = gear.AddComponent<DreamFloat>();
                turn.bobHeight = 0f;
                turn.spin = new Vector3(0f, (i % 2 == 0 ? 1f : -1f) * Random.Range(8f, 15f), 0f);
            }

            var eyes = WatchingEyes(root, eyesMat, 20, 15f, 70f);

            // --- Lanterns, stardust and a blue planet rising.
            var lanterns = Group("Moon Lanterns", root);
            foreach (var p in Scatter(30, 4f, 58f, 6f, null, (keyPos, 3f)))
            {
                var lantern = CreatePrimitive(PrimitiveType.Sphere, "Lantern", lanterns.transform, lanternMat, false);
                lantern.transform.localPosition = p + Vector3.up * Random.Range(2.5f, 4.5f);
                lantern.transform.localScale = Vector3.one * 0.35f;
                lantern.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                PointLight(lantern.transform, "Light", Vector3.zero, new Color(1f, 0.85f, 0.6f), 6f, 1f);
                var bob = lantern.AddComponent<DreamFloat>();
                bob.bobHeight = 0.25f;
                bob.bobSpeed = 0.15f;
            }
            var stardust = Particles(root, "Stardust", new Vector3(0f, 3f, 0f), new Vector3(90f, 6f, 90f), 80f,
                new Color(0.8f, 0.9f, 1f), new Color(1f, 0.9f, 0.6f), 0.03f, 0.06f, ctx.sparkle);
            var planet = CreatePrimitive(PrimitiveType.Sphere, "Blue Planet", root, planetMat, false);
            planet.transform.localPosition = new Vector3(-50f, 40f, 85f);
            planet.transform.localScale = Vector3.one * 26f;
            planet.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var home = CreateWayHome(root, new Vector3(0f, 0f, 4f), 180f, stone);

            PlaceArtifacts(ctx, new Color(1f, 0.45f, 0.05f), new Color(0.75f, 0.85f, 1f), GroundRoute(new Vector3(0f, 0f, 7f), keyPos, 7f),
                Scatter(12, 8f, 72f, 4f, occupied, (Vector3.zero, 6f), (keyPos, 7f), (towerPos, 7f),
                    (sweeperSpots[0], 6f), (sweeperSpots[1], 6f), (sweeperSpots[2], 6f), (sweeperSpots[3], 6f)));
            nightmare.nightmareOnly = new[] { sweepers, eyes };
            nightmare.dreamOnly = new[] { home };
            foreach (var go in nightmare.dreamOnly)
                go.SetActive(false);
        }

        // =====================================================================
        // 5. Crystal Caverns — light the dim crystals in the dark, find the Heart Crystal
        // =====================================================================

        static void BuildCrystalCaverns(World world)
        {
            var ctx = SetupWorld(world, Vector3.zero, 0f, true);
            var root = ctx.root;

            var floorMat = Lit("Cave_Floor", new Color(0.35f, 0.3f, 0.45f), 0.3f, new Color(0.03f, 0.02f, 0.06f));
            var rock = Lit("Cave_Rock", new Color(0.42f, 0.36f, 0.55f), 0.25f, new Color(0.04f, 0.03f, 0.07f));
            var dimCrystal = Lit("Cave_Crystal_Dim", new Color(0.2f, 0.25f, 0.35f), 0.9f, new Color(0.08f, 0.12f, 0.2f));
            var litCrystal = Lit("Cave_Crystal_Lit", new Color(0.7f, 1f, 1f), 0.95f, new Color(1.2f, 2.2f, 2.4f));
            var heartMat = Lit("Cave_HeartCrystal", new Color(1f, 0.5f, 0.75f), 0.95f, new Color(2f, 0.6f, 1.4f));
            var shadowMat = Lit("Cave_Shadow", new Color(0.01f, 0f, 0.02f), 0.95f, Color.black);
            var shadowRim = Lit("Cave_ShadowRim", new Color(0.3f, 0.05f, 0.5f), 0.5f, new Color(0.6f, 0.1f, 1f));
            var eyesMat = Lit("Cave_Eyes", new Color(0.8f, 0.3f, 1f), 0.5f, new Color(1.6f, 0.4f, 2.2f));
            var capMat = Lit("Cave_MushroomCap", new Color(0.4f, 0.9f, 1f), 0.6f, new Color(0.4f, 1.2f, 1.4f));
            var stemMat = Lit("Cave_MushroomStem", new Color(0.9f, 0.9f, 0.85f), 0.3f, new Color(0.1f, 0.1f, 0.1f));
            var gemMats = new[]
            {
                Lit("Cave_Gem_Rose", new Color(1f, 0.5f, 0.75f), 0.95f, new Color(1f, 0.25f, 0.6f)),
                Lit("Cave_Gem_Sky", new Color(0.5f, 0.8f, 1f), 0.95f, new Color(0.3f, 0.7f, 1.2f)),
                Lit("Cave_Gem_Lime", new Color(0.7f, 1f, 0.5f), 0.95f, new Color(0.5f, 1.1f, 0.3f)),
                Lit("Cave_Gem_Amber", new Color(1f, 0.8f, 0.4f), 0.95f, new Color(1.2f, 0.8f, 0.2f)),
                Lit("Cave_Gem_Violet", new Color(0.75f, 0.55f, 1f), 0.95f, new Color(0.8f, 0.4f, 1.4f)),
            };
            var gemLights = new[]
            {
                new Color(1f, 0.5f, 0.8f), new Color(0.5f, 0.8f, 1f), new Color(0.7f, 1f, 0.5f), new Color(1f, 0.8f, 0.4f), new Color(0.8f, 0.6f, 1f),
            };

            var nightmare = ctx.nightmare;
            nightmare.tokenName = "Heart Crystal";
            nightmare.introMessage = "The Crystal Caverns have gone dark...\nBeware the pools of living shadow!\nLight the dim crystals to find the Heart Crystal.";
            nightmare.dreamMessage = "Light pours from every crystal.\nThe caverns sing!\nA portal home now waits where you started.";
            var atmosphere = new Atmosphere
            {
                sky = Sky("Cave_Sky_Dream", new Color(0.05f, 0.1f, 0.2f), new Color(0.3f, 0.2f, 0.5f),
                    new Color(0.1f, 0.05f, 0.2f), new Color(0.4f, 0.9f, 1f), 60f),
                fogColor = new Color(0.2f, 0.18f, 0.35f),
                fogDensity = 0.025f,
                ambientSky = new Color(0.4f, 0.45f, 0.65f),
                ambientEquator = new Color(0.4f, 0.35f, 0.55f),
                ambientGround = new Color(0.2f, 0.18f, 0.3f),
                sunColor = new Color(0.7f, 0.85f, 1f),
                sunIntensity = 0.6f,
            };
            ApplyAtmosphere(atmosphere, ctx.sun);
            ctx.vision.nightmare = NightmareLook(new Color(0.2f, 0.18f, 0.32f));


            var heartPos = new Vector3(0f, 0f, 30f);
            var homePos = new Vector3(0f, 0f, 4f);
            UseTerrain(world.scene, 2f, 68f, 6f, (Vector3.zero, 7f), (heartPos, 3f));
            Ground(root, floorMat, 230f);
            Boundary(root, 70f, 40);
            var occupied = new List<Vector3>();
            var beaconPoints = Scatter(5, 18f, 62f, 20f, occupied, (Vector3.zero, 6f), (heartPos, 6f));

            // --- The cave itself: a ceiling, walls of huge boulders, stalactites and stalagmites.
            var cave = Group("Cave", root);
            var ceiling = CreatePrimitive(PrimitiveType.Plane, "Ceiling", cave.transform, rock, false);
            ceiling.transform.localPosition = new Vector3(0f, 14f, 0f);
            ceiling.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            ceiling.transform.localScale = new Vector3(18f, 1f, 18f);
            ceiling.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            for (int i = 0; i < 64; i++)
            {
                var rot = Quaternion.Euler(0f, i * 5.625f + Random.Range(-2f, 2f), 0f);
                var s = new Vector3(Random.Range(6f, 10f), Random.Range(14f, 20f), Random.Range(6f, 10f));
                var wall = CreatePrimitive(PrimitiveType.Sphere, "Cave Wall", cave.transform, rock, false);
                wall.transform.localPosition = rot * new Vector3(0f, s.y * 0.35f, Random.Range(75f, 79f));
                wall.transform.localRotation = rot;
                wall.transform.localScale = s;
            }
            for (int i = 0; i < 180; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Mathf.Sqrt(Random.Range(4f, 4500f));
                float h = Random.Range(2f, 5f);
                float w = Random.Range(0.5f, 1.2f);
                var stalactite = CreatePrimitive(PrimitiveType.Sphere, "Stalactite", cave.transform, rock, false);
                stalactite.transform.localPosition = new Vector3(Mathf.Cos(a) * r, 14f, Mathf.Sin(a) * r);
                stalactite.transform.localScale = new Vector3(w, h, w);
            }
            foreach (var p in Scatter(120, 5f, 66f, 3f, occupied, (Vector3.zero, 5f), (heartPos, 4f), (homePos, 2.5f)))
            {
                float h = Random.Range(1.5f, 4f);
                float w = Random.Range(0.6f, 1.4f);
                var stalagmite = CreatePrimitive(PrimitiveType.Sphere, "Stalagmite", cave.transform, rock, true);
                stalagmite.transform.localPosition = p;
                stalagmite.transform.localScale = new Vector3(w, h, w);
            }

            // --- The dim crystals: walk up to one to set it blazing.
            var crystalMesh = SaveMesh(CreateCrystal(0.35f, 1.6f), "CaveCrystal");
            EdgeDressing(root, 72f, 50, rock, gemMats, false, crystalMesh, 2);
            var beacons = Group("Dim Crystals", root);
            foreach (var p in beaconPoints)
            {
                var beacon = Group("Dim Crystal", beacons.transform);
                beacon.transform.localPosition = p;
                var collectible = beacon.AddComponent<DreamCollectible>();
                collectible.touchDistance = 1.8f;
                var unlit = Group("Unlit", beacon.transform);
                var lit = Group("Lit", beacon.transform);
                for (int c = 0; c < 4; c++)
                {
                    var rot = Quaternion.Euler(c == 0 ? 0f : Random.Range(20f, 40f), c * 120f + Random.Range(-20f, 20f), 0f);
                    float s = c == 0 ? 1.2f : Random.Range(0.6f, 0.9f);
                    foreach (var (group, mat) in new[] { (unlit, dimCrystal), (lit, litCrystal) })
                    {
                        var shard = MeshObject("Crystal", group.transform, crystalMesh, mat);
                        shard.transform.localPosition = rot * Vector3.up * 0.8f * s;
                        shard.transform.localRotation = rot;
                        shard.transform.localScale = Vector3.one * s;
                    }
                }
                PointLight(unlit.transform, "Faint Glow", Vector3.up * 1.2f, new Color(0.3f, 0.5f, 0.8f), 4f, 0.6f);
                PointLight(lit.transform, "Light", Vector3.up * 1.6f, new Color(0.5f, 0.95f, 1f), 16f, 2.2f);
                Particles(lit.transform, "Glimmer", Vector3.up * 1.2f, new Vector3(1.5f, 2f, 1.5f), 10f,
                    new Color(0.5f, 1f, 1f), Color.white, 0.03f, 0.07f, ctx.sparkle);
                collectible.hideOnCollect = unlit;
                collectible.showOnCollect = lit;
                lit.SetActive(false);
            }

            // --- The Heart Crystal rises on its pillar once every crystal is lit.
            var pillar = CreatePrimitive(PrimitiveType.Cylinder, "Heart Pillar", root, rock, true);
            pillar.transform.localPosition = heartPos + Vector3.up * 0.5f;
            pillar.transform.localScale = new Vector3(1f, 0.5f, 1f);
            UseMeshCollider(pillar);
            var token = CreateToken(root, "Heart Crystal", heartPos + Vector3.up * 1.5f, new Color(1f, 0.5f, 0.8f),
                "The Heart Crystal is beating with light...\nReach out and take it!", out var heart);
            token.hintDistance = 7f;
            var heartGem = MeshObject("Crystal", heart, SaveMesh(CreateCrystal(0.14f, 0.4f), "HeartCrystal"), heartMat);
            heartGem.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Particles(token.transform.parent, "Heart Aura", Vector3.zero, new Vector3(1.2f, 1f, 1.2f), 12f,
                new Color(1f, 0.5f, 0.8f), new Color(0.8f, 0.6f, 1f), 0.03f, 0.08f, ctx.sparkle);
            CollectionGoal(ctx, token, "CRYSTALS LIT", beaconPoints.Count, "The crystal blazes with light!",
                "All the crystals shine together...\nThe Heart Crystal has appeared on the pillar in the middle of the cave!");

            // --- Nightmare: pools of living shadow and watching eyes.
            const string shadowMessage = "The shadows swallowed you...\nYou wake up where you started.";
            var ring = RingMesh();
            var shadows = Group("Shadow Pools", root);
            foreach (var p in Scatter(32, 6f, 64f, 4f, occupied, (Vector3.zero, 5f), (heartPos, 4f)))
            {
                float r = Random.Range(1.2f, 2.2f);
                var pool = Hazard(shadows.transform, "Shadow Pool", p, r, shadowMessage).gameObject;
                var creep = pool.AddComponent<DreamCreep>();
                creep.maxGrowth = 1.4f;
                creep.growTime = 240f;
                creep.chaseSpeed = 0.6f;
                creep.chaseRange = 10f;
                pool.transform.localRotation = AlongGround(p);
                var disc = CreatePrimitive(PrimitiveType.Cylinder, "Shadow", pool.transform, shadowMat, false);
                disc.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                disc.transform.localScale = new Vector3(r * 2f, 0.02f, r * 2f);
                var rim = MeshObject("Rim", pool.transform, ring, shadowRim);
                rim.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                rim.transform.localScale = new Vector3(r, r, 3f);
                Particles(pool.transform, "Wisps", Vector3.up * 0.4f, new Vector3(r * 1.5f, 0.6f, r * 1.5f), 6f,
                    new Color(0.05f, 0f, 0.1f, 0.7f), new Color(0.15f, 0.02f, 0.25f, 0.5f), 0.3f, 0.7f, ctx.mist);
            }
            var eyes = WatchingEyes(root, eyesMat, 24, 8f, 64f);

            // --- A garden of glowing gems, mushrooms and glowworms.
            var garden = Group("Crystal Garden", root);
            int lights = 0;
            foreach (var p in Scatter(170, 3f, 66f, 2f, null, (Vector3.zero, 3f), (heartPos, 3f), (homePos, 2f)))
            {
                var cluster = Group("Gem Cluster", garden.transform);
                cluster.transform.localPosition = p;
                int kind = Random.Range(0, gemMats.Length);
                int shards = Random.Range(2, 5);
                for (int c = 0; c < shards; c++)
                {
                    var rot = Quaternion.Euler(Random.Range(0f, 35f), Random.Range(0f, 360f), 0f);
                    float s = Random.Range(0.3f, 0.7f);
                    var shard = MeshObject("Gem", cluster.transform, crystalMesh, gemMats[kind]);
                    shard.transform.localPosition = rot * Vector3.up * 0.8f * s;
                    shard.transform.localRotation = rot;
                    shard.transform.localScale = Vector3.one * s;
                    shard.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                if (lights++ % 4 == 0)
                    PointLight(cluster.transform, "Glow", Vector3.up * 0.6f, gemLights[kind], 6f, 1.2f);
            }
            foreach (var p in Scatter(120, 3f, 66f, 1.5f, null, (Vector3.zero, 3f), (heartPos, 3f), (homePos, 2f)))
            {
                var mushroom = Group("Glowshroom", garden.transform);
                mushroom.transform.localPosition = p;
                float h = Random.Range(0.3f, 0.9f);
                var stem = CreatePrimitive(PrimitiveType.Cylinder, "Stem", mushroom.transform, stemMat, false);
                stem.transform.localPosition = Vector3.up * h * 0.5f;
                stem.transform.localScale = new Vector3(0.08f, h * 0.5f, 0.08f);
                var cap = CreatePrimitive(PrimitiveType.Sphere, "Cap", mushroom.transform, capMat, false);
                cap.transform.localPosition = Vector3.up * h;
                cap.transform.localScale = new Vector3(0.4f, 0.18f, 0.4f) * Random.Range(0.8f, 1.4f);
            }
            var glowworms = Particles(root, "Glowworms", new Vector3(0f, 12.5f, 0f), new Vector3(130f, 2f, 130f), 120f,
                new Color(0.4f, 1f, 0.9f), new Color(0.6f, 0.9f, 1f), 0.04f, 0.08f, ctx.sparkle);
            var home = CreateWayHome(root, homePos, 180f, rock);

            PlaceArtifacts(ctx, new Color(0.7f, 0.2f, 1f), new Color(0.5f, 1f, 0.95f), GroundRoute(new Vector3(0f, 0f, 7f), heartPos, 6f),
                Scatter(12, 8f, 64f, 3f, occupied, (Vector3.zero, 6f), (heartPos, 5f)));
            nightmare.nightmareOnly = new[] { shadows, eyes };
            nightmare.dreamOnly = new[] { home };
            foreach (var go in nightmare.dreamOnly)
                go.SetActive(false);
        }

        // =====================================================================
        // 6. Upside-Down City — cross the rooftops over the void, find the Compass of Up
        // =====================================================================

        static void BuildUpsideDownCity(World world)
        {
            var ctx = SetupWorld(world, Vector3.zero, 0f, true);
            var root = ctx.root;

            var roofMat = Lit("City_Roof", new Color(0.75f, 0.7f, 0.8f), 0.3f, new Color(0.06f, 0.05f, 0.08f));
            var wallA = Lit("City_WallA", new Color(0.55f, 0.75f, 0.85f), 0.4f, new Color(0.04f, 0.06f, 0.08f));
            var wallB = Lit("City_WallB", new Color(0.9f, 0.6f, 0.75f), 0.4f, new Color(0.08f, 0.04f, 0.06f));
            var windows = Lit("City_Windows", new Color(1f, 0.9f, 0.6f), 0.8f, new Color(1.6f, 1.3f, 0.6f));
            var bridgeMat = Lit("City_Bridge", new Color(0.95f, 0.85f, 0.6f), 0.4f, new Color(0.1f, 0.08f, 0.04f));
            var searchMat = Lit("City_Searchlight", new Color(1f, 0.2f, 0.15f), 0.5f, new Color(2.5f, 0.3f, 0.2f));
            var compassMat = Lit("City_Compass", new Color(0.6f, 1f, 0.9f), 0.95f, new Color(0.6f, 1.6f, 1.3f));
            var needleMat = Lit("City_CompassNeedle", new Color(1f, 0.35f, 0.5f), 0.9f, new Color(1.6f, 0.3f, 0.6f));
            var lanternMat = Lit("City_Lantern", new Color(1f, 0.85f, 0.6f), 0.5f, new Color(2f, 1.4f, 0.7f));
            var leafMat = Lit("City_Garden", new Color(0.45f, 0.9f, 0.55f), 0.2f, new Color(0.05f, 0.12f, 0.06f));
            var potMat = Lit("City_Planter", new Color(0.8f, 0.45f, 0.35f), 0.2f, new Color(0.06f, 0.03f, 0.02f));

            var nightmare = ctx.nightmare;
            nightmare.tokenName = "Compass of Up";
            nightmare.introMessage = "The Upside-Down City is falling into the sky...\nDon't fall, and stay out of the searchlights!\nCross the rooftops to find the Compass of Up.";
            nightmare.dreamMessage = "The compass points the way.\nThe city settles into a sweet dream.\nA portal home now waits where you started.";
            var atmosphere = new Atmosphere
            {
                sky = Sky("City_Sky_Dream", new Color(0.35f, 0.55f, 0.95f), new Color(1f, 0.7f, 0.6f),
                    new Color(0.6f, 0.5f, 0.9f), new Color(1f, 0.8f, 0.9f), 40f),
                fogColor = new Color(0.85f, 0.7f, 0.85f),
                fogDensity = 0.012f,
                ambientSky = new Color(0.65f, 0.65f, 0.85f),
                ambientEquator = new Color(0.8f, 0.65f, 0.7f),
                ambientGround = new Color(0.45f, 0.4f, 0.6f),
                sunColor = new Color(1f, 0.92f, 0.85f),
                sunIntensity = 1.2f,
            };
            ApplyAtmosphere(atmosphere, ctx.sun);
            ctx.vision.nightmare = NightmareLook(new Color(0.5f, 0.22f, 0.28f));

            var fall = nightmare.gameObject.AddComponent<DreamFallReset>();
            fall.minHeight = -8f;
            fall.message = "You fell into the sky below...\nYou wake up where you started.";

            // --- Rooftops floating over the void, joined by narrow bridges. All tops sit at y = 0.
            (Vector3 center, Vector2 size)[] roofs =
            {
                (new Vector3(0f, 0f, 0f), new Vector2(10f, 10f)),
                (new Vector3(12f, 0f, 9f), new Vector2(6f, 6f)),
                (new Vector3(4f, 0f, 19f), new Vector2(7f, 6f)),
                (new Vector3(-9f, 0f, 23f), new Vector2(6f, 6f)),
                (new Vector3(6f, 0f, 31f), new Vector2(6f, 6f)),
                (new Vector3(-6f, 0f, 36f), new Vector2(7f, 7f)),
                (new Vector3(14f, 0f, 40f), new Vector2(6f, 6f)),
                (new Vector3(2f, 0f, 47f), new Vector2(7f, 6f)),
                (new Vector3(-12f, 0f, 51f), new Vector2(6f, 6f)),
                (new Vector3(10f, 0f, 57f), new Vector2(6f, 6f)),
                (new Vector3(-2f, 0f, 66f), new Vector2(10f, 9f)),
            };
            var links = new[] { (0, 1), (1, 2), (2, 3), (2, 4), (3, 5), (4, 5), (4, 6), (5, 7), (6, 7), (7, 8), (7, 9), (8, 10), (9, 10) };
            int compassRoof = roofs.Length - 1;
            // The rooftop where the player starts (and where the way home appears) gets a low parapet,
            // open only where its bridge leaves.
            var spawnExits = new List<Vector3>();
            foreach (var (a, b) in links)
                if (a == 0 || b == 0)
                {
                    var n = (roofs[a == 0 ? b : a].center - roofs[0].center).normalized;
                    spawnExits.Add(roofs[0].center + n * RoofExit(roofs[0].size, n));
                }
            var compassPos = roofs[compassRoof].center;

            var city = Group("Rooftops", root);
            for (int i = 0; i < roofs.Length; i++)
            {
                var (center, size) = roofs[i];
                var building = Group($"Building {i + 1}", city.transform);
                building.transform.localPosition = center;
                var top = Box("Roof", building.transform, roofMat, new Vector3(0f, -0.2f, 0f), new Vector3(size.x, 0.4f, size.y));
                top.AddComponent<TeleportationArea>();

                float depth = Random.Range(30f, 45f);
                Box("Tower", building.transform, i % 2 == 0 ? wallA : wallB, new Vector3(0f, -0.4f - depth * 0.5f, 0f),
                    new Vector3(size.x - 0.4f, depth, size.y - 0.4f), false);
                for (float y = -2f; y > -depth; y -= 3f)
                    Box("Windows", building.transform, windows, new Vector3(0f, y, 0f), new Vector3(size.x - 0.3f, 0.6f, size.y - 0.3f), false);
                if (i != 0 && i != compassRoof)
                    Box("Vent", building.transform, roofMat, new Vector3(size.x * 0.5f - 0.6f, 0.3f, size.y * 0.5f - 0.6f), new Vector3(0.8f, 0.6f, 0.8f), false);
            }

            Parapet(city.transform, roofs[0].center, roofs[0].size, spawnExits, roofMat);

            var bridges = Group("Bridges", root);
            var rails = Group("Dream Rails", root);
            foreach (var (a, b) in links)
            {
                Vector3 from = roofs[a].center, to = roofs[b].center;
                var dir = to - from;
                var rot = Quaternion.LookRotation(dir);
                var bridge = Box("Bridge", bridges.transform, bridgeMat, (from + to) * 0.5f + Vector3.down * 0.12f, new Vector3(1.2f, 0.2f, dir.magnitude));
                bridge.transform.localRotation = rot;
                bridge.AddComponent<TeleportationArea>();

                // In the dream, railings along the open stretch make every crossing safe.
                var n = dir.normalized;
                var gapStart = from + n * RoofExit(roofs[a].size, n);
                var gapEnd = to - n * RoofExit(roofs[b].size, n);
                for (int side = -1; side <= 1; side += 2)
                {
                    var rail = Box("Rail", rails.transform, bridgeMat, (gapStart + gapEnd) * 0.5f + rot * new Vector3(side * 0.65f, 0.45f, 0f),
                        new Vector3(0.08f, 0.9f, Vector3.Distance(gapStart, gapEnd)));
                    rail.transform.localRotation = rot;
                }
            }

            // --- The city hanging upside down from the sky.
            var skyline = Group("Upside-Down Skyline", root);
            foreach (var p in Scatter(90, 8f, 100f, 7f, null))
            {
                float w = Random.Range(3f, 7f), d = Random.Range(3f, 7f), h = Random.Range(10f, 24f);
                var tower = Group("Hanging Tower", skyline.transform);
                tower.transform.localPosition = p + Vector3.up * 42f;
                Box("Body", tower.transform, Random.value < 0.5f ? wallA : wallB, new Vector3(0f, -h * 0.5f, 0f), new Vector3(w, h, d), false);
                for (float y = -2f; y > -h; y -= 3f)
                    Box("Windows", tower.transform, windows, new Vector3(0f, y, 0f), new Vector3(w + 0.1f, 0.6f, d + 0.1f), false);
                Box("Roof", tower.transform, roofMat, new Vector3(0f, -h - 0.2f, 0f), new Vector3(w + 0.4f, 0.4f, d + 0.4f), false);
                var spire = CreatePrimitive(PrimitiveType.Cylinder, "Antenna", tower.transform, roofMat, false);
                spire.transform.localPosition = new Vector3(0f, -h - 2f, 0f);
                spire.transform.localScale = new Vector3(0.15f, 1.8f, 0.15f);
            }

            // --- The Compass of Up, whose needle always points to the sky.
            Box("Compass Stand", root, roofMat, compassPos + Vector3.up * 0.5f, new Vector3(0.8f, 1f, 0.8f));
            var token = CreateToken(root, "Compass of Up", compassPos + Vector3.up * 1.5f, new Color(0.6f, 1f, 0.9f),
                "The Compass of Up is spinning nearby...\nReach out and grab it!", out var compass);
            token.hintDistance = 7f;
            var dial = CreatePrimitive(PrimitiveType.Cylinder, "Dial", compass, compassMat, false);
            dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            dial.transform.localScale = new Vector3(0.3f, 0.02f, 0.3f);
            MeshObject("Rim", compass, SaveMesh(CreateEllipticTorus(0.16f, 0.16f, 0.02f, 32, 8), "CompassRing"), compassMat);
            for (int side = -1; side <= 1; side += 2)
                Box("Needle", compass, needleMat, new Vector3(0f, 0.05f, side * 0.03f), new Vector3(0.03f, 0.22f, 0.02f), false);
            Particles(root, "Compass Glimmer", compassPos + Vector3.up * 1.5f, new Vector3(1f, 1f, 1f), 12f,
                new Color(0.6f, 1f, 0.9f), Color.white, 0.03f, 0.07f, ctx.sparkle);

            // --- Nightmare: searchlights sweeping the rooftops and eyes in the void.
            const string searchMessage = "The searchlight found you...\nYou wake up where you started.";
            var searchlights = Group("Searchlights", root);
            foreach (var (roof, radius, speed) in new[] { (2, 2f, 40f), (5, 2.5f, -35f), (7, 2.2f, 32f), (8, 2f, -38f), (compassRoof, 3f, 30f) })
            {
                var pivot = Group("Searchlight", searchlights.transform);
                pivot.transform.localPosition = roofs[roof].center;
                var spin = pivot.AddComponent<DreamFloat>();
                spin.bobHeight = 0f;
                spin.spin = new Vector3(0f, speed, 0f);
                var escalation = pivot.AddComponent<DreamEscalation>();
                escalation.goal = compassPos;
                escalation.range = 50f;
                escalation.boost = 1.2f;

                var spot = Hazard(pivot.transform, "Beam Spot", new Vector3(0f, 0f, radius), 1.1f, searchMessage).gameObject;
                var disc = CreatePrimitive(PrimitiveType.Cylinder, "Light Pool", spot.transform, searchMat, false);
                disc.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                disc.transform.localScale = new Vector3(2f, 0.01f, 2f);
                disc.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                PointLight(spot.transform, "Light", Vector3.up * 3f, new Color(1f, 0.25f, 0.2f), 6f, 3f);
            }

            var eyes = WatchingEyes(root, searchMat, 20, 15f, 75f);

            // --- Rooftop gardens, floating lanterns and sparkles.
            var gardens = Group("Rooftop Gardens", root);
            for (int i = 1; i < roofs.Length; i++)
            {
                var (center, size) = roofs[i];
                var corner = center + new Vector3(-size.x * 0.5f + 0.7f, 0f, size.y * 0.5f - 0.7f);
                var planter = Box("Planter", gardens.transform, potMat, corner + Vector3.up * 0.25f, new Vector3(0.9f, 0.5f, 0.9f), false);
                var bush = CreatePrimitive(PrimitiveType.Sphere, "Bush", planter.transform, leafMat, false);
                bush.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                bush.transform.localScale = new Vector3(1.4f, 2.4f, 1.4f);
            }
            var lanterns = Group("Floating Lanterns", root);
            for (int i = 0; i < 44; i++)
            {
                var (center, size) = roofs[i % roofs.Length];
                var lantern = CreatePrimitive(PrimitiveType.Sphere, "Lantern", lanterns.transform, lanternMat, false);
                lantern.transform.localPosition = center + new Vector3(Random.Range(-size.x, size.x), Random.Range(2.5f, 6f), Random.Range(-size.y, size.y));
                lantern.transform.localScale = Vector3.one * 0.3f;
                lantern.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                if (i % 3 == 0)
                    PointLight(lantern.transform, "Light", Vector3.zero, new Color(1f, 0.85f, 0.6f), 6f, 1f);
                var bob = lantern.AddComponent<DreamFloat>();
                bob.bobHeight = 0.3f;
                bob.bobSpeed = Random.Range(0.1f, 0.2f);
            }
            var sparkles = Particles(root, "Dream Sparkles", new Vector3(0f, 3f, 33f), new Vector3(50f, 6f, 80f), 60f,
                new Color(1f, 0.8f, 0.9f), new Color(0.7f, 0.9f, 1f), 0.03f, 0.07f, ctx.sparkle);
            var home = CreateWayHome(root, new Vector3(0f, 0f, 3.5f), 180f, roofMat);

            // Artifacts: the route hops roof to roof across bridges; paired artifacts stand on roof corners.
            var route = new List<Vector3>();
            int[] chain = { 0, 1, 2, 3, 5, 7, 8, compassRoof };
            for (int i = 0; i < chain.Length; i++)
            {
                route.Add(roofs[chain[i]].center);
                if (i + 1 < chain.Length)
                    route.Add((roofs[chain[i]].center + roofs[chain[i + 1]].center) * 0.5f);
            }
            var spots = new List<Vector3>();
            for (int i = 1; i < compassRoof; i++)
                spots.Add(roofs[i].center + new Vector3(roofs[i].size.x * 0.5f - 0.9f, 0f, -(roofs[i].size.y * 0.5f - 0.9f)));
            PlaceArtifacts(ctx, new Color(1f, 0.1f, 0.2f), new Color(1f, 0.85f, 0.6f), route, spots);
            nightmare.nightmareOnly = new[] { searchlights, eyes };
            nightmare.dreamOnly = new[] { rails, home };
            foreach (var go in nightmare.dreamOnly)
                go.SetActive(false);
        }

        /// <summary>A knee-high wall around a rooftop's edge in one-metre pieces, leaving gaps at <paramref name="openings"/>.</summary>
        static void Parapet(Transform parent, Vector3 center, Vector2 size, List<Vector3> openings, Material mat)
        {
            var parapet = Group("Parapet", parent);
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            for (int edge = 0; edge < 4; edge++)
            {
                bool alongX = edge < 2;
                float fixedCoord = edge % 2 == 0 ? (alongX ? hz : hx) : -(alongX ? hz : hx);
                float length = alongX ? size.x : size.y;
                int pieces = Mathf.RoundToInt(length);
                for (int i = 0; i < pieces; i++)
                {
                    float t = -length * 0.5f + (i + 0.5f) * length / pieces;
                    var local = alongX ? new Vector3(t, 0f, fixedCoord) : new Vector3(fixedCoord, 0f, t);
                    bool open = false;
                    foreach (var o in openings)
                        if (FlatDistance(center + local, o) < 1.3f)
                            open = true;
                    if (open)
                        continue;
                    Box("Parapet Wall", parapet.transform, mat, center + local + Vector3.up * 0.5f,
                        alongX ? new Vector3(length / pieces + 0.02f, 1f, 0.2f) : new Vector3(0.2f, 1f, length / pieces + 0.02f));
                }
            }
        }

        /// <summary>Distance from a rooftop's center to its edge along the flat direction <paramref name="n"/>.</summary>
        static float RoofExit(Vector2 size, Vector3 n)
        {
            return Mathf.Min(size.x * 0.5f / Mathf.Max(Mathf.Abs(n.x), 1e-4f), size.y * 0.5f / Mathf.Max(Mathf.Abs(n.z), 1e-4f));
        }
    }
}
