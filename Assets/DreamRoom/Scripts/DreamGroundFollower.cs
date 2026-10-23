using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace DreamStudio
{
    /// <summary>
    /// Keeps the player's feet on rolling ground. Thumbstick locomotion already slides the rig's
    /// character controller over hills, but walking in the real room (or moving the headset in the
    /// simulator) moves only the head, so going uphill the floor would end up inside the hill. This
    /// lifts the rig so the floor under the head meets the ground. It only ever lifts: going down is
    /// left to gravity, so jumping and falling (off a rooftop, say) still work.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class DreamGroundFollower : MonoBehaviour
    {
        [Tooltip("Ignore ground more than this far above the current floor (a rooftop overhead, a ledge).")]
        public float maxStep = 1.5f;

        [Tooltip("How quickly the rig rises onto higher ground.")]
        public float riseSpeed = 12f;

        readonly RaycastHit[] m_Hits = new RaycastHit[16];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("Ground Follower");
            DontDestroyOnLoad(go);
            go.AddComponent<DreamGroundFollower>();
        }

        void LateUpdate()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null)
                return;

            var rig = origin.transform;
            var head = origin.Camera.transform.position;
            float floor = rig.position.y;

            // Look straight down through the head for the highest walkable surface (the ground,
            // a rooftop, a bridge: anything you can teleport onto) that isn't too far above us.
            var start = new Vector3(head.x, floor + maxStep + 2f, head.z);
            int count = Physics.RaycastNonAlloc(start, Vector3.down, m_Hits, maxStep + 6f, ~0, QueryTriggerInteraction.Ignore);
            float ground = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = m_Hits[i];
                if (hit.point.y > floor + maxStep || hit.point.y <= ground)
                    continue;
                if (hit.collider.GetComponentInParent<TeleportationArea>() != null)
                    ground = hit.point.y;
            }

            float rise = ground - floor;
            if (rise <= 0.005f)
                return;

            float step = rise * (1f - Mathf.Exp(-riseSpeed * Time.deltaTime));
            rig.position += Vector3.up * Mathf.Max(step, Mathf.Min(rise, 0.005f));
            Physics.SyncTransforms();
        }
    }
}
