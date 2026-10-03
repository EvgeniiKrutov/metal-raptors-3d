using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace MetalRaptors
{
    public static class NightLights
    {
        public const int MaxLights = 32;

        public const float ViewWidth = 860f;

        public static float PlaneZ = 100f;

        public const uint CasterRenderingLayer = 1u << 8;

        const int FlashCapacity = 48;
        const int CandidateCapacity = 160;
        const float FlashRise = 0.12f;

        struct FlashSlot
        {
            public Vector3 position;
            public Color color;
            public float intensity;
            public float radius;
            public float start;
            public float duration;
            public int priority;
            public bool live;
        }

        struct Candidate
        {
            public int priority;
            public float weight;
            public Vector4 a;
            public Vector4 b;
            public Vector4 c;
            public Vector4 d;
        }

        static readonly List<NightLight2D> _active = new List<NightLight2D>(64);
        static readonly FlashSlot[] _flashes = new FlashSlot[FlashCapacity];
        static readonly Candidate[] _candidates = new Candidate[CandidateCapacity];

        static readonly ProfilerMarker CollectMarker = new ProfilerMarker("NightLights.Collect");

        public static IReadOnlyList<NightLight2D> Active => _active;

        internal static void Register(NightLight2D light)
        {
            if (light == null || light.registryIndex >= 0) return;
            light.registryIndex = _active.Count;
            _active.Add(light);
        }

        internal static void Unregister(NightLight2D light)
        {
            if (light == null) return;
            int index = light.registryIndex;
            light.registryIndex = -1;
            if (index < 0 || index >= _active.Count || _active[index] != light) return;

            int last = _active.Count - 1;
            NightLight2D moved = _active[last];
            _active[index] = moved;
            moved.registryIndex = index;
            _active.RemoveAt(last);
            if (moved == light) light.registryIndex = -1;
        }

        public static void MarkCaster(GameObject root, bool cast = true)
        {
            if (root == null) return;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.renderingLayerMask = cast
                    ? renderer.renderingLayerMask | CasterRenderingLayer
                    : renderer.renderingLayerMask & ~CasterRenderingLayer;
        }

        internal static NightLight2D ShadowCaster()
        {
            NightLight2D best = null;
            for (int i = 0; i < _active.Count; i++)
            {
                NightLight2D light = _active[i];
                if (light == null || !light.castShadows || light.type != NightLightType.Cone) continue;
                if (best == null || light.priority > best.priority) best = light;
            }
            return best;
        }

        public static void Flash(Vector3 position, Color color, float intensity, float radius,
            float duration, int priority = 0)
        {
            if (!NightLightingController.Running) return;
            if (intensity <= 0f || radius <= 0f || duration <= 0f) return;

            float now = Time.time;
            int slot = -1;
            float weakest = float.MaxValue;

            for (int i = 0; i < FlashCapacity; i++)
            {
                FlashSlot f = _flashes[i];
                if (!f.live || now - f.start >= f.duration)
                {
                    slot = i;
                    break;
                }

                float progress = (now - f.start) / f.duration;
                float score = f.priority + (1f - progress) * 0.99f;
                if (score < weakest && f.priority <= priority)
                {
                    weakest = score;
                    slot = i;
                }
            }

            if (slot < 0) return;

            _flashes[slot] = new FlashSlot
            {
                position = position,
                color = color,
                intensity = intensity,
                radius = radius,
                start = now,
                duration = duration,
                priority = priority,
                live = true,
            };
        }

        public static void ClearFlashes()
        {
            for (int i = 0; i < FlashCapacity; i++) _flashes[i].live = false;
        }

        static float Envelope(float t)
        {
            if (t < FlashRise) return t / FlashRise;
            float k = 1f - (t - FlashRise) / (1f - FlashRise);
            return k * k;
        }

        static Vector4 Linear(Color c, float intensity)
        {
            Color l = QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
            return new Vector4(l.r * intensity, l.g * intensity, l.b * intensity, 0f);
        }

        internal static Rect PlaneArea(Camera cam, float planeZ, float guard)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                Ray ray = cam.ViewportPointToRay(new Vector3(i & 1, (i >> 1) & 1, 0f));
                float dz = ray.direction.z;
                float t = Mathf.Abs(dz) > 1e-4f ? (planeZ - ray.origin.z) / dz : -1f;
                if (t <= 0f || t > cam.farClipPlane) t = cam.farClipPlane;
                Vector3 p = ray.origin + ray.direction * t;
                minX = Mathf.Min(minX, p.x);
                minY = Mathf.Min(minY, p.y);
                maxX = Mathf.Max(maxX, p.x);
                maxY = Mathf.Max(maxY, p.y);
            }

            var center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            var size = new Vector2(Mathf.Max(1f, (maxX - minX) * guard), Mathf.Max(1f, (maxY - minY) * guard));
            return new Rect(center - size * 0.5f, size);
        }

        internal static int Collect(Rect area, float planeZ, int maxLights,
            Vector4[] a, Vector4[] b, Vector4[] c, Vector4[] d, out float depthRange)
        {
            using (CollectMarker.Auto())
            {
                float now = Time.time;
                int count = 0;

                for (int i = 0; i < _active.Count && count < CandidateCapacity; i++)
                {
                    NightLight2D light = _active[i];
                    if (light == null) continue;

                    float range = Mathf.Max(0.01f, light.range);
                    Vector3 pos = light.transform.position;
                    if (!Reaches(area, planeZ, pos, range)) continue;

                    float intensity = light.CurrentIntensity(now);
                    if (intensity <= 0f) continue;

                    bool cone = light.type == NightLightType.Cone;
                    Vector2 dir = light.PlaneDirection;
                    float outer = Mathf.Clamp(light.outerAngle, 0.2f, 179f);
                    float inner = Mathf.Clamp(light.innerAngle, 0f, outer - 0.1f);

                    Vector4 col = Linear(light.color, intensity);
                    col.w = cone ? 1f : 0f;

                    _candidates[count++] = new Candidate
                    {
                        priority = light.priority,
                        weight = intensity * range,
                        a = new Vector4(pos.x, pos.y, dir.x, dir.y),
                        b = new Vector4(range,
                            Mathf.Cos(inner * 0.5f * Mathf.Deg2Rad),
                            Mathf.Cos(outer * 0.5f * Mathf.Deg2Rad),
                            Mathf.Max(0f, light.sourceRadius)),
                        c = col,
                        d = new Vector4(Mathf.Clamp(light.falloff, 0.1f, 8f),
                            light.occludedByTerrain ? 1f : 0f, pos.z, 0f),
                    };
                }

                for (int i = 0; i < FlashCapacity && count < CandidateCapacity; i++)
                {
                    FlashSlot f = _flashes[i];
                    if (!f.live) continue;

                    float t = (now - f.start) / f.duration;
                    if (t >= 1f || t < 0f)
                    {
                        _flashes[i].live = false;
                        continue;
                    }

                    if (!Reaches(area, planeZ, f.position, f.radius)) continue;

                    float intensity = f.intensity * Envelope(t);
                    if (intensity <= 0f) continue;

                    _candidates[count++] = new Candidate
                    {
                        priority = f.priority,
                        weight = intensity * f.radius,
                        a = new Vector4(f.position.x, f.position.y, 1f, 0f),
                        b = new Vector4(f.radius, 1f, 0f, 0f),
                        c = Linear(f.color, intensity),
                        d = new Vector4(1.5f, 0f, f.position.z, 0f),
                    };
                }

                Sort(count);

                int packed = Mathf.Min(count, Mathf.Clamp(maxLights, 0, MaxLights));
                depthRange = 1f;
                for (int i = 0; i < packed; i++)
                {
                    depthRange = Mathf.Max(depthRange, _candidates[i].b.x);
                    a[i] = _candidates[i].a;
                    b[i] = _candidates[i].b;
                    c[i] = _candidates[i].c;
                    d[i] = _candidates[i].d;
                }
                for (int i = packed; i < MaxLights; i++)
                {
                    a[i] = Vector4.zero;
                    b[i] = Vector4.zero;
                    c[i] = Vector4.zero;
                    d[i] = Vector4.zero;
                }
                return packed;
            }
        }

        static bool Reaches(Rect area, float planeZ, Vector3 position, float range)
        {
            if (Mathf.Abs(position.z - planeZ) > range) return false;
            return position.x + range >= area.xMin && position.x - range <= area.xMax
                && position.y + range >= area.yMin && position.y - range <= area.yMax;
        }

        static void Sort(int count)
        {
            for (int i = 1; i < count; i++)
            {
                Candidate key = _candidates[i];
                int j = i - 1;
                while (j >= 0 && Before(key, _candidates[j]))
                {
                    _candidates[j + 1] = _candidates[j];
                    j--;
                }
                _candidates[j + 1] = key;
            }
        }

        static bool Before(in Candidate x, in Candidate y)
        {
            if (x.priority != y.priority) return x.priority > y.priority;
            return x.weight > y.weight;
        }
    }
}
