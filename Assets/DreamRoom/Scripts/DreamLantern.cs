using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace DreamStudio
{
    /// <summary>
    /// A lantern the player can take down from its hook and carry to light the way. It flickers gently,
    /// hangs still on its hook until first taken, and falls to the ground when let go. Waking up (or the
    /// lantern falling out of the world) puts it back on its hook, unless it is in the player's hand.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
    public class DreamLantern : MonoBehaviour
    {
        public Light glow;
        [Tooltip("How much the light wavers (0 = steady).")]
        [Range(0f, 0.5f)] public float flicker = 0.12f;
        [TextArea] public string firstGrabMessage = "The lantern glows warmly in your hand.\nCarry it to light the way.";

        XRGrabInteractable m_Grab;
        Rigidbody m_Body;
        NightmareWorld m_World;
        Vector3 m_HomePosition;
        Quaternion m_HomeRotation;
        float m_BaseIntensity;
        bool m_LetGo;
        bool m_Greeted;

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();
            m_Body = GetComponent<Rigidbody>();
            m_HomePosition = transform.position;
            m_HomeRotation = transform.rotation;
            if (glow != null)
                m_BaseIntensity = glow.intensity;

            m_Grab.selectEntered.AddListener(_ => Taken());
            m_Grab.selectExited.AddListener(_ => m_LetGo = true);
        }

        void Start()
        {
            // NightmareWorld sets its Instance in Awake, so it is safe to look it up here.
            m_World = NightmareWorld.Instance;
            if (m_World != null)
                m_World.WokeUp += ReturnToHook;
        }

        void OnDestroy()
        {
            if (m_World != null)
                m_World.WokeUp -= ReturnToHook;
        }

        void Taken()
        {
            DreamHaptics.Pulse(0.3f, 0.1f);
            if (m_Greeted)
                return;
            m_Greeted = true;
            if (DreamHud.Instance != null)
                DreamHud.Instance.ShowMessage(firstGrabMessage, 4f);
        }

        void Update()
        {
            if (glow == null)
                return;
            float n = Mathf.PerlinNoise(Time.time * 3f, 0.37f);
            glow.intensity = m_BaseIntensity * (1f - flicker + flicker * 2f * n);
        }

        void FixedUpdate()
        {
            // The grab restores the body's old (kinematic) state on release; after that, let it fall.
            if (m_LetGo && !m_Grab.isSelected)
            {
                m_LetGo = false;
                m_Body.isKinematic = false;
                m_Body.useGravity = true;
            }

            if (transform.position.y < m_HomePosition.y - 30f)
                ReturnToHook();
        }

        /// <summary>Hangs the lantern back on its hook, unless the player is holding it.</summary>
        public void ReturnToHook()
        {
            if (m_Grab.isSelected)
                return;
            m_LetGo = false;
            m_Body.isKinematic = true;
            m_Body.useGravity = false;
            transform.SetPositionAndRotation(m_HomePosition, m_HomeRotation);
            Physics.SyncTransforms();
        }
    }
}
