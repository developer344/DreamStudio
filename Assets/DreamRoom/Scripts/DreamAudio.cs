using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DreamStudio
{
    public enum DreamSound
    {
        None,
        // Ambience loops, one per place.
        HubAmbience, ForestAmbience, OceanAmbience, CandyAmbience, MoonAmbience, CaveAmbience, CityAmbience,
        // Loops tied to things.
        NightmareDrone, PortalHum, GearGrind, ClockTick, StalkerBreath, Heartbeat,
        // One-shots.
        Choir, TokenChime, Collect, Gulp, Caught, PortalWhoosh, Click,
    }

    /// <summary>
    /// Plays the game's sound. Every sound has a built-in synthesized default (<see cref="DreamSynth"/>);
    /// drop an audio file named after the sound (e.g. "ForestAmbience.wav") into a
    /// Resources/DreamAudio folder to replace it. Lives across scene loads so one-shots (a portal
    /// whoosh) carry on into the next scene, and ambience crossfades between places.
    /// </summary>
    public class DreamAudio : MonoBehaviour
    {
        public static DreamAudio Instance { get; private set; }

        static readonly Dictionary<DreamSound, AudioClip> s_Clips = new Dictionary<DreamSound, AudioClip>();

        AudioSource m_OneShots;
        AudioSource m_Ambience;
        AudioSource m_AmbienceOut;
        AudioSource m_Drone;
        AudioSource m_Heartbeat;
        float m_AmbienceVolume;
        float m_DroneTarget;
        float m_HeartbeatRequest;
        float m_Crossfade = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Create()
        {
            var go = new GameObject("Dream Audio");
            DontDestroyOnLoad(go);
            go.AddComponent<DreamAudio>();
        }

        void Awake()
        {
            Instance = this;
            m_OneShots = NewSource(false);
            m_Ambience = NewSource(true);
            m_AmbienceOut = NewSource(true);
            m_Drone = NewSource(true);
            m_Drone.clip = Clip(DreamSound.NightmareDrone);
            m_Drone.volume = 0f;
            m_Drone.Play();
            m_Heartbeat = NewSource(true);
            m_Heartbeat.clip = Clip(DreamSound.Heartbeat);
            m_Heartbeat.volume = 0f;
            m_Heartbeat.Play();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            m_DroneTarget = 0f;
        }

        AudioSource NewSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        /// <summary>The clip for a sound: a Resources/DreamAudio override if there is one, else the synthesized default.</summary>
        public static AudioClip Clip(DreamSound sound)
        {
            if (sound == DreamSound.None)
                return null;
            if (!s_Clips.TryGetValue(sound, out var clip) || clip == null)
            {
                clip = Resources.Load<AudioClip>("DreamAudio/" + sound);
                if (clip == null)
                    clip = DreamSynth.Make(sound);
                s_Clips[sound] = clip;
            }
            return clip;
        }

        /// <summary>A sound heard the same everywhere (UI, rewards, stingers).</summary>
        public static void Play(DreamSound sound, float volume = 1f)
        {
            if (Instance != null && sound != DreamSound.None)
                Instance.m_OneShots.PlayOneShot(Clip(sound), volume);
        }

        /// <summary>A one-shot coming from a point in the world.</summary>
        public static void PlayAt(DreamSound sound, Vector3 position, float volume = 1f)
        {
            if (sound != DreamSound.None)
                AudioSource.PlayClipAtPoint(Clip(sound), position, volume);
        }

        /// <summary>Crossfades the background loop to <paramref name="sound"/>.</summary>
        public static void SetAmbience(DreamSound sound, float volume = 0.5f)
        {
            var self = Instance;
            if (self == null)
                return;
            var clip = Clip(sound);
            if (self.m_Ambience.clip == clip)
            {
                self.m_AmbienceVolume = volume;
                return;
            }
            (self.m_Ambience, self.m_AmbienceOut) = (self.m_AmbienceOut, self.m_Ambience);
            self.m_Ambience.clip = clip;
            self.m_Ambience.volume = 0f;
            if (clip != null)
                self.m_Ambience.Play();
            self.m_AmbienceVolume = volume;
            self.m_Crossfade = 0f;
        }

        /// <summary>How loud the low nightmare drone under everything is (0 = gone).</summary>
        public static void SetNightmareDrone(float level)
        {
            if (Instance != null)
                Instance.m_DroneTarget = Mathf.Clamp01(level);
        }

        /// <summary>Asks for a heartbeat this frame (0..1: faster and louder with danger). The strongest request wins.</summary>
        public static void RequestHeartbeat(float intensity)
        {
            if (Instance != null)
                Instance.m_HeartbeatRequest = Mathf.Max(Instance.m_HeartbeatRequest, Mathf.Clamp01(intensity));
        }

        /// <summary>Adds a looping 3D sound to <paramref name="target"/>.</summary>
        public static AudioSource AttachLoop(GameObject target, DreamSound sound, float volume, float maxDistance)
        {
            var source = target.AddComponent<AudioSource>();
            source.clip = Clip(sound);
            source.loop = true;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            source.time = Random.Range(0f, source.clip != null ? source.clip.length : 0f);
            source.Play();
            return source;
        }

        // Late, so this frame's heartbeat requests (made in other scripts' Update) are all in.
        void LateUpdate()
        {
            m_Crossfade = Mathf.MoveTowards(m_Crossfade, 1f, Time.deltaTime / 2f);
            m_Ambience.volume = m_AmbienceVolume * m_Crossfade;
            m_AmbienceOut.volume = m_AmbienceOut.volume > 0f ? Mathf.MoveTowards(m_AmbienceOut.volume, 0f, Time.deltaTime / 2f) : 0f;
            if (m_AmbienceOut.volume <= 0f && m_AmbienceOut.isPlaying)
                m_AmbienceOut.Stop();

            m_Drone.volume = Mathf.MoveTowards(m_Drone.volume, m_DroneTarget * 0.35f, Time.deltaTime * 0.3f);

            float beat = m_HeartbeatRequest;
            m_Heartbeat.volume = Mathf.MoveTowards(m_Heartbeat.volume, beat * 0.9f, Time.deltaTime * 2f);
            m_Heartbeat.pitch = 0.85f + beat * 0.7f;
            m_HeartbeatRequest = 0f;
        }
    }
}
