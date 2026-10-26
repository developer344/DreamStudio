using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace DreamStudio
{
    /// <summary>
    /// A door that will not open. Grab its handle and it rattles in its frame; the way out is not through it.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class DreamLockedDoor : MonoBehaviour
    {
        [Tooltip("The door leaf, which shakes when the handle is tried.")]
        public Transform leaf;
        [TextArea]
        public string[] messages =
        {
            "The door is locked.",
            "It won't budge.",
            "There is no way out through the door.",
        };

        int m_Tries;
        bool m_Rattling;

        void Awake()
        {
            GetComponent<XRSimpleInteractable>().selectEntered.AddListener(_ => Try());
        }

        void Try()
        {
            var world = NightmareWorld.Instance;
            if (world != null && world.IsDream)
                return;
            DreamAudio.Play(DreamSound.Click);
            DreamHaptics.Pulse(0.6f, 0.2f);
            if (DreamHud.Instance != null)
                DreamHud.Instance.ShowMessage(messages[Mathf.Min(m_Tries, messages.Length - 1)], 2.5f);
            m_Tries++;
            if (!m_Rattling && leaf != null)
                StartCoroutine(Rattle());
        }

        IEnumerator Rattle()
        {
            m_Rattling = true;
            var start = leaf.localPosition;
            for (float t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                leaf.localPosition = start + new Vector3(Mathf.Sin(t * 90f) * 0.008f, 0f, Mathf.Sin(t * 70f) * 0.006f);
                yield return null;
            }
            leaf.localPosition = start;
            m_Rattling = false;
        }
    }
}
