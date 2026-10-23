using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DreamStudio
{
    /// <summary>
    /// Editor-only fallback so the left and right arrow keys always turn the view while testing
    /// without a headset. The Meta XR Simulator also binds the arrows, but it only rotates whichever
    /// device it currently has selected (headset or a controller) and only while it has keyboard focus.
    /// If the simulator is already turning the head, this stays out of the way.
    /// </summary>
    public class DreamKeyboardTurn : MonoBehaviour
    {
        public float degreesPerSecond = 90f;

        float m_LastHeadYaw;
        bool m_HasLastYaw;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (!Application.isEditor)
                return;
            var go = new GameObject("Keyboard Turn (Editor)");
            DontDestroyOnLoad(go);
            go.AddComponent<DreamKeyboardTurn>();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var origin = FindAnyObjectByType<XROrigin>();
            if (keyboard == null || origin == null || origin.Camera == null)
            {
                m_HasLastYaw = false;
                return;
            }

            float input = (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f);

            // Yaw of the tracked head relative to the rig: it only changes when the simulator
            // (or a real headset) turns the head, never when we turn the rig.
            float headYaw = origin.Camera.transform.localEulerAngles.y;
            bool simulatorTurning = m_HasLastYaw && Mathf.Abs(Mathf.DeltaAngle(m_LastHeadYaw, headYaw)) > 0.05f;
            m_LastHeadYaw = headYaw;
            m_HasLastYaw = true;

            if (input != 0f && !simulatorTurning)
                origin.RotateAroundCameraUsingOriginUp(input * degreesPerSecond * Time.deltaTime);
        }
    }
}
