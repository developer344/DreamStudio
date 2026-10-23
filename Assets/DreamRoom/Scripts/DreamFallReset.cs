using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// Catches players who fall off the world: once the head drops below <see cref="minHeight"/>,
    /// they wake up back at the spawn point.
    /// </summary>
    [RequireComponent(typeof(NightmareWorld))]
    public class DreamFallReset : MonoBehaviour
    {
        public float minHeight = -6f;
        [TextArea] public string message = "You fell...\nYou wake up where you started.";

        NightmareWorld m_World;

        void Awake()
        {
            m_World = GetComponent<NightmareWorld>();
        }

        void Update()
        {
            var head = DreamPlayer.Head;
            if (head != null && head.position.y < minHeight)
                m_World.WakeUp(message);
        }
    }
}
