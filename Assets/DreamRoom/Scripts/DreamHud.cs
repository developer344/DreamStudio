using System.Collections;
using TMPro;
using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// Comfort-friendly VR HUD: a message panel with a status line and a meter bar under it (e.g. remaining
    /// oxygen), and a full-view tint/fade. The panel travels with the player exactly, so walking, swimming or
    /// teleporting never moves it in view; it only turns, smoothly, when the player looks well away from it.
    /// </summary>
    public class DreamHud : MonoBehaviour
    {
        public static DreamHud Instance { get; private set; }

        [Tooltip("Material using DreamStudio/ScreenFade.")]
        public Material fadeMaterial;

        [Tooltip("TMP overlay shader so text stays readable on top of fades.")]
        public Shader overlayTextShader;

        public float panelDistance = 1.6f;
        [Tooltip("How far (degrees) the gaze can turn away before the panel turns to follow.")]
        public float deadZone = 30f;
        [Tooltip("How quickly the panel turns back in front once it follows (degrees per second at most).")]
        public float turnSpeed = 120f;

        static readonly int ColorId = Shader.PropertyToID("_Color");

        Transform m_Panel;
        TextMeshPro m_Message;
        TextMeshPro m_Status;
        Renderer m_FadeRenderer;
        Material m_FadeInstance;
        Transform m_MeterBack;
        Transform m_MeterFill;
        Material m_MeterBackMaterial;
        Material m_MeterFillMaterial;

        const float MeterWidth = 0.5f;
        const float MeterHeight = 0.035f;
        const float MeterY = -0.36f;

        float m_MessageUntil;
        float m_PanelYaw;
        bool m_Recentering;
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
            if (m_MeterBackMaterial != null)
                Destroy(m_MeterBackMaterial);
            if (m_MeterFillMaterial != null)
                Destroy(m_MeterFillMaterial);
        }

        /// <summary>Shows the meter bar under the status line, filled to <paramref name="fill"/> (0..1).</summary>
        public void SetMeter(float fill, Color color)
        {
            EnsureCreated();
            if (m_MeterBack == null)
                return;
            fill = Mathf.Clamp01(fill);
            m_MeterBack.gameObject.SetActive(true);
            m_MeterFill.gameObject.SetActive(fill > 0.001f);
            m_MeterFill.localPosition = new Vector3(-MeterWidth * 0.5f + MeterWidth * fill * 0.5f, MeterY, -0.002f);
            m_MeterFill.localScale = new Vector3(MeterWidth * fill, MeterHeight, 1f);
            m_MeterFillMaterial.SetColor(ColorId, color);
        }

        public void HideMeter()
        {
            if (m_MeterBack != null)
                m_MeterBack.gameObject.SetActive(false);
            if (m_MeterFill != null)
                m_MeterFill.gameObject.SetActive(false);
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

            // The panel moves with the head exactly (so moving never shifts it in view) and keeps its own
            // heading, which only turns toward the gaze once the gaze strays past the dead zone, then eases
            // back in front and settles.
            float headYaw = HeadYaw(head);
            float off = Mathf.DeltaAngle(m_PanelYaw, headYaw);
            if (Mathf.Abs(off) > deadZone)
                m_Recentering = true;
            if (m_Recentering)
            {
                float step = Mathf.Min(turnSpeed * Time.deltaTime, Mathf.Abs(off) * (1f - Mathf.Exp(-4f * Time.deltaTime)) + 10f * Time.deltaTime);
                m_PanelYaw = Mathf.MoveTowardsAngle(m_PanelYaw, headYaw, step);
                if (Mathf.Abs(Mathf.DeltaAngle(m_PanelYaw, headYaw)) < 2f)
                    m_Recentering = false;
            }
            var heading = Quaternion.Euler(0f, m_PanelYaw, 0f);
            m_Panel.position = head.position + heading * Vector3.forward * panelDistance + Vector3.down * 0.15f;
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
            m_PanelYaw = HeadYaw(head);
            m_Panel.position = head.position + Quaternion.Euler(0f, m_PanelYaw, 0f) * Vector3.forward * panelDistance;
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

                // The meter: a dark track and a coloured fill, drawn on top like the fade so walls never hide it.
                // The fade shader culls front faces, so the quads are turned to show their backs to the player.
                m_MeterBackMaterial = new Material(fadeMaterial);
                m_MeterBackMaterial.SetColor(ColorId, new Color(0.11f, 0.14f, 0.22f, 0.55f));
                m_MeterFillMaterial = new Material(fadeMaterial);
                m_MeterFillMaterial.renderQueue = m_MeterBackMaterial.renderQueue + 1;
                m_MeterBack = MeterQuad("Meter Track", m_MeterBackMaterial);
                m_MeterBack.localPosition = new Vector3(0f, MeterY, 0f);
                m_MeterBack.localScale = new Vector3(MeterWidth + 0.02f, MeterHeight + 0.016f, 1f);
                m_MeterFill = MeterQuad("Meter Fill", m_MeterFillMaterial);
                HideMeter();
            }
        }

        Transform MeterQuad(string name, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(m_Panel, false);
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return quad.transform;
        }

        /// <summary>The direction the head faces, around the vertical axis (degrees).</summary>
        static float HeadYaw(Transform head)
        {
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            return Quaternion.LookRotation(forward.normalized, Vector3.up).eulerAngles.y;
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
