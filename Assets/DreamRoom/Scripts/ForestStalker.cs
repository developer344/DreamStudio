using System.Collections;
using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// A shadow that drifts through the trees after the player. It creeps faster when you
    /// aren't looking at it, and blinks back behind you if you outrun it. If it reaches you,
    /// the view goes dark and you wake up back at the start.
    /// </summary>
    public class ForestStalker : MonoBehaviour
    {
        public float speed = 1.7f;
        [Tooltip("Speed multiplier while the player is looking straight at it.")]
        public float watchedSpeedMultiplier = 0.4f;
        [Tooltip("Speed multiplier while it is behind the player.")]
        public float unwatchedSpeedMultiplier = 1.3f;
        public float catchDistance = 1.1f;
        [Tooltip("Distance at which the view starts darkening with dread.")]
        public float dreadDistance = 9f;
        [Tooltip("If the player gets farther than this, it reappears behind them.")]
        public float maxDistance = 22f;
        public float reappearDistance = 13f;
        public float startDelay = 10f;

        public Light eyeLight;

        [Tooltip("Ground the stalker glides over, so it follows hills.")]
        public Collider ground;

        [Tooltip("The token: the closer the player gets to it, the harder the stalker hunts.")]
        public Transform goal;
        [Tooltip("Extra speed when the player is right at the goal (1 = twice as fast).")]
        public float huntBoost = 0.8f;
        public float huntRange = 40f;

        Vector3 m_StartPosition;
        AudioSource m_Breath;
        float m_ActiveAt;
        bool m_Catching;
        bool m_Released;
        float m_BaseEyeLight;

        void Start()
        {
            m_StartPosition = transform.position;
            m_ActiveAt = Time.time + startDelay;
            if (eyeLight != null)
                m_BaseEyeLight = eyeLight.intensity;
            m_Breath = DreamAudio.AttachLoop(gameObject, DreamSound.StalkerBreath, 0.8f, 14f);
        }

        void Update()
        {
            var world = NightmareWorld.Instance;
            var hud = DreamHud.Instance;

            if (world != null && world.IsDream)
            {
                if (!m_Released && hud != null)
                    hud.SetTint(Color.clear, 0f);
                if (m_Breath != null)
                    m_Breath.Stop();
                m_Released = true;
                return;
            }

            if (eyeLight != null)
                eyeLight.intensity = m_BaseEyeLight * (Random.value > 0.03f ? 1f : 0.1f);

            var head = DreamPlayer.Head;
            if (head == null || m_Catching || Time.time < m_ActiveAt)
                return;

            Vector3 toPlayer = head.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            if (distance > maxDistance)
            {
                Vector3 behind = -Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, behind) * Random.Range(-4f, 4f);
                Vector3 pos = head.position + behind * reappearDistance + side;
                pos.y = m_StartPosition.y;
                transform.position = pos;
                FollowGround();
                return;
            }

            Vector3 dir = toPlayer / Mathf.Max(distance, 0.001f);
            bool watched = Vector3.Dot(head.forward, -dir) > 0.75f;
            float hunt = goal != null ? 1f - Mathf.Clamp01(DreamPlayer.FlatDistance(goal.position) / huntRange) : 0f;
            float s = speed * (watched ? watchedSpeedMultiplier : unwatchedSpeedMultiplier) * (1f + huntBoost * hunt);
            transform.position += dir * s * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            FollowGround();

            if (hud != null)
            {
                float dread = 1f - Mathf.Clamp01((distance - catchDistance) / (dreadDistance - catchDistance));
                hud.SetTint(new Color(0.25f, 0f, 0.05f), dread * 0.55f);
                DreamAudio.RequestHeartbeat(dread);
            }

            if (distance < catchDistance)
                StartCoroutine(CatchRoutine());
        }

        void FollowGround()
        {
            var ray = new Ray(transform.position + Vector3.up * 10f, Vector3.down);
            if (ground != null && ground.Raycast(ray, out var hit, 30f))
                transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
        }

        IEnumerator CatchRoutine()
        {
            m_Catching = true;
            var hud = DreamHud.Instance;
            var world = NightmareWorld.Instance;

            if (hud != null)
            {
                yield return hud.FadeTo(1f, 0.4f, Color.black);
                hud.SetTint(Color.clear, 0f);
                hud.ShowMessage("It caught you...\nYou wake up where you started.\nFind the Token of Fear!", 5f);
            }

            if (world != null && world.spawnPoint != null)
                DreamPlayer.Teleport(world.spawnPoint);
            transform.position = m_StartPosition;
            m_ActiveAt = Time.time + startDelay * 0.6f;

            yield return new WaitForSeconds(1.5f);
            if (hud != null)
                yield return hud.FadeTo(0f, 1.2f, Color.black);
            m_Catching = false;
        }
    }
}
