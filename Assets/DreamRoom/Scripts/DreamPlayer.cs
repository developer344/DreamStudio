using Unity.XR.CoreUtils;
using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// Helpers for finding and moving the VR player.
    /// </summary>
    public static class DreamPlayer
    {
        public static Transform Head => Camera.main != null ? Camera.main.transform : null;

        /// <summary>
        /// Moves the XR Origin so the player's head stands over <paramref name="target"/>, facing its forward.
        /// </summary>
        public static void Teleport(Transform target) => Teleport(target.position, target.eulerAngles.y);

        /// <summary>
        /// Moves the XR Origin so the player's head stands over <paramref name="position"/>, facing <paramref name="yaw"/>.
        /// </summary>
        public static void Teleport(Vector3 position, float yaw)
        {
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null)
                return;

            var controller = origin.GetComponentInChildren<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null)
                controller.enabled = false;

            float yawDelta = yaw - origin.Camera.transform.eulerAngles.y;
            origin.RotateAroundCameraUsingOriginUp(yawDelta);

            Vector3 headOnFloor = origin.Camera.transform.position;
            headOnFloor.y = origin.transform.position.y;
            origin.transform.position += position - headOnFloor;

            if (controller != null)
                controller.enabled = wasEnabled;
        }

        /// <summary>Horizontal distance between the head and a world point.</summary>
        public static float FlatDistance(Vector3 point)
        {
            var head = Head;
            if (head == null)
                return float.MaxValue;
            Vector3 d = point - head.position;
            d.y = 0f;
            return d.magnitude;
        }
    }
}
