using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// One of several pieces a world asks you to gather. Walk up to it to collect it; it reports to
    /// the world's <see cref="DreamCollectionGoal"/>. It can vanish (a star you pick up) or change
    /// state (a crystal you light) by swapping <see cref="hideOnCollect"/> for <see cref="showOnCollect"/>.
    /// </summary>
    public class DreamCollectible : MonoBehaviour
    {
        [Tooltip("Horizontal head distance (m) that collects this piece.")]
        public float touchDistance = 1f;

        public GameObject hideOnCollect;
        public GameObject showOnCollect;

        public bool IsCollected { get; private set; }

        void Awake()
        {
            if (showOnCollect != null)
                showOnCollect.SetActive(false);
        }

        void Update()
        {
            if (IsCollected || DreamPlayer.FlatDistance(transform.position) >= touchDistance)
                return;

            IsCollected = true;
            DreamAudio.Play(DreamSound.Collect);
            DreamHaptics.Pulse(0.5f, 0.15f);
            if (hideOnCollect != null)
                hideOnCollect.SetActive(false);
            if (showOnCollect != null)
                showOnCollect.SetActive(true);
            if (DreamCollectionGoal.Instance != null)
                DreamCollectionGoal.Instance.Collect();
        }
    }
}
