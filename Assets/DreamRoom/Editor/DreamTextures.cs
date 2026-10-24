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
        TreeBark, // realistic furrowed bark: tall plates split by deep fissures, with lichen (in colour)
        ForestFloor, // humus soil, moss, fallen leaves, twigs and pebbles (in colour)
        Seabed,   // rippled sand, silt, shell fragments and pebbles (in colour)
        Polyp,    // living coral: tiny cup-shaped polyps packed over the surface
        Maze,     // brain coral: winding ridges and valleys
        Planks,   // weathered ship's planking: boards with butt joints, dark seams and grain (in colour)
        Marble,   // polished marble: soft clouding crossed by wandering veins
    }

    /// <summary>
    /// Procedural, seamlessly tiling surface textures (a detail albedo plus a matching normal map),
    /// generated once into Textures/Surfaces. The albedo is mostly light grey so the material's
    /// base colour still decides the hue; it adds grime, grain and relief on top.
    /// </summary>
    public static class DreamTextures
    {
        const int Size = 256;

        /// <summary>Texture size per surface: realistic bark is seen up close on every trunk, so it gets more detail.</summary>
        static int SizeFor(Surface surface) => surface == Surface.TreeBark || surface == Surface.ForestFloor || surface == Surface.Seabed || surface == Surface.Planks ? 512 : Size;
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
            // Portal Room gates
            ("Gate_Bark", Surface.Bark, 1.5f, 1.2f), ("Gate_Moss", Surface.Organic, 2f, 1f), ("Gate_Reef", Surface.Stone, 2f, 1f),
            ("Gate_Coral_", Surface.Organic, 2f, 1.2f), ("Gate_Kelp", Surface.Organic, 1f, 0.8f), ("Gate_Shell", Surface.Glaze, 1f, 0.8f),
            ("Gate_Iron", Surface.Metal, 2f, 1f),
            ("Lantern_Iron", Surface.Metal, 2f, 1f), ("Gate_Brick", Surface.Brick, 4f, 0.8f), ("Gate_Concrete", Surface.Grain, 2f, 0.8f), ("Gate_Hydrant", Surface.Metal, 1f, 0.6f),
            // Haunted Forest
            ("Forest_Ground_Dream", Surface.ForestFloor, 60f, 1.2f), ("Forest_Bark_Dream", Surface.Bark, 1.5f, 1.2f),
            ("Forest_TreeBark", Surface.TreeBark, 2f, 1.3f), ("Forest_DeadBark", Surface.TreeBark, 2f, 1.5f),
            ("Forest_Rock", Surface.Stone, 2f, 1.2f), ("Forest_Moss", Surface.Organic, 2f, 1f), ("Forest_LogEnd", Surface.Grain, 1f, 0.8f),
            ("Forest_Stone_Dream", Surface.Stone, 1.5f, 1f), ("Forest_Leaves_", Surface.Organic, 2f, 1f),
            ("Forest_Blossom", Surface.Organic, 2f, 1f), ("Forest_Shadow", Surface.Grain, 2f, 1f), ("Forest_Flower_", Surface.Glaze, 1f, 0.6f),
            // Sunken Depths
            ("Ocean_Sand_Dream", Surface.Seabed, 45f, 1f), ("Ocean_Rock_Dream", Surface.Stone, 2f, 1f), ("Ocean_Kelp_Dream", Surface.Organic, 1f, 0.8f),
            ("Ocean_Temple_Dream", Surface.Stone, 3f, 1f), ("Ocean_Wreck_Dream", Surface.Bark, 2f, 1.2f), ("Ocean_Coral_", Surface.Organic, 2f, 1.2f),
            ("Reef_Coral_", Surface.Polyp, 1f, 1f), ("Reef_Brain_", Surface.Maze, 1f, 1.2f), ("Reef_Sponge_", Surface.Organic, 1f, 1f),
            ("Reef_Algae", Surface.Organic, 1f, 1f), ("Reef_Starfish", Surface.Grain, 1f, 1f), ("Reef_Urchin", Surface.Grain, 1f, 0.6f),
            ("Ocean_Clam", Surface.Glaze, 2f, 0.8f),
            ("Wreck_Planks", Surface.Planks, 1f, 1.2f), ("Wreck_Timber", Surface.Bark, 1f, 1.2f), ("Wreck_Iron", Surface.Metal, 1f, 1f), ("Wreck_Algae", Surface.Organic, 1f, 1f), ("Ocean_Fish_", Surface.Glaze, 1f, 0.5f),
            // Upside-Down City
            ("City_Roof", Surface.Grain, 4f, 1f), ("City_WallA", Surface.Brick, 4f, 0.8f), ("City_WallB", Surface.Brick, 4f, 0.8f),
            ("City_Bridge", Surface.Bark, 3f, 1f), ("City_Planter", Surface.Brick, 1f, 0.8f), ("City_Garden", Surface.Organic, 2f, 1f),
            // Start Room palace
            ("Palace_Marble", Surface.Marble, 1f, 0.15f), ("Palace_MarbleDark", Surface.Marble, 1f, 0.15f), ("Palace_MarbleCream", Surface.Marble, 1f, 0.15f),
            ("Palace_Wall", Surface.Grain, 1f, 0.25f), ("Palace_Panel", Surface.Grain, 1f, 0.25f), ("Palace_Velvet", Surface.Grain, 1f, 0.3f),
            ("Palace_Roof", Surface.Metal, 1f, 0.6f),
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
                case Surface.TreeBark: return 1.1f;
                case Surface.ForestFloor: return 2.5f;
                case Surface.Seabed: return 3f;
                case Surface.Polyp: return 0.35f;
                case Surface.Maze: return 0.6f;
                case Surface.Planks: return 2.2f;
                case Surface.Marble: return 1.6f;
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
            int size = SizeFor(surface);
            var height = new float[size * size];
            var albedo = new Color[size * size];
            float bump = 4f;

            if (surface == Surface.ForestFloor)
                PaintForestFloor(size, height, albedo, ref bump);
            else if (surface == Surface.Seabed)
                PaintSeabed(size, height, albedo, ref bump);
            else
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    Sample(surface, u, v, out float h, out Color c, ref bump);
                    height[y * size + x] = h;
                    c.a = 1f;
                    albedo[y * size + x] = c;
                }

            // Bump is tuned per pixel at the default size; keep slopes the same at higher resolutions.
            bump *= (float)size / Size;
            var normals = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = H(height, x + 1, y, size) - H(height, x - 1, y, size);
                float dy = H(height, x, y + 1, size) - H(height, x, y - 1, size);
                var n = new Vector3(-dx * bump, -dy * bump, 1f).normalized;
                normals[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }

            Write(albedoPath, albedo, false, size);
            Write(normalPath, normals, true, size);
        }

        static float H(float[] height, int x, int y, int size) => height[Mod(y, size) * size + Mod(x, size)];

        static void Write(string path, Color[] pixels, bool isNormal, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, isNormal);
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
            importer.maxTextureSize = size;
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
                case Surface.TreeBark:
                {
                    // Long bark ridges split by deep, interlacing furrows: the furrows follow where stretched,
                    // gently warped noise crosses zero, so they wander up the trunk, merge and branch. Ridges are
                    // rounded with steep walls, streaked with fibres, and carry patches of grey-green lichen.
                    float warp = Fbm(u, v, 3, 2, 3, 101) * 0.1f;
                    float n = Noise((u + warp) * 12f, v * 2f, 12, 2, 102) + 0.5f * Noise((u + warp) * 24f, v * 5f, 24, 5, 108);
                    float ridgeSide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Abs(n) - 0.03f) / 0.14f));
                    float ridgeTop = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Abs(n) / 0.55f));
                    float fibre = Fbm(u, v, 64, 6, 3, 103) * 0.5f + 0.5f;
                    float grit = Fbm(u, v, 32, 32, 3, 104) * 0.5f + 0.5f;
                    float tone = Fbm(u, v, 6, 3, 3, 106) * 0.5f + 0.5f;
                    h = ridgeSide * (0.75f + 0.15f * ridgeTop + 0.1f * fibre);

                    var face = Color.Lerp(new Color(0.28f, 0.24f, 0.2f), new Color(0.54f, 0.49f, 0.44f),
                        0.45f * tone + 0.25f * fibre + 0.15f * grit + 0.15f * ridgeTop);
                    var inner = Color.Lerp(new Color(0.05f, 0.04f, 0.035f), new Color(0.16f, 0.115f, 0.085f), Smooth(ridgeSide / 0.4f));
                    c = Color.Lerp(inner, face, Smooth((ridgeSide - 0.35f) / 0.55f));
                    float lichen = Smooth((Fbm(u, v, 6, 3, 4, 107) - 0.25f) / 0.3f) * Smooth((ridgeSide - 0.7f) / 0.3f);
                    c = Color.Lerp(c, new Color(0.56f, 0.6f, 0.5f) * (0.85f + 0.15f * grit), lichen * 0.55f);
                    bump = 8f;
                    break;
                }
                case Surface.Marble:
                {
                    // Bold veins where warped bands cross zero, finer veins crossing them, over soft clouding.
                    float warp = Fbm(u, v, 3, 3, 4, 171) * 0.8f;
                    float vein = 1f - Smooth(Mathf.Abs(Mathf.Sin((u * 3f + v * 2f + warp) * Mathf.PI)) / 0.06f);
                    float fine = 1f - Smooth(Mathf.Abs(Mathf.Sin((u * 7f - v * 5f + Fbm(u, v, 6, 6, 3, 172) * 1.2f) * Mathf.PI)) / 0.035f);
                    float cloud = Fbm(u, v, 4, 4, 4, 173) * 0.5f + 0.5f;
                    c = Grey(Mathf.Clamp01(0.86f + 0.12f * cloud - 0.32f * vein - 0.12f * fine));
                    h = 0.5f + 0.02f * cloud;
                    bump = 0.5f;
                    break;
                }
                case Surface.Planks:
                {
                    // Boards running along u, ten to a tile, each row broken by butt joints at its own places,
                    // with dark seams, fine grain streaks, and water-bleached grey wood stained in patches.
                    const int rows = 10;
                    float ry = v * rows;
                    int row = Mathf.FloorToInt(ry);
                    float fy = ry - row;
                    int boards = 2 + Mathf.FloorToInt(Hash01(Mod(row, rows), 161) * 2f);
                    float bx = u * boards + Hash01(Mod(row, rows), 162) * boards;
                    int board = Mathf.FloorToInt(bx);
                    float fx = bx - board;
                    int id = Mod(board, boards) + Mod(row, rows) * 7;
                    float seamAcross = Smooth(Mathf.Min(fy, 1f - fy) / 0.07f);
                    float seamAlong = Smooth(Mathf.Min(fx, 1f - fx) / boards / 0.004f);
                    float seam = Mathf.Min(seamAcross, seamAlong);
                    float grain = Fbm(u, v, 4, 160, 3, 164) * 0.5f + 0.5f;
                    float stain = Fbm(u, v, 4, 4, 4, 165) * 0.5f + 0.5f;
                    var wood = Color.Lerp(new Color(0.34f, 0.28f, 0.22f), new Color(0.56f, 0.53f, 0.48f),
                        0.4f * Hash01(id, 166) + 0.35f * grain + 0.25f * stain);
                    c = Color.Lerp(new Color(0.07f, 0.06f, 0.05f), wood, seam);
                    // Two nail heads at each butt joint.
                    float nail = Mathf.Min(fx, 1f - fx) * 2.2f / boards < 0.012f && Mathf.Abs(Mathf.Abs(fy - 0.5f) - 0.25f) < 0.06f ? 1f : 0f;
                    c = Color.Lerp(c, new Color(0.25f, 0.18f, 0.14f), nail * 0.8f);
                    h = seam * (0.85f + 0.1f * grain) + 0.05f * stain + nail * 0.05f;
                    bump = 6f;
                    break;
                }
                case Surface.Polyp:
                {
                    // Packed coral polyps: each cell a shallow cup with a raised rim.
                    Worley(u, v, 22, 141, out float f1, out float f2, out int cell);
                    float rim = Smooth(1f - (f2 - f1) / 0.12f);
                    float cup = Smooth(f1 / 0.45f);
                    float grain = Fbm(u, v, 32, 32, 2, 142) * 0.5f + 0.5f;
                    h = 0.6f * cup + 0.4f * rim;
                    c = Grey((0.62f + 0.3f * cup + 0.08f * rim) * (0.9f + 0.1f * grain) * (0.92f + 0.08f * Hash01(cell, 143)));
                    bump = 5f;
                    break;
                }
                case Surface.Maze:
                {
                    // Brain coral: the contours of warped noise make winding, branching ridges.
                    float n = Fbm(u, v, 5, 5, 3, 151);
                    float ridge = Mathf.Abs(Mathf.Sin(n * 18f));
                    float grain = Fbm(u, v, 48, 48, 2, 152) * 0.5f + 0.5f;
                    h = Smooth(ridge / 0.8f);
                    c = Grey((0.55f + 0.4f * h) * (0.9f + 0.1f * grain));
                    bump = 6f;
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

        static float Smooth(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        // ---------- Painting ----------

        /// <summary>
        /// A pixel canvas for painting shapes (in pixel coordinates). A wrapping canvas tiles seamlessly; a
        /// clipped one keeps every stroke inside <see cref="clip"/>, for regions of an atlas.
        /// </summary>
        class Canvas
        {
            public int width, height;
            public Color[] color;
            public float[] bumps;      // optional height, for a normal map
            public bool wrap;
            public RectInt clip;

            public bool Index(int x, int y, out int i)
            {
                if (wrap)
                {
                    x = Mod(x, width);
                    y = Mod(y, height);
                }
                else if (x < clip.xMin || x >= clip.xMax || y < clip.yMin || y >= clip.yMax)
                {
                    i = -1;
                    return false;
                }
                i = y * width + x;
                return true;
            }

            public void Put(int x, int y, Color c, float h)
            {
                if (!Index(x, y, out int i))
                    return;
                c.a = 1f;
                color[i] = c;
                if (bumps != null)
                    bumps[i] = h;
            }
        }

        static void Disc(Canvas c, Vector2 at, float radius, Color color, float h = 0f, float dome = 0f)
        {
            int x0 = Mathf.FloorToInt(at.x - radius), x1 = Mathf.CeilToInt(at.x + radius);
            int y0 = Mathf.FloorToInt(at.y - radius), y1 = Mathf.CeilToInt(at.y + radius);
            float r2 = radius * radius;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d2 = (x + 0.5f - at.x) * (x + 0.5f - at.x) + (y + 0.5f - at.y) * (y + 0.5f - at.y);
                if (d2 <= r2)
                    c.Put(x, y, color, h + dome * (1f - d2 / Mathf.Max(r2, 1e-4f)));
            }
        }

        /// <summary>A stroke from <paramref name="from"/> to <paramref name="to"/>, its radius going from r0 to r1, bending sideways by <paramref name="bend"/>.</summary>
        static void Stroke(Canvas c, Vector2 from, Vector2 to, float r0, float r1, Color c0, Color c1, float h = 0f, float bend = 0f)
        {
            var axis = to - from;
            var side = new Vector2(-axis.y, axis.x).normalized;
            int steps = Mathf.Max(2, Mathf.CeilToInt(axis.magnitude * 1.5f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                var p = from + axis * t + side * (bend * t * t);
                Disc(c, p, Mathf.Max(0.6f, Mathf.Lerp(r0, r1, t)), Color.Lerp(c0, c1, t), h);
            }
        }

        /// <summary>
        /// A pointed leaf from its stem end at <paramref name="at"/> along <paramref name="angle"/> (radians):
        /// darker edges, a lighter midrib, faint veins, and optional lobes along its sides (for fern pinnae).
        /// </summary>
        static void Leaf(Canvas c, Vector2 at, float angle, float length, float widthRatio, Color tone, float h = 0f, int lobes = 0)
        {
            var axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var side = new Vector2(-axis.y, axis.x);
            float halfWidth = length * widthRatio * 0.5f;
            var tip = at + axis * length;
            int x0 = Mathf.FloorToInt(Mathf.Min(at.x, tip.x) - halfWidth), x1 = Mathf.CeilToInt(Mathf.Max(at.x, tip.x) + halfWidth);
            int y0 = Mathf.FloorToInt(Mathf.Min(at.y, tip.y) - halfWidth), y1 = Mathf.CeilToInt(Mathf.Max(at.y, tip.y) + halfWidth);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var d = new Vector2(x + 0.5f, y + 0.5f) - at;
                float s = Vector2.Dot(d, axis) / length;
                if (s <= 0f || s >= 1f)
                    continue;
                float t = Vector2.Dot(d, side);
                float w = halfWidth * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(s, 0.8f)), 0.75f);
                if (lobes > 0)
                    w *= 0.72f + 0.28f * Mathf.Abs(Mathf.Sin(s * lobes * Mathf.PI));
                if (Mathf.Abs(t) >= w)
                    continue;
                float across = Mathf.Abs(t) / w;
                float shade = (1f - 0.28f * across * across) * (0.85f + 0.15f * s);
                bool midrib = Mathf.Abs(t) < Mathf.Max(0.8f, halfWidth * 0.07f);
                if (midrib)
                    shade *= 1.15f;
                shade *= 0.96f + 0.04f * Mathf.Abs(Mathf.Sin((s * 9f - across * 2.2f) * Mathf.PI));
                c.Put(x, y, tone * shade, h + 0.08f * (1f - across * across) + (midrib ? 0.03f : 0f));
            }
        }

        static float R(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        static Color Pick(System.Random rng, params Color[] colors) => colors[rng.Next(colors.Length)];

        // ---------- Forest floor ----------

        /// <summary>
        /// Dark humus soil with patches of moss, strewn with fallen leaves in browns, ochres and rusts (older
        /// ones underneath darker), twigs and the odd pebble. Seamlessly tiling, in colour, with height for relief.
        /// </summary>
        static void PaintForestFloor(int size, float[] height, Color[] albedo, ref float bump)
        {
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float grain = Fbm(u, v, 24, 24, 4, 121) * 0.5f + 0.5f;
                float clod = Fbm(u, v, 6, 6, 4, 122) * 0.5f + 0.5f;
                var soil = Color.Lerp(new Color(0.13f, 0.095f, 0.07f), new Color(0.3f, 0.22f, 0.15f), 0.55f * clod + 0.45f * grain);
                float moss = Smooth((Fbm(u, v, 4, 4, 4, 123) - 0.18f) / 0.3f);
                float mossFine = Fbm(u, v, 48, 48, 2, 124) * 0.5f + 0.5f;
                soil = Color.Lerp(soil, Color.Lerp(new Color(0.15f, 0.23f, 0.07f), new Color(0.32f, 0.42f, 0.14f), mossFine), moss * 0.7f);
                soil.a = 1f;
                albedo[y * size + x] = soil;
                height[y * size + x] = 0.15f * clod + 0.1f * grain + 0.12f * moss * mossFine;
            }

            var canvas = new Canvas { width = size, height = size, color = albedo, bumps = height, wrap = true };
            var rng = new System.Random(125);
            float px = size;

            for (int i = 0; i < 45; i++)
            {
                var from = new Vector2(R(rng, 0f, px), R(rng, 0f, px));
                float a = R(rng, 0f, Mathf.PI * 2f);
                var to = from + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R(rng, 0.04f, 0.14f) * px;
                var bark = Color.Lerp(new Color(0.16f, 0.11f, 0.07f), new Color(0.3f, 0.22f, 0.14f), R(rng, 0f, 1f));
                Stroke(canvas, from, to, px * 0.0035f, px * 0.002f, bark, bark * 0.9f, 0.45f, R(rng, -4f, 4f));
            }

            const int leaves = 560;
            for (int i = 0; i < leaves; i++)
            {
                float depth = (float)i / leaves;
                var tone = Pick(rng, new Color(0.42f, 0.26f, 0.12f), new Color(0.55f, 0.36f, 0.14f), new Color(0.36f, 0.2f, 0.1f),
                    new Color(0.3f, 0.22f, 0.12f), new Color(0.5f, 0.45f, 0.25f));
                Leaf(canvas, new Vector2(R(rng, 0f, px), R(rng, 0f, px)), R(rng, 0f, Mathf.PI * 2f), R(rng, 0.03f, 0.065f) * px,
                    R(rng, 0.4f, 0.6f), tone * (0.6f + 0.4f * depth), 0.35f + 0.35f * depth);
            }

            for (int i = 0; i < 35; i++)
            {
                var stone = Color.Lerp(new Color(0.32f, 0.3f, 0.28f), new Color(0.5f, 0.48f, 0.45f), R(rng, 0f, 1f));
                Disc(canvas, new Vector2(R(rng, 0f, px), R(rng, 0f, px)), R(rng, 0.003f, 0.009f) * px, stone, 0.5f, 0.3f);
            }
            bump = 5f;
        }

        // ---------- Understory atlas ----------

        /// <summary>Where each plant sits in <see cref="UnderstoryAtlas"/>, in UV space.</summary>
        public static class Understory
        {
            public static readonly Rect Grass = new Rect(0f, 0.5f, 0.5f, 0.5f);
            public static readonly Rect Fern = new Rect(0.5f, 0.5f, 0.25f, 0.5f);
            public static readonly Rect Flowers = new Rect(0.75f, 0.5f, 0.25f, 0.5f);
            public static readonly Rect Litter = new Rect(0f, 0f, 0.5f, 0.5f);
            public static readonly Rect Moss = new Rect(0.5f, 0.25f, 0.25f, 0.25f);
            public static readonly Rect Clover = new Rect(0.75f, 0.25f, 0.25f, 0.25f);
            public static readonly Rect Sprout = new Rect(0.5f, 0f, 0.25f, 0.25f);
            public static readonly Rect SeedGrass = new Rect(0.75f, 0f, 0.25f, 0.25f);
        }

        const int AtlasSize = 1024;

        /// <summary>
        /// One alpha-cut texture holding every small forest plant, so they can all share one material:
        /// a clump of grass blades, a fern frond, wildflowers (daisies, buttercups, bluebells, campion), a
        /// patch of fallen leaves, a patch of moss, clover, a sprouting plant and grass gone to seed.
        /// </summary>
        public static Texture2D UnderstoryAtlas()
        {
            string path = $"{Dir}/Understory_v{Version}.png";
            if (File.Exists(path))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            DreamBuildUtils.EnsureFolder(Dir);
            const int n = AtlasSize;
            var px = new Color[n * n];
            var fill = new Color(0.25f, 0.33f, 0.14f, 0f);
            for (int i = 0; i < px.Length; i++)
                px[i] = fill;
            var rng = new System.Random(5150);

            PaintGrass(Region(px, Understory.Grass), rng);
            PaintFern(Region(px, Understory.Fern), rng);
            PaintFlowers(Region(px, Understory.Flowers), rng);
            PaintLitter(Region(px, Understory.Litter), rng);
            PaintMoss(Region(px, Understory.Moss), rng);
            PaintClover(Region(px, Understory.Clover), rng);
            PaintSprout(Region(px, Understory.Sprout), rng);
            PaintSeedGrass(Region(px, Understory.SeedGrass), rng);

            WriteCutout(path, px, n);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>A canvas clipped to one atlas region, with a small transparent margin so mipmaps don't bleed.</summary>
        static Canvas Region(Color[] px, Rect uv)
        {
            const int margin = 4;
            var rect = new RectInt(Mathf.RoundToInt(uv.x * AtlasSize) + margin, Mathf.RoundToInt(uv.y * AtlasSize) + margin,
                Mathf.RoundToInt(uv.width * AtlasSize) - margin * 2, Mathf.RoundToInt(uv.height * AtlasSize) - margin * 2);
            return new Canvas { width = AtlasSize, height = AtlasSize, color = px, clip = rect };
        }

        static void WriteCutout(string path, Color[] px, int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = 0.45f;
            importer.maxTextureSize = n;
            importer.SaveAndReimport();
        }

        /// <summary>Tapering grass blades rising from the bottom edge, bending as they go; a few dry ones.</summary>
        static void PaintGrass(Canvas c, System.Random rng)
        {
            var r = c.clip;
            const int blades = 80;
            for (int b = 0; b < blades; b++)
            {
                float depth = (float)b / blades;
                float x = r.xMin + r.width * R(rng, 0.12f, 0.88f);
                float h = r.height * R(rng, 0.45f, 0.97f);
                float w = R(rng, 5f, 11f);
                bool dry = rng.NextDouble() < 0.15;
                var root = dry ? new Color(0.35f, 0.32f, 0.16f) : new Color(0.12f, 0.22f, 0.06f);
                var tip = dry ? new Color(0.62f, 0.56f, 0.32f) : Color.Lerp(new Color(0.36f, 0.5f, 0.16f), new Color(0.5f, 0.6f, 0.22f), R(rng, 0f, 1f));
                float shade = 0.7f + 0.3f * depth;
                Stroke(c, new Vector2(x, r.yMin + 1f), new Vector2(x + R(rng, -0.12f, 0.12f) * h, r.yMin + h), w * 0.5f, 0.6f,
                    root * shade, tip * shade, 0f, R(rng, -0.25f, 0.25f) * h);
            }
        }

        /// <summary>A fern frond: a curving stalk with paired, lobed leaflets, widest a third of the way up.</summary>
        static void PaintFern(Canvas c, System.Random rng)
        {
            var r = c.clip;
            var bottom = new Vector2(r.center.x, r.yMin + 2f);
            float length = r.height - 6f;
            const int pairs = 28;
            for (int i = 0; i < pairs; i++)
            {
                float t = 0.05f + 0.93f * i / (pairs - 1f);
                var at = bottom + new Vector2(Mathf.Sin(t * 2.2f) * r.width * 0.06f, t * length);
                float shape = t < 0.3f ? 0.55f + 0.45f * (t / 0.3f) : 1f - 0.88f * ((t - 0.3f) / 0.7f);
                float leaflet = r.width * 0.46f * shape;
                var tone = Color.Lerp(new Color(0.2f, 0.38f, 0.1f), new Color(0.34f, 0.52f, 0.17f), R(rng, 0f, 1f));
                for (int side = -1; side <= 1; side += 2)
                {
                    float angle = (90f - side * R(rng, 50f, 60f)) * Mathf.Deg2Rad;
                    Leaf(c, at, angle, leaflet, 0.3f, tone, 0f, 7);
                }
            }
            Stroke(c, bottom, bottom + new Vector2(Mathf.Sin(2.2f) * r.width * 0.06f, length), 3f, 0.8f,
                new Color(0.28f, 0.36f, 0.14f), new Color(0.32f, 0.45f, 0.16f), 0f, 0f);
        }

        /// <summary>Wildflowers on thin stems: daisies, buttercups, bluebells and pink campion.</summary>
        static void PaintFlowers(Canvas c, System.Random rng)
        {
            var r = c.clip;
            var stemColor = new Color(0.24f, 0.4f, 0.14f);
            for (int s = 0; s < 9; s++)
            {
                var from = new Vector2(r.xMin + r.width * R(rng, 0.15f, 0.85f), r.yMin + 1f);
                var top = new Vector2(from.x + R(rng, -0.15f, 0.15f) * r.width, r.yMin + r.height * R(rng, 0.45f, 0.92f));
                float bend = R(rng, -12f, 12f);
                Stroke(c, from, top, 2.2f, 1.3f, stemColor * 0.85f, stemColor, 0f, bend);
                for (int l = 0; l < 2; l++)
                {
                    var at = Vector2.Lerp(from, top, R(rng, 0.15f, 0.5f));
                    Leaf(c, at, (90f + (l == 0 ? -1f : 1f) * R(rng, 40f, 60f)) * Mathf.Deg2Rad, r.height * 0.06f, 0.25f, stemColor * 1.1f);
                }

                switch (rng.Next(4))
                {
                    case 0: // daisy
                        for (int p = 0; p < 12; p++)
                            Leaf(c, top, p * Mathf.PI / 6f, 22f, 0.32f, new Color(0.95f, 0.95f, 0.92f));
                        Disc(c, top, 5.5f, new Color(0.95f, 0.75f, 0.15f));
                        break;
                    case 1: // buttercup
                        for (int p = 0; p < 5; p++)
                        {
                            float a = p * Mathf.PI * 0.4f;
                            Disc(c, top + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6.5f, 6.5f, new Color(1f, 0.83f, 0.12f));
                        }
                        Disc(c, top, 3f, new Color(0.8f, 0.62f, 0.1f));
                        break;
                    case 2: // bluebells, hanging along the top of the stem
                        for (int p = 0; p < 4; p++)
                        {
                            var at = top + new Vector2(p * 7f * Mathf.Sign(bend + 0.01f), -p * 9f);
                            Leaf(c, at, (270f + R(rng, -20f, 20f)) * Mathf.Deg2Rad, 15f, 0.65f, new Color(0.45f, 0.4f, 0.85f));
                        }
                        break;
                    default: // campion
                        for (int p = 0; p < 5; p++)
                            Leaf(c, top, p * Mathf.PI * 0.4f + 0.3f, 12f, 0.65f, new Color(0.9f, 0.45f, 0.6f));
                        Disc(c, top, 2.2f, new Color(0.6f, 0.2f, 0.35f));
                        break;
                }
            }
        }

        /// <summary>A loose, ragged-edged pile of fallen leaves (autumn colours, older ones underneath) and twigs.</summary>
        static void PaintLitter(Canvas c, System.Random rng)
        {
            var r = c.clip;
            var center = r.center;
            float radius = r.width * 0.36f; // leaves reach past this; keep them clear of the region's edge
            for (int i = 0; i < 14; i++)
            {
                float a = R(rng, 0f, Mathf.PI * 2f);
                var from = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * R(rng, 0f, 0.6f);
                float b = R(rng, 0f, Mathf.PI * 2f);
                Stroke(c, from, from + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * R(rng, 0.15f, 0.3f) * r.width, 3f, 1.5f,
                    new Color(0.2f, 0.14f, 0.09f), new Color(0.26f, 0.19f, 0.12f), 0f, R(rng, -8f, 8f));
            }
            const int leaves = 230;
            for (int i = 0; i < leaves; i++)
            {
                float depth = (float)i / leaves;
                float a = R(rng, 0f, Mathf.PI * 2f);
                var at = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * Mathf.Sqrt(R(rng, 0f, 1f));
                var tone = Pick(rng, new Color(0.55f, 0.32f, 0.12f), new Color(0.62f, 0.42f, 0.15f), new Color(0.45f, 0.22f, 0.1f),
                    new Color(0.35f, 0.25f, 0.12f), new Color(0.6f, 0.52f, 0.25f));
                Leaf(c, at, R(rng, 0f, Mathf.PI * 2f), R(rng, 0.07f, 0.12f) * r.width, R(rng, 0.4f, 0.6f), tone * (0.55f + 0.45f * depth));
            }
        }

        /// <summary>A soft cushion of moss with a ragged, wandering edge and fine speckle.</summary>
        static void PaintMoss(Canvas c, System.Random rng)
        {
            var r = c.clip;
            var center = r.center;
            float radius = r.width * 0.47f;
            for (int y = r.yMin; y < r.yMax; y++)
            for (int x = r.xMin; x < r.xMax; x++)
            {
                var d = new Vector2(x + 0.5f, y + 0.5f) - center;
                float angle = Mathf.Atan2(d.y, d.x);
                float edge = 0.72f + 0.2f * Mathf.PerlinNoise(Mathf.Cos(angle) * 1.6f + 3f, Mathf.Sin(angle) * 1.6f + 3f)
                    + 0.08f * Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
                if (d.magnitude / radius > edge)
                    continue;
                float speck = Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.6f + Mathf.PerlinNoise(x * 0.9f + 50f, y * 0.9f) * 0.4f;
                float patch = Mathf.PerlinNoise(x * 0.03f + 10f, y * 0.03f);
                var moss = Color.Lerp(new Color(0.14f, 0.24f, 0.06f), new Color(0.38f, 0.5f, 0.15f), speck * 0.7f + patch * 0.3f);
                c.Put(x, y, moss, 0f);
            }
        }

        /// <summary>Clover: three-leaved sprigs with pale chevrons, and a few white flower heads.</summary>
        static void PaintClover(Canvas c, System.Random rng)
        {
            var r = c.clip;
            for (int i = 0; i < 20; i++)
            {
                float a = R(rng, 0f, Mathf.PI * 2f);
                var at = r.center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r.width * 0.36f * Mathf.Sqrt(R(rng, 0f, 1f));
                float leaflet = R(rng, 9f, 14f);
                float spin = R(rng, 0f, Mathf.PI * 2f);
                var tone = Color.Lerp(new Color(0.16f, 0.34f, 0.1f), new Color(0.26f, 0.46f, 0.16f), R(rng, 0f, 1f));
                for (int k = 0; k < 3; k++)
                {
                    float b = spin + k * Mathf.PI * 2f / 3f;
                    var dir = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                    Disc(c, at + dir * leaflet * 0.85f, leaflet, tone);
                    Disc(c, at + dir * leaflet * 0.7f, leaflet * 0.3f, tone * 1.35f);
                }
            }
            for (int f = 0; f < 4; f++)
            {
                var at = r.center + new Vector2(R(rng, -0.3f, 0.3f), R(rng, -0.3f, 0.3f)) * r.width;
                for (int k = 0; k < 14; k++)
                    Disc(c, at + new Vector2(R(rng, -7f, 7f), R(rng, -7f, 7f)), 3f, new Color(0.95f, 0.93f, 0.88f));
            }
        }

        /// <summary>A young leafy plant seen from the side: a few stems with broad, upturned leaves.</summary>
        static void PaintSprout(Canvas c, System.Random rng)
        {
            var r = c.clip;
            var bottom = new Vector2(r.center.x, r.yMin + 1f);
            for (int s = 0; s < 4; s++)
            {
                var top = bottom + new Vector2(R(rng, -0.3f, 0.3f) * r.width, r.height * R(rng, 0.5f, 0.85f));
                var stem = new Color(0.28f, 0.42f, 0.15f);
                Stroke(c, bottom, top, 2.5f, 1.5f, stem * 0.85f, stem, 0f, R(rng, -8f, 8f));
                int leaves = 3 + rng.Next(3);
                for (int l = 0; l < leaves; l++)
                {
                    var at = Vector2.Lerp(bottom, top, 0.35f + 0.65f * l / Mathf.Max(1, leaves - 1));
                    float angle = (90f + (l % 2 == 0 ? -1f : 1f) * R(rng, 35f, 70f)) * Mathf.Deg2Rad;
                    var tone = Color.Lerp(new Color(0.22f, 0.42f, 0.11f), new Color(0.4f, 0.6f, 0.2f), R(rng, 0f, 1f));
                    Leaf(c, at, angle, r.height * R(rng, 0.18f, 0.28f), 0.45f, tone);
                }
            }
        }

        /// <summary>Thin grass with a few tall stems gone to seed, their heads drooping a little.</summary>
        static void PaintSeedGrass(Canvas c, System.Random rng)
        {
            var r = c.clip;
            for (int b = 0; b < 30; b++)
            {
                float x = r.xMin + r.width * R(rng, 0.15f, 0.85f);
                float h = r.height * R(rng, 0.3f, 0.7f);
                var tone = Color.Lerp(new Color(0.3f, 0.42f, 0.14f), new Color(0.6f, 0.55f, 0.32f), R(rng, 0f, 1f));
                Stroke(c, new Vector2(x, r.yMin + 1f), new Vector2(x + R(rng, -0.1f, 0.1f) * h, r.yMin + h), 3f, 0.6f, tone * 0.7f, tone, 0f, R(rng, -0.2f, 0.2f) * h);
            }
            for (int s = 0; s < 5; s++)
            {
                var from = new Vector2(r.xMin + r.width * R(rng, 0.2f, 0.8f), r.yMin + 1f);
                var top = new Vector2(from.x + R(rng, -0.12f, 0.12f) * r.width, r.yMin + r.height * R(rng, 0.7f, 0.95f));
                Stroke(c, from, top, 1.6f, 1f, new Color(0.45f, 0.45f, 0.25f), new Color(0.6f, 0.55f, 0.32f), 0f, R(rng, -6f, 6f));
                Leaf(c, top - new Vector2(0f, r.height * 0.2f), (90f + R(rng, -15f, 15f)) * Mathf.Deg2Rad, r.height * 0.24f, 0.28f, new Color(0.7f, 0.62f, 0.4f));
            }
        }

        // ---------- Seabed ----------

        /// <summary>
        /// Rippled sand: fine grain under soft, wandering wave ripples, darker patches of silt, a faint film
        /// of algae, and scattered shell fragments and pebbles. Seamlessly tiling, in colour, with relief.
        /// </summary>
        static void PaintSeabed(int size, float[] height, Color[] albedo, ref float bump)
        {
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float warp = Fbm(u, v, 3, 3, 3, 131) * 0.18f;
                float ripple = 0.5f + 0.5f * Mathf.Sin((v * 9f + warp * 6f + Fbm(u, v, 6, 6, 2, 132) * 0.4f) * Mathf.PI * 2f);
                ripple = Mathf.Pow(ripple, 1.6f);
                float grain = Fbm(u, v, 64, 64, 2, 133) * 0.5f + 0.5f;
                float silt = Smooth((Fbm(u, v, 3, 3, 4, 134) - 0.15f) / 0.35f);
                float algae = Smooth((Fbm(u, v, 5, 5, 4, 135) - 0.3f) / 0.3f);
                var sand = Color.Lerp(new Color(0.66f, 0.6f, 0.47f), new Color(0.84f, 0.78f, 0.63f), 0.55f * ripple + 0.45f * grain);
                sand = Color.Lerp(sand, new Color(0.47f, 0.43f, 0.36f) * (0.9f + 0.2f * grain), silt * 0.55f);
                sand = Color.Lerp(sand, new Color(0.42f, 0.45f, 0.3f) * (0.9f + 0.2f * grain), algae * 0.3f);
                sand.a = 1f;
                albedo[y * size + x] = sand;
                height[y * size + x] = 0.45f * ripple + 0.1f * grain;
            }

            var canvas = new Canvas { width = size, height = size, color = albedo, bumps = height, wrap = true };
            var rng = new System.Random(136);
            float px = size;
            for (int i = 0; i < 140; i++)
            {
                var shell = Pick(rng, new Color(0.92f, 0.88f, 0.8f), new Color(0.88f, 0.76f, 0.7f), new Color(0.8f, 0.78f, 0.72f), new Color(0.7f, 0.6f, 0.5f));
                Leaf(canvas, new Vector2(R(rng, 0f, px), R(rng, 0f, px)), R(rng, 0f, Mathf.PI * 2f), R(rng, 0.008f, 0.022f) * px, R(rng, 0.5f, 0.9f), shell, 0.7f);
            }
            for (int i = 0; i < 60; i++)
            {
                var stone = Color.Lerp(new Color(0.3f, 0.29f, 0.27f), new Color(0.55f, 0.52f, 0.48f), R(rng, 0f, 1f));
                Disc(canvas, new Vector2(R(rng, 0f, px), R(rng, 0f, px)), R(rng, 0.002f, 0.007f) * px, stone, 0.6f, 0.3f);
            }
            bump = 4f;
        }

        // ---------- Reef atlas ----------

        /// <summary>Where each plant sits in <see cref="ReefAtlas"/>, in UV space.</summary>
        public static class Reef
        {
            public static readonly Rect Seagrass = new Rect(0f, 0.5f, 0.5f, 0.5f);
            public static readonly Rect KelpBlade = new Rect(0.5f, 0.5f, 0.25f, 0.5f);
            /// <summary>A solid strip of kelp stalk, to stretch along a stipe.</summary>
            public static readonly Rect KelpStipe = new Rect(0.5f + 8f / AtlasSize, 0.5f + 8f / AtlasSize, 6f / AtlasSize, 40f / AtlasSize);
            public static readonly Rect SeaWhip = new Rect(0.75f, 0.5f, 0.25f, 0.5f);
            public static readonly Rect Rubble = new Rect(0f, 0f, 0.5f, 0.5f);
            public static readonly Rect Algae = new Rect(0.5f, 0.25f, 0.25f, 0.25f);
            public static readonly Rect SeaFan = new Rect(0.75f, 0.25f, 0.25f, 0.25f);
            public static readonly Rect RedWeed = new Rect(0.5f, 0f, 0.25f, 0.25f);
            public static readonly Rect Wrack = new Rect(0.75f, 0f, 0.25f, 0.25f);
        }

        /// <summary>
        /// One alpha-cut texture holding the sea-bottom plants: eelgrass, a giant kelp blade (with a strip of
        /// its stalk), sea whips, a patch of shell and coral rubble, a patch of turf algae, a sea fan, bushy red
        /// seaweed and bladder wrack.
        /// </summary>
        public static Texture2D ReefAtlas()
        {
            string path = $"{Dir}/ReefAtlas_v{Version}.png";
            if (File.Exists(path))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            DreamBuildUtils.EnsureFolder(Dir);
            const int n = AtlasSize;
            var px = new Color[n * n];
            var fill = new Color(0.3f, 0.32f, 0.16f, 0f);
            for (int i = 0; i < px.Length; i++)
                px[i] = fill;
            var rng = new System.Random(6160);

            PaintSeagrass(Region(px, Reef.Seagrass), rng);
            PaintKelpBlade(Region(px, Reef.KelpBlade), rng);
            PaintSeaWhips(Region(px, Reef.SeaWhip), rng);
            PaintRubble(Region(px, Reef.Rubble), rng);
            PaintTurfAlgae(Region(px, Reef.Algae), rng);
            PaintSeaFan(Region(px, Reef.SeaFan), rng);
            PaintRedWeed(Region(px, Reef.RedWeed), rng);
            PaintWrack(Region(px, Reef.Wrack), rng);

            WriteCutout(path, px, n);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Long, narrow eelgrass ribbons swaying from the bottom edge, some browning at the tips.</summary>
        static void PaintSeagrass(Canvas c, System.Random rng)
        {
            var r = c.clip;
            const int blades = 60;
            for (int b = 0; b < blades; b++)
            {
                float depth = (float)b / blades;
                float x = r.xMin + r.width * R(rng, 0.15f, 0.85f);
                float h = r.height * R(rng, 0.5f, 0.97f);
                var root = new Color(0.2f, 0.3f, 0.1f);
                var tip = rng.NextDouble() < 0.25 ? new Color(0.5f, 0.45f, 0.22f) : Color.Lerp(new Color(0.32f, 0.45f, 0.15f), new Color(0.45f, 0.55f, 0.2f), R(rng, 0f, 1f));
                float shade = 0.7f + 0.3f * depth;
                Stroke(c, new Vector2(x, r.yMin + 1f), new Vector2(x + R(rng, -0.15f, 0.15f) * h, r.yMin + h), R(rng, 2.2f, 3.6f), 1.6f,
                    root * shade, tip * shade, 0f, R(rng, -0.35f, 0.35f) * h);
            }
        }

        /// <summary>
        /// A giant kelp blade: a gas bladder on a short stalk at the bottom, then a long, ruffled, golden-brown
        /// blade with lengthwise corrugations. A strip of plain stalk sits in the corner for stretching along stipes.
        /// </summary>
        static void PaintKelpBlade(Canvas c, System.Random rng)
        {
            var r = c.clip;
            var stipe = new Color(0.4f, 0.32f, 0.14f);
            for (int y = r.yMin + 4; y < r.yMin + 48; y++)
            for (int x = r.xMin + 4; x < r.xMin + 14; x++)
                c.Put(x, y, stipe * (0.9f + 0.1f * Mathf.PerlinNoise(x * 0.5f, y * 0.2f)), 0f);

            float cx = r.center.x;
            Stroke(c, new Vector2(cx, r.yMin + 1f), new Vector2(cx, r.yMin + 26f), 3f, 3f, stipe, stipe);
            Disc(c, new Vector2(cx, r.yMin + 34f), 9f, new Color(0.5f, 0.42f, 0.18f));
            Disc(c, new Vector2(cx - 2f, r.yMin + 37f), 3f, new Color(0.62f, 0.55f, 0.28f));

            float bottom = r.yMin + 42f, top = r.yMax - 4f;
            float maxHalf = r.width * 0.28f;
            for (int y = Mathf.FloorToInt(bottom); y < top; y++)
            {
                float t = (y - bottom) / (top - bottom);
                // Irregular wrinkles along the edge, not a regular zigzag.
                float wrinkle = 0.78f + 0.12f * Mathf.PerlinNoise(t * 24f, 3.3f) + 0.1f * Mathf.PerlinNoise(t * 61f, 8.1f);
                float half = maxHalf * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.7f)), 0.6f) * wrinkle;
                float wave = (Mathf.PerlinNoise(t * 4f, 1.7f) - 0.5f) * r.width * 0.12f;
                for (int x = Mathf.FloorToInt(cx + wave - half); x <= cx + wave + half; x++)
                {
                    float across = (x - cx - wave) / Mathf.Max(half, 1f);
                    float corrugate = 0.88f + 0.12f * Mathf.Sin(across * 7f + t * 30f);
                    var tone = Color.Lerp(new Color(0.45f, 0.35f, 0.12f), new Color(0.6f, 0.5f, 0.2f), t * 0.6f + 0.4f * Mathf.PerlinNoise(x * 0.05f, y * 0.02f));
                    c.Put(x, y, tone * corrugate * (1f - 0.2f * across * across), 0f);
                }
            }
        }

        /// <summary>Sea whips: slender, sparsely branching gorgonian rods in purple and orange.</summary>
        static void PaintSeaWhips(Canvas c, System.Random rng)
        {
            var r = c.clip;
            var bottom = new Vector2(r.center.x, r.yMin + 1f);
            for (int s = 0; s < 7; s++)
            {
                var tone = s % 3 == 0 ? new Color(0.75f, 0.42f, 0.22f) : new Color(0.55f, 0.3f, 0.5f);
                var top = bottom + new Vector2(R(rng, -0.35f, 0.35f) * r.width, r.height * R(rng, 0.55f, 0.97f));
                float bend = R(rng, -14f, 14f);
                Stroke(c, bottom, top, 3.5f, 1.6f, tone * 0.8f, tone, 0f, bend);
                if (rng.NextDouble() < 0.6)
                {
                    var from = Vector2.Lerp(bottom, top, R(rng, 0.3f, 0.6f));
                    Stroke(c, from, from + new Vector2(R(rng, -0.25f, 0.25f) * r.width, r.height * R(rng, 0.2f, 0.35f)), 2.4f, 1.4f, tone * 0.9f, tone, 0f, R(rng, -8f, 8f));
                }
            }
        }

        /// <summary>A ragged patch of shell fragments, bits of dead coral and pebbles.</summary>
        static void PaintRubble(Canvas c, System.Random rng)
        {
            var r = c.clip;
            var center = r.center;
            float radius = r.width * 0.38f;
            Vector2 Spot() { float a = R(rng, 0f, Mathf.PI * 2f); return center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * Mathf.Sqrt(R(rng, 0f, 1f)); }
            for (int i = 0; i < 40; i++)
            {
                var from = Spot();
                float a = R(rng, 0f, Mathf.PI * 2f);
                var bone = Color.Lerp(new Color(0.78f, 0.75f, 0.68f), new Color(0.9f, 0.88f, 0.82f), R(rng, 0f, 1f));
                Stroke(c, from, from + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R(rng, 12f, 30f), R(rng, 3f, 5f), 2.5f, bone * 0.9f, bone, 0f, R(rng, -5f, 5f));
            }
            for (int i = 0; i < 160; i++)
            {
                var shell = Pick(rng, new Color(0.92f, 0.88f, 0.8f), new Color(0.88f, 0.74f, 0.68f), new Color(0.75f, 0.7f, 0.62f), new Color(0.6f, 0.5f, 0.42f));
                Leaf(c, Spot(), R(rng, 0f, Mathf.PI * 2f), R(rng, 10f, 24f), R(rng, 0.55f, 0.95f), shell * R(rng, 0.75f, 1f));
            }
            for (int i = 0; i < 50; i++)
                Disc(c, Spot(), R(rng, 3f, 8f), Color.Lerp(new Color(0.3f, 0.29f, 0.27f), new Color(0.55f, 0.52f, 0.48f), R(rng, 0f, 1f)));
        }

        /// <summary>A soft patch of olive-brown turf algae with a ragged edge.</summary>
        static void PaintTurfAlgae(Canvas c, System.Random rng)
        {
            var r = c.clip;
            var center = r.center;
            float radius = r.width * 0.47f;
            for (int y = r.yMin; y < r.yMax; y++)
            for (int x = r.xMin; x < r.xMax; x++)
            {
                var d = new Vector2(x + 0.5f, y + 0.5f) - center;
                float angle = Mathf.Atan2(d.y, d.x);
                float edge = 0.7f + 0.22f * Mathf.PerlinNoise(Mathf.Cos(angle) * 1.6f + 7f, Mathf.Sin(angle) * 1.6f + 7f)
                    + 0.08f * Mathf.PerlinNoise(x * 0.08f + 20f, y * 0.08f);
                if (d.magnitude / radius > edge)
                    continue;
                float speck = Mathf.PerlinNoise(x * 0.4f + 5f, y * 0.4f) * 0.6f + Mathf.PerlinNoise(x * 1.1f, y * 1.1f + 9f) * 0.4f;
                float patch = Mathf.PerlinNoise(x * 0.03f, y * 0.03f + 40f);
                c.Put(x, y, Color.Lerp(new Color(0.22f, 0.24f, 0.1f), new Color(0.48f, 0.47f, 0.22f), speck * 0.7f + patch * 0.3f), 0f);
            }
        }

        /// <summary>A sea fan: branches fanning up from one foot, joined by a fine, irregular lattice.</summary>
        static void PaintSeaFan(Canvas c, System.Random rng)
        {
            var r = c.clip;
            var foot = new Vector2(r.center.x, r.yMin + 2f);
            var tone = new Color(0.58f, 0.3f, 0.5f);
            // The fan's outline is an upright half-ellipse that fits the region.
            var reach = new Vector2(r.width * 0.46f, r.height * 0.92f);
            const int branches = 36;
            var tips = new Vector2[branches];
            for (int b = 0; b < branches; b++)
            {
                float a = Mathf.Lerp(12f, 168f, b / (branches - 1f)) * Mathf.Deg2Rad + R(rng, -0.03f, 0.03f);
                float length = R(rng, 0.8f, 1f);
                tips[b] = foot + new Vector2(Mathf.Cos(a) * reach.x, Mathf.Sin(a) * reach.y) * length;
                var shade = tone * R(rng, 0.85f, 1.05f);
                Stroke(c, foot, tips[b], 2.2f, 0.9f, shade * 0.8f, shade, 0f, R(rng, -5f, 5f));
            }
            // A dense, fine lattice: short links between neighbouring branches, a few missing.
            const int rings = 26;
            for (int ring = 1; ring <= rings; ring++)
            {
                float t = ring / (rings + 1f);
                for (int b = 0; b < branches - 1; b++)
                {
                    if (rng.NextDouble() < 0.12)
                        continue;
                    var p0 = Vector2.Lerp(foot, tips[b], t + R(rng, -0.015f, 0.015f));
                    var p1 = Vector2.Lerp(foot, tips[b + 1], t + R(rng, -0.015f, 0.015f));
                    Stroke(c, p0, p1, 0.8f, 0.8f, tone * 0.85f, tone * 0.85f);
                }
            }
        }

        /// <summary>Bushy red seaweed: fine fronds forking again and again from a small holdfast.</summary>
        static void PaintRedWeed(Canvas c, System.Random rng)
        {
            var r = c.clip;
            void Frond(Vector2 from, float angle, float length, float width, int depth)
            {
                var to = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;
                var tone = Color.Lerp(new Color(0.42f, 0.12f, 0.12f), new Color(0.66f, 0.26f, 0.2f), R(rng, 0f, 1f));
                Stroke(c, from, to, width, width * 0.7f, tone * 0.85f, tone, 0f, R(rng, -4f, 4f));
                if (depth == 0)
                    return;
                for (int k = -1; k <= 1; k += 2)
                    Frond(to, angle + k * R(rng, 0.25f, 0.55f), length * R(rng, 0.6f, 0.8f), width * 0.75f, depth - 1);
            }
            var foot = new Vector2(r.center.x, r.yMin + 2f);
            for (int s = 0; s < 5; s++)
                Frond(foot, (90f + R(rng, -35f, 35f)) * Mathf.Deg2Rad, r.height * R(rng, 0.22f, 0.3f), 3.2f, 4);
        }

        /// <summary>Bladder wrack: flat olive-brown fronds forking in pairs, with paired air bladders.</summary>
        static void PaintWrack(Canvas c, System.Random rng)
        {
            var r = c.clip;
            void Frond(Vector2 from, float angle, float length, float width, int depth)
            {
                var to = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length;
                var tone = Color.Lerp(new Color(0.35f, 0.3f, 0.12f), new Color(0.5f, 0.44f, 0.18f), R(rng, 0f, 1f));
                Stroke(c, from, to, width, width * 0.85f, tone * 0.85f, tone, 0f, R(rng, -3f, 3f));
                var mid = Vector2.Lerp(from, to, 0.5f);
                var side = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * width * 0.9f;
                Disc(c, mid + side, width * 0.6f, tone * 1.15f);
                Disc(c, mid - side, width * 0.6f, tone * 1.15f);
                if (depth == 0)
                    return;
                for (int k = -1; k <= 1; k += 2)
                    Frond(to, angle + k * R(rng, 0.3f, 0.5f), length * R(rng, 0.7f, 0.85f), width * 0.85f, depth - 1);
            }
            var foot = new Vector2(r.center.x, r.yMin + 2f);
            for (int s = 0; s < 3; s++)
                Frond(foot, (90f + R(rng, -30f, 30f)) * Mathf.Deg2Rad, r.height * R(rng, 0.2f, 0.26f), 5f, 3);
        }

        // ---------- Leaf cards ----------

        const int LeafSize = 512;

        /// <summary>
        /// A cluster of leaves on a transparent background, for alpha-cut foliage cards: about a hundred
        /// pointed leaves of varied greens fanning out from the middle, the ones underneath darker, each with
        /// a lighter midrib and darker edges. The blossom version scatters small pale five-petalled flowers on top.
        /// </summary>
        public static Texture2D LeafCards(bool blossom)
        {
            string path = $"{Dir}/LeafCards{(blossom ? "Blossom" : "")}_v{Version}.png";
            if (!File.Exists(path))
                GenerateLeafCards(path, blossom);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void GenerateLeafCards(string path, bool blossom)
        {
            DreamBuildUtils.EnsureFolder(Dir);
            const int n = LeafSize;
            var px = new Color[n * n];
            var fill = new Color(0.27f, 0.36f, 0.16f, 0f);
            for (int i = 0; i < px.Length; i++)
                px[i] = fill;
            var rng = new System.Random(blossom ? 4242 : 2424);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // Twigs first, so leaves cover most of them.
            var twig = new Color(0.25f, 0.18f, 0.12f, 1f);
            for (int t = 0; t < 7; t++)
            {
                // Each twig wanders out from the middle in a few short, slightly bent steps.
                float a = t / 7f * Mathf.PI * 2f + R(-0.3f, 0.3f);
                var at = new Vector2(0.5f, 0.5f);
                float reach = R(0.22f, 0.34f);
                for (int k = 0; k < 4; k++)
                {
                    a += R(-0.35f, 0.35f);
                    var next = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * reach * 0.25f;
                    DrawLine(px, n, at, next, Mathf.Lerp(0.0045f, 0.0025f, k / 3f), twig);
                    at = next;
                }
            }

            const int leaves = 140;
            for (int i = 0; i < leaves; i++)
            {
                // Denser toward the middle, thinning to a ragged edge.
                float r = Mathf.Sqrt(R(0f, 1f)) * 0.36f;
                float a = R(0f, Mathf.PI * 2f);
                var at = new Vector2(0.5f, 0.5f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                float angle = a + R(-0.7f, 0.7f);
                float length = R(0.075f, 0.12f);
                float depth = (float)i / leaves;
                var tone = Color.Lerp(new Color(0.16f, 0.28f, 0.1f), new Color(0.42f, 0.55f, 0.2f), R(0f, 1f));
                if (R(0f, 1f) < 0.12f)
                    tone = Color.Lerp(tone, new Color(0.55f, 0.55f, 0.2f), 0.5f);
                DrawLeaf(px, n, at, angle, length, tone * (0.55f + 0.45f * depth));
            }

            if (blossom)
            {
                for (int b = 0; b < 75; b++)
                {
                    float r = Mathf.Sqrt(R(0f, 1f)) * 0.36f;
                    float a = R(0f, Mathf.PI * 2f);
                    var at = new Vector2(0.5f, 0.5f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    var petal = Color.Lerp(new Color(1f, 0.86f, 0.9f), new Color(1f, 0.97f, 0.97f), R(0f, 1f));
                    float size = R(0.007f, 0.011f);
                    float spin = R(0f, Mathf.PI * 2f);
                    for (int k = 0; k < 5; k++)
                    {
                        float pa = spin + k * Mathf.PI * 0.4f;
                        DrawDisc(px, n, at + new Vector2(Mathf.Cos(pa), Mathf.Sin(pa)) * size, size * 0.85f, petal);
                    }
                    DrawDisc(px, n, at, size * 0.45f, new Color(0.95f, 0.8f, 0.35f, 1f));
                }
            }

            WriteCutout(path, px, n);
        }

        /// <summary>One pointed leaf from its stem end at <paramref name="at"/>, pointing along <paramref name="angle"/>.</summary>
        static void DrawLeaf(Color[] px, int n, Vector2 at, float angle, float length, Color tone)
        {
            var axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var side = new Vector2(-axis.y, axis.x);
            float halfWidth = length * 0.22f;
            DrawLine(px, n, at - axis * length * 0.18f, at, 0.004f, new Color(0.3f, 0.32f, 0.14f, 1f));

            var tip = at + axis * length;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(at.x, tip.x) - halfWidth) * n));
            int x1 = Mathf.Min(n - 1, Mathf.CeilToInt((Mathf.Max(at.x, tip.x) + halfWidth) * n));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(at.y, tip.y) - halfWidth) * n));
            int y1 = Mathf.Min(n - 1, Mathf.CeilToInt((Mathf.Max(at.y, tip.y) + halfWidth) * n));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var d = new Vector2((x + 0.5f) / n, (y + 0.5f) / n) - at;
                float s = Vector2.Dot(d, axis) / length;
                if (s <= 0f || s >= 1f)
                    continue;
                float t = Vector2.Dot(d, side);
                float w = halfWidth * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(s, 0.8f)), 0.75f);
                if (Mathf.Abs(t) >= w)
                    continue;
                float across = Mathf.Abs(t) / w;
                float shade = 1f - 0.28f * across * across;               // darker toward the edges
                shade *= 0.85f + 0.15f * s;                                // a touch lighter toward the tip
                if (Mathf.Abs(t) < halfWidth * 0.07f)
                    shade *= 1.18f;                                        // the midrib
                float vein = Mathf.Abs(Mathf.Sin((s * 9f - across * 2.2f) * Mathf.PI));
                shade *= 0.96f + 0.04f * vein;
                var c = tone * shade;
                c.a = 1f;
                px[y * n + x] = c;
            }
        }

        static void DrawLine(Color[] px, int n, Vector2 from, Vector2 to, float width, Color color)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(from, to) * n * 2f);
            for (int i = 0; i <= steps; i++)
                DrawDisc(px, n, Vector2.Lerp(from, to, i / (float)Mathf.Max(1, steps)), width * Mathf.Lerp(1f, 0.5f, i / (float)Mathf.Max(1, steps)), color);
        }

        static void DrawDisc(Color[] px, int n, Vector2 at, float radius, Color color)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((at.x - radius) * n)), x1 = Mathf.Min(n - 1, Mathf.CeilToInt((at.x + radius) * n));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((at.y - radius) * n)), y1 = Mathf.Min(n - 1, Mathf.CeilToInt((at.y + radius) * n));
            color.a = 1f;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (new Vector2((x + 0.5f) / n - at.x, (y + 0.5f) / n - at.y).sqrMagnitude <= radius * radius)
                    px[y * n + x] = color;
        }

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
