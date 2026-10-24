using System.Collections;
using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// The player's air supply in an underwater nightmare. Air drains over time and is topped up
    /// by <see cref="AirBubble"/>s. Running out blacks you out back at the start. Once the dream is
    /// restored the player can breathe freely.
    /// </summary>
    public class UnderwaterBreath : MonoBehaviour
    {
        public static UnderwaterBreath Instance { get; private set; }

        [Tooltip("Seconds of air when full.")]
        public float maxAir = 60f;
        [Range(0f, 1f)] public float lowAirThreshold = 0.3f;

        public float Air { get; private set; }
        public bool CanBreathe { get; private set; }

        bool m_BlackingOut;

        void Awake()
        {
            Instance = this;
            Air = maxAir;
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
                if (!CanBreathe && hud != null && !world.ArrivedRestored)
                {
                    hud.SetTint(Color.clear, 0f);
                    hud.SetStatus("AIR  FULL", new Color(0.6f, 1f, 0.95f));
                    StartCoroutine(ClearStatusLater(6f));
                }
                CanBreathe = true;
                return;
            }

            if (m_BlackingOut)
                return;

            Air = Mathf.Max(0f, Air - Time.deltaTime);
            float ratio = Air / maxAir;

            if (hud != null)
            {
                Color color = ratio > lowAirThreshold ? new Color(0.7f, 0.95f, 1f) : new Color(1f, 0.45f, 0.4f);
                hud.SetStatus($"AIR  {Mathf.CeilToInt(ratio * 100f)}%", color);

                float low = 1f - Mathf.Clamp01(ratio / lowAirThreshold);
                DreamAudio.RequestHeartbeat(low);
                float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * Mathf.Lerp(2f, 8f, low));
                hud.SetTint(new Color(0f, 0.02f, 0.08f), low * 0.75f * pulse);
            }

            if (Air <= 0f)
                StartCoroutine(BlackOutRoutine());
        }

        public void AddAir(float seconds)
        {
            if (CanBreathe)
                return;
            Air = Mathf.Min(maxAir, Air + seconds);
            if (DreamHud.Instance != null)
                DreamHud.Instance.ShowMessage("You gulp a bubble of air!", 1.5f);
        }

        IEnumerator BlackOutRoutine()
        {
            m_BlackingOut = true;
            var hud = DreamHud.Instance;
            var world = NightmareWorld.Instance;

            if (hud != null)
            {
                yield return hud.FadeTo(1f, 0.8f, Color.black);
                hud.SetTint(Color.clear, 0f);
                hud.ShowMessage("You ran out of air...\nTouch air bubbles to breathe.\nFind the Magic Shell!", 5f);
            }

            if (world != null && world.spawnPoint != null)
                DreamPlayer.Teleport(world.spawnPoint);
            Air = maxAir;

            yield return new WaitForSeconds(1.5f);
            if (hud != null)
                yield return hud.FadeTo(0f, 1.2f, Color.black);
            m_BlackingOut = false;
        }

        IEnumerator ClearStatusLater(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (DreamHud.Instance != null)
                DreamHud.Instance.SetStatus("", Color.white);
        }
    }
}
