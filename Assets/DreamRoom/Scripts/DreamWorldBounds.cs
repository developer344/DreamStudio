using Unity.XR.CoreUtils;
using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// Keeps the player inside a world's round play area however they move: thumbstick, teleport,
    /// swimming, walking in the real room or moving the headset in the simulator (the last two move
    /// only the head, so walls alone can't stop them). If the head crosses the edge, the rig is slid
    /// straight back in. As a last safety net, anyone who somehow drops far below the world wakes up
    /// back at the start.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    public class DreamWorldBounds : MonoBehaviour
    {
        [Tooltip("How far (m) from this object's position the player's head may go.")]
        public float radius = 79f;

        [Tooltip("Falling this far (m) below this object's height counts as falling out of the world.")]
        public float fallDepth = 15f;

        [TextArea] public string fellOutMessage = "You slipped out of the dream...\nYou wake up where you started.";

        void LateUpdate()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null)
                return;

            var rig = origin.transform;
            var head = origin.Camera.transform.position;
            var center = transform.position;

            var offset = new Vector2(head.x - center.x, head.z - center.z);
            if (offset.magnitude > radius)
            {
                var inside = offset.normalized * radius;
                rig.position += new Vector3(inside.x - offset.x, 0f, inside.y - offset.y);
                Physics.SyncTransforms();
            }

            if (head.y < center.y - fallDepth)
            {
                var world = NightmareWorld.Instance;
                if (world != null)
                    world.WakeUp(fellOutMessage);
            }
        }
    }
}
