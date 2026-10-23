using System;
using UnityEngine;

namespace DreamStudio
{
    public enum ComfortSetting { SmoothTurn, Vignette, Seated, SwimSpeed, ShowFps }

    /// <summary>
    /// The player's comfort choices, saved between sessions. Changed from the console in the Start Room
    /// and applied to the rig in every scene by <see cref="DreamComfortRig"/>.
    /// </summary>
    public static class DreamComfort
    {
        public static event Action Changed;

        public static bool SmoothTurn => PlayerPrefs.GetInt("Comfort_SmoothTurn", 0) == 1;
        public static bool Vignette => PlayerPrefs.GetInt("Comfort_Vignette", 1) == 1;
        public static bool Seated => PlayerPrefs.GetInt("Comfort_Seated", 0) == 1;
        public static bool ShowFps => PlayerPrefs.GetInt("Comfort_ShowFps", 0) == 1;

        /// <summary>0 slow, 1 normal, 2 fast.</summary>
        public static int SwimSpeed => PlayerPrefs.GetInt("Comfort_SwimSpeed", 1);
        public static float SwimSpeedMultiplier => SwimSpeed == 0 ? 0.65f : SwimSpeed == 2 ? 1.4f : 1f;

        /// <summary>Steps a setting to its next value.</summary>
        public static void Cycle(ComfortSetting setting)
        {
            switch (setting)
            {
                case ComfortSetting.SmoothTurn: Toggle("Comfort_SmoothTurn", SmoothTurn); break;
                case ComfortSetting.Vignette: Toggle("Comfort_Vignette", Vignette); break;
                case ComfortSetting.Seated: Toggle("Comfort_Seated", Seated); break;
                case ComfortSetting.ShowFps: Toggle("Comfort_ShowFps", ShowFps); break;
                case ComfortSetting.SwimSpeed: PlayerPrefs.SetInt("Comfort_SwimSpeed", (SwimSpeed + 1) % 3); break;
            }
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        static void Toggle(string key, bool current) => PlayerPrefs.SetInt(key, current ? 0 : 1);

        public static string Describe(ComfortSetting setting)
        {
            switch (setting)
            {
                case ComfortSetting.SmoothTurn: return SmoothTurn ? "Turning: Smooth" : "Turning: Snap";
                case ComfortSetting.Vignette: return Vignette ? "Comfort Vignette: On" : "Comfort Vignette: Off";
                case ComfortSetting.Seated: return Seated ? "Play Mode: Seated" : "Play Mode: Standing";
                case ComfortSetting.SwimSpeed: return SwimSpeed == 0 ? "Swim Speed: Slow" : SwimSpeed == 2 ? "Swim Speed: Fast" : "Swim Speed: Normal";
                case ComfortSetting.ShowFps: return ShowFps ? "FPS Meter: On" : "FPS Meter: Off";
                default: return setting.ToString();
            }
        }
    }
}
