using System;
using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// How the player sees a world: a filter worn like a lens, not a change to the world itself.
    /// The nightmare look darkens and tints the view behind a heavy vignette; once the token is
    /// found it blends into the dream look, which shows the world as it really is with a soft glow.
    /// </summary>
    public class DreamVision : MonoBehaviour
    {
        [Serializable]
        public class Look
        {
            [Tooltip("Multiplies the middle of the view.")]
            public Color tint = Color.white;
            [Tooltip("Multiplies the edges of the view.")]
            public Color edgeTint = Color.white;
            [Tooltip("Added at the edges of the view. Alpha scales it.")]
            public Color glow = Color.clear;
            [Tooltip("How far off-center (0 = straight ahead) the vignette starts and is complete.")]
            public float vignetteStart = 0.05f;
            public float vignetteEnd = 0.45f;
            [Range(0f, 1f)] public float pulse;
        }

        [Tooltip("Material using DreamStudio/VisionFilter.")]
        public Material filterMaterial;

        public Look nightmare = new Look();
        public Look dream = new Look();

        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int EdgeTintId = Shader.PropertyToID("_EdgeTint");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int VignetteStartId = Shader.PropertyToID("_VignetteStart");
        static readonly int VignetteEndId = Shader.PropertyToID("_VignetteEnd");
        static readonly int PulseId = Shader.PropertyToID("_Pulse");

        Material m_Instance;
        float m_Blend;

        /// <summary>0 = nightmare filter, 1 = dream filter.</summary>
        public float Blend => m_Blend;

        /// <summary>0 = nightmare filter, 1 = dream filter.</summary>
        public void SetBlend(float k)
        {
            m_Blend = Mathf.Clamp01(k);
            Apply();
        }

        void LateUpdate()
        {
            // The XR camera may not exist on the first frames, so the lens is created lazily.
            if (m_Instance != null || filterMaterial == null)
                return;
            var head = DreamPlayer.Head;
            if (head == null)
                return;

            var lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lens.name = "Vision Filter";
            Destroy(lens.GetComponent<Collider>());
            lens.transform.SetParent(head, false);
            lens.transform.localScale = Vector3.one * 0.45f;
            m_Instance = new Material(filterMaterial);
            var lensRenderer = lens.GetComponent<Renderer>();
            lensRenderer.sharedMaterial = m_Instance;
            lensRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Apply();
        }

        void OnDestroy()
        {
            if (m_Instance != null)
                Destroy(m_Instance);
        }

        void Apply()
        {
            if (m_Instance == null)
                return;
            float k = m_Blend;
            m_Instance.SetColor(TintId, Color.Lerp(nightmare.tint, dream.tint, k));
            m_Instance.SetColor(EdgeTintId, Color.Lerp(nightmare.edgeTint, dream.edgeTint, k));
            m_Instance.SetColor(GlowId, Color.Lerp(nightmare.glow, dream.glow, k));
            m_Instance.SetFloat(VignetteStartId, Mathf.Lerp(nightmare.vignetteStart, dream.vignetteStart, k));
            m_Instance.SetFloat(VignetteEndId, Mathf.Lerp(nightmare.vignetteEnd, dream.vignetteEnd, k));
            m_Instance.SetFloat(PulseId, Mathf.Lerp(nightmare.pulse, dream.pulse, k));
        }
    }
}
