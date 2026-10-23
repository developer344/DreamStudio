using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// Speeds up a spinning danger (gear sweepers, searchlights) as the player closes in on the goal,
    /// and optionally as the world's countdown runs out, so the nightmare gets fiercer near the end.
    /// </summary>
    [RequireComponent(typeof(DreamFloat))]
    public class DreamEscalation : MonoBehaviour
    {
        public Vector3 goal;
        [Tooltip("Within this distance (m) of the goal the spin starts to quicken.")]
        public float range = 40f;
        [Tooltip("Extra spin at full intensity (1 = twice as fast).")]
        public float boost = 1f;
        [Tooltip("Also quicken as the world's countdown runs out.")]
        public bool followCountdown;

        DreamFloat m_Float;
        Vector3 m_BaseSpin;
        DreamCountdown m_Countdown;

        void Start()
        {
            m_Float = GetComponent<DreamFloat>();
            m_BaseSpin = m_Float.spin;
            if (followCountdown && NightmareWorld.Instance != null)
                m_Countdown = NightmareWorld.Instance.GetComponent<DreamCountdown>();
        }

        void Update()
        {
            float closeness = 1f - Mathf.Clamp01(DreamPlayer.FlatDistance(goal) / range);
            float urgency = m_Countdown != null ? m_Countdown.Progress : 0f;
            m_Float.spin = m_BaseSpin * (1f + boost * Mathf.Max(closeness, urgency));
        }
    }
}
