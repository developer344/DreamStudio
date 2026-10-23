using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// Starts a sound when its scene loads: either the scene's background ambience (heard everywhere)
    /// or a 3D loop coming from this object (gears grinding, a clock ticking).
    /// </summary>
    public class DreamSoundEmitter : MonoBehaviour
    {
        public DreamSound sound;
        [Range(0f, 1f)] public float volume = 0.5f;

        [Tooltip("Off: plays everywhere as this scene's ambience. On: a 3D loop from this object.")]
        public bool spatial = true;

        [Tooltip("For 3D loops: the distance (m) at which it fades out completely.")]
        public float maxDistance = 15f;

        void Start()
        {
            if (spatial)
                DreamAudio.AttachLoop(gameObject, sound, volume, maxDistance);
            else
                DreamAudio.SetAmbience(sound, volume);
        }
    }
}
