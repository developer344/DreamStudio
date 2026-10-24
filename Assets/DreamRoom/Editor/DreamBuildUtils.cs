using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;

namespace DreamStudio.Editor
{
    /// <summary>
    /// Shared helpers for the Dream Studio scene builders: the world list, materials,
    /// procedural meshes, primitives, portals and particles.
    /// </summary>
    public static class DreamBuildUtils
    {
        public const string AssetRoot = "Assets/DreamRoom";
        public const string MaterialDir = AssetRoot + "/Materials";
        public const string MeshDir = AssetRoot + "/Meshes";
        public const string TextureDir = AssetRoot + "/Textures";
        public const string WorldSceneDir = "Assets/Scenes/Worlds";
        public const string HubScenePath = "Assets/Scenes/BasicScene.unity";
        public const string HubSceneName = "BasicScene";
        /// <summary>Where the Start Room used to live as its own scene; now it shares the hub scene, so this is kept out of the build.</summary>
        public const string LobbyScenePath = "Assets/Scenes/DreamLobby.unity";
        const string XRRigPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        public const float PortalHalfWidth = 1.2f;
        public const float PortalHalfHeight = 1.6f;

        public struct World
        {
            public string name;
            public string scene;
            public Color inner;
            public Color outer;
            /// <summary>Fixed per world, so a world's random layout stays the same when the list changes.</summary>
            public int seed;

            public World(string name, string scene, Color inner, Color outer, int seed)
            {
                this.name = name;
                this.scene = scene;
                this.inner = inner;
                this.outer = outer;
                this.seed = seed;
            }
        }

        /// <summary>The worlds, in hub portal order (left, ahead, right). Scene files live in <see cref="WorldSceneDir"/>.</summary>
        public static readonly World[] Worlds =
        {
            new World("Haunted Forest", "HauntedForest", new Color(0.65f, 1f, 0.7f), new Color(0.3f, 0.12f, 0.5f), 1000),
            new World("Sunken Depths", "SunkenDepths", new Color(0.4f, 0.95f, 1f), new Color(0.1f, 0.2f, 0.75f), 1001),
            new World("Upside-Down City", "UpsideDownCity", new Color(0.95f, 0.55f, 1f), new Color(0.2f, 0.85f, 0.7f), 1005),
        };

        public static void EnsureFolders()
        {
            EnsureFolder(MaterialDir);
            EnsureFolder(MeshDir);
            EnsureFolder(TextureDir);
            EnsureFolder(WorldSceneDir);
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---------- Materials ----------

        public static Material LoadOrCreateMaterial(string name, string shaderName)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var shader = Shader.Find(shaderName);
            if (shader == null)
                Debug.LogError($"[DreamStudio] Shader '{shaderName}' not found.");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            return mat;
        }

