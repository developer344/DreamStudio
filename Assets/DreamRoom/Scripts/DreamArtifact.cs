using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// Something that is always in the world but looks different depending on how the player sees it.
    /// Through the nightmare filter they see <see cref="nightmareLook"/>; through the dream filter,
    /// <see cref="dreamLook"/>. Leave one empty for an artifact that only one of the two can reveal.
    /// The looks swap as the <see cref="DreamVision"/> blends: the nightmare one shrinks away over the
    /// first half of the blend and the dream one grows in over the second half.
    /// </summary>
    public class DreamArtifact : MonoBehaviour
    {
        [Tooltip("Seen only through the nightmare filter. Leave empty for a dream-only artifact.")]
        public GameObject nightmareLook;

        [Tooltip("Seen only through the dream filter. Leave empty for a nightmare-only artifact.")]
        public GameObject dreamLook;

        Vector3 m_NightmareScale = Vector3.one;
        Vector3 m_DreamScale = Vector3.one;

        void Awake()
        {
            if (nightmareLook != null)
                m_NightmareScale = nightmareLook.transform.localScale;
            if (dreamLook != null)
                m_DreamScale = dreamLook.transform.localScale;
        }

        void Update()
        {
            var world = NightmareWorld.Instance;
            float blend = world != null && world.vision != null ? world.vision.Blend : 1f;
            Show(nightmareLook, m_NightmareScale, 1f - Mathf.Clamp01(blend * 2f));
            Show(dreamLook, m_DreamScale, Mathf.Clamp01(blend * 2f - 1f));
        }

        static void Show(GameObject look, Vector3 scale, float k)
        {
            if (look == null)
                return;
            bool visible = k > 0.001f;
            if (look.activeSelf != visible)
                look.SetActive(visible);
            if (visible)
                look.transform.localScale = scale * Mathf.SmoothStep(0f, 1f, k);
        }
    }
}
