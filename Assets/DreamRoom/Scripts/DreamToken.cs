using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace DreamStudio
{
    /// <summary>
    /// The object a player must find to turn a nightmare into a dream.
    /// Claimed by grabbing it, or simply by putting your head right next to it.
    /// </summary>
    public class DreamToken : MonoBehaviour
    {
        [Tooltip("Head distance (m) that also claims the token, for players who don't grab.")]
        public float touchDistance = 0.7f;

        [Tooltip("Distance (m) at which the player gets a 'you are close' hint.")]
        public float hintDistance = 6f;

        [TextArea] public string nearbyHint = "You feel something powerful nearby...";

        public Light glow;
        public float pulseSpeed = 1.5f;

        bool m_Claimed;
        bool m_Hinted;
        float m_BaseGlow;

        void Awake()
        {
            if (TryGetComponent<XRGrabInteractable>(out var grab))
                grab.selectEntered.AddListener(_ => Claim());
            if (glow != null)
                m_BaseGlow = glow.intensity;
        }

        void Update()
        {
            if (glow != null)
            {
                float pulse = m_Claimed ? 1.6f : 1f + Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) * 0.35f;
                glow.intensity = m_BaseGlow * pulse;
            }

            if (m_Claimed)
                return;

            var head = DreamPlayer.Head;
            if (head == null)
                return;

            float distance = Vector3.Distance(head.position, transform.position);
            if (!m_Hinted && distance < hintDistance)
            {
                m_Hinted = true;
                if (DreamHud.Instance != null)
                    DreamHud.Instance.ShowMessage(nearbyHint, 4f);
            }

            if (distance < touchDistance)
                Claim();
        }

        /// <summary>For a world restored on an earlier visit: the token is gone, taken last time.</summary>
        public void SetAlreadyClaimed()
        {
            m_Claimed = true;
            // Every token sits on its own hover parent, which also holds any sparkle around it.
            (transform.parent != null ? transform.parent.gameObject : gameObject).SetActive(false);
        }

        public void Claim()
        {
            if (m_Claimed)
                return;
            m_Claimed = true;
            DreamAudio.Play(DreamSound.TokenChime);
            DreamHaptics.Pulse(0.8f, 0.3f);
            if (NightmareWorld.Instance != null)
                NightmareWorld.Instance.RestoreDream();
        }
    }
}
