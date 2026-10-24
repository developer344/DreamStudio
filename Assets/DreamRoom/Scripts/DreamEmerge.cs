using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// Makes a piece look as if it is pushing its way out of a portal: it slowly slides out along its
    /// own up axis and sinks back again, each piece at its own pace.
    /// </summary>
    public class DreamEmerge : MonoBehaviour
    {
        [Tooltip("How far (m) the piece slides out and back.")]
        public float distance = 0.06f;
        [Tooltip("Cycles per second.")]
        public float speed = 0.2f;

        Vector3 m_Start;
        float m_Phase;

        void Start()
        {
            m_Start = transform.localPosition;
            m_Phase = Random.value * Mathf.PI * 2f;
        }

        void Update()
        {
            float k = Mathf.Sin(Time.time * speed * Mathf.PI * 2f + m_Phase) * 0.5f + 0.5f;
            transform.localPosition = m_Start + transform.localRotation * Vector3.up * (k * distance);
        }
    }
}
