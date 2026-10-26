using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// A nightmare on the clock. Time ticks down in the HUD; when it runs out you wake up back
    /// at the start and the clock is wound again. Stops once the dream is restored.
    /// </summary>
    [RequireComponent(typeof(NightmareWorld))]
    public class DreamCountdown : MonoBehaviour
    {
        public float seconds = 150f;
        [Range(0f, 1f)] public float warningThreshold = 0.25f;
        public string label = "MIDNIGHT IN";
        [TextArea] public string timeUpMessage = "The clock struck midnight...\nYou wake up where you started.";

        NightmareWorld m_World;
        float m_Remaining;
        bool m_Cleared;

        void Awake()
        {
            m_World = GetComponent<NightmareWorld>();
            m_Remaining = seconds;
            m_World.WokeUp += ResetClock;
        }

        void OnDestroy()
        {
            if (m_World != null)
                m_World.WokeUp -= ResetClock;
        }

        void ResetClock() => m_Remaining = seconds;

        /// <summary>0 when the clock is freshly wound, 1 at midnight.</summary>
        public float Progress => m_World != null && m_World.IsDream ? 0f : 1f - m_Remaining / seconds;

        void Update()
        {
            var hud = DreamHud.Instance;

            if (m_World.IsDream)
            {
                if (!m_Cleared && hud != null)
                {
                    hud.SetTint(Color.clear, 0f);
                    hud.SetStatus("", Color.white);
                }
                m_Cleared = true;
                return;
            }

            if (m_World.IsWakingUp)
                return;

            m_Remaining = Mathf.Max(0f, m_Remaining - Time.deltaTime);
            float ratio = m_Remaining / seconds;

            if (hud != null)
            {
                int s = Mathf.CeilToInt(m_Remaining);
                Color color = ratio > warningThreshold ? new Color(1f, 0.9f, 0.55f) : new Color(1f, 0.45f, 0.4f);
                hud.SetStatus($"{label}  {s / 60}:{s % 60:00}", color);

                float low = 1f - Mathf.Clamp01(ratio / warningThreshold);
                DreamAudio.RequestHeartbeat(low);
                hud.SetTint(new Color(0.12f, 0.05f, 0f), low * 0.6f);
            }

            if (m_Remaining <= 0f)
                m_World.WakeUp(timeUpMessage);
        }
    }
}
