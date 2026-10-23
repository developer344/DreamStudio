using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace DreamStudio
{
    /// <summary>
    /// Drives a world that the player first sees as a nightmare. The world itself is always its
    /// bright, real self; the nightmare is a <see cref="DreamVision"/> filter over the player's eyes,
    /// plus the creatures and dangers that only exist while it lasts. Finding the token lifts the
    /// filter into the dream look, the nightmare things shrink away, and the way home appears back
    /// at the start point, where the player has to walk to leave.
    /// </summary>
    public class NightmareWorld : MonoBehaviour
    {
        public static NightmareWorld Instance { get; private set; }

        [Header("Story")]
        public string tokenName = "Token";
        [TextArea] public string introMessage;
        [TextArea] public string dreamMessage;
        [TextArea] public string arrivedHomeMessage = "Step through the portal to return to the Portal Room.";

        [Header("Player")]
        public Transform spawnPoint;
        [Tooltip("How close (m) to the spawn point counts as having made it back to the start.")]
        public float returnDistance = 5f;

        [Header("Objects")]
        [Tooltip("Nightmare creatures and dangers. Direct children shrink away when the nightmare lifts.")]
        public GameObject[] nightmareOnly;
        [Tooltip("Hidden until the nightmare lifts, such as the way home. Direct children grow in one by one.")]
        public GameObject[] dreamOnly;

        [Header("Vision")]
        public DreamVision vision;
        public float transitionDuration = 6f;

        public UnityEvent onDreamRestored;

        public bool IsDream { get; private set; }
        public bool IsWakingUp { get; private set; }

        /// <summary>Raised once the player has been put back at the spawn point by <see cref="WakeUp"/>.</summary>
        public event Action WokeUp;

        bool m_Returning;

        /// <summary>
        /// Worlds restored this play session. Kept in memory only, so every new session (each time Play is
        /// pressed in the editor, or the game is launched) starts with every world a nightmare again.
        /// </summary>
        static readonly HashSet<string> s_Restored = new HashSet<string>();

        // Runs at the start of every play session, even when the editor skips reloading scripts.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetProgress() => s_Restored.Clear();

        public static bool IsRestored(string sceneName) => s_Restored.Contains(sceneName);

        void Awake()
        {
            Instance = this;

            foreach (var go in dreamOnly)
                if (go != null)
                    go.SetActive(false);
            foreach (var go in nightmareOnly)
                if (go != null)
                    go.SetActive(true);

            if (vision != null)
                vision.SetBlend(0f);
            DreamAudio.SetNightmareDrone(1f);
        }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);
            if (!IsDream && DreamHud.Instance != null)
                DreamHud.Instance.ShowMessage(introMessage, 8f);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (!m_Returning || spawnPoint == null)
                return;
            var hud = DreamHud.Instance;
            if (hud == null)
                return;

            // Guide the player back to where they started, where the way home now waits.
            float distance = DreamPlayer.FlatDistance(spawnPoint.position);
            if (distance < returnDistance)
            {
                m_Returning = false;
                hud.SetStatus("", Color.white);
                hud.ShowMessage(arrivedHomeMessage, 5f);
                return;
            }
            hud.SetStatus($"RETURN TO THE START  {Mathf.CeilToInt(distance)} m", new Color(1f, 0.9f, 0.97f));
        }

        /// <summary>Called when the player claims the world's token.</summary>
        public void RestoreDream()
        {
            if (IsDream)
                return;
            IsDream = true;
            s_Restored.Add(SceneManager.GetActiveScene().name);
            StartCoroutine(TransitionRoutine());
        }

        /// <summary>Blacks out, shows <paramref name="message"/> and puts the player back at the spawn point.</summary>
        public void WakeUp(string message)
        {
            if (IsWakingUp)
                return;
            StartCoroutine(WakeUpRoutine(message));
        }

        IEnumerator WakeUpRoutine(string message)
        {
            IsWakingUp = true;
            DreamAudio.Play(DreamSound.Caught);
            DreamHaptics.Pulse(1f, 0.4f);
            var hud = DreamHud.Instance;
            if (hud != null)
            {
                yield return hud.FadeTo(1f, 0.4f, Color.black);
                hud.SetTint(Color.clear, 0f);
                hud.ShowMessage(message, 5f);
            }

            if (spawnPoint != null)
                DreamPlayer.Teleport(spawnPoint);
            WokeUp?.Invoke();

            yield return new WaitForSeconds(1.5f);
            if (hud != null)
                yield return hud.FadeTo(0f, 1.2f, Color.black);
            IsWakingUp = false;
        }

        IEnumerator TransitionRoutine()
        {
            DreamAudio.SetNightmareDrone(0f);
            DreamAudio.Play(DreamSound.Choir);
            var hud = DreamHud.Instance;
            if (hud != null)
            {
                hud.SetTint(Color.clear, 0f);
                hud.ShowMessage($"You found the {tokenName}!", 3f);
                yield return hud.FadeTo(0.8f, 0.5f, Color.white);
            }

            var shrinking = CollectParts(nightmareOnly);
            var growing = CollectParts(dreamOnly);
            foreach (var go in dreamOnly)
                if (go != null)
                    go.SetActive(true);
            foreach (var part in growing)
                part.transform.localScale = Vector3.zero;

            if (hud != null)
                hud.FadeTo(0f, 1.5f, Color.white);

            for (float t = 0f; t < transitionDuration; t += Time.deltaTime)
            {
                if (vision != null)
                    vision.SetBlend(Mathf.SmoothStep(0f, 1f, t / transitionDuration));

                float shrinkK = Mathf.Clamp01(t / (transitionDuration * 0.5f));
                foreach (var part in shrinking)
                    if (part.transform != null)
                        part.transform.localScale = part.scale * (1f - Mathf.SmoothStep(0f, 1f, shrinkK));

                // Stagger growth by distance from the player so the dream spreads outward.
                foreach (var part in growing)
                {
                    float delay = Mathf.Clamp01(part.distance / 60f) * 0.5f;
                    float g = Mathf.Clamp01((t / transitionDuration - delay) / 0.5f);
                    part.transform.localScale = part.scale * EaseOutBack(g);
                }
                yield return null;
            }

            if (vision != null)
                vision.SetBlend(1f);
            foreach (var part in growing)
                part.transform.localScale = part.scale;
            foreach (var go in nightmareOnly)
                if (go != null)
                    go.SetActive(false);
            foreach (var part in shrinking)
                part.transform.localScale = part.scale;

            if (hud != null)
                hud.ShowMessage(dreamMessage, 8f);
            m_Returning = true;
            onDreamRestored?.Invoke();
        }

        struct Part
        {
            public Transform transform;
            public Vector3 scale;
            public float distance;
        }

        static List<Part> CollectParts(GameObject[] roots)
        {
            var parts = new List<Part>();
            var head = DreamPlayer.Head;
            foreach (var root in roots)
            {
                if (root == null)
                    continue;
                var t = root.transform;
                if (t.childCount == 0)
                {
                    parts.Add(MakePart(t, head));
                    continue;
                }
                for (int i = 0; i < t.childCount; i++)
                    parts.Add(MakePart(t.GetChild(i), head));
            }
            return parts;
        }

        static Part MakePart(Transform t, Transform head)
        {
            return new Part
            {
                transform = t,
                scale = t.localScale,
                distance = head != null ? Vector3.Distance(head.position, t.position) : 0f,
            };
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
