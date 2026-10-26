using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// The claustrophobia nightmare: a small, dim room whose four walls and ceiling slowly close in, faster
    /// and faster. If they close on the player, they wake up back where they started.
    ///
    /// The way out is to push back. A hand pressed into a wall shoves it outward (the walls only ever move
    /// one way on their own), and warm sunset light leaks in around a wall as it gives. Push one wall far
    /// enough past where it started and the whole room falls open like a box unfolding: the walls topple
    /// outward onto the grass, the ceiling lifts away, and the nightmare lifts on a garden under glass.
    /// Nothing says so; a few gentle nudges come after being crushed.
    ///
    /// Each wall's transform sits on the floor at the middle of its inner face, its local +Z pointing into
    /// the room; the room's center is this transform.
    /// </summary>
    public class DreamClosingRoom : MonoBehaviour
    {
        public Transform[] walls = new Transform[4];
        public Transform ceiling;
        [Tooltip("Glowing strips along each wall's foot and top, one renderer per wall, brightening as it is pushed.")]
        public Renderer[] leaks = new Renderer[4];
        public Light bulb;

        [Header("Closing in")]
        public float startHalfSize = 2.5f;
        [Tooltip("Half the room's width at which the walls have closed on the player.")]
        public float crushHalfSize = 0.65f;
        public float startDelay = 8f;
        [Tooltip("Speed (m/s) the walls creep in at first...")]
        public float closeSpeed = 0.025f;
        [Tooltip("...and how quickly that speed grows (m/s per second).")]
        public float closeAcceleration = 0.0012f;
        public float maxCloseSpeed = 0.1f;
        public float startCeiling = 3f;
        public float lowestCeiling = 2.1f;

        [Header("Pushing back")]
        [Tooltip("Push a wall this far past where it started and the room falls open.")]
        public float breakThrough = 0.7f;
        [Tooltip("How far beyond a wall's edges a hand still counts as touching it.")]
        public float reachMargin = 0.2f;

        [TextArea] public string breakthroughMessage = "You push... and the wall gives way.\nThe room falls open around you.\nIt was only a dream.";
        [TextArea]
        public string[] crushedMessages =
        {
            "The walls closed in...\nYou wake up where you started.",
            "The walls closed in again...\nYou never even touched them.",
            "Again...\nWhat would happen if you pushed back?",
        };

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        float[] m_Offset = new float[4];    // how far each wall has moved in from where it started (negative: pushed out)
        Vector3[] m_WallStart = new Vector3[4];
        Quaternion[] m_WallRotation = new Quaternion[4];
        Vector3 m_CeilingStart;
        Vector3[] m_LeakScale = new Vector3[4];
        float m_Closing;
        float m_ActiveAt;
        int m_Crushed;
        bool m_Open;
        Transform m_LeftHand, m_RightHand;
        AudioSource m_Grind;
        MaterialPropertyBlock m_Block;
        float m_NextHaptic;
        NightmareWorld m_World;

        void Start()
        {
            m_Block = new MaterialPropertyBlock();
            for (int i = 0; i < walls.Length; i++)
            {
                m_WallStart[i] = walls[i].localPosition;
                m_WallRotation[i] = walls[i].localRotation;
                if (leaks != null && i < leaks.Length && leaks[i] != null)
                    m_LeakScale[i] = leaks[i].transform.localScale;
            }
            if (ceiling != null)
                m_CeilingStart = ceiling.localPosition;
            m_ActiveAt = Time.time + startDelay;
            m_Grind = DreamAudio.AttachLoop(gameObject, DreamSound.GearGrind, 0f, 12f);

            m_World = NightmareWorld.Instance;
            if (m_World != null)
            {
                m_World.WokeUp += ResetRoom;
                if (m_World.ArrivedRestored)
                    OpenInstantly();
            }
            FindHands();
        }

        void OnDestroy()
        {
            if (m_World != null)
                m_World.WokeUp -= ResetRoom;
        }

        void FindHands()
        {
            var origin = FindAnyObjectByType<XROrigin>();
            if (origin == null)
                return;
            foreach (var t in origin.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Left Controller")
                    m_LeftHand = t;
                else if (t.name == "Right Controller")
                    m_RightHand = t;
            }
        }

        Vector3 Inward(int i) => transform.rotation * (m_WallRotation[i] * Vector3.forward);

        void Update()
        {
            if (m_Open)
                return;
            var world = m_World != null ? m_World : NightmareWorld.Instance;
            if (world != null && (world.IsDream || world.IsWakingUp))
                return;
            if (m_LeftHand == null && m_RightHand == null)
                FindHands();

            // Creep in, faster and faster.
            if (Time.time >= m_ActiveAt)
            {
                m_Closing += Time.deltaTime;
                float speed = Mathf.Min(maxCloseSpeed, closeSpeed + closeAcceleration * m_Closing);
                for (int i = 0; i < walls.Length; i++)
                    m_Offset[i] += speed * Time.deltaTime;
            }

            // A hand pressed into a wall pushes it back.
            bool pushing = false;
            foreach (var hand in new[] { m_LeftHand, m_RightHand })
                if (hand != null)
                    pushing |= Push(hand.position);

            // Place the walls, the ceiling and the light leaking in.
            float squeeze = 0f;
            for (int i = 0; i < walls.Length; i++)
            {
                walls[i].localPosition = m_WallStart[i] + m_WallRotation[i] * Vector3.forward * m_Offset[i];
                squeeze += Mathf.Max(0f, m_Offset[i]);
                SetLeak(i, Mathf.Clamp01(-m_Offset[i] / breakThrough));
            }
            float closed = Mathf.Clamp01(squeeze / walls.Length / (startHalfSize - crushHalfSize));
            if (ceiling != null)
                ceiling.localPosition = m_CeilingStart + Vector3.down * (startCeiling - lowestCeiling) * closed;
            if (bulb != null)
                bulb.intensity = (0.9f + 0.1f * Mathf.PerlinNoise(Time.time * 6f, 0f)) * (Random.value > 0.02f ? 1f : 0.2f) * 1.4f;

            float tension = Mathf.Max(closed, 0f);
            DreamAudio.RequestHeartbeat(tension);
            if (m_Grind != null)
                m_Grind.volume = Time.time >= m_ActiveAt ? 0.15f + 0.45f * tension + (pushing ? 0.3f : 0f) : 0f;
            var hud = DreamHud.Instance;
            if (hud != null)
                hud.SetTint(new Color(0.05f, 0.06f, 0.12f), tension * 0.5f);

            // Pushed through: the room falls open.
            for (int i = 0; i < walls.Length; i++)
                if (m_Offset[i] <= -breakThrough)
                {
                    StartCoroutine(FallOpen(i));
                    if (world != null)
                        world.RestoreDream(breakthroughMessage);
                    return;
                }

            // Closed on the player.
            float widthX = 2f * startHalfSize - m_Offset[1] - m_Offset[3];
            float widthZ = 2f * startHalfSize - m_Offset[0] - m_Offset[2];
            if (Mathf.Min(widthX, widthZ) < crushHalfSize * 2f && world != null)
                world.WakeUp(crushedMessages[Mathf.Min(m_Crushed++, crushedMessages.Length - 1)]);
        }

        /// <summary>Pushes any wall this hand has pressed into back to the hand. True if it pushed one.</summary>
        bool Push(Vector3 hand)
        {
            bool pushed = false;
            for (int i = 0; i < walls.Length; i++)
            {
                var inward = Inward(i);
                var surface = walls[i].position;
                float depth = Vector3.Dot(hand - surface, inward); // below zero: the hand is inside the wall
                if (depth >= 0f || depth < -0.6f)
                    continue;
                var along = Vector3.Cross(Vector3.up, inward).normalized;
                float sideways = Mathf.Abs(Vector3.Dot(hand - surface, along));
                float height = hand.y - transform.position.y;
                float halfWidth = startHalfSize + 0.5f;
                if (sideways > halfWidth + reachMargin || height < -reachMargin || height > startCeiling + reachMargin)
                    continue;
                m_Offset[i] += depth; // back to where the hand is
                pushed = true;
                if (Time.time >= m_NextHaptic)
                {
                    DreamHaptics.Pulse(0.35f, 0.08f);
                    m_NextHaptic = Time.time + 0.15f;
                }
            }
            return pushed;
        }

        void SetLeak(int i, float amount)
        {
            if (leaks == null || i >= leaks.Length || leaks[i] == null)
                return;
            leaks[i].enabled = amount > 0.02f;
            leaks[i].GetPropertyBlock(m_Block);
            m_Block.SetColor(EmissionId, new Color(2.4f, 1.3f, 0.6f) * amount);
            leaks[i].SetPropertyBlock(m_Block);
            leaks[i].transform.localScale = Vector3.Scale(m_LeakScale[i], new Vector3(1f, 0.3f + 2.7f * amount, 1f + 2f * amount));
        }

        /// <summary>Waking up puts the room back as it was.</summary>
        void ResetRoom()
        {
            for (int i = 0; i < walls.Length; i++)
            {
                m_Offset[i] = 0f;
                walls[i].localPosition = m_WallStart[i];
                SetLeak(i, 0f);
            }
            if (ceiling != null)
                ceiling.localPosition = m_CeilingStart;
            m_Closing = 0f;
            m_ActiveAt = Time.time + startDelay * 0.6f;
        }

        /// <summary>The walls topple outward onto the ground, the pushed one first; the ceiling lifts away.</summary>
        IEnumerator FallOpen(int first)
        {
            m_Open = true;
            if (m_Grind != null)
                m_Grind.Stop();
            DreamAudio.Play(DreamSound.PortalWhoosh);
            DreamHaptics.Pulse(0.9f, 0.3f);
            if (bulb != null)
                bulb.enabled = false;

            var from = new Quaternion[walls.Length];
            var fromPos = new Vector3[walls.Length];
            for (int i = 0; i < walls.Length; i++)
            {
                from[i] = walls[i].localRotation;
                fromPos[i] = walls[i].localPosition;
                SetLeak(i, 0f);
            }
            Vector3 ceilingFrom = ceiling != null ? ceiling.localPosition : Vector3.zero;
            Vector3 ceilingScale = ceiling != null ? ceiling.localScale : Vector3.one;

            const float duration = 2.2f;
            for (float t = 0f; t < duration + 0.6f; t += Time.deltaTime)
            {
                for (int i = 0; i < walls.Length; i++)
                {
                    float delay = i == first ? 0f : 0.25f + 0.12f * ((i - first + walls.Length) % walls.Length);
                    float k = Mathf.Clamp01((t - delay) / duration);
                    float fall = k * k; // gathers speed as it goes over
                    float bounce = k >= 1f ? 0f : Mathf.Sin(Mathf.Clamp01((k - 0.85f) / 0.15f) * Mathf.PI) * 0.06f;
                    walls[i].localRotation = from[i] * Quaternion.Euler(-90f * fall + bounce * 30f, 0f, 0f);
                    walls[i].localPosition = fromPos[i] + Vector3.up * 0.12f * fall;
                }
                if (ceiling != null)
                {
                    float c = Mathf.Clamp01(t / duration);
                    ceiling.localPosition = ceilingFrom + Vector3.up * 9f * c * c;
                    ceiling.localScale = ceilingScale * (1f - c);
                }
                yield return null;
            }
            if (ceiling != null)
                ceiling.gameObject.SetActive(false);
        }

        /// <summary>Coming back to a garden already restored: the room lies open from the start.</summary>
        void OpenInstantly()
        {
            m_Open = true;
            for (int i = 0; i < walls.Length; i++)
            {
                walls[i].localRotation = m_WallRotation[i] * Quaternion.Euler(-90f, 0f, 0f);
                walls[i].localPosition = m_WallStart[i] + Vector3.up * 0.12f;
                SetLeak(i, 0f);
            }
            if (ceiling != null)
                ceiling.gameObject.SetActive(false);
            if (bulb != null)
                bulb.enabled = false;
            if (m_Grind != null)
                m_Grind.Stop();
        }
    }
}
