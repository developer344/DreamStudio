using System.Collections;
using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// Counts <see cref="DreamCollectible"/>s. Once enough are gathered, the hidden
    /// <see cref="reward"/> (usually the world's token) pops into view.
    /// </summary>
    public class DreamCollectionGoal : MonoBehaviour
    {
        public static DreamCollectionGoal Instance { get; private set; }

        [Tooltip("Shown in the HUD status line, e.g. 'SUGAR STARS'.")]
        public string itemName = "PIECES";
        public int required = 5;

        [Tooltip("Hidden until every piece is collected.")]
        public GameObject reward;

        [TextArea] public string collectMessage = "You found one!";
        [TextArea] public string completeMessage = "Something appears...";

        public int Collected { get; private set; }

        bool m_Cleared;

        void Awake()
        {
            Instance = this;
            if (reward != null)
                reward.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            var hud = DreamHud.Instance;
            if (hud == null)
                return;

            var world = NightmareWorld.Instance;
            if (world != null && world.IsDream)
            {
                if (!m_Cleared)
                    hud.SetStatus("", Color.white);
                m_Cleared = true;
                return;
            }

            hud.SetStatus($"{itemName}  {Collected} / {required}", new Color(1f, 0.92f, 0.6f));
        }

        public void Collect()
        {
            Collected++;
            var hud = DreamHud.Instance;
            if (Collected < required)
            {
                if (hud != null)
                    hud.ShowMessage($"{collectMessage}\n{required - Collected} to go.", 2.5f);
                return;
            }

            if (hud != null)
                hud.ShowMessage(completeMessage, 6f);
            if (reward != null && !reward.activeSelf)
                StartCoroutine(RevealRoutine());
        }

        IEnumerator RevealRoutine()
        {
            var t = reward.transform;
            Vector3 scale = t.localScale;
            t.localScale = Vector3.zero;
            reward.SetActive(true);
            for (float time = 0f; time < 1.2f; time += Time.deltaTime)
            {
                t.localScale = scale * Mathf.SmoothStep(0f, 1f, time / 1.2f);
                yield return null;
            }
            t.localScale = scale;
        }
    }
}
