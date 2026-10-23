using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace DreamStudio
{
    /// <summary>A harmless star in the Start Room for practising grabbing, the way tokens are claimed.</summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class DreamPracticeGrab : MonoBehaviour
    {
        [TextArea] public string message = "That's it! Grab a world's token just like this.";

        bool m_Praised;

        void Awake()
        {
            GetComponent<XRGrabInteractable>().selectEntered.AddListener(_ => Grabbed());
        }

        void Grabbed()
        {
            DreamAudio.Play(DreamSound.Collect);
            DreamHaptics.Pulse(0.5f, 0.15f);
            if (!m_Praised && DreamHud.Instance != null)
                DreamHud.Instance.ShowMessage(message, 4f);
            m_Praised = true;
        }
    }
}
