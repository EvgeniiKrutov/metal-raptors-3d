using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class VillagePeople : MonoBehaviour
    {
        const int CountMin = 10, CountMax = 16;
        const float Height = 13f;
        const float Width = 4.6f;
        const float Thickness = 3f;
        const float HeightJitter = 0.08f;
        const float WomanChance = 0.45f;

        const float SpeedMin = 6f, SpeedMax = 10f;
        const float StepRateBase = 2.2f;
        const float StepSpeedReference = 8f;
        const float HopHeight = 0.7f;
        const float ArriveRadius = 1.2f;
        const float TurnRate = 240f;
        const float HaltChance = 0.12f;
        const float HaltMin = 1.5f, HaltMax = 5f;
        const float StraightChance = 0.8f;
        const float StraightDot = 0.3f;
        const float Lift = 0.05f;
        const float RunFactor = 3f;
        const float RunStepFactor = 1.6f;

        static readonly List<VillagePeople> Live = new List<VillagePeople>();

        static readonly Color[] Shirts =
        {
            new Color(0.84f, 0.82f, 0.76f),
            new Color(0.36f, 0.42f, 0.56f),
            new Color(0.55f, 0.31f, 0.24f),
            new Color(0.33f, 0.41f, 0.30f),
            new Color(0.27f, 0.26f, 0.28f),
            new Color(0.70f, 0.62f, 0.46f),
        };

        static readonly Color[] Trousers =
        {
            new Color(0.30f, 0.24f, 0.18f),
            new Color(0.17f, 0.16f, 0.17f),
            new Color(0.38f, 0.34f, 0.28f),
        };

        static readonly Color[] Skirts =
        {
            new Color(0.60f, 0.20f, 0.18f),
            new Color(0.24f, 0.30f, 0.50f),
            new Color(0.25f, 0.38f, 0.28f),
            new Color(0.20f, 0.18f, 0.20f),
        };

        static readonly Color[] Hats =
        {
            new Color(0.22f, 0.28f, 0.20f),
            new Color(0.13f, 0.12f, 0.12f),
            new Color(0.40f, 0.30f, 0.20f),
        };

        static readonly Color[] Scarves =
        {
            new Color(0.88f, 0.86f, 0.80f),
            new Color(0.62f, 0.22f, 0.20f),
            new Color(0.36f, 0.26f, 0.18f),
        };

        static readonly Color[] Skins =
        {
            new Color(0.80f, 0.64f, 0.50f),
            new Color(0.72f, 0.55f, 0.42f),
            new Color(0.62f, 0.46f, 0.34f),
        };

        static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

        class Walker
        {
            public Transform tr;
            public Vector3 position;
            public float heading;
            public int from, to;
            public float speed;
            public float halt;
            public float stepPhase;
        }

        Transform _village;
        WalkGraph _graph;
        readonly List<Walker> _walkers = new List<Walker>();
        bool _panic;
        Vector3 _danger;

        void OnEnable() => Live.Add(this);

        void OnDisable() => Live.Remove(this);

        public static void PanicNear(Vector3 point, float reach)
        {
            foreach (VillagePeople people in Live)
            {
                if (people == null || people._village == null) continue;

                Vector3 gap = people._village.position - point;
                gap.y = 0f;
                if (gap.sqrMagnitude <= reach * reach) people.Panic(point);
            }
        }

        public void Panic(Vector3 point)
        {
            _panic = true;
            _danger = point;
            foreach (var w in _walkers)
            {
                w.halt = 0f;
                int previous = w.from;
                w.from = w.to;
                w.to = Flee(w.from, previous);
            }
        }

        int Flee(int at, int previous)
        {
            int[] links = _graph.links[at];
            if (links.Length == 0) return previous;

            int best = links[0];
            float far = float.MinValue;
            foreach (int n in links)
            {
                Vector3 gap = World(n) - _danger;
                gap.y = 0f;
                float d = gap.sqrMagnitude;
                if (d <= far) continue;
                far = d;
                best = n;
            }
            return best;
        }

        public static VillagePeople Begin(Transform parent, Transform village, WalkGraph graph, int seed)
        {
            if (village == null || graph == null || graph.nodes == null || graph.nodes.Length == 0) return null;

            var people = new GameObject("Villagers").AddComponent<VillagePeople>();
            people.transform.SetParent(parent, false);
            people._village = village;
            people._graph = graph;
            people.Populate(new System.Random(seed));
            return people;
        }

        void Populate(System.Random rng)
        {
            var starts = new List<int>();
            for (int i = 0; i < _graph.nodes.Length; i++)
                if (_graph.links[i].Length > 0) starts.Add(i);
            if (starts.Count == 0) return;

            int count = rng.Next(CountMin, CountMax + 1);
            for (int i = 0; i < count; i++)
            {
                int from = starts[rng.Next(starts.Count)];
                int[] next = _graph.links[from];
                int to = next[rng.Next(next.Length)];

                var walker = new Walker
                {
                    tr = BuildFigure(transform, rng),
                    position = World(from),
                    from = from,
                    to = to,
                    speed = Mathf.Lerp(SpeedMin, SpeedMax, (float)rng.NextDouble()),
                    halt = rng.NextDouble() < 0.3 ? Mathf.Lerp(HaltMin, HaltMax, (float)rng.NextDouble()) : 0f,
                    stepPhase = (float)rng.NextDouble() * Mathf.PI * 2f,
                };

                Vector3 d = World(to) - walker.position;
                walker.heading = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                _walkers.Add(walker);
                Pose(walker, 0f);
            }
        }

        Vector3 World(int node) => _village.TransformPoint(_graph.nodes[node]);

        void Update()
        {
            if (_village == null) return;

            float dt = Time.deltaTime;
            foreach (var w in _walkers)
            {
                if (w.halt > 0f)
                {
                    w.halt -= dt;
                    Pose(w, 0f);
                    continue;
                }

                Vector3 target = World(w.to);
                Vector3 d = target - w.position;
                d.y = 0f;

                if (d.magnitude <= ArriveRadius)
                {
                    Arrive(w);
                    continue;
                }

                float want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                w.heading = Mathf.MoveTowardsAngle(w.heading, want, TurnRate * dt);
                float rad = w.heading * Mathf.Deg2Rad;
                float pace = _panic ? w.speed * RunFactor : w.speed;
                float step = Mathf.Min(pace * dt, d.magnitude);
                w.position.x += Mathf.Sin(rad) * step;
                w.position.z += Mathf.Cos(rad) * step;

                Vector3 a = World(w.from);
                float total = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(target.x, target.z));
                float done = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(w.position.x, w.position.z));
                w.position.y = Mathf.Lerp(a.y, target.y, total > 0.01f ? Mathf.Clamp01(done / total) : 1f);

                w.stepPhase += StepRateBase * w.speed / StepSpeedReference * Mathf.PI * dt
                               * (_panic ? RunStepFactor : 1f);
                Pose(w, Mathf.Abs(Mathf.Sin(w.stepPhase)) * HopHeight);
            }
        }

        void Arrive(Walker w)
        {
            int previous = w.from;
            w.from = w.to;
            if (_panic)
            {
                w.to = Flee(w.from, previous);
                return;
            }

            w.to = NextNode(w.from, previous);
            if (Random.value < HaltChance) w.halt = Random.Range(HaltMin, HaltMax);
        }

        int NextNode(int at, int previous)
        {
            int[] links = _graph.links[at];
            if (links.Length == 0) return previous;

            Vector3 here = _graph.nodes[at];
            Vector3 came = here - _graph.nodes[previous];
            came.y = 0f;
            bool straight = came.sqrMagnitude > 1e-4f && Random.value < StraightChance;

            if (straight)
            {
                int best = -1;
                float bestDot = StraightDot;
                for (int i = 0; i < links.Length; i++)
                {
                    int n = links[i];
                    if (n == previous) continue;
                    Vector3 go = _graph.nodes[n] - here;
                    go.y = 0f;
                    float dot = Vector3.Dot(came.normalized, go.normalized);
                    if (dot <= bestDot) continue;
                    bestDot = dot;
                    best = n;
                }

                if (best >= 0) return best;
            }

            int options = 0;
            for (int i = 0; i < links.Length; i++) if (links[i] != previous) options++;
            if (options == 0) return previous;

            int pick = Random.Range(0, options);
            for (int i = 0; i < links.Length; i++)
            {
                if (links[i] == previous) continue;
                if (pick-- == 0) return links[i];
            }
            return previous;
        }

        void Pose(Walker w, float hop)
        {
            w.tr.SetPositionAndRotation(w.position + Vector3.up * (Lift + hop),
                Quaternion.Euler(0f, w.heading, 0f));
        }

        static Transform BuildFigure(Transform parent, System.Random rng)
        {
            bool woman = rng.NextDouble() < WomanChance;
            float h = Height * (1f + Mathf.Lerp(-HeightJitter, HeightJitter, (float)rng.NextDouble()))
                      * (woman ? 0.95f : 1f);

            var root = new GameObject(woman ? "Villager (woman)" : "Villager (man)");
            root.transform.SetParent(parent, false);

            float lower = h * 0.44f, upper = h * 0.30f, face = h * 0.15f, top = h * 0.11f;
            Color skin = Pick(Skins, rng);

            if (woman)
            {
                AddSegment(root.transform, Pick(Skirts, rng), lower * 0.5f, Width * 1.2f, lower, Thickness * 1.25f);
                AddSegment(root.transform, Pick(Shirts, rng), lower + upper * 0.5f, Width * 0.9f, upper, Thickness);
                AddSegment(root.transform, skin, lower + upper + face * 0.5f, Width * 0.8f, face, Thickness * 0.9f);
                AddSegment(root.transform, Pick(Scarves, rng), lower + upper + face + top * 0.5f,
                    Width * 0.9f, top, Thickness * 1f);
            }
            else
            {
                AddSegment(root.transform, Pick(Trousers, rng), lower * 0.5f, Width * 0.95f, lower, Thickness);
                AddSegment(root.transform, Pick(Shirts, rng), lower + upper * 0.5f, Width, upper, Thickness * 1.05f);
                AddSegment(root.transform, skin, lower + upper + face * 0.5f, Width * 0.8f, face, Thickness * 0.9f);
                AddSegment(root.transform, Pick(Hats, rng), lower + upper + face + top * 0.5f,
                    Width * 1.15f, top, Thickness * 1.2f);
            }

            NightLights.MarkCaster(root);
            return root.transform;
        }

        static Color Pick(Color[] colors, System.Random rng) => colors[rng.Next(colors.Length)];

        static void AddSegment(Transform parent, Color color,
            float centreY, float width, float height, float thickness)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Segment";

            var col = go.GetComponent<Collider>();
            if (col != null) { col.enabled = false; Destroy(col); }

            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, centreY, 0f);
            go.transform.localScale = new Vector3(width, height, thickness);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor(color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material MaterialFor(Color color)
        {
            if (Materials.TryGetValue(color, out Material mat) && mat != null) return mat;

            mat = new Material(NightReceivers.Lit) { name = "Villager cloth" };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0f);
            Materials[color] = mat;
            return mat;
        }
    }
}
