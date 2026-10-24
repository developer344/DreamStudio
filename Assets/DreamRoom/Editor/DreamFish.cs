using System.Collections.Generic;
using UnityEngine;
using static DreamStudio.Editor.DreamBuildUtils;
using Bits = DreamStudio.Editor.DreamUnderstory.Bits;

namespace DreamStudio.Editor
{
    /// <summary>
    /// Fish with real fish shapes: a streamlined body, flattened from side to side, swelling behind the head
    /// and narrowing to the tail; darker along the back than on the belly; a dorsal fin, an anal fin and a
    /// pair of pectoral fins; eyes; and a forked tail on its own pivot so it can beat (see <see cref="DreamFishSwim"/>).
    /// Facing local +Z. Three species: a slim yellowtail, a small silver herring and a deep-bodied red snapper.
    /// </summary>
    public static class DreamFish
    {
        public enum Species { Yellowtail, Herring, Snapper }

        class Kind
        {
            public float length, height, width;   // body, without the tail
            public Color back, belly, fins;
            public float tailBeat;
        }

        static Kind KindOf(Species s)
        {
            switch (s)
            {
                case Species.Herring:
                    return new Kind { length = 0.24f, height = 0.055f, width = 0.03f, back = new Color(0.16f, 0.3f, 0.38f), belly = new Color(0.86f, 0.89f, 0.9f), fins = new Color(0.6f, 0.66f, 0.7f), tailBeat = 3.5f };
                case Species.Snapper:
                    return new Kind { length = 0.38f, height = 0.15f, width = 0.055f, back = new Color(0.75f, 0.28f, 0.22f), belly = new Color(0.95f, 0.7f, 0.64f), fins = new Color(0.82f, 0.36f, 0.3f), tailBeat = 1.8f };
                default:
                    return new Kind { length = 0.34f, height = 0.09f, width = 0.045f, back = new Color(0.36f, 0.44f, 0.55f), belly = new Color(0.86f, 0.82f, 0.8f), fins = new Color(0.95f, 0.8f, 0.25f), tailBeat = 2.4f };
            }
        }

        static readonly Dictionary<Species, (Mesh body, Mesh tail, Material[] mats)> s_Cache = new Dictionary<Species, (Mesh, Mesh, Material[])>();

        /// <summary>Clears the meshes made by the last build, so each build regenerates them.</summary>
        public static void Reset() => s_Cache.Clear();

        /// <summary>One fish of <paramref name="species"/>, swimming along its local +Z.</summary>
        public static GameObject Create(Transform parent, Species species, string name = "Fish")
        {
            var kind = KindOf(species);
            if (!s_Cache.TryGetValue(species, out var parts))
            {
                var mats = new[]
                {
                    Lit($"Fish_{species}_Back", kind.back, 0.75f, Color.black),
                    Lit($"Fish_{species}_Belly", kind.belly, 0.85f, kind.belly * 0.05f),
                    Lit($"Fish_{species}_Fins", kind.fins, 0.5f, Color.black),
                    Lit("Fish_Eye", new Color(0.03f, 0.03f, 0.04f), 0.95f, Color.black),
                };
                parts = (BodyMesh($"Fish_{species}_Body", kind), TailMesh($"Fish_{species}_Tail", kind), mats);
                s_Cache[species] = parts;
            }

            var fish = new GameObject(name);
            fish.transform.SetParent(parent, false);
            var body = MeshObject("Body", fish.transform, parts.body, parts.mats[0]);
            body.GetComponent<MeshRenderer>().sharedMaterials = parts.mats;

            var tailPivot = new GameObject("Tail");
            tailPivot.transform.SetParent(fish.transform, false);
            tailPivot.transform.localPosition = new Vector3(0f, 0f, -kind.length * 0.5f);
            MeshObject("Fin", tailPivot.transform, parts.tail, parts.mats[2]);

            var swim = fish.AddComponent<DreamFishSwim>();
            swim.tail = tailPivot.transform;
            swim.beat = kind.tailBeat * Random.Range(0.85f, 1.15f);
            return fish;
        }

        // =====================================================================
        // Meshes
        // =====================================================================

