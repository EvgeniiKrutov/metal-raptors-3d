using UnityEngine;

namespace MetalRaptors
{
    public enum NightLightType { Cone, Point }

    public class NightLight2D : MonoBehaviour
    {
        public NightLightType type = NightLightType.Cone;
        [ColorUsage(false, true)] public Color color = new Color(1f, 0.851f, 0.627f);
        public float intensity = 1f;
        public float range = 100f;
        [Range(0f, 179f)] public float innerAngle = 30f;
        [Range(0.1f, 179f)] public float outerAngle = 60f;
        public float sourceRadius = 2f;
        [Range(1f, 3f)] public float falloff = 1.5f;
        public Vector3 localDirection = Vector3.forward;
        public int priority;
        public bool occludedByTerrain;
        public bool castShadows;
        public Vector2 flicker;

        internal int registryIndex = -1;
        float _seed;

        void OnEnable()
        {
            _seed = Random.value * 100f;
            NightLights.Register(this);
        }

        void OnDisable() => NightLights.Unregister(this);

        public Vector2 PlaneDirection
        {
            get
            {
                Vector3 d = transform.TransformDirection(localDirection);
                var flat = new Vector2(d.x, d.y);
                float len = flat.magnitude;
                return len > 1e-5f ? flat / len : Vector2.right;
            }
        }

        public float CurrentIntensity(float time)
        {
            if (flicker.x <= 0f) return intensity;
            float noise = Mathf.PerlinNoise(time * Mathf.Max(0f, flicker.y), _seed) * 2f - 1f;
            return Mathf.Max(0f, intensity * (1f + flicker.x * noise));
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 1f);
            Vector3 origin = transform.position;

            if (type == NightLightType.Point)
            {
                DrawCircle(origin, range);
                return;
            }

            Vector2 dir = PlaneDirection;
            var forward = new Vector3(dir.x, dir.y, 0f);
            Vector3 apex = origin - forward * sourceRadius;
            DrawEdges(apex, forward, outerAngle, range + sourceRadius);
            Gizmos.color = new Color(color.r, color.g, color.b, 0.5f);
            DrawEdges(apex, forward, innerAngle, range + sourceRadius);
            DrawArc(origin, forward, outerAngle, range);
        }

        static void DrawEdges(Vector3 apex, Vector3 forward, float angle, float length)
        {
            float half = angle * 0.5f;
            Gizmos.DrawLine(apex, apex + Quaternion.Euler(0f, 0f, half) * forward * length);
            Gizmos.DrawLine(apex, apex + Quaternion.Euler(0f, 0f, -half) * forward * length);
        }

        static void DrawArc(Vector3 origin, Vector3 forward, float angle, float radius)
        {
            const int Segments = 24;
            float half = angle * 0.5f;
            Vector3 prev = origin + Quaternion.Euler(0f, 0f, -half) * forward * radius;
            for (int i = 1; i <= Segments; i++)
            {
                float a = Mathf.Lerp(-half, half, (float)i / Segments);
                Vector3 next = origin + Quaternion.Euler(0f, 0f, a) * forward * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        static void DrawCircle(Vector3 origin, float radius)
        {
            const int Segments = 32;
            Vector3 prev = origin + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                Vector3 next = origin + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
