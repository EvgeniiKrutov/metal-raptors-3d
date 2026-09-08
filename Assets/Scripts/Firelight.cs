using UnityEngine;

namespace MetalRaptors
{
    public static class Firelight
    {
        static Vector3 _gain = Vector3.one;
        static bool _lit;

        public static void Grade(Color filter, float boost)
        {
            float peak = Mathf.Max(filter.r, Mathf.Max(filter.g, filter.b));
            _gain = new Vector3(
                peak / Mathf.Max(1e-3f, filter.r),
                peak / Mathf.Max(1e-3f, filter.g),
                peak / Mathf.Max(1e-3f, filter.b)) * Mathf.Max(0f, boost);
            _lit = true;
        }

        public static void Clear()
        {
            _gain = Vector3.one;
            _lit = false;
        }

        public static Color Warm(Color c) =>
            new Color(c.r * _gain.x, c.g * _gain.y, c.b * _gain.z, c.a);

        public static Color Beam(Color c)
        {
            if (!_lit) return c;

            Color warm = Warm(c);
            float peak = Mathf.Max(warm.r, Mathf.Max(warm.g, warm.b));
            if (peak <= 1e-3f) return c;

            float k = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) / peak;
            return new Color(warm.r * k, warm.g * k, warm.b * k, c.a);
        }
    }
}
