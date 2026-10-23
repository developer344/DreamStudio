using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace DreamStudio
{
    /// <summary>Controller rumble for moments that should feel physical.</summary>
    public static class DreamHaptics
    {
        /// <summary>Rumbles both controllers. Amplitude 0..1, duration in seconds.</summary>
        public static void Pulse(float amplitude = 0.5f, float duration = 0.1f)
        {
            foreach (var player in Object.FindObjectsByType<HapticImpulsePlayer>())
                player.SendHapticImpulse(amplitude, duration);
        }
    }
}
