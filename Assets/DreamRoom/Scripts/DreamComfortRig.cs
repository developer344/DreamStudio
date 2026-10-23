using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace DreamStudio
{
    /// <summary>
    /// Applies <see cref="DreamComfort"/> to the XR rig in every scene: snap or smooth turning, the seated
    /// height lift, a comfort vignette that narrows the view while the rig moves or turns (not when the
    /// player physically moves their head), and an optional FPS meter for checking performance on Quest.
    /// </summary>
    [DefaultExecutionOrder(1200)]
    public class DreamComfortRig : MonoBehaviour
    {
        [Tooltip("How much the view is raised in seated mode (m).")]
        public float seatedLift = 0.45f;

        XROrigin m_Origin;
        float m_BaseOffset;
        Vector3 m_LastRigPosition;
        float m_LastRigYaw;
        float m_Vignette;
        Material m_VignetteMaterial;
        Renderer m_VignetteRenderer;
        TextMeshPro m_Fps;
        float m_FpsTimer;
        int m_FpsFrames;

        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int EdgeTintId = Shader.PropertyToID("_EdgeTint");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int VignetteStartId = Shader.PropertyToID("_VignetteStart");
        static readonly int VignetteEndId = Shader.PropertyToID("_VignetteEnd");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("Comfort");
            DontDestroyOnLoad(go);
            go.AddComponent<DreamComfortRig>();
        }

        void LateUpdate()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null)
                return;
            if (origin != m_Origin)
                Attach(origin);

            foreach (var manager in origin.GetComponentsInChildren<ControllerInputActionManager>())
                if (manager.smoothTurnEnabled != DreamComfort.SmoothTurn)
                    manager.smoothTurnEnabled = DreamComfort.SmoothTurn;

            var offset = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : null;
            if (offset != null)
            {
                var p = offset.localPosition;
                p.y = m_BaseOffset + (DreamComfort.Seated ? seatedLift : 0f);
                offset.localPosition = p;
            }

            UpdateVignette(origin);
            UpdateFps(origin);
        }

        void Attach(XROrigin origin)
        {
            m_Origin = origin;
            m_BaseOffset = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform.localPosition.y : 0f;
            m_LastRigPosition = origin.transform.position;
            m_LastRigYaw = origin.transform.eulerAngles.y;

            var head = origin.Camera.transform;
            var shader = Shader.Find("DreamStudio/VisionFilter");
            if (shader != null)
            {
                var lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lens.name = "Comfort Vignette";
                Destroy(lens.GetComponent<Collider>());
                lens.transform.SetParent(head, false);
                lens.transform.localScale = Vector3.one * 0.42f;
                m_VignetteMaterial = new Material(shader);
                m_VignetteMaterial.SetColor(TintId, Color.white);
                m_VignetteMaterial.SetColor(EdgeTintId, Color.black);
                m_VignetteMaterial.SetColor(GlowId, Color.clear);
                m_VignetteRenderer = lens.GetComponent<Renderer>();
                m_VignetteRenderer.sharedMaterial = m_VignetteMaterial;
                m_VignetteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                m_VignetteRenderer.enabled = false;
            }

            var fps = new GameObject("FPS Meter");
            fps.transform.SetParent(head, false);
            fps.transform.localPosition = new Vector3(-0.22f, -0.2f, 0.7f);
            m_Fps = fps.AddComponent<TextMeshPro>();
            m_Fps.fontSize = 0.6f;
            m_Fps.alignment = TextAlignmentOptions.Left;
            m_Fps.rectTransform.sizeDelta = new Vector2(0.4f, 0.1f);
            m_Fps.text = "";
        }

        void UpdateVignette(XROrigin origin)
        {
            var rig = origin.transform;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float speed = Vector3.Distance(rig.position, m_LastRigPosition) / dt;
            float turn = Mathf.Abs(Mathf.DeltaAngle(m_LastRigYaw, rig.eulerAngles.y)) / dt;
            m_LastRigPosition = rig.position;
            m_LastRigYaw = rig.eulerAngles.y;

            // Teleports and snap turns are instant jumps, not motion: ignore them.
            bool moving = (speed > 0.3f && speed < 15f) || (turn > 20f && turn < 400f);
            float target = DreamComfort.Vignette && moving ? 1f : 0f;
            m_Vignette = Mathf.MoveTowards(m_Vignette, target, dt * (target > m_Vignette ? 4f : 1.5f));

            if (m_VignetteRenderer == null)
                return;
            m_VignetteRenderer.enabled = m_Vignette > 0.01f;
            m_VignetteMaterial.SetFloat(VignetteStartId, Mathf.Lerp(1.2f, 0.22f, m_Vignette));
            m_VignetteMaterial.SetFloat(VignetteEndId, Mathf.Lerp(1.6f, 0.6f, m_Vignette));
        }

        void UpdateFps(XROrigin origin)
        {
            if (m_Fps == null)
                return;
            if (!DreamComfort.ShowFps)
            {
                m_Fps.text = "";
                return;
            }
            m_FpsFrames++;
            m_FpsTimer += Time.unscaledDeltaTime;
            if (m_FpsTimer < 0.5f)
                return;
            float fps = m_FpsFrames / m_FpsTimer;
            m_Fps.text = $"{fps:0} fps";
            m_Fps.color = fps >= 70f ? new Color(0.6f, 1f, 0.6f) : fps >= 55f ? new Color(1f, 0.9f, 0.4f) : new Color(1f, 0.45f, 0.4f);
            m_FpsFrames = 0;
            m_FpsTimer = 0f;
        }
    }
}
