using System.Collections.Generic;
using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// A stream of water that pushes swimmers along this object's forward direction, strongest at its
    /// center. Read by <see cref="DreamSwimming"/>. Disabling it (when the nightmare lifts) stills it.
    /// </summary>
    public class DreamCurrent : MonoBehaviour
    {
        static readonly List<DreamCurrent> s_Active = new List<DreamCurrent>();

        public float radius = 7f;
        [Tooltip("Push (m/s²) at the center.")]
        public float strength = 2f;

        void OnEnable() => s_Active.Add(this);
        void OnDisable() => s_Active.Remove(this);

        /// <summary>The combined push of every active current at a point.</summary>
        public static Vector3 FlowAt(Vector3 point)
        {
            var flow = Vector3.zero;
            foreach (var current in s_Active)
            {
                var offset = point - current.transform.position;
                offset.y = 0f;
                float falloff = 1f - Mathf.Clamp01(offset.magnitude / current.radius);
                flow += current.transform.forward * current.strength * falloff;
            }
            return flow;
        }
    }
}
