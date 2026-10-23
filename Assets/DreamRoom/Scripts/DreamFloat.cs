using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// Gentle bobbing and spinning for dreamy floating props.
    /// </summary>
    public class DreamFloat : MonoBehaviour
    {
        public float bobHeight = 0.15f;
        public float bobSpeed = 0.5f;
        public Vector3 spin = new Vector3(0f, 10f, 0f);

        Vector3 m_StartPos;
        float m_Phase;

        void Start()
        {
            m_StartPos = transform.localPosition;
            m_Phase = Random.value * Mathf.PI * 2f;
        }

        void Update()
        {
            float offset = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f + m_Phase) * bobHeight;
            transform.localPosition = m_StartPos + Vector3.up * offset;
            transform.Rotate(spin * Time.deltaTime, Space.Self);
        }
    }
}
