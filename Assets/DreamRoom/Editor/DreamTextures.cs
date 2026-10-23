using System.IO;
using UnityEditor;
using UnityEngine;

namespace DreamStudio.Editor
{
    public enum Surface
    {
        Grain,    // fine mottled grit: concrete, dust, gingerbread
        Bark,     // long fibrous grooves: tree bark, old wood
        Stone,    // cracked cells: rocks, temple stone, flagstones
        Sand,     // wind-blown ripples over grain
        Metal,    // brushed streaks with blotches of rust
        Brick,    // staggered bricks and mortar
        Crystal,  // flat facets with bright edges
        Glaze,    // smooth sugar glaze with sparkly specks
        Organic,  // soft clumps: leaves, moss, coral, mushrooms
    }

    /// <summary>
    /// Procedural, seamlessly tiling surface textures (a detail albedo plus a matching normal map),
    /// generated once into Textures/Surfaces. The albedo is mostly light grey so the material's
    /// base colour still decides the hue; it adds grime, grain and relief on top.
    /// </summary>
    public static class DreamTextures
    {
        const int Size = 256;
        const int Version = 1;

        static string Dir => DreamBuildUtils.TextureDir + "/Surfaces";

        /// <summary>
        /// Which surface each material gets, by material name (an entry ending in "_" matches every
        /// material starting with it): surface, tiling, bump strength.
        /// </summary>
        static readonly (string name, Surface surface, float tiling, float bump)[] Assignments =
        {
            // Dream Room
            ("Floor", Surface.Stone, 6f, 0.8f), ("Wall", Surface.Brick, 3f, 0.8f), ("Column", Surface.Stone, 1.5f, 0.8f),
            ("Trim", Surface.Metal, 2f, 0.6f), ("Cloud", Surface.Organic, 1f, 0.5f),
            // Haunted Forest
            ("Forest_Ground_Dream", Surface.Organic, 60f, 1f), ("Forest_Bark_Dream", Surface.Bark, 1.5f, 1.2f),
            ("Forest_Stone_Dream", Surface.Stone, 1.5f, 1f), ("Forest_Leaves_", Surface.Organic, 2f, 1f),
            ("Forest_Blossom", Surface.Organic, 2f, 1f), ("Forest_Shadow", Surface.Grain, 2f, 1f), ("Forest_Flower_", Surface.Glaze, 1f, 0.6f),
            // Sunken Depths
            ("Ocean_Sand_Dream", Surface.Sand, 45f, 1f), ("Ocean_Rock_Dream", Surface.Stone, 2f, 1f), ("Ocean_Kelp_Dream", Surface.Organic, 1f, 0.8f),
            ("Ocean_Temple_Dream", Surface.Stone, 3f, 1f), ("Ocean_Wreck_Dream", Surface.Bark, 2f, 1.2f), ("Ocean_Coral_", Surface.Organic, 2f, 1.2f),
            ("Ocean_Clam", Surface.Glaze, 2f, 0.8f), ("Ocean_Fish_", Surface.Glaze, 1f, 0.5f),
            // Candy Forest
            ("Candy_Frosting", Surface.Glaze, 40f, 1f), ("Candy_Gingerbread", Surface.Grain, 3f, 1.5f), ("Candy_Stick", Surface.Glaze, 1.5f, 0.6f),
            ("Candy_Icing", Surface.Glaze, 1.5f, 0.8f), ("Candy_Stripe", Surface.Glaze, 1.5f, 0.6f), ("Candy_Pink", Surface.Glaze, 1.5f, 0.6f),
            ("Candy_Mint", Surface.Glaze, 1.5f, 0.6f), ("Candy_Lemon", Surface.Glaze, 1.5f, 0.6f), ("Candy_Grape", Surface.Glaze, 1.5f, 0.6f),
            ("Candy_Orange", Surface.Glaze, 1.5f, 0.6f), ("Candy_CottonCandy", Surface.Organic, 1f, 0.6f),
            // Clockwork Moon
            ("Moon_Dust", Surface.Grain, 45f, 1.2f), ("Moon_Rock", Surface.Stone, 2f, 1f), ("Moon_Brass", Surface.Metal, 2f, 0.8f),
            ("Moon_Tower", Surface.Brick, 3f, 1f), ("Moon_Iron", Surface.Metal, 2f, 1f), ("Moon_HotIron", Surface.Metal, 2f, 1f),
            ("Moon_ClockFace", Surface.Grain, 1f, 0.5f), ("Moon_Planet", Surface.Organic, 3f, 0.8f),
            // Crystal Caverns
            ("Cave_Floor", Surface.Stone, 40f, 1f), ("Cave_Rock", Surface.Stone, 3f, 1.2f), ("Cave_Crystal_", Surface.Crystal, 1f, 0.8f),
            ("Cave_Gem_", Surface.Crystal, 1f, 0.8f), ("Cave_HeartCrystal", Surface.Crystal, 1f, 0.8f),
            ("Cave_MushroomCap", Surface.Organic, 1f, 0.8f), ("Cave_MushroomStem", Surface.Grain, 1f, 0.6f),
            // Upside-Down City
            ("City_Roof", Surface.Grain, 4f, 1f), ("City_WallA", Surface.Brick, 4f, 0.8f), ("City_WallB", Surface.Brick, 4f, 0.8f),
            ("City_Bridge", Surface.Bark, 3f, 1f), ("City_Planter", Surface.Brick, 1f, 0.8f), ("City_Garden", Surface.Organic, 2f, 1f),
            // Start Room
            ("Lobby_Floor", Surface.Bark, 4f, 0.8f), ("Lobby_Wall", Surface.Grain, 3f, 0.6f), ("Lobby_Wood", Surface.Bark, 1f, 1f),
            ("Lobby_Rug", Surface.Organic, 2f, 0.6f), ("Lobby_Blanket", Surface.Glaze, 1f, 0.6f), ("Lobby_Sheet", Surface.Glaze, 1f, 0.4f),
            ("Lobby_Sign", Surface.Grain, 1f, 0.5f), ("Lobby_Book_", Surface.Grain, 1f, 0.5f),
            // Artifacts
            ("Artifact_Bone", Surface.Grain, 1f, 0.8f), ("Artifact_Iron", Surface.Metal, 1.5f, 1f), ("Artifact_Gravestone", Surface.Stone, 1f, 1.2f),
            ("Artifact_Marble", Surface.Stone, 1f, 0.4f), ("Artifact_Gold", Surface.Metal, 1f, 0.4f), ("Artifact_Moss", Surface.Organic, 1f, 1.2f),
            ("Artifact_Wood", Surface.Bark, 1f, 1.2f), ("Artifact_Pink", Surface.Glaze, 1f, 0.6f), ("Artifact_Mint", Surface.Glaze, 1f, 0.6f),
            ("Artifact_Sky", Surface.Glaze, 1f, 0.6f), ("Artifact_Lemon", Surface.Glaze, 1f, 0.6f),
        };

