using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// A nightmare danger (slime, a sweeping gear, a searchlight). Getting your head within
    /// <see cref="radius"/> of it, measured across the ground, wakes you up back at the start.
    /// Harmless once the dream is restored.
    /// </summary>
    public class DreamHazard : MonoBehaviour
    {
        public float radius = 0.8f;
        [TextArea] public string message = "The nightmare caught you...\nYou wake up where you started.";

        void Update()
        {
            var world = NightmareWorld.Instance;
            if (world == null || world.IsDream || world.IsWakingUp)
                return;

            if (DreamPlayer.FlatDistance(transform.position) < radius)
                world.WakeUp(message);
        }
    }
}