        /// <summary>URP Lit material. Emission is always enabled so nightmare/dream pairs can blend.</summary>
        public static Material Lit(string name, Color baseColor, float smoothness, Color emission)
        {
            var mat = LoadOrCreateMaterial(name, "Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            DreamTextures.ApplyAssigned(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material Sky(string name, Color top, Color horizon, Color bottom, Color nebula, float starDensity)
        {
            var mat = LoadOrCreateMaterial(name, "DreamStudio/DreamSky");
            mat.SetColor("_TopColor", top);
            mat.SetColor("_HorizonColor", horizon);
            mat.SetColor("_BottomColor", bottom);
            mat.SetColor("_NebulaColor", nebula);
            mat.SetFloat("_StarDensity", starDensity);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material Sparkle()
        {
            var mat = LoadOrCreateMaterial("Sparkle", "Universal Render Pipeline/Particles/Unlit");
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f); // additive
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetTexture("_BaseMap", SoftDotTexture());
            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Alpha-blended soft dots, for mist and murk that should darken rather than glow.</summary>
        public static Material Mist()
        {
            var mat = LoadOrCreateMaterial("Mist", "Universal Render Pipeline/Particles/Unlit");
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f); // alpha
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetTexture("_BaseMap", SoftDotTexture());
            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Soft dark dots laid flat under props, standing in for ambient occlusion.</summary>
        public static Material ContactShadowMaterial()
        {
            var mat = LoadOrCreateMaterial("ContactShadow", "Universal Render Pipeline/Particles/Unlit");
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f); // alpha
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent - 50;
            mat.SetTexture("_BaseMap", SoftDotTexture());
            mat.SetColor("_BaseColor", new Color(0f, 0f, 0.02f, 0.55f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Points every URP asset's lighting at the scale of the worlds: sun shadows reach 45 m with soft
        /// cascades (the template default only drew them 2.5 m out), and point lights are switched on
        /// per pixel (the template had them disabled, so lanterns, candles and portal glows lit nothing).
        /// </summary>
        public static void ConfigureLighting()
        {
            var assets = new HashSet<RenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline != null)
                assets.Add(GraphicsSettings.defaultRenderPipeline);
            for (int i = 0; i < QualitySettings.count; i++)
            {
                var asset = QualitySettings.GetRenderPipelineAssetAt(i);
                if (asset != null)
                    assets.Add(asset);
            }

            foreach (var asset in assets)
            {
                var so = new SerializedObject(asset);
                SetFloat(so, "m_ShadowDistance", 45f);
                SetInt(so, "m_ShadowCascadeCount", 2);
                SetFloat(so, "m_Cascade2Split", 0.25f);
                SetInt(so, "m_MainLightShadowmapResolution", 2048);
                SetFloat(so, "m_ShadowDepthBias", 1f);
                SetFloat(so, "m_ShadowNormalBias", 1f);
                SetBool(so, "m_MainLightShadowsSupported", true);
                SetBool(so, "m_SoftShadowsSupported", true);
                SetInt(so, "m_AdditionalLightsRenderingMode", 1); // per pixel
                SetInt(so, "m_AdditionalLightsPerObjectLimit", 4);
                SetBool(so, "m_AdditionalLightShadowsSupported", false);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
            }
        }

        static void SetFloat(SerializedObject so, string name, float value)
        {
            var p = so.FindProperty(name);
            if (p != null)
                p.floatValue = value;
        }

        static void SetInt(SerializedObject so, string name, int value)
        {
            var p = so.FindProperty(name);
            if (p != null)
                p.intValue = value;
        }

        static void SetBool(SerializedObject so, string name, bool value)
        {
            var p = so.FindProperty(name);
            if (p != null)
                p.boolValue = value;
        }

        /// <summary>
        /// Lays a soft dark patch on the ground under everything that stands on it, so props sit in the
        /// world instead of floating on it. Patches under things that move, appear or vanish (anything
        /// scripted other than swaying, or under <paramref name="toggledRoots"/>) go beside the object so
        /// they follow it; the rest are gathered into one static group so they batch together.
        /// On rolling ground, pass <paramref name="groundHeight"/> and <paramref name="groundNormal"/> so the
        /// patches lie along the slope.
        /// </summary>
        public static void AddContactShadows(Transform root, ICollection<Transform> toggledRoots,
            System.Func<Vector3, float> groundHeight = null, System.Func<Vector3, Vector3> groundNormal = null)
        {
            var mat = ContactShadowMaterial();
            var staticGroup = new GameObject("Contact Shadows").transform;
            staticGroup.SetParent(root, false);

            var targets = new List<(MeshRenderer renderer, Bounds bounds)>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.shadowCastingMode == ShadowCastingMode.Off || !r.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
                    continue;
                if (r.name == "Fallen Log") // long things bring their own long shadow
                    continue;
                // Mesh bounds work for inactive objects too, unlike Renderer.bounds.
                var b = TransformBounds(filter.sharedMesh.bounds, r.transform.localToWorldMatrix);
                float footprint = Mathf.Max(b.size.x, b.size.z);
                float ground = groundHeight != null ? groundHeight(b.center) : 0f;
                if (b.min.y - ground < -0.5f || b.min.y - ground > 0.45f || b.max.y - ground < 0.2f || footprint < 0.15f || footprint > 9f)
                    continue;
                targets.Add((r, b));
            }

            var placed = new List<(Transform parent, Vector3 center, float size)>();
            foreach (var (r, b) in targets)
            {
                float size = Mathf.Max(b.size.x, b.size.z) * 1.5f + 0.3f;
                float ground = groundHeight != null ? groundHeight(b.center) : 0f;
                // Things raised on a platform cast their patch onto the platform; the rest onto the ground.
                bool onGround = b.min.y - ground < 0.1f;
                var center = new Vector3(b.center.x, (onGround ? ground : b.min.y) + 0.03f, b.center.z);
                var tilt = onGround && groundNormal != null ? Quaternion.FromToRotation(Vector3.up, groundNormal(center)) : Quaternion.identity;
                var parent = FollowsObject(r.transform, root, toggledRoots) ? r.transform.parent : staticGroup;

                bool duplicate = false;
                foreach (var p in placed)
                    if (p.parent == parent && (p.center - center).sqrMagnitude < size * size * 0.12f && p.size >= size * 0.7f)
                    {
                        duplicate = true;
                        break;
                    }
                if (duplicate)
                    continue;
                placed.Add((parent, center, size));

                var blob = CreatePrimitive(PrimitiveType.Quad, "Contact Shadow", null, mat, false);
                var blobRenderer = blob.GetComponent<Renderer>();
                blobRenderer.shadowCastingMode = ShadowCastingMode.Off;
                blobRenderer.receiveShadows = false;
                blob.transform.SetPositionAndRotation(center, tilt * Quaternion.Euler(90f, 0f, 0f));
                blob.transform.localScale = Vector3.one * size;
                blob.transform.SetParent(parent, true);
            }
        }

        static bool FollowsObject(Transform t, Transform root, ICollection<Transform> toggledRoots)
        {
            for (; t != null && t != root; t = t.parent)
            {
                if (toggledRoots.Contains(t))
                    return true;
                foreach (var behaviour in t.GetComponents<MonoBehaviour>())
                    if (behaviour != null && !(behaviour is DreamSway) && !(behaviour is UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea))
                        return true;
            }
            return false;
        }

        public static Bounds TransformBounds(Bounds local, Matrix4x4 m)
        {
            var result = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                result.Encapsulate(m.MultiplyPoint3x4(corner));
            }
            return result;
        }

        public static Material ScreenFade()
        {
            var mat = LoadOrCreateMaterial("ScreenFade", "DreamStudio/ScreenFade");
            mat.SetColor("_Color", Color.clear);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material PortalSurface(string key, Color inner, Color outer)
        {
            var mat = LoadOrCreateMaterial($"Portal_{key}", "DreamStudio/PortalSwirl");
            mat.SetColor("_ColorA", inner);
            mat.SetColor("_ColorB", outer);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material PortalRing(string key, Color inner, Color outer)
        {
            return Lit($"PortalRing_{key}", inner, 0.9f, Color.Lerp(inner, outer, 0.4f) * 1.5f);
        }

        public static Texture2D SoftDotTexture()
        {
            string path = $"{TextureDir}/SoftDot.png";
            if (!File.Exists(path))
            {
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- Scene pieces ----------

        public static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Material mat, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider)
                Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        public static GameObject Box(string name, Transform parent, Material mat, Vector3 localPos, Vector3 localScale, bool keepCollider = true)
        {
            var go = CreatePrimitive(PrimitiveType.Cube, name, parent, mat, keepCollider);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            return go;
        }

        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public static Light PointLight(Transform parent, string name, Vector3 localPos, Color color, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>
        /// A swirling oval portal facing its local +Z. The portal's origin is the center of the oval.
        /// </summary>
        public static DreamPortal CreatePortal(Transform parent, string label, string sceneName, string materialKey,
            Color inner, Color outer, Vector3 labelLocalPos)
        {
            var portal = new GameObject($"Portal - {label}");
            portal.transform.SetParent(parent, false);

            var surface = CreatePrimitive(PrimitiveType.Quad, "Surface", portal.transform, PortalSurface(materialKey, inner, outer), false);
            surface.transform.localScale = new Vector3(PortalHalfWidth * 2f, PortalHalfHeight * 2f, 1f);
            surface.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            MeshObject("Frame", portal.transform, PortalRingMesh(), PortalRing(materialKey, inner, outer));

            var light = PointLight(portal.transform, "Portal Light", new Vector3(0f, 0f, 0.8f), Color.Lerp(inner, outer, 0.5f), 4.5f, 1.5f);

            // Invisible barrier just behind the surface so players can't walk through
            // when the portal has no destination yet.
            var barrier = portal.AddComponent<BoxCollider>();
            barrier.center = new Vector3(0f, 0f, -0.45f);
            barrier.size = new Vector3(PortalHalfWidth * 2f + 0.4f, PortalHalfHeight * 2f + 0.5f, 0.1f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(portal.transform, false);
            labelGo.transform.localPosition = labelLocalPos;
            labelGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var text = labelGo.AddComponent<TextMeshPro>();
            text.text = label;
            text.fontSize = 3.2f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.Lerp(inner, Color.white, 0.5f);
            text.rectTransform.sizeDelta = new Vector2(PortalHalfWidth * 2f + 1.4f, 0.7f);

            var dreamPortal = portal.AddComponent<DreamPortal>();
            dreamPortal.worldName = label;
            dreamPortal.sceneName = sceneName;
            dreamPortal.halfSize = new Vector2(PortalHalfWidth, PortalHalfHeight);
            dreamPortal.surfaceRenderer = surface.GetComponent<Renderer>();
            dreamPortal.portalLight = light;
            return dreamPortal;
        }

        public static ParticleSystem Particles(Transform parent, string name, Vector3 localPos, Vector3 boxSize, float rate,
            Color colorA, Color colorB, float sizeMin, float sizeMax, Material mat, float speedMin = 0.02f, float speedMax = 0.1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.maxParticles = Mathf.CeilToInt(rate * 11f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = boxSize;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.3f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>Instantiates the XRI starter rig (plus an interaction manager) at a spawn pose.</summary>
        public static GameObject CreateXRRig(Vector3 position, float yaw)
        {
            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XRRigPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[DreamStudio] XR rig prefab not found at {XRRigPrefabPath}.");
                return null;
            }
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return rig;
        }

        // ---------- Meshes ----------

        public static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = name;
                Object.DestroyImmediate(mesh);
                return existing;
            }
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static Mesh PortalRingMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshDir}/PortalRing.asset");
            if (mesh != null)
                return mesh;
            return SaveMesh(CreateEllipticTorus(PortalHalfWidth + 0.06f, PortalHalfHeight + 0.06f, 0.1f, 96, 16), "PortalRing");
        }

        /// <summary>
        /// A flat regular polygon in the XZ plane, oriented so each side faces one of the directions
        /// 0°, 360/sides°, ... (the way the room walls are laid out).
        /// </summary>
        public static Mesh CreatePolygon(float radius, int sides)
        {
            var verts = new Vector3[sides + 1];
            var uvs = new Vector2[sides + 1];
            var tris = new int[sides * 3];
            verts[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            float step = 360f / sides;
            for (int k = 0; k < sides; k++)
            {
                float a = (step * 0.5f + k * step) * Mathf.Deg2Rad;
                verts[k + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                uvs[k + 1] = new Vector2(verts[k + 1].x / (2f * radius) + 0.5f, verts[k + 1].z / (2f * radius) + 0.5f);
            }
            for (int k = 0; k < sides; k++)
            {
                tris[k * 3] = 0;
                tris[k * 3 + 1] = k + 1;
                tris[k * 3 + 2] = (k + 1) % sides + 1;
            }
            var mesh = new Mesh { vertices = verts, uv = uvs, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Torus whose centerline is an ellipse (semi-axes a, b) in the XY plane.</summary>
        public static Mesh CreateEllipticTorus(float a, float b, float tube, int segments, int sides)
        {
            int vertCount = (segments + 1) * (sides + 1);
            var verts = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var tris = new int[segments * sides * 6];

            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments * Mathf.PI * 2f;
                var center = new Vector3(a * Mathf.Cos(u), b * Mathf.Sin(u), 0f);
                var tangent = new Vector3(-a * Mathf.Sin(u), b * Mathf.Cos(u), 0f).normalized;
                var outward = new Vector3(tangent.y, -tangent.x, 0f);
                for (int j = 0; j <= sides; j++)
                {
                    float v = (float)j / sides * Mathf.PI * 2f;
                    var n = outward * Mathf.Cos(v) + Vector3.forward * Mathf.Sin(v);
                    int idx = i * (sides + 1) + j;
                    verts[idx] = center + n * tube;
                    normals[idx] = n;
                    uvs[idx] = new Vector2((float)i / segments, (float)j / sides);
                }
            }

            int t = 0;
            for (int i = 0; i < segments; i++)
            for (int j = 0; j < sides; j++)
            {
                int a0 = i * (sides + 1) + j;
                int a1 = (i + 1) * (sides + 1) + j;
                tris[t++] = a0;
                tris[t++] = a1;
                tris[t++] = a0 + 1;
                tris[t++] = a1;
                tris[t++] = a1 + 1;
                tris[t++] = a0 + 1;
            }

            var mesh = new Mesh { vertices = verts, normals = normals, uv = uvs, triangles = tris };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A faceted gem: an elongated octahedron with flat-shaded faces.</summary>
        public static Mesh CreateCrystal(float radius, float height)
        {
            var top = new Vector3(0f, height * 0.5f, 0f);
            var bottom = new Vector3(0f, -height * 0.5f, 0f);
            var ring = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                ring[i] = new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
            }

            var verts = new Vector3[6 * 2 * 3];
            var tris = new int[verts.Length];
            int v = 0;
            for (int i = 0; i < 6; i++)
            {
                var p0 = ring[i];
                var p1 = ring[(i + 1) % 6];
                verts[v] = top; verts[v + 1] = p0; verts[v + 2] = p1;
                verts[v + 3] = bottom; verts[v + 4] = p1; verts[v + 5] = p0;
                v += 6;
            }
            for (int i = 0; i < tris.Length; i++)
                tris[i] = i;

            var mesh = new Mesh { vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
