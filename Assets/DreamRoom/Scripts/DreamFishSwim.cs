using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// A swimming fish's motion: the tail beats from side to side and the body sways gently against it,
    /// the tail a little behind the body, like the wave that runs down a real fish.
    /// </summary>
    public class DreamFishSwim : MonoBehaviour
    {
        public Transform tail;
        [Tooltip("Tail beats per second.")]
        public float beat = 2.5f;
        public float tailAngle = 24f;
        public float bodyAngle = 4f;

        Quaternion m_BodyStart;
        Quaternion m_TailStart;
        float m_Phase;

        void Start()
        {
            m_BodyStart = transform.localRotation;
            if (tail != null)
                m_TailStart = tail.localRotation;
            m_Phase = Random.value * Mathf.PI * 2f;
        }

        void Update()
        {
            float t = Time.time * beat * Mathf.PI * 2f + m_Phase;
            transform.localRotation = m_BodyStart * Quaternion.Euler(0f, Mathf.Sin(t) * bodyAngle, 0f);
            if (tail != null)
                tail.localRotation = m_TailStart * Quaternion.Euler(0f, Mathf.Sin(t - 1.2f) * tailAngle, 0f);
        }
    }
}
