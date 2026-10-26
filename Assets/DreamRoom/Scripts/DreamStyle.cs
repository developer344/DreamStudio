using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// The game's illustration palette, after the Dream Decoder cards: warm paper, blush, peach, coral,
    /// terracotta and mustard; sand; dusty sky blue, sea teal, sage, fern and olive green; cocoa, taupe,
    /// lavender, and the deep navy of the night cards as the only dark. The Storybook shader pulls every surface toward these colours. Set as shader
    /// globals when the game starts (and in the editor, by DreamStyleEditor).
    /// </summary>
    public static class DreamStyle
    {
        public static readonly Color[] Palette =
        {
            new Color(0.96f, 0.93f, 0.88f), // paper cream
            new Color(0.94f, 0.79f, 0.73f), // blush
            new Color(0.96f, 0.71f, 0.57f), // peach
            new Color(0.91f, 0.46f, 0.34f), // coral
            new Color(0.74f, 0.38f, 0.29f), // terracotta
            new Color(0.94f, 0.75f, 0.32f), // mustard
            new Color(0.87f, 0.8f, 0.68f),  // sand
            new Color(0.63f, 0.76f, 0.8f),  // dusty sky blue
            new Color(0.38f, 0.6f, 0.6f),   // sea teal
            new Color(0.56f, 0.68f, 0.47f), // sage
            new Color(0.27f, 0.46f, 0.35f), // fern green
            new Color(0.64f, 0.62f, 0.4f),  // olive
            new Color(0.56f, 0.42f, 0.34f), // cocoa
            new Color(0.62f, 0.56f, 0.52f), // taupe
            new Color(0.73f, 0.69f, 0.81f), // lavender
            new Color(0.13f, 0.16f, 0.25f), // night navy
        };

        static readonly int PaletteId = Shader.PropertyToID("_DreamPalette");
        static readonly int CountId = Shader.PropertyToID("_DreamPaletteCount");
        static readonly int ContrastId = Shader.PropertyToID("_DreamContrast");
        static readonly int SaturationId = Shader.PropertyToID("_DreamSaturation");

        /// <summary>The colour grade of places that are always dreams (the Start Room and the Portal Room).</summary>
        public const float DreamContrast = 1.2f;
        public const float DreamSaturation = 1.18f;

        /// <summary>Sets the illustration's colour grade: contrast and saturation (1 = unchanged).</summary>
        public static void SetGrade(float contrast, float saturation)
        {
            Shader.SetGlobalFloat(ContrastId, contrast);
            Shader.SetGlobalFloat(SaturationId, saturation);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Apply()
        {
            var values = new Vector4[16];
            for (int i = 0; i < values.Length && i < Palette.Length; i++)
                values[i] = new Vector4(Palette[i].r, Palette[i].g, Palette[i].b, 1f); // gamma values; the shader compares in gamma
            Shader.SetGlobalVectorArray(PaletteId, values);
            Shader.SetGlobalFloat(CountId, Mathf.Min(Palette.Length, 16));
            SetGrade(DreamContrast, DreamSaturation);
        }

        // Every scene starts on the dream grade; a world's DreamVision then sets its own.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void WatchScenes()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => SetGrade(DreamContrast, DreamSaturation);
        }
    }
}
