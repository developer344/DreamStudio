using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// A pocket of air rising from a vent. Swim your head into it to refill your air.
    /// It reforms after a while.
    /// </summary>
    public class AirBubble : MonoBehaviour
    {
        public float airAmount = 25f;
        public float touchRadius = 0.8f;
        public float reformTime = 12f;
        public GameObject visual;

        float m_ReadyAt;

        void Update()
        {
            bool ready = Time.time >= m_ReadyAt;
            if (visual != null && visual.activeSelf != ready)
                visual.SetActive(ready);

            var breath = UnderwaterBreath.Instance;
            var head = DreamPlayer.Head;
            if (!ready || breath == null || breath.CanBreathe || head == null)
                return;

            if (Vector3.Distance(head.position, transform.position) < touchRadius)
            {
                breath.AddAir(airAmount);
                DreamAudio.Play(DreamSound.Gulp);
                DreamHaptics.Pulse(0.3f, 0.1f);
                m_ReadyAt = Time.time + reformTime;
            }
        }
    }
}
