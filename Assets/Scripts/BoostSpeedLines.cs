using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class BoostSpeedLines
    {
        const int Count = 26;
        const int SortingOrder = -1;
        const float LengthMin = 0.12f, LengthMax = 0.3f;
        const float ThickMin = 0.003f, ThickMax = 0.006f;
        const float RushMin = 1.6f, RushMax = 2.6f;
        const float EdgeShare = 0.85f, Band = 0.3f, Margin = 0.02f;
        const float ClearInner = 0.45f, ClearOuter = 0.85f, ClearWidth = 0.6f;
        const float Opacity = 0.4f;
        const float MinFacing = 0.2f;
        const int TextureLength = 64, TextureWidth = 8;

        static Texture2D _texture;

        class Line
        {
            public RectTransform rt;
            public RawImage image;
            public float x, y, length, thick, rush, direction;
            public bool live;
        }

        readonly List<Line> _lines = new List<Line>();
        readonly GameObject _root;
        readonly Canvas _canvas;
        readonly float _dim;
        float _direction = -1f;
        bool _shown;

        public BoostSpeedLines(float dim)
        {
            _dim = dim;
            _root = new GameObject("Boost Speed Lines", typeof(Canvas));
            _canvas = _root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            _canvas.enabled = false;

            Texture2D texture = StreakTexture();
            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject("Speed Line", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(_root.transform, false);

                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);

                var image = go.GetComponent<RawImage>();
                image.texture = texture;
                image.raycastTarget = false;
                image.color = Color.clear;

                _lines.Add(new Line { rt = rt, image = image });
            }
        }

        public void Tick(float dt, float heading, float level, bool feeding)
        {
            if (level <= 0f)
            {
                if (_shown) Clear();
                return;
            }

            float facing = Mathf.Cos(heading);
            if (Mathf.Abs(facing) > MinFacing) _direction = facing > 0f ? -1f : 1f;

            if (!_shown)
            {
                foreach (Line line in _lines) Launch(line, scatter: true);
                _canvas.enabled = true;
                _shown = true;
            }

            float width = Screen.width / Mathf.Max(0.01f, _canvas.scaleFactor);
            float height = Screen.height / Mathf.Max(0.01f, _canvas.scaleFactor);
            foreach (Line line in _lines)
            {
                if (line.live) line.x += line.direction * line.rush * dt;
                bool gone = line.direction < 0f
                    ? line.x + line.length * 0.5f < 0f
                    : line.x - line.length * 0.5f > 1f;

                if (!line.live || gone)
                {
                    if (!feeding)
                    {
                        line.live = false;
                        line.image.color = Color.clear;
                        continue;
                    }
                    Launch(line, scatter: false);
                }

                float dx = (line.x - 0.5f) * 2f, dy = (line.y - 0.5f) * 2f;
                float clear = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ClearInner, ClearOuter,
                    Mathf.Sqrt(dx * dx * ClearWidth + dy * dy)));

                line.rt.anchoredPosition = new Vector2(line.x * width, line.y * height);
                line.rt.sizeDelta = new Vector2(line.length * width, line.thick * height);
                line.image.color = new Color(1f, 1f, 1f, Opacity * _dim * level * clear);
            }
        }

        public void Release()
        {
            if (_root != null) Object.Destroy(_root);
            _lines.Clear();
        }

        void Launch(Line line, bool scatter)
        {
            line.direction = _direction;
            line.length = Random.Range(LengthMin, LengthMax);
            line.thick = Random.Range(ThickMin, ThickMax);
            line.rush = Random.Range(RushMin, RushMax);
            line.x = scatter
                ? Random.value
                : line.direction < 0f ? 1f + line.length * 0.5f : -line.length * 0.5f;

            if (Random.value < EdgeShare)
                line.y = Random.value < 0.5f
                    ? Random.Range(Margin, Band)
                    : Random.Range(1f - Band, 1f - Margin);
            else line.y = Random.Range(Band, 1f - Band);

            line.live = true;
        }

        static Texture2D StreakTexture()
        {
            if (_texture != null) return _texture;

            var pixels = new Color32[TextureLength * TextureWidth];
            for (int y = 0; y < TextureWidth; y++)
            {
                float v = (y + 0.5f) / TextureWidth;
                float edge = Mathf.Pow(Mathf.Sin(Mathf.PI * v), 0.6f);
                for (int x = 0; x < TextureLength; x++)
                {
                    float u = (x + 0.5f) / TextureLength;
                    float taper = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.7f);
                    byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(edge * taper));
                    pixels[y * TextureLength + x] = new Color32(255, 255, 255, a);
                }
            }

            _texture = new Texture2D(TextureLength, TextureWidth, TextureFormat.RGBA32, false)
            {
                name = "Boost Streak",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            _texture.SetPixels32(pixels);
            _texture.Apply(false, true);
            return _texture;
        }

        public void Clear()
        {
            foreach (Line line in _lines)
            {
                line.live = false;
                if (line.image != null) line.image.color = Color.clear;
            }
            if (_canvas != null) _canvas.enabled = false;
            _shown = false;
        }
    }
}