        /// <summary>
        /// The body: elliptical cross-sections along Z (tail end at -length/2, nose at +length/2), swelling
        /// a third of the way back from the nose; back and belly in separate submeshes; fins and eyes added
        /// as double-sided pieces.
        /// </summary>
        static Mesh BodyMesh(string name, Kind k)
        {
            var b = new Bits(4); // back, belly, fins, eyes
            const int stations = 16, around = 12;
            float L = k.length;

            // Depth along the body (u: 0 at the tail stalk, 1 at the nose): a slender stalk, a long gradual
            // swell to the deepest point a little ahead of the middle, then a rounded head.
            float Envelope(float u)
            {
                u = Mathf.Clamp01(u);
                float f = u < 0.6f ? Mathf.Pow(Mathf.Sin(Mathf.PI * 0.5f * u / 0.6f), 1.6f) : Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((u - 0.6f) / 0.4f, 2f)));
                return 0.12f + 0.88f * f;
            }
            float Belly(float u) => -0.12f * k.height * Mathf.Sin(Mathf.PI * u);
            float ZAt(float u) => (u - 0.5f) * L;

            var rings = new int[stations];
            for (int i = 0; i < stations; i++)
            {
                float u = i / (stations - 1f) * 0.99f; // stop just short of the nose; a tip vertex closes it
                float hh = k.height * 0.5f * Envelope(u), hw = k.width * 0.5f * Envelope(u);
                rings[i] = b.v.Count;
                for (int j = 0; j < around; j++)
                {
                    float a = j * Mathf.PI * 2f / around;
                    var p = new Vector3(Mathf.Cos(a) * hw, Belly(u) + Mathf.Sin(a) * hh, ZAt(u));
                    var n = new Vector3(Mathf.Cos(a) * hh, Mathf.Sin(a) * hw, 0f).normalized;
                    b.Vert(p, n, Vector2.zero);
                }
            }
            for (int i = 0; i < stations - 1; i++)
            for (int j = 0; j < around; j++)
            {
                int a = rings[i] + j, c = rings[i] + (j + 1) % around, d = rings[i + 1] + (j + 1) % around, e = rings[i + 1] + j;
                var mid = (b.v[a] + b.v[d]) * 0.5f;
                float u = (i + 0.5f) / (stations - 1f) * 0.99f;
                var axis = new Vector3(0f, Belly(u), mid.z);
                Quad(b, mid.y > axis.y + k.height * 0.06f ? 0 : 1, a, c, d, e, mid - axis);
            }
            int nose = b.Vert(new Vector3(0f, Belly(1f) - k.height * 0.04f, L * 0.5f + L * 0.012f), Vector3.forward, Vector2.zero);
            int tailEnd = b.Vert(new Vector3(0f, 0f, ZAt(0f) - 0.005f), Vector3.back, Vector2.zero);
            for (int j = 0; j < around; j++)
            {
                int last = rings[stations - 1];
                Tri(b, 0, last + j, last + (j + 1) % around, nose, Vector3.forward);
                Tri(b, 1, rings[0] + j, rings[0] + (j + 1) % around, tailEnd, Vector3.back);
            }

            // Fins: a swept-back dorsal fin, a small anal fin, and two pectoral fins behind the gills.
            float top(float u) => Belly(u) + k.height * 0.5f * Envelope(u);
            float bottom(float u) => Belly(u) - k.height * 0.5f * Envelope(u);
            Fin(b, new Vector3(0f, top(0.62f) - 0.003f, ZAt(0.62f)), new Vector3(0f, top(0.62f) + k.height * 0.42f, ZAt(0.45f)),
                new Vector3(0f, top(0.3f) - 0.003f, ZAt(0.3f)), Vector3.right);
            Fin(b, new Vector3(0f, bottom(0.33f) + 0.003f, ZAt(0.33f)), new Vector3(0f, bottom(0.25f) - k.height * 0.28f, ZAt(0.2f)),
                new Vector3(0f, bottom(0.18f) + 0.003f, ZAt(0.18f)), Vector3.right);
            foreach (int side in new[] { -1, 1 })
            {
                float u = 0.7f;
                var root = new Vector3(side * k.width * 0.42f * Envelope(u), Belly(u) - k.height * 0.1f, ZAt(u));
                Fin(b, root + Vector3.forward * L * 0.03f, root + new Vector3(side * k.width * 0.6f, -k.height * 0.18f, -L * 0.14f),
                    root - Vector3.forward * L * 0.04f, new Vector3(0f, 1f, 0f));
            }