        /// <summary>Applies the surface listed for <paramref name="mat"/>'s name in <see cref="Assignments"/>, if any.</summary>
        public static void ApplyAssigned(Material mat)
        {
            foreach (var a in Assignments)
            {
                bool match = a.name.EndsWith("_") ? mat.name.StartsWith(a.name) : mat.name == a.name;
                if (match)
                {
                    Surfaced(mat, a.surface, a.tiling, a.bump);
                    return;
                }
            }
        }

        const string TriplanarShaderName = "DreamStudio/TriplanarLit";

        /// <summary>
        /// The world-space texturing shader, or null if it is missing or failed to compile, in which
        /// case materials stay on URP Lit and use the meshes' own UVs.
        /// </summary>
        static Shader TriplanarShader()
        {
            var shader = Shader.Find(TriplanarShaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
            {
                if (!s_WarnedNoTriplanar)
                    Debug.LogWarning($"[DreamTextures] '{TriplanarShaderName}' is missing or has errors; using URP Lit with UV texturing instead.");
                s_WarnedNoTriplanar = true;
                return null;
            }
            return shader;
        }

        static bool s_WarnedNoTriplanar;

        /// <summary>Real-world size (meters) of one repeat of each surface when projected by the triplanar shader.</summary>
        static float MetersPerTile(Surface surface)
        {
            switch (surface)
            {
                case Surface.Bark: return 1.2f;
                case Surface.Stone: return 2.5f;
                case Surface.Sand: return 3f;
                case Surface.Metal: return 1.2f;
                case Surface.Brick: return 2.4f;
                case Surface.Crystal: return 1f;
                case Surface.Glaze: return 1.5f;
                case Surface.Organic: return 1.5f;
                default: return 2.5f;
            }
        }

        /// <summary>
        /// Gives a Lit material a detail texture and normal map. With the triplanar shader the texture is
        /// projected at a fixed real-world size (smaller for the finely detailed artifacts); otherwise it
        /// follows the mesh UVs, repeated <paramref name="tiling"/> times.
        /// </summary>
        public static Material Surfaced(Material mat, Surface surface, float tiling, float bumpScale = 1f)
        {
            var triplanar = TriplanarShader();
            if (triplanar != null)
            {
                mat.shader = triplanar;
                float meters = MetersPerTile(surface) * (mat.name.StartsWith("Artifact_") ? 0.4f : 1f);
                mat.SetFloat("_WorldScale", 1f / meters);
                mat.SetFloat("_BlendSharpness", 4f);
            }
            var (albedo, normal) = Get(surface);
            mat.SetTexture("_BaseMap", albedo);
            mat.SetTextureScale("_BaseMap", Vector2.one * tiling);
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", bumpScale);
            mat.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static (Texture2D albedo, Texture2D normal) Get(Surface surface)
        {
            string albedoPath = $"{Dir}/{surface}_v{Version}_Albedo.png";
            string normalPath = $"{Dir}/{surface}_v{Version}_Normal.png";
            if (!File.Exists(albedoPath) || !File.Exists(normalPath))
                Generate(surface, albedoPath, normalPath);
            return (AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath), AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        }

        // ---------- Generation ----------

        static void Generate(Surface surface, string albedoPath, string normalPath)
        {
            DreamBuildUtils.EnsureFolder(Dir);
            var height = new float[Size * Size];
            var albedo = new Color[Size * Size];
            float bump = 4f;

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = (float)x / Size, v = (float)y / Size;
                Sample(surface, u, v, out float h, out Color c, ref bump);
                height[y * Size + x] = h;
                c.a = 1f;
                albedo[y * Size + x] = c;
            }

            var normals = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = H(height, x + 1, y) - H(height, x - 1, y);
                float dy = H(height, x, y + 1) - H(height, x, y - 1);
                var n = new Vector3(-dx * bump, -dy * bump, 1f).normalized;
                normals[y * Size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }

            Write(albedoPath, albedo, false);
            Write(normalPath, normals, true);
        }

        static float H(float[] height, int x, int y) => height[Mod(y, Size) * Size + Mod(x, Size)];

        static void Write(string path, Color[] pixels, bool isNormal)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, isNormal);
            tex.SetPixels(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !isNormal;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 2;
            importer.maxTextureSize = Size;
            importer.SaveAndReimport();
        }

