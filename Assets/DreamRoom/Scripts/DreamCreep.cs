using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace DreamStudio
{
    /// <summary>
    /// Makes a nightmare danger worse the longer it's left alone: it slowly spreads (growing its look and
    /// its deadly radius together) and can creep toward a player who comes near, sliding over the ground.
    /// Waking up resets it, so every attempt starts fair.
    /// </summary>
    [RequireComponent(typeof(DreamHazard))]
    public class DreamCreep : MonoBehaviour
    {
        [Tooltip("How big (x) it grows by the end of growTime.")]
        public float maxGrowth = 1.8f;
        public float growTime = 200f;

        [Tooltip("Speed (m/s) it creeps toward a nearby player. 0 keeps it in place.")]
        public float chaseSpeed;
        public float chaseRange = 12f;
        [Tooltip("How far (m) it may wander from where it started.")]
        public float leash = 12f;

        DreamHazard m_Hazard;
        float m_BaseRadius;
        Vector3 m_BaseScale;
        Vector3 m_Home;
        float m_StartTime;
        NightmareWorld m_World;

        void Start()
        {
            m_Hazard = GetComponent<DreamHazard>();
            m_BaseRadius = m_Hazard.radius;
            m_BaseScale = transform.localScale;
            m_Home = transform.position;
            m_StartTime = Time.time;
            m_World = NightmareWorld.Instance;
            if (m_World != null)
                m_World.WokeUp += ResetCreep;
        }

        void OnDestroy()
        {
            if (m_World != null)
                m_World.WokeUp -= ResetCreep;
        }

        void ResetCreep()
        {
            m_StartTime = Time.time;
            transform.position = m_Home;
        }

        void Update()
        {
            if (m_World == null || m_World.IsDream)
                return;

            float growth = Mathf.Lerp(1f, maxGrowth, Mathf.Clamp01((Time.time - m_StartTime) / growTime));
            transform.localScale = m_BaseScale * growth;
            m_Hazard.radius = m_BaseRadius * growth;

            var head = DreamPlayer.Head;
            if (chaseSpeed <= 0f || head == null || m_World.IsWakingUp)
                return;
            var toPlayer = head.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.magnitude > chaseRange || toPlayer.magnitude < 0.1f)
                return;

            var next = transform.position + toPlayer.normalized * chaseSpeed * Time.deltaTime;
            var fromHome = next - m_Home;
            fromHome.y = 0f;
            if (fromHome.magnitude > leash)
                next = m_Home + fromHome.normalized * leash + Vector3.up * (next.y - m_Home.y);
            next.y = GroundUnder(next, transform.position.y);
            transform.position = next;
        }

        static readonly RaycastHit[] s_Hits = new RaycastHit[8];

        /// <summary>Height of the walkable ground under a point, so the danger slides over hills.</summary>
        static float GroundUnder(Vector3 p, float fallback)
        {
            int count = Physics.RaycastNonAlloc(p + Vector3.up * 5f, Vector3.down, s_Hits, 15f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
                if (s_Hits[i].point.y > best && s_Hits[i].collider.GetComponentInParent<TeleportationArea>() != null)
                    best = s_Hits[i].point.y;
            return float.IsNegativeInfinity(best) ? fallback : best + 0.02f;
        }
    }
}
