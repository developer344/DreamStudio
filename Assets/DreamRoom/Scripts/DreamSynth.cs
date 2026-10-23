using System;
using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// Synthesizes the game's default sounds in code, so it is never silent even before real audio
    /// files are added. Loops are made seamless by crossfading their tail into their head.
    /// </summary>
    public static class DreamSynth
    {
        const int Rate = 22050;
        const float Tau = Mathf.PI * 2f;

        public static AudioClip Make(DreamSound sound)
        {
            var rng = new System.Random((int)sound * 7919 + 17);
            switch (sound)
            {
                case DreamSound.HubAmbience: return Loop(sound, 12f, (t, s) => Pad(t, new[] { 130.8f, 164.8f, 196f, 246.9f }, 0.05f) + Air(s, rng, 0.05f, 900f));
                case DreamSound.ForestAmbience: return Loop(sound, 10f, (t, s) => Wind(s, rng, t, 600f, 0.35f) + Chirps(t, rng, 0.05f));
                case DreamSound.OceanAmbience: return Loop(sound, 10f, (t, s) => Wind(s, rng, t, 220f, 0.55f) + Blips(t, rng, 0.08f));
                case DreamSound.CandyAmbience: return Loop(sound, 12f, (t, s) => Wind(s, rng, t, 900f, 0.15f) + MusicBox(t, rng));
                case DreamSound.MoonAmbience: return Loop(sound, 10f, (t, s) => Pad(t, new[] { 55f, 110f, 164.8f }, 0.06f) + Air(s, rng, 0.06f, 1500f));
                case DreamSound.CaveAmbience: return Echo(Loop(sound, 12f, (t, s) => Air(s, rng, 0.06f, 300f) + Drips(t, rng)), 0.31f, 0.4f);
                case DreamSound.CityAmbience: return Loop(sound, 10f, (t, s) => Wind(s, rng, t, 1200f, 0.4f) + Pad(t, new[] { 98f, 146.8f }, 0.03f));
                case DreamSound.NightmareDrone: return Loop(sound, 10f, (t, s) => Pad(t, new[] { 55f, 58.3f, 82.4f, 87.3f }, 0.12f) + Wind(s, rng, t, 120f, 0.4f));
                case DreamSound.PortalHum: return Loop(sound, 4f, (t, s) => Pad(t, new[] { 220f, 277.2f, 329.6f, 440f }, 0.09f) * (0.8f + 0.2f * Mathf.Sin(t * Tau * 3f)));
                case DreamSound.GearGrind: return Loop(sound, 2f, (t, s) => Clicks(t, 12f, rng, 0.35f) + Pad(t, new[] { 73.4f, 110f }, 0.05f));
                case DreamSound.ClockTick: return Loop(sound, 2f, (t, s) => Click(t, 0f, 2200f) * 0.6f + Click(t, 1f, 1600f) * 0.5f);
                case DreamSound.StalkerBreath: return Loop(sound, 3f, (t, s) => Breath(s, rng, t));
                case DreamSound.Heartbeat: return Loop(sound, 1f, (t, s) => Thump(t, 0f) + Thump(t, 0.28f) * 0.7f);
                case DreamSound.Choir: return Shot(sound, 4.5f, t => Choir(t));
                case DreamSound.TokenChime: return Shot(sound, 2.5f, t => Bell(t, 880f) + Bell(t - 0.12f, 1318.5f) * 0.7f + Bell(t - 0.24f, 1760f) * 0.5f);
                case DreamSound.Collect: return Shot(sound, 0.8f, t => Bell(t, 659.3f) * 0.6f + Bell(t - 0.08f, 830.6f) * 0.6f + Bell(t - 0.16f, 987.8f) * 0.6f);
                case DreamSound.Gulp: return Shot(sound, 0.45f, t => Sweep(t, 0f, 0.15f, 300f, 900f) + Sweep(t, 0.18f, 0.15f, 350f, 1000f));
                case DreamSound.Caught: return Shot(sound, 1.4f, t => Hit(t, rng) + Sweep(t, 0f, 1.2f, 220f, 55f) * 0.6f);
                case DreamSound.PortalWhoosh: return Shot(sound, 1.3f, t => Whoosh(t, rng));
                case DreamSound.Click: return Shot(sound, 0.06f, t => Click(t, 0f, 1800f));
                default: return null;
            }
        }

        // ---------- Building clips ----------

        /// <summary>A seamless loop: generate a little extra and crossfade the tail into the head.</summary>
        static AudioClip Loop(DreamSound name, float seconds, Func<float, int, float> sample)
        {
            int length = Mathf.RoundToInt(seconds * Rate);
            int fade = Rate / 2;
            var raw = new float[length + fade];
            for (int i = 0; i < raw.Length; i++)
                raw[i] = sample((float)i / Rate, i);
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float k = i < fade ? (float)i / fade : 1f;
                data[i] = i < fade ? raw[i] * k + raw[length + i] * (1f - k) : raw[i];
            }
            return ToClip(name, data);
        }

        static AudioClip Shot(DreamSound name, float seconds, Func<float, float> sample)
        {
            var data = new float[Mathf.RoundToInt(seconds * Rate)];
            for (int i = 0; i < data.Length; i++)
                data[i] = sample((float)i / Rate);
            return ToClip(name, data);
        }

        static AudioClip ToClip(DreamSound name, float[] data)
        {
            float peak = 0f;
            foreach (float d in data)
                peak = Mathf.Max(peak, Mathf.Abs(d));
            if (peak > 0.95f)
                for (int i = 0; i < data.Length; i++)
                    data[i] *= 0.95f / peak;
            var clip = AudioClip.Create(name.ToString(), data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A feedback echo over a finished clip (for caves).</summary>
        static AudioClip Echo(AudioClip clip, float delaySeconds, float feedback)
        {
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            int delay = Mathf.RoundToInt(delaySeconds * Rate);
            for (int i = 0; i < data.Length; i++)
                data[i] += data[(i - delay + data.Length) % data.Length] * feedback;
            clip.SetData(data, 0);
            return clip;
        }

        // ---------- Ingredients ----------

        static float s_Low, s_Low2, s_Band;

        static float Noise(System.Random rng) => (float)rng.NextDouble() * 2f - 1f;

        static float LowPass(ref float state, float x, float cutoff)
        {
            float a = 1f - Mathf.Exp(-Tau * cutoff / Rate);
            state += a * (x - state);
            return state;
        }

        /// <summary>Soft filtered noise: air, room tone.</summary>
        static float Air(int s, System.Random rng, float level, float cutoff) => LowPass(ref s_Low, Noise(rng), cutoff) * level * 4f;

        /// <summary>Gusting wind: filtered noise swelling slowly.</summary>
        static float Wind(int s, System.Random rng, float t, float cutoff, float level)
        {
            float gust = 0.55f + 0.45f * Mathf.Sin(t * Tau * 0.1f) * Mathf.Sin(t * Tau * 0.037f + 1f);
            return LowPass(ref s_Low2, Noise(rng), cutoff * (0.7f + 0.6f * gust)) * level * gust * 3f;
        }

        /// <summary>A soft chord with slow tremolo and a little detune.</summary>
        static float Pad(float t, float[] notes, float level)
        {
            float sum = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float trem = 0.7f + 0.3f * Mathf.Sin(t * Tau * (0.13f + i * 0.05f));
                sum += (Mathf.Sin(t * Tau * notes[i]) + 0.5f * Mathf.Sin(t * Tau * notes[i] * 1.003f)) * trem;
            }
            return sum * level / notes.Length * 3f;
        }

        static float Envelope(float t, float attack, float decay) => t < 0f ? 0f : (t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay));

        static float Bell(float t, float freq)
        {
            if (t < 0f)
                return 0f;
            float env = Envelope(t, 0.005f, 0.6f);
            return (Mathf.Sin(t * Tau * freq) + 0.4f * Mathf.Sin(t * Tau * freq * 2.76f) * Mathf.Exp(-t * 4f)) * env * 0.4f;
        }

        static float Click(float t, float at, float freq)
        {
            float dt = t - at;
            return dt < 0f || dt > 0.05f ? 0f : Mathf.Sin(dt * Tau * freq) * Mathf.Exp(-dt * 120f);
        }

        static float Clicks(float t, float perSecond, System.Random rng, float level)
        {
            float phase = t * perSecond;
            float dt = (phase - Mathf.Floor(phase)) / perSecond;
            return Mathf.Sin(dt * Tau * 900f) * Mathf.Exp(-dt * 200f) * level + Noise(rng) * Mathf.Exp(-dt * 300f) * level * 0.5f;
        }

        static float Thump(float t, float at)
        {
            float dt = t - at;
            return dt < 0f ? 0f : Mathf.Sin(dt * Tau * (55f - dt * 40f)) * Mathf.Exp(-dt * 18f) * 0.9f;
        }

        static float Sweep(float t, float at, float length, float from, float to)
        {
            float dt = t - at;
            if (dt < 0f || dt > length)
                return 0f;
            float k = dt / length;
            float freq = Mathf.Lerp(from, to, k);
            return Mathf.Sin(dt * Tau * freq) * Mathf.Sin(k * Mathf.PI) * 0.5f;
        }

        static float Breath(int s, System.Random rng, float t)
        {
            float cycle = Mathf.Sin(t / 3f * Tau);
            float env = Mathf.Max(0f, cycle) * 0.8f + Mathf.Max(0f, -cycle) * 0.5f;
            float hiss = LowPass(ref s_Band, Noise(rng), 1400f) - LowPass(ref s_Low, Noise(rng) * 0.2f, 300f);
            return hiss * env * 0.8f;
        }

        static float Choir(float t)
        {
            float env = Mathf.Clamp01(t / 1.2f) * Mathf.Clamp01((4.5f - t) / 1.5f);
            float vibrato = 1f + 0.006f * Mathf.Sin(t * Tau * 5f);
            float sum = 0f;
            foreach (float f in new[] { 261.6f, 329.6f, 392f, 523.3f })
                for (int v = -1; v <= 1; v++)
                    sum += Mathf.Sin(t * Tau * f * vibrato * (1f + v * 0.004f)) * (1f / (1f + Mathf.Abs(v)));
            return sum * env * 0.08f;
        }

        static float Hit(float t, System.Random rng)
        {
            return LowPass(ref s_Low, Noise(rng), 400f) * Mathf.Exp(-t * 3f) * 1.5f;
        }

        static float Whoosh(float t, System.Random rng)
        {
            float k = t / 1.3f;
            float env = Mathf.Sin(k * Mathf.PI);
            return LowPass(ref s_Low2, Noise(rng), 200f + 3000f * env) * env * 1.4f;
        }

        // Sparse random events over a loop, from a fixed per-sound pattern.
        static float Chirps(float t, System.Random rng, float level)
        {
            float sum = 0f;
            foreach (float at in new[] { 1.3f, 1.45f, 4.8f, 7.2f, 7.32f })
                sum += Sweep(t, at, 0.09f, 2800f, 3600f);
            return sum * level * 4f;
        }

        static float Blips(float t, System.Random rng, float level)
        {
            float sum = 0f;
            foreach (float at in new[] { 0.7f, 2.9f, 3.05f, 5.6f, 8.1f, 8.3f })
                sum += Sweep(t, at, 0.12f, 400f, 1100f);
            return sum * level * 4f;
        }

        static float MusicBox(float t, System.Random rng)
        {
            float[] notes = { 1046.5f, 1174.7f, 1318.5f, 1568f, 1760f, 2093f };
            float sum = 0f;
            for (int i = 0; i < 9; i++)
                sum += Bell(t - (0.6f + i * 1.25f), notes[(i * 4 + 1) % notes.Length]) * 0.35f;
            return sum;
        }

        static float Drips(float t, System.Random rng)
        {
            float sum = 0f;
            foreach (float at in new[] { 0.9f, 3.4f, 5.1f, 8.6f, 10.2f })
                sum += Sweep(t, at, 0.06f, 1900f, 1100f) * 0.6f;
            return sum;
        }
    }
}
