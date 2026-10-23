using System.Collections;
using TMPro;
using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// Comfort-friendly VR HUD: a message panel that lazily follows the player's gaze,
    /// a status line (e.g. remaining air), and a full-view tint/fade.
    /// </summary>
    public class DreamHud : MonoBehaviour
    {
        public static DreamHud Instance { get; private set; }

        [Tooltip("Material using DreamStudio/ScreenFade.")]
        public Material fadeMaterial;

        [Tooltip("TMP overlay shader so text stays readable on top of fades.")]
        public Shader overlayTextShader;

        public float panelDistance = 1.6f;
        public float followSpeed = 2.5f;

        static readonly int ColorId = Shader.PropertyToID("_Color");

        Transform m_Panel;
        TextMeshPro m_Message;
        TextMeshPro m_Status;
        Renderer m_FadeRenderer;
        Material m_FadeInstance;

        float m_MessageUntil;
        Color m_TintColor = Color.clear;
        float m_TintAlpha;
        Color m_FadeColor = Color.black;
        float m_Fade;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (m_FadeInstance != null)
                Destroy(m_FadeInstance);
        }

        public void ShowMessage(string text, float duration = 5f)
        {
            EnsureCreated();
            if (m_Message == null)
                return;
            m_Message.text = text;
            m_MessageUntil = Time.time + duration;
        }

        public void SetStatus(string text, Color color)
        {
            EnsureCreated();
            if (m_Status == null)
                return;
            m_Status.text = text;
            m_Status.color = color;
        }

        /// <summary>Persistent tint (danger, low air). Alpha 0 clears it.</summary>
        public void SetTint(Color color, float alpha)
        {
            m_TintColor = color;
            m_TintAlpha = Mathf.Clamp01(alpha);
        }

        public Coroutine FadeTo(float target, float duration, Color color)
        {
            return StartCoroutine(FadeRoutine(target, duration, color));
        }

        IEnumerator FadeRoutine(float target, float duration, Color color)
        {
            m_FadeColor = color;
            float start = m_Fade;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                m_Fade = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            m_Fade = target;
        }

        void LateUpdate()
        {
            EnsureCreated();
            var head = DreamPlayer.Head;
            if (head == null || m_Panel == null)
                return;

            // Lazy follow: drift toward a point in front of the gaze instead of being head-locked.
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
                forward = head.up;
            forward.Normalize();
            Vector3 targetPos = head.position + forward * panelDistance + Vector3.down * 0.15f;
            float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            m_Panel.position = Vector3.Lerp(m_Panel.position, targetPos, k);
            m_Panel.rotation = Quaternion.LookRotation(m_Panel.position - head.position, Vector3.up);

            if (m_Message != null)
            {
                float remaining = m_MessageUntil - Time.time;
                m_Message.alpha = Mathf.Clamp01(remaining);
            }

            if (m_FadeRenderer != null)
            {
                if (m_FadeRenderer.transform.parent != head)
                    m_FadeRenderer.transform.SetParent(head, false);

                float alpha = Mathf.Max(m_TintAlpha, m_Fade);
                Color c = alpha > 0f ? Color.Lerp(m_TintColor, m_FadeColor, m_Fade / alpha) : Color.clear;
                c.a = alpha;
                m_FadeRenderer.enabled = alpha > 0.001f;
                m_FadeInstance.SetColor(ColorId, c);
            }
        }

        void EnsureCreated()
        {
            if (m_Panel != null)
                return;
            var head = DreamPlayer.Head;
            if (head == null)
                return;

            m_Panel = new GameObject("HUD Panel").transform;
            m_Panel.position = head.position + head.forward * panelDistance;
            m_Message = CreateText("Message", new Vector3(0f, 0.12f, 0f), 0.9f, Color.white);
            m_Status = CreateText("Status", new Vector3(0f, -0.28f, 0f), 0.7f, Color.white);
            m_Message.alpha = 0f;

            if (fadeMaterial != null)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Screen Fade";
                Destroy(sphere.GetComponent<Collider>());
                sphere.transform.SetParent(head, false);
                sphere.transform.localScale = Vector3.one * 0.5f;
                m_FadeInstance = new Material(fadeMaterial);
                m_FadeRenderer = sphere.GetComponent<Renderer>();
                m_FadeRenderer.sharedMaterial = m_FadeInstance;
                m_FadeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                m_FadeRenderer.enabled = false;
            }
        }

        TextMeshPro CreateText(string name, Vector3 localPos, float fontSize, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(m_Panel, false);
            go.transform.localPosition = localPos;
            var text = go.AddComponent<TextMeshPro>();
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.rectTransform.sizeDelta = new Vector2(1.4f, 0.6f);
            if (overlayTextShader != null)
                text.fontMaterial.shader = overlayTextShader;
            return text;
        }
    }
}
