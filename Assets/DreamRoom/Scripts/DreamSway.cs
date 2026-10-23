using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// Slow back-and-forth rocking, like kelp in a current or branches in a breeze.
    /// </summary>
    public class DreamSway : MonoBehaviour
    {
        public float angle = 8f;
        public float speed = 0.25f;

        Quaternion m_StartRot;
        float m_Phase;

        void Start()
        {
            m_StartRot = transform.localRotation;
            m_Phase = Random.value * Mathf.PI * 2f;
        }

        void Update()
        {
            float t = Time.time * speed * Mathf.PI * 2f + m_Phase;
            transform.localRotation = m_StartRot * Quaternion.Euler(Mathf.Sin(t) * angle, 0f, Mathf.Cos(t * 0.7f) * angle * 0.6f);
        }
    }
}
