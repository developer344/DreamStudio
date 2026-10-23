using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace DreamStudio
{
    /// <summary>
    /// Turns walking into swimming. Gravity is switched off so the player floats with a gentle bob, the
    /// thumbstick swims wherever they look (up and down too), and pulling the controllers through the
    /// water pushes them the other way, like a breaststroke. Strokes add speed; the water's drag slowly
    /// takes it away again. A trail of bubbles streams off the player while they move.
    /// </summary>
    [RequireComponent(typeof(NightmareWorld))]
    public class DreamSwimming : MonoBehaviour
    {
        [Header("Thumbstick")]
        public float swimSpeed = 1.6f;

        [Header("Arm Strokes")]
        [Tooltip("How fast (m/s) a hand must move through the water, relative to the head, to count as a stroke.")]
        public float strokeThreshold = 0.5f;
        [Tooltip("How much of the hands' speed becomes push.")]
        public float strokePower = 1.4f;
        public float maxSpeed = 3f;
        [Tooltip("How quickly the water slows the player down (per second).")]
        public float drag = 1.6f;

        [Header("Floating")]
        public float bobHeight = 0.04f;
        public float bobSpeed = 0.25f;
        [Tooltip("How high above where they started the player can swim.")]
        public float maxRise = 14f;

        [Tooltip("Bubbles that stream from the player while swimming. Moved to the head at runtime.")]
        public ParticleSystem bubbles;

        XROrigin m_Origin;
        CharacterController m_Controller;
        Transform m_LeftHand;
        Transform m_RightHand;
        Vector3 m_LastLeft;
        Vector3 m_LastRight;
        bool m_HasLastHands;
        Vector3 m_Velocity;
        float m_LastBob;
        float m_StartHeight;

        void Start()
        {
            GetComponent<NightmareWorld>().WokeUp += Stop;
        }

        void OnDestroy()
        {
            var world = GetComponent<NightmareWorld>();
            if (world != null)
                world.WokeUp -= Stop;
        }

        /// <summary>Kills all momentum, e.g. after blacking out and waking at the start.</summary>
        public void Stop()
        {
            m_Velocity = Vector3.zero;
            m_HasLastHands = false;
        }

        bool Configure()
        {
            m_Origin = FindAnyObjectByType<XROrigin>();
            if (m_Origin == null || m_Origin.Camera == null)
                return false;

            foreach (var gravity in m_Origin.GetComponentsInChildren<GravityProvider>(true))
                gravity.useGravity = false;
            foreach (var move in m_Origin.GetComponentsInChildren<ContinuousMoveProvider>(true))
            {
                move.enableFly = true;
                move.moveSpeed = swimSpeed * DreamComfort.SwimSpeedMultiplier;
            }

            m_Controller = m_Origin.GetComponentInChildren<CharacterController>();
            foreach (var t in m_Origin.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Left Controller")
                    m_LeftHand = t;
                else if (t.name == "Right Controller")
                    m_RightHand = t;
            }

            m_StartHeight = m_Origin.transform.position.y;
            if (bubbles != null)
            {
                bubbles.transform.SetParent(m_Origin.Camera.transform, false);
                bubbles.transform.localPosition = new Vector3(0f, -0.2f, 0.15f);
            }
            return true;
        }

        void Update()
        {
            if (m_Origin == null && !Configure())
                return;
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            var rig = m_Origin.transform;
            var head = m_Origin.Camera.transform.position;
            var push = Stroke(m_LeftHand, head, ref m_LastLeft, dt) + Stroke(m_RightHand, head, ref m_LastRight, dt);
            m_HasLastHands = true;

            m_Velocity += (push * DreamComfort.SwimSpeedMultiplier + DreamCurrent.FlowAt(head)) * dt;
            m_Velocity *= Mathf.Exp(-drag * dt);
            m_Velocity = Vector3.ClampMagnitude(m_Velocity, maxSpeed);

            float bob = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f) * bobHeight;
            var move = m_Velocity * dt + Vector3.up * (bob - m_LastBob);
            m_LastBob = bob;

            // The water has a top: don't let the player swim up out of the world.
            float ceiling = m_StartHeight + maxRise;
            if (rig.position.y + move.y > ceiling)
            {
                move.y = Mathf.Min(0f, ceiling - rig.position.y);
                m_Velocity.y = Mathf.Min(0f, m_Velocity.y);
            }

            if (m_Controller != null && m_Controller.enabled)
                m_Controller.Move(move);
            else
                rig.position += move;

            if (bubbles != null)
            {
                var emission = bubbles.emission;
                emission.rateOverTime = 4f + m_Velocity.magnitude * 18f;
            }
        }

        /// <summary>
        /// The push from one hand this frame. Hand motion is measured relative to the head (so walking
        /// around the room doesn't count) in the rig's frame (so turning the head doesn't count either).
        /// </summary>
        Vector3 Stroke(Transform hand, Vector3 head, ref Vector3 last, float dt)
        {
            if (hand == null)
                return Vector3.zero;

            var rig = m_Origin.transform;
            var relative = rig.InverseTransformDirection(hand.position - head);
            var velocity = (relative - last) / dt;
            last = relative;

            // Skip the first frame, and spikes from snap turns or teleports.
            float speed = velocity.magnitude;
            if (!m_HasLastHands || speed < strokeThreshold || speed > 8f)
                return Vector3.zero;

            // Pushing the water one way moves the swimmer the other way.
            return rig.TransformDirection(-velocity) * strokePower;
        }
    }
}