            // Eyes: small dark domes on each side, near the front.
            foreach (int side in new[] { -1, 1 })
            {
                float u = 0.88f;
                var at = new Vector3(side * k.width * 0.5f * Envelope(u) * 0.95f, Belly(u) + k.height * 0.12f, ZAt(u));
                float r = k.height * 0.11f;
                var outward = new Vector3(side, 0f, 0f);
                int center = b.Vert(at + outward * r * 0.4f, outward, Vector2.zero);
                int first = b.v.Count;
                for (int j = 0; j < 8; j++)
                {
                    float a = j * Mathf.PI / 4f;
                    var rim = at + new Vector3(0f, Mathf.Sin(a) * r, Mathf.Cos(a) * r);
                    b.Vert(rim, (outward + (rim - at).normalized * 0.6f).normalized, Vector2.zero);
                }
                for (int j = 0; j < 8; j++)
                    Tri(b, 3, center, first + j, first + (j + 1) % 8, outward);
            }
            return b.ToMesh(name);
        }

        /// <summary>The tail: a forked fin in the vertical plane, from a narrow root back to two pointed lobes.</summary>
        static Mesh TailMesh(string name, Kind k)
        {
            var b = new Bits(1);
            float root = k.height * 0.5f * 0.14f + 0.004f;
            float span = k.height * 0.62f;
            float back = k.length * 0.36f;
            var rootTop = new Vector3(0f, root, 0.01f);
            var rootBottom = new Vector3(0f, -root, 0.01f);
            var upperTip = new Vector3(0f, span, -back);
            var lowerTip = new Vector3(0f, -span * 0.95f, -back * 0.97f);
            var notch = new Vector3(0f, 0f, -back * 0.45f);
            var upperMid = new Vector3(0f, span * 0.55f, -back * 0.55f);
            var lowerMid = new Vector3(0f, -span * 0.52f, -back * 0.55f);
            foreach (var (p0, p1, p2) in new[]
            {
                (rootTop, upperMid, notch), (upperMid, upperTip, notch), (rootTop, notch, rootBottom),
                (rootBottom, notch, lowerMid), (lowerMid, notch, lowerTip),
            })
                DoubleTri(b, 0, p0, p1, p2, Vector3.right);
            return b.ToMesh(name);
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>A quad from existing vertices, wound to face along <paramref name="outward"/>.</summary>
        static void Quad(Bits b, int sub, int a, int c, int d, int e, Vector3 outward)
        {
            var n = Vector3.Cross(b.v[c] - b.v[a], b.v[d] - b.v[a]);
            b.sub[sub].AddRange(Vector3.Dot(n, outward) >= 0f ? new[] { a, c, d, a, d, e } : new[] { a, d, c, a, e, d });
        }

        /// <summary>A triangle from existing vertices, wound to face along <paramref name="outward"/>.</summary>
        static void Tri(Bits b, int sub, int a, int c, int d, Vector3 outward)
        {
            var n = Vector3.Cross(b.v[c] - b.v[a], b.v[d] - b.v[a]);
            b.sub[sub].AddRange(Vector3.Dot(n, outward) >= 0f ? new[] { a, c, d } : new[] { a, d, c });
        }

        /// <summary>A thin fin seen from both sides: the triangle twice, back to back, with its own normals.</summary>
        static void DoubleTri(Bits b, int sub, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 side)
        {
            foreach (var n in new[] { side, -side })
            {
                int a = b.Vert(p0, n, Vector2.zero), c = b.Vert(p1, n, Vector2.zero), d = b.Vert(p2, n, Vector2.zero);
                Tri(b, sub, a, c, d, n);
            }
        }

        /// <summary>A fin: front root, tip and rear root, double-sided, in the fins submesh.</summary>
        static void Fin(Bits b, Vector3 front, Vector3 tip, Vector3 rear, Vector3 side)
        {
            var normal = Vector3.Cross(tip - front, rear - front).normalized;
            if (normal.sqrMagnitude < 1e-4f)
                normal = side;
            DoubleTri(b, 2, front, tip, rear, normal);
        }
    }
}
