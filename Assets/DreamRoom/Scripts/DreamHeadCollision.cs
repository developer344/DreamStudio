using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace DreamStudio
{
    /// <summary>
    /// Stops the player's head phasing into solid things. The rig's character controller already
    /// blocks thumbstick movement, but walking in the real room (or moving the headset in the
    /// simulator) moves only the head. When the head pushes into a solid collider, the rig is slid
    /// back sideways by exactly the overlap, so the view stays outside rocks, trees, walls and props.
    /// Walkable surfaces (ground, rooftops) are left to <see cref="DreamGroundFollower"/>.
    /// </summary>
    [DefaultExecutionOrder(1050)]
    public class DreamHeadCollision : MonoBehaviour
    {
        [Tooltip("Size of the head (m).")]
        public float headRadius = 0.15f;

        readonly Collider[] m_Overlaps = new Collider[16];
        SphereCollider m_Probe;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("Head Collision");
            DontDestroyOnLoad(go);
            go.AddComponent<DreamHeadCollision>();
        }

        void Awake()
        {
            // A trigger on Ignore Raycast: used only to measure overlaps, never to block anything.
            gameObject.layer = 2;
            m_Probe = gameObject.AddComponent<SphereCollider>();
            m_Probe.isTrigger = true;
            m_Probe.radius = headRadius;
        }

        void LateUpdate()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null || origin.Camera == null)
                return;

            var rig = origin.transform;
            var head = origin.Camera.transform.position;
            transform.position = head;

            int count = Physics.OverlapSphereNonAlloc(head, headRadius, m_Overlaps, ~0, QueryTriggerInteraction.Ignore);
            var push = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                var other = m_Overlaps[i];
                if (other == m_Probe || other.transform.IsChildOf(rig) || IsPassable(other))
                    continue;
                if (Physics.ComputePenetration(m_Probe, head, Quaternion.identity, other, other.transform.position, other.transform.rotation,
                        out var direction, out float distance))
                    push += direction * distance;
            }

            push.y = 0f;
            if (push.sqrMagnitude < 1e-6f)
                return;
            rig.position += push;
            Physics.SyncTransforms();
        }

        /// <summary>Things the head may enter: the ground it stands on, and things meant to be touched or grabbed.</summary>
        static bool IsPassable(Collider other)
        {
            return other.GetComponentInParent<TeleportationArea>() != null
                || other.GetComponentInParent<XRBaseInteractable>() != null
                || other.GetComponentInParent<DreamPortal>() != null;
        }
    }
}
