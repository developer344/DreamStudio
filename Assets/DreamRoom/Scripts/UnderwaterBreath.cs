using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// The player's oxygen in an underwater nightmare, shown as a meter under the HUD status line.
    /// Oxygen drains over time and <see cref="AirBubble"/>s top it up, though each bubble gives a little
    /// less than the one before: clinging to air works less and less. Nothing tells the player to stop.
    ///
    /// The way out is to face the fear. When the oxygen runs out the player starts to drown (the view
    /// darkens, the heart pounds, the controllers shudder), and if they hold on for
    /// <see cref="drownDelay"/> seconds without grabbing a bubble, they find they can breathe the water:
    /// it was a dream all along, and the nightmare lifts. Every so often, while they keep clinging, a
    /// quiet dream-sign thought surfaces to make them wonder.
    /// </summary>
    public class UnderwaterBreath : MonoBehaviour
    {
        public static UnderwaterBreath Instance { get; private set; }

        [Tooltip("Seconds of oxygen when full.")]
        public float maxAir = 60f;
        [Range(0f, 1f)] public float lowAirThreshold = 0.3f;
        [Tooltip("Seconds of drowning, with no oxygen, before the player breathes the water.")]
        public float drownDelay = 2.5f;
        [Tooltip("Each bubble gives this fraction of what the one before gave.")]
        [Range(0.5f, 1f)] public float bubbleFalloff = 0.9f;
        [Tooltip("Seconds between the quiet dream-sign thoughts, while the player keeps clinging to air.")]
        public float thoughtInterval = 100f;

        [TextArea]
        public string[] thoughts =
        {
            "The fish drift past you, unhurried.\nNot one of them is reaching for air.",
            "Your chest should be burning by now...\nIt isn't.",
            "How long have you been down here?\nLong enough to have drowned.",
            "Why are you so afraid of the water?",
        };

        [TextArea] public string breakthroughMessage = "You can't hold on any longer... you breathe in.\nThe water fills your lungs like air.\nYou were dreaming all along.";

        public float Air { get; private set; }
        public bool CanBreathe { get; private set; }
        public bool Drowning => !CanBreathe && Air <= 0f;

        int m_Gulps;
        float m_DrowningFor;
        float m_NextThoughtAt;
        int m_Thought;
        float m_NextShudder;

        void Awake()
        {
            Instance = this;
            Air = maxAir;
            m_NextThoughtAt = Time.time + thoughtInterval;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            var hud = DreamHud.Instance;
            var world = NightmareWorld.Instance;

            if (world != null && world.IsDream)
            {
                if (!CanBreathe && hud != null)
                {
                    hud.SetTint(Color.clear, 0f);
                    hud.SetStatus("", Color.white);
                    hud.HideMeter();
                }
                CanBreathe = true;
                return;
            }
            if (world != null && world.IsWakingUp)
                return;

            Air = Mathf.Max(0f, Air - Time.deltaTime);
            float ratio = Air / maxAir;
            float low = 1f - Mathf.Clamp01(ratio / lowAirThreshold);

            if (hud != null)
            {
                var healthy = new Color(0.63f, 0.82f, 0.84f);
                var danger = new Color(0.91f, 0.46f, 0.34f);
                float flash = Drowning ? 0.5f + 0.5f * Mathf.Sin(Time.time * 12f) : 1f;
                var color = Color.Lerp(healthy, danger, low);
                hud.SetStatus($"OXYGEN  {Mathf.CeilToInt(ratio * 100f)}%", color);
                hud.SetMeter(Drowning ? 1f : ratio, Drowning ? danger * flash : color);
            }

            if (!Drowning)
            {
                m_DrowningFor = 0f;
                DreamAudio.RequestHeartbeat(low);
                if (hud != null)
                    hud.SetTint(new Color(0.08f, 0.1f, 0.2f), low * 0.7f);
                MaybeThink(hud);
                return;
            }

            // Drowning: the dark closes in, the heart races, the hands shudder... and then you breathe.
            m_DrowningFor += Time.deltaTime;
            float k = Mathf.Clamp01(m_DrowningFor / drownDelay);
            DreamAudio.RequestHeartbeat(1f);
            if (hud != null)
                hud.SetTint(new Color(0.05f, 0.07f, 0.14f), Mathf.Lerp(0.7f, 0.92f, k));
            if (Time.time >= m_NextShudder)
            {
                DreamHaptics.Pulse(Mathf.Lerp(0.3f, 0.8f, k), 0.12f);
                m_NextShudder = Time.time + Mathf.Lerp(0.5f, 0.25f, k);
            }
            if (m_DrowningFor >= drownDelay && world != null)
            {
                DreamAudio.Play(DreamSound.Gulp);
                world.RestoreDream(breakthroughMessage);
            }
        }

        /// <summary>A bubble of air: less each time, as the nightmare tightens its grip.</summary>
        public void AddAir(float seconds)
        {
            if (CanBreathe)
                return;
            float gain = seconds * Mathf.Pow(bubbleFalloff, m_Gulps);
            m_Gulps++;
            Air = Mathf.Min(maxAir, Air + gain);
            m_DrowningFor = 0f;
            if (DreamHud.Instance != null)
                DreamHud.Instance.ShowMessage(m_Gulps < 6 ? "You gulp a bubble of air!" : "You gulp a bubble of air...\nIt seems to fill you less each time.", 1.8f);
        }

        void MaybeThink(DreamHud hud)
        {
            if (hud == null || thoughts == null || m_Thought >= thoughts.Length || Time.time < m_NextThoughtAt)
                return;
            hud.ShowMessage(thoughts[m_Thought++], 6f);
            m_NextThoughtAt = Time.time + thoughtInterval;
        }
    }
}
