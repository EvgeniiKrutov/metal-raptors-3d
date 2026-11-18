using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public enum HudIcon
    {
        Bomb,
        Boost,
        Roll,
        Fire,
        Light,
        Dive,
        Fix,
        Pause,
    }

    public static class HudIcons
    {
        const int Size = 128;
        const float RocketTilt = 45f;

        static readonly Dictionary<HudIcon, Sprite> Cache = new Dictionary<HudIcon, Sprite>();

        static readonly float[] RocketFin = { 0.39f, 0.47f, 0.21f, 0.25f, 0.45f, 0.32f };
        static readonly float[] RocketFlame = { 0.43f, 0.27f, 0.50f, 0.10f, 0.57f, 0.27f };
        static readonly float[] RollHead = { 0.64f, 0.50f, 0.90f, 0.50f, 0.77f, 0.70f };
        static readonly float[] LampBody = { 0.12f, 0.41f, 0.40f, 0.28f, 0.40f, 0.72f, 0.12f, 0.59f };
        static readonly float[] DiveHead = { 0.26f, 0.44f, 0.50f, 0.12f, 0.74f, 0.44f };

        public static Sprite Get(HudIcon icon)
        {
            if (Cache.TryGetValue(icon, out Sprite cached) && cached != null) return cached;

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = $"Hud Icon {icon}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size);
                    float alpha = Mathf.Clamp01(0.5f - Distance(icon, p) * Size);
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.HideAndDontSave;

            Cache[icon] = sprite;
            return sprite;
        }

        static float Distance(HudIcon icon, Vector2 p)
        {
            switch (icon)
            {
                case HudIcon.Bomb: return Bomb(p);
                case HudIcon.Boost: return Rocket(p);
                case HudIcon.Roll: return Roll(p);
                case HudIcon.Fire: return Crosshair(p);
                case HudIcon.Light: return Lamp(p);
                case HudIcon.Dive: return Dive(p);
                case HudIcon.Fix: return Wrench(p);
                default: return Pause(p);
            }
        }

        static float Bomb(Vector2 p)
        {
            float d = Disc(p, 0.44f, 0.42f, 0.27f);
            d = Mathf.Min(d, Line(p, 0.62f, 0.60f, 0.67f, 0.65f, 0.15f));
            d = Mathf.Max(d, -Arc(p, 0.44f, 0.42f, 0.17f, 0.045f, 110f, 160f));
            d = Mathf.Min(d, Line(p, 0.68f, 0.68f, 0.76f, 0.78f, 0.055f));
            return Mathf.Min(d, Disc(p, 0.80f, 0.83f, 0.06f));
        }

        static float Rocket(Vector2 p)
        {
            Vector2 q = Turn(p, RocketTilt);
            var folded = new Vector2(0.5f - Mathf.Abs(q.x - 0.5f), q.y);

            float body = Mathf.Max(Disc(q, 0.30f, 0.54f, 0.35f), Disc(q, 0.70f, 0.54f, 0.35f));
            body = Mathf.Max(body, 0.32f - q.y);
            body = Mathf.Max(body, -Disc(q, 0.5f, 0.60f, 0.065f));

            float d = Mathf.Min(body, Convex(folded, RocketFin));
            return Mathf.Min(d, Convex(q, RocketFlame));
        }

        static float Roll(Vector2 p)
        {
            float d = Arc(p, 0.5f, 0.5f, 0.27f, 0.085f, 60f, 360f);
            return Mathf.Min(d, Convex(p, RollHead));
        }

        static float Crosshair(Vector2 p)
        {
            float d = Ring(p, 0.5f, 0.5f, 0.27f, 0.07f);
            d = Mathf.Min(d, Line(p, 0.5f, 0.64f, 0.5f, 0.90f, 0.07f));
            d = Mathf.Min(d, Line(p, 0.5f, 0.10f, 0.5f, 0.36f, 0.07f));
            d = Mathf.Min(d, Line(p, 0.10f, 0.5f, 0.36f, 0.5f, 0.07f));
            d = Mathf.Min(d, Line(p, 0.64f, 0.5f, 0.90f, 0.5f, 0.07f));
            return Mathf.Min(d, Disc(p, 0.5f, 0.5f, 0.045f));
        }

        static float Lamp(Vector2 p)
        {
            float d = Convex(p, LampBody);
            d = Mathf.Min(d, Line(p, 0.54f, 0.50f, 0.88f, 0.50f, 0.07f));
            d = Mathf.Min(d, Line(p, 0.52f, 0.66f, 0.82f, 0.83f, 0.07f));
            return Mathf.Min(d, Line(p, 0.52f, 0.34f, 0.82f, 0.17f, 0.07f));
        }

        static float Dive(Vector2 p)
        {
            float d = Line(p, 0.5f, 0.84f, 0.5f, 0.42f, 0.11f);
            return Mathf.Min(d, Convex(p, DiveHead));
        }

        static float Wrench(Vector2 p)
        {
            float d = Mathf.Min(Line(p, 0.27f, 0.27f, 0.60f, 0.60f, 0.13f),
                Disc(p, 0.66f, 0.66f, 0.19f));
            return Mathf.Max(d, -Line(p, 0.68f, 0.68f, 0.95f, 0.95f, 0.14f));
        }

        static float Pause(Vector2 p)
        {
            return Mathf.Min(Line(p, 0.38f, 0.27f, 0.38f, 0.73f, 0.13f),
                Line(p, 0.62f, 0.27f, 0.62f, 0.73f, 0.13f));
        }

        static Vector2 Turn(Vector2 p, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            float x = p.x - 0.5f;
            float y = p.y - 0.5f;
            return new Vector2(0.5f + x * cos - y * sin, 0.5f + x * sin + y * cos);
        }

        static float Disc(Vector2 p, float x, float y, float radius)
        {
            float dx = p.x - x;
            float dy = p.y - y;
            return Mathf.Sqrt(dx * dx + dy * dy) - radius;
        }

        static float Ring(Vector2 p, float x, float y, float radius, float stroke)
            => Mathf.Abs(Disc(p, x, y, radius)) - stroke * 0.5f;

        static float Line(Vector2 p, float ax, float ay, float bx, float by, float stroke)
        {
            float px = p.x - ax;
            float py = p.y - ay;
            float ex = bx - ax;
            float ey = by - ay;
            float along = Mathf.Clamp01((px * ex + py * ey) / (ex * ex + ey * ey));
            float dx = px - ex * along;
            float dy = py - ey * along;
            return Mathf.Sqrt(dx * dx + dy * dy) - stroke * 0.5f;
        }

        static float Arc(Vector2 p, float x, float y, float radius, float stroke, float fromDeg,
            float toDeg)
        {
            float dx = p.x - x;
            float dy = p.y - y;
            float mid = (fromDeg + toDeg) * 0.5f;
            float half = (toDeg - fromDeg) * 0.5f;
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

            if (Mathf.Abs(Mathf.DeltaAngle(angle, mid)) <= half)
                return Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - radius) - stroke * 0.5f;

            float from = fromDeg * Mathf.Deg2Rad;
            float to = toDeg * Mathf.Deg2Rad;
            float start = Disc(p, x + Mathf.Cos(from) * radius, y + Mathf.Sin(from) * radius, 0f);
            float end = Disc(p, x + Mathf.Cos(to) * radius, y + Mathf.Sin(to) * radius, 0f);
            return Mathf.Min(start, end) - stroke * 0.5f;
        }

        static float Convex(Vector2 p, float[] corners)
        {
            float d = float.NegativeInfinity;
            int count = corners.Length / 2;
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                float ax = corners[i * 2];
                float ay = corners[i * 2 + 1];
                float ex = corners[next * 2] - ax;
                float ey = corners[next * 2 + 1] - ay;
                float length = Mathf.Sqrt(ex * ex + ey * ey);
                d = Mathf.Max(d, ((p.x - ax) * ey - (p.y - ay) * ex) / length);
            }
            return d;
        }
    }
}
