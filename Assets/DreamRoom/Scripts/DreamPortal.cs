using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace DreamStudio
{
    /// <summary>
    /// A portal to one of the dream worlds. Glows brighter as the player's head approaches
    /// and loads <see cref="sceneName"/> when the head passes into the portal surface.
    /// The portal faces along its local +Z (toward the room).
    /// </summary>
    public class DreamPortal : MonoBehaviour
    {
        [Tooltip("Display name of the world behind this portal.")]
        public string worldName = "Dream World";

        [Tooltip("Scene to load on entry. Must be added to Build Profiles / Build Settings.")]
        public string sceneName;

        [Tooltip("Half-size of the elliptical opening in local units (x = half width, y = half height).")]
        public Vector2 halfSize = new Vector2(1.2f, 1.6f);
        [Tooltip("The surface's shape: 0 an ellipse, 1 a rectangle, 2 a triangle pointing up (matches its material).")]
        public int shape;

        [Tooltip("How close (meters) the head must get to the portal plane to count as entering.")]
        public float entryDepth = 0.25f;

        [Tooltip("Distance at which the portal starts to brighten.")]
        public float glowDistance = 3f;

        [Tooltip("Players coming back from this portal's world arrive in front of it (hub portals).")]
        public bool arriveHere;

        [Tooltip("How far in front of the portal returning players arrive.")]
        public float arrivalDistance = 1.6f;

        public Renderer surfaceRenderer;
        public Light portalLight;

        public UnityEvent<DreamPortal> onEntered;

        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int SwirlSpeedId = Shader.PropertyToID("_SwirlSpeed");

        /// <summary>The scene the player most recently left through a portal.</summary>
        public static string CameFrom { get; private set; }

        // A new play session never starts "coming back" from a world.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCameFrom() => CameFrom = null;

        MaterialPropertyBlock m_Block;
        float m_BaseIntensity = 1.6f;
        float m_BaseLight;
        bool m_Entered;

        void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            if (surfaceRenderer != null && surfaceRenderer.sharedMaterial != null)
                m_BaseIntensity = surfaceRenderer.sharedMaterial.GetFloat(IntensityId);
            if (portalLight != null)
                m_BaseLight = portalLight.intensity;
        }

        IEnumerator Start()
        {
            DreamAudio.AttachLoop(gameObject, DreamSound.PortalHum, 0.35f, 8f);
            if (!arriveHere || string.IsNullOrEmpty(sceneName) || sceneName != CameFrom)
                yield break;
            CameFrom = null;

            // Let the XR rig pick up its tracked pose before moving it.
            yield return null;
            yield return null;

            Vector3 arrival = transform.position + Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * arrivalDistance;
            arrival.y = Physics.Raycast(arrival, Vector3.down, out var hit, halfSize.y + 2f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : transform.position.y - halfSize.y;
            DreamPlayer.Teleport(arrival, transform.eulerAngles.y);
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            // Unscaled, so a portal that is still growing in (scale near zero) can't map the
            // head onto its center and fire by accident.
            Vector3 local = Quaternion.Inverse(transform.rotation) * (cam.transform.position - transform.position);
            float distance = Vector3.Distance(cam.transform.position, transform.position);
            float proximity = 1f - Mathf.Clamp01(distance / glowDistance);

            if (surfaceRenderer != null)
            {
                surfaceRenderer.GetPropertyBlock(m_Block);
                m_Block.SetFloat(IntensityId, m_BaseIntensity * (1f + proximity * 0.8f));
                m_Block.SetFloat(SwirlSpeedId, 0.6f + proximity * 1.4f);
                surfaceRenderer.SetPropertyBlock(m_Block);
            }

            if (portalLight != null)
                portalLight.intensity = m_BaseLight * (1f + proximity) + Mathf.Sin(Time.time * 2f) * 0.15f;

            float ex = local.x / halfSize.x;
            float ey = local.y / halfSize.y;
            bool open = transform.lossyScale.y > 0.9f;
            bool inside = open && InsideShape(ex, ey) && local.z < entryDepth && local.z > -entryDepth;

            if (inside && !m_Entered)
            {
                m_Entered = true;
                Enter();
            }
            else if (!inside)
            {
                m_Entered = false;
            }
        }

        /// <summary>Whether a point (in half-size units from the centre) lies within the portal's shape.</summary>
        bool InsideShape(float ex, float ey)
        {
            switch (shape)
            {
                case 1: return Mathf.Abs(ex) < 1f && Mathf.Abs(ey) < 1f;
                case 2: return Mathf.Abs(ey) < 1f && Mathf.Abs(ex) < (1f - ey) * 0.5f;
                default: return ex * ex + ey * ey < 1f;
            }
        }

        void Enter()
        {
            onEntered?.Invoke(this);
            DreamAudio.Play(DreamSound.PortalWhoosh);
            DreamHaptics.Pulse(0.6f, 0.25f);

            if (!string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName))
            {
                CameFrom = SceneManager.GetActiveScene().name;
                SceneManager.LoadScene(sceneName);
            }
            else
                Debug.Log($"[DreamPortal] Entered '{worldName}'. Assign a scene name in the build list to travel there.", this);
        }
    }
}