        /// <summary>Height (0..1) and detail colour of one surface at texture coordinate (u, v).</summary>
        static void Sample(Surface surface, float u, float v, out float h, out Color c, ref float bump)
        {
            switch (surface)
            {
                case Surface.Bark:
                {
                    float warp = Fbm(u, v, 3, 3, 4, 11) * 0.35f;
                    float groove = 1f - Mathf.Abs(Noise((u + warp) * 14f, v * 2f, 14, 2, 12));
                    float fibre = Fbm(u, v, 48, 4, 2, 13) * 0.5f + 0.5f;
                    h = groove * 0.8f + fibre * 0.2f;
                    c = Grey(0.5f + 0.38f * groove + 0.12f * fibre);
                    bump = 7f;
                    break;
                }
                case Surface.Stone:
                {
                    Worley(u, v, 6, 21, out float f1, out float f2, out int cell);
                    float crack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((f2 - f1) / 0.09f));
                    float grain = Fbm(u, v, 16, 16, 4, 22) * 0.5f + 0.5f;
                    float shade = 0.8f + 0.2f * Hash01(cell, 23);
                    h = crack * (0.75f + 0.25f * grain);
                    c = Grey(shade * (0.55f + 0.45f * crack) * (0.82f + 0.18f * grain));
                    bump = 6f;
                    break;
                }
                case Surface.Sand:
                {
                    float warp = Fbm(u, v, 3, 3, 3, 31) * 0.6f;
                    float ripple = 0.5f + 0.5f * Mathf.Sin((v * 10f + warp) * Mathf.PI * 2f);
                    float grit = Fbm(u, v, 64, 64, 2, 32) * 0.5f + 0.5f;
                    h = ripple * 0.7f + grit * 0.3f;
                    c = Grey(0.8f + 0.12f * ripple + 0.08f * grit);
                    bump = 3f;
                    break;
                }
                case Surface.Metal:
                {
                    float streak = Noise(u * 64f, v * 2f, 64, 2, 41) * 0.5f + 0.5f;
                    float rust = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Fbm(u, v, 4, 4, 5, 42) - 0.05f) / 0.35f));
                    float pit = Fbm(u, v, 32, 32, 3, 43) * 0.5f + 0.5f;
                    h = 0.3f * streak + rust * 0.5f * pit;
                    var metal = Grey(0.7f + 0.2f * streak);
                    var rustColour = new Color(0.78f, 0.45f, 0.28f) * (0.7f + 0.3f * pit);
                    c = Color.Lerp(metal, rustColour, rust * 0.85f);
                    bump = 4f;
                    break;
                }
                case Surface.Brick:
                {
                    const int rows = 8, perRow = 4;
                    float ry = v * rows;
                    int row = Mathf.FloorToInt(ry);
                    float rx = u * perRow + (row % 2 == 0 ? 0f : 0.5f);
                    int col = Mathf.FloorToInt(rx);
                    float fx = rx - col, fy = ry - row;
                    float edge = Mathf.Min(Mathf.Min(fx, 1f - fx) * 2f, Mathf.Min(fy, 1f - fy) * 4f);
                    float face = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge - 0.08f) / 0.12f));
                    float grain = Fbm(u, v, 24, 24, 3, 51) * 0.5f + 0.5f;
                    float shade = 0.75f + 0.25f * Hash01(Mod(col, perRow) + row * 31, 52);
                    h = face * (0.85f + 0.15f * grain);
                    c = Color.Lerp(Grey(0.55f), Grey(shade * (0.85f + 0.15f * grain)), face);
                    bump = 5f;
                    break;
                }
                case Surface.Crystal:
                {
                    Worley(u, v, 5, 61, out float f1, out float f2, out int cell);
                    float edge = 1f - Mathf.Clamp01((f2 - f1) / 0.05f);
                    float facet = 0.65f + 0.35f * Hash01(cell, 62);
                    h = 1f - Mathf.Clamp01(f1 * 1.6f);
                    c = Grey(Mathf.Min(1f, facet + edge * 0.35f));
                    bump = 3f;
                    break;
                }
                case Surface.Glaze:
                {
                    float swirl = Fbm(u, v, 4, 4, 3, 71) * 0.5f + 0.5f;
                    float speck = Hash01(Mathf.FloorToInt(u * Size) + Mathf.FloorToInt(v * Size) * Size, 72) > 0.975f ? 1f : 0f;
                    h = 0.6f * swirl + 0.4f * speck;
                    c = Grey(Mathf.Min(1f, 0.86f + 0.08f * swirl + 0.14f * speck));
                    bump = 2f;
                    break;
                }
                case Surface.Organic:
                {
                    Worley(u, v, 10, 81, out float f1, out _, out int cell);
                    float clump = 1f - Mathf.Clamp01(f1 * 2.2f);
                    float fine = Fbm(u, v, 32, 32, 3, 82) * 0.5f + 0.5f;
                    h = clump * 0.7f + fine * 0.3f;
                    c = Grey((0.62f + 0.3f * clump) * (0.85f + 0.15f * fine) * (0.9f + 0.1f * Hash01(cell, 83)));
                    bump = 4f;
                    break;
                }
                default:
                {
                    float coarse = Fbm(u, v, 8, 8, 4, 91) * 0.5f + 0.5f;
                    float fine = Fbm(u, v, 64, 64, 2, 92) * 0.5f + 0.5f;
                    h = 0.6f * coarse + 0.4f * fine;
                    c = Grey(0.68f + 0.22f * coarse + 0.1f * fine);
                    bump = 3f;
                    break;
                }
            }
        }

        static Color Grey(float g) => new Color(g, g, g, 1f);

        // ---------- Tileable noise ----------

        static int Mod(int a, int m) => ((a % m) + m) % m;

        static int Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return h ^ (h >> 16);
            }
        }

        static float Hash01(int n, int seed) => (Hash(n, n * 7 + 3, seed) & 0xFFFF) / 65535f;

        static float Grad(int h, float x, float y)
        {
            switch (h & 7)
            {
                case 0: return x + y;
                case 1: return x - y;
                case 2: return -x + y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        /// <summary>Gradient noise in roughly [-1, 1] that repeats every (periodX, periodY) units.</summary>
        static float Noise(float x, float y, int periodX, int periodY, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float sx = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
            float sy = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);
            int ax = Mod(x0, periodX), bx = Mod(x0 + 1, periodX);
            int ay = Mod(y0, periodY), by = Mod(y0 + 1, periodY);
            float n00 = Grad(Hash(ax, ay, seed), fx, fy);
            float n10 = Grad(Hash(bx, ay, seed), fx - 1f, fy);
            float n01 = Grad(Hash(ax, by, seed), fx, fy - 1f);
            float n11 = Grad(Hash(bx, by, seed), fx - 1f, fy - 1f);
            return Mathf.Lerp(Mathf.Lerp(n00, n10, sx), Mathf.Lerp(n01, n11, sx), sy);
        }

        /// <summary>Fractal noise over the unit square, tileable, roughly in [-1, 1].</summary>
        static float Fbm(float u, float v, int periodX, int periodY, int octaves, int seed)
        {
            float sum = 0f, amp = 0.6f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Noise(u * periodX, v * periodY, periodX, periodY, seed + o * 17);
                periodX *= 2;
                periodY *= 2;
                amp *= 0.5f;
            }
            return Mathf.Clamp(sum, -1f, 1f);
        }

        /// <summary>Tileable cellular noise: distance to the nearest (f1) and second-nearest (f2) feature point.</summary>
        static void Worley(float u, float v, int cells, int seed, out float f1, out float f2, out int nearestCell)
        {
            float x = u * cells, y = v * cells;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            f1 = f2 = float.MaxValue;
            nearestCell = 0;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int gx = cx + ox, gy = cy + oy;
                int id = Mod(gx, cells) + Mod(gy, cells) * cells;
                float px = gx + Hash01(id, seed);
                float py = gy + Hash01(id, seed + 1);
                float d = new Vector2(px - x, py - y).magnitude;
                if (d < f1)
                {
                    f2 = f1;
                    f1 = d;
                    nearestCell = id;
                }
                else if (d < f2)
                {
                    f2 = d;
                }
            }
        }
    }
}
