using System;
using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// How the player sees a world: a filter worn like a lens, not a change to the world itself.
    /// The nightmare look darkens and tints the view behind a heavy vignette; once the token is
    /// found it blends into the dream look, which shows the world as it really is with a soft glow.
    /// The blend also drives the illustration's colour grade (flat and muted in the nightmare, crisper and
    /// more vivid in the dream; see <see cref="DreamStyle.SetGrade"/>) and, once dreaming, a drift of
    /// sparkles around the player.
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
        }

        [Tooltip("Material using DreamStudio/VisionFilter.")]
        public Material filterMaterial;

        public Look nightmare = new Look();
        public Look dream = new Look();

        [Header("Colour grade")]
        public float nightmareContrast = 0.95f;
        public float dreamContrast = 1.2f;
        public float nightmareSaturation = 0.8f;
        public float dreamSaturation = 1.18f;

        [Header("Dream sparkles")]
        [Tooltip("Particle material for the sparkles that drift round the player once the dream is restored.")]
        public Material sparkleMaterial;
        public float sparkleRate = 28f;

        ParticleSystem m_Sparkles;

        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int EdgeTintId = Shader.PropertyToID("_EdgeTint");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int VignetteStartId = Shader.PropertyToID("_VignetteStart");
        static readonly int VignetteEndId = Shader.PropertyToID("_VignetteEnd");

        Material m_Instance;
        float m_Blend;

        /// <summary>0 = nightmare filter, 1 = dream filter.</summary>
        public float Blend => m_Blend;

        /// <summary>0 = nightmare filter, 1 = dream filter.</summary>
        public void SetBlend(float k)
        {
            m_Blend = Mathf.Clamp01(k);
            DreamStyle.SetGrade(Mathf.Lerp(nightmareContrast, dreamContrast, m_Blend), Mathf.Lerp(nightmareSaturation, dreamSaturation, m_Blend));
            Apply();
        }

        void Start()
        {
            DreamStyle.SetGrade(Mathf.Lerp(nightmareContrast, dreamContrast, m_Blend), Mathf.Lerp(nightmareSaturation, dreamSaturation, m_Blend));
        }

        void LateUpdate()
        {
            UpdateSparkles();
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

        /// <summary>Sparkles drifting in the air round the player, fading in with the dream.</summary>
        void UpdateSparkles()
        {
            var head = DreamPlayer.Head;
            if (m_Blend <= 0.01f || sparkleMaterial == null || head == null)
            {
                if (m_Sparkles != null)
                {
                    var off = m_Sparkles.emission;
                    off.rateOverTime = 0f;
                }
                return;
            }
            if (m_Sparkles == null)
                m_Sparkles = CreateSparkles(head);
            var emission = m_Sparkles.emission;
            emission.rateOverTime = sparkleRate * m_Blend;
        }

        ParticleSystem CreateSparkles(Transform head)
        {
            var go = new GameObject("Dream Sparkles");
            go.transform.SetParent(head, false);
            go.transform.localPosition = new Vector3(0f, -0.3f, 1.2f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.035f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.75f), new Color(0.85f, 0.95f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(5f, 2.5f, 5f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.4f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var twinkle = ps.sizeOverLifetime;
            twinkle.enabled = true;
            twinkle.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1f), new Keyframe(0.6f, 0.5f), new Keyframe(1f, 1f)));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = sparkleMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
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
        }
    }
}
