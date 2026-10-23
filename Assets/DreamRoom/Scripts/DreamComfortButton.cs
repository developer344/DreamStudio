using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace DreamStudio
{
    /// <summary>
    /// A button on the comfort console. Point at it (or reach it) and squeeze the grip to step its
    /// setting; the label always shows the current choice.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class DreamComfortButton : MonoBehaviour
    {
        public ComfortSetting setting;
        public TextMeshPro label;

        void Awake()
        {
            GetComponent<XRSimpleInteractable>().selectEntered.AddListener(_ => Press());
            DreamComfort.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            DreamComfort.Changed -= Refresh;
        }

        void Press()
        {
            DreamComfort.Cycle(setting);
            DreamAudio.Play(DreamSound.Click);
            DreamHaptics.Pulse(0.3f, 0.05f);
        }

        void Refresh()
        {
            if (label != null)
                label.text = DreamComfort.Describe(setting);
        }
    }
}
