using System.Collections;
using TMPro;
using UnityEngine;

namespace DreamStudio
{
    /// <summary>
    /// Shows progress in the Portal Room: every portal whose world has been restored gets a golden
    /// sparkle and a "Dream restored" line, a counter shows how many are done, and once every world is
    /// restored the finale lights up.
    /// </summary>
    public class DreamHubProgress : MonoBehaviour
    {
        [Tooltip("Text showing how many dreams are restored (e.g. front and back of a turning sign).")]
        public TextMeshPro[] counters;
        [Tooltip("Shown only once every world has been restored.")]
        public GameObject finale;
        [Tooltip("Particle material for the sparkle around restored portals.")]
        public Material sparkle;

        IEnumerator Start()
        {
            if (finale != null)
                finale.SetActive(false);

            int total = 0, restored = 0;
            foreach (var portal in FindObjectsByType<DreamPortal>())
            {
                if (!portal.arriveHere)
                    continue;
                total++;
                if (NightmareWorld.IsRestored(portal.sceneName))
                {
                    restored++;
                    MarkRestored(portal);
                }
            }

            foreach (var counter in counters)
                if (counter != null)
                    counter.text = $"Dreams restored\n<size=140%>{restored} / {total}</size>";

            if (total == 0 || restored < total)
                yield break;

            if (finale != null)
                finale.SetActive(true);
            yield return new WaitForSeconds(1.5f);
            DreamAudio.Play(DreamSound.Choir);
            DreamHaptics.Pulse(0.6f, 0.5f);
            if (DreamHud.Instance != null)
                DreamHud.Instance.ShowMessage("You did it!\nEvery nightmare has turned back into a dream.", 8f);
        }

        void MarkRestored(DreamPortal portal)
        {
            var label = portal.GetComponentInChildren<TextMeshPro>();
            if (label != null)
                label.text = $"{portal.worldName}\n<size=60%><color=#FFE08A>Dream restored</color></size>";
            if (portal.portalLight != null)
                portal.portalLight.color = Color.Lerp(portal.portalLight.color, new Color(1f, 0.85f, 0.5f), 0.5f);

            if (sparkle == null)
                return;
            var go = new GameObject("Restored Sparkle");
            go.transform.SetParent(portal.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.5f), Color.white);
            var emission = ps.emission;
            emission.rateOverTime = 15f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.4f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = sparkle;
        }
    }
}
