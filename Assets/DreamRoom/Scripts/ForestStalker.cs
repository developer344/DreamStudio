using System.Collections;
using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// A shadow that drifts through the trees after the player. Behind them it rushes; at the edge of
    /// their view it creeps; looked at straight on (and not hidden behind a tree) it cannot move at all.
    /// If it reaches them the view goes dark and they wake up back at the start.
    ///
    /// The way out is to face it: hold its gaze for <see cref="stareToBanish"/> seconds and it trembles,
    /// shrinks and comes apart, and the nightmare lifts. Looking away lets that progress drain away quickly.
    /// Nothing says so; the player finds out by turning to face their fear (and, after being caught a few
    /// times, by a quiet nudge). Assign <see cref="body"/> to the visible body (its own hover bob is untouched).
    /// </summary>
    public class ForestStalker : MonoBehaviour
    {
        public float speed = 1.7f;
        [Tooltip("Speed multiplier while it is in view but not stared at.")]
        public float glimpsedSpeedMultiplier = 0.5f;
        [Tooltip("Speed multiplier while it is behind the player.")]
        public float unwatchedSpeedMultiplier = 1.35f;
        public float catchDistance = 1.1f;
        [Tooltip("Distance at which the view starts darkening with dread.")]
        public float dreadDistance = 9f;
        [Tooltip("If the player gets farther than this, it reappears behind them.")]
        public float maxDistance = 22f;
        public float reappearDistance = 13f;
        public float startDelay = 10f;

        [Header("Facing it")]
        [Tooltip("How close to straight ahead (degrees) counts as staring at it.")]
        public float stareAngle = 14f;
        [Tooltip("It can be stared down from this far away.")]
        public float stareRange = 24f;
        [Tooltip("Seconds of unbroken staring that banish it and lift the nightmare.")]
        public float stareToBanish = 5f;
        [Tooltip("How much faster stare progress drains while the player looks away.")]
        public float stareDecay = 2f;

        [TextArea] public string banishedMessage = "You stand your ground and stare it down.\nThe shadow trembles... and comes apart like smoke.\nIt was only a dream.";
        [TextArea]
        public string[] caughtMessages =
        {
            "It caught you...\nYou wake up where you started.",
            "It caught you again...\nRunning only seems to make it stronger.",
            "It caught you...\nIt never moves while you are watching it.",
        };

        public Light eyeLight;
        [Tooltip("The visible body, which trembles and shrinks while stared at.")]
        public Transform body;

        [Tooltip("Ground the stalker glides over, so it follows hills.")]
        public Collider ground;

        Vector3 m_StartPosition;
        AudioSource m_Breath;
        float m_BaseBreathVolume;
        float m_ActiveAt;
        bool m_Catching;
        bool m_Released;
        float m_BaseEyeLight;
        float m_Stare;
        int m_Caught;
        Vector3 m_BodyScale;

        /// <summary>0..1: how far the player is toward staring it down.</summary>
        public float StareProgress => Mathf.Clamp01(m_Stare / stareToBanish);

        void Start()
        {
            m_StartPosition = transform.position;
            m_ActiveAt = Time.time + startDelay;
            if (eyeLight != null)
                m_BaseEyeLight = eyeLight.intensity;
            if (body != null)
                m_BodyScale = body.localScale;
            m_Breath = DreamAudio.AttachLoop(gameObject, DreamSound.StalkerBreath, 0.8f, 14f);
            if (m_Breath != null)
                m_BaseBreathVolume = m_Breath.volume;
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

            var head = DreamPlayer.Head;
            if (head == null || m_Catching || Time.time < m_ActiveAt)
            {
                Flicker(0f);
                return;
            }

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
                m_Stare = 0f;
                return;
            }

            Vector3 dir = toPlayer / Mathf.Max(distance, 0.001f);
            bool stared = IsStaredAt(head, distance);
            bool glimpsed = !stared && Vector3.Dot(head.forward, -dir) > 0.75f;

            if (stared)
            {
                // Held in the player's gaze it cannot move; the longer they hold it, the weaker it gets.
                m_Stare += Time.deltaTime;
                if (m_Stare >= stareToBanish && world != null)
                {
                    world.RestoreDream(banishedMessage);
                    return;
                }
            }
            else
            {
                m_Stare = Mathf.Max(0f, m_Stare - Time.deltaTime * stareDecay);
                float s = speed * (glimpsed ? glimpsedSpeedMultiplier : unwatchedSpeedMultiplier);
                transform.position += dir * s * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                FollowGround();
            }

            float progress = StareProgress;
            Flicker(progress);
            if (m_Breath != null)
                m_Breath.volume = m_BaseBreathVolume * (1f - 0.8f * progress);

            if (hud != null)
            {
                float dread = 1f - Mathf.Clamp01((distance - catchDistance) / (dreadDistance - catchDistance));
                dread *= 1f - progress; // facing it, the dread lifts
                hud.SetTint(new Color(0.25f, 0f, 0.05f), dread * 0.55f);
                DreamAudio.RequestHeartbeat(Mathf.Max(dread, stared ? 0.6f * (1f - progress) : 0f));
            }

            if (distance < catchDistance && !stared)
                StartCoroutine(CatchRoutine());
        }

        /// <summary>True while the player looks straight at it, close enough, with nothing in between.</summary>
        bool IsStaredAt(Transform head, float distance)
        {
            if (distance > stareRange)
                return false;
            // Its eyes sit about 2.5 m up, lower as it shrinks.
            Vector3 eyes = transform.position + Vector3.up * 2.5f * (1f - 0.3f * StareProgress);
            Vector3 look = eyes - head.position;
            float angle = Vector3.Angle(head.forward, look);
            // Up close it fills the view, so allow a wider cone.
            float allowed = stareAngle + Mathf.Lerp(16f, 0f, Mathf.Clamp01(distance / 6f));
            if (angle > allowed)
                return false;
            return !Physics.Raycast(head.position, look.normalized, look.magnitude - 0.6f, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Its eyes flicker and dim, and its body trembles and shrinks, as it is stared down.</summary>
        void Flicker(float progress)
        {
            if (eyeLight != null)
            {
                float flicker = Random.value > Mathf.Lerp(0.03f, 0.35f, progress) ? 1f : 0.1f;
                eyeLight.intensity = m_BaseEyeLight * flicker * (1f - 0.7f * progress);
            }
            if (body != null)
            {
                // Scale and a shiver of rotation only: the body's hover bob owns its position.
                body.localScale = m_BodyScale * (1f - 0.3f * progress);
                body.localRotation = Quaternion.Euler(Random.Range(-5f, 5f) * progress, Random.Range(-8f, 8f) * progress, Random.Range(-5f, 5f) * progress);
            }
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
                hud.ShowMessage(caughtMessages[Mathf.Min(m_Caught, caughtMessages.Length - 1)], 5f);
            }
            m_Caught++;

            if (world != null && world.spawnPoint != null)
                DreamPlayer.Teleport(world.spawnPoint);
            transform.position = m_StartPosition;
            m_ActiveAt = Time.time + startDelay * 0.6f;
            m_Stare = 0f;

            yield return new WaitForSeconds(1.5f);
            if (hud != null)
                yield return hud.FadeTo(0f, 1.2f, Color.black);
            m_Catching = false;
        }
    }
}
