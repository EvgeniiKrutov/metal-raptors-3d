using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class TerrainSilhouette : MaskableGraphic
    {
        const int Samples = 110;
        const float SeaLevel = 0.17f;

        TerrainKind _kind = TerrainKind.Verdun;
        int _seed;
        Color _front = Color.black;
        Color _back = Color.gray;

        public static TerrainSilhouette Create(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TerrainSilhouette));
            go.transform.SetParent(parent, false);

            var view = go.GetComponent<TerrainSilhouette>();
            view.raycastTarget = false;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return view;
        }

        public void SetProfile(TerrainKind kind, int seed)
        {
            _kind = kind;
            _seed = seed;
            SetVerticesDirty();
        }

        public void SetTint(Color front, Color back)
        {
            _front = front;
            _back = back;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = GetPixelAdjustedRect();
            AddLayer(vh, rect, _back, false);
            AddLayer(vh, rect, _front, true);
        }

        void AddLayer(VertexHelper vh, Rect rect, Color color, bool front)
        {
            int first = vh.currentVertCount;

            for (int i = 0; i <= Samples; i++)
            {
                float t = i / (float)Samples;
                float h = Mathf.Clamp(Height(t, front), 0.02f, 0.98f);
                float x = rect.xMin + rect.width * t;

                vh.AddVert(new Vector3(x, rect.yMin, 0f), color, Vector2.zero);
                vh.AddVert(new Vector3(x, rect.yMin + rect.height * h, 0f), color, Vector2.zero);
            }

            for (int i = 0; i < Samples; i++)
            {
                int a = first + i * 2;
                vh.AddTriangle(a, a + 1, a + 3);
                vh.AddTriangle(a, a + 3, a + 2);
            }
        }

        float Height(float t, bool front)
        {
            switch (_kind)
            {
                case TerrainKind.Flanders: return Coast(t, front);
                case TerrainKind.Dolomites: return Peaks(t, front);
                default: return Craters(t, front);
            }
        }

        float Craters(float t, bool front)
        {
            int bank = front ? 0 : 40;
            float wave = front ? 0.07f : 0.09f;
            float h = (front ? 0.30f : 0.46f)
                      + wave * Wave(t, Range(bank + 1, 1.2f, 2.0f), Phase(bank + 2))
                      + wave * 0.55f * Wave(t, Range(bank + 3, 2.6f, 4.2f), Phase(bank + 4));

            if (!front) return h;

            for (int k = 0; k < 3; k++)
            {
                float centre = Range(bank + 10 + k, 0.12f, 0.9f);
                float width = Range(bank + 20 + k, 0.035f, 0.07f);
                float d = (t - centre) / width;

                h -= 0.085f * Mathf.Exp(-d * d);
                h += 0.03f * Mathf.Exp(-1.6f * Sqr(Mathf.Abs(d) - 1.8f));
            }
            return h;
        }

        float Coast(float t, bool front)
        {
            int bank = front ? 0 : 40;
            float shore = Range(1, 0.44f, 0.66f) - (front ? 0f : 0.06f);

            float land = (front ? 0.32f : 0.46f)
                         + 0.055f * Wave(t, Range(bank + 3, 1.6f, 2.6f), Phase(bank + 4))
                         + 0.03f * Wave(t, Range(bank + 5, 3.4f, 5f), Phase(bank + 6));

            float sea = front ? SeaLevel : SeaLevel + 0.035f;
            float beach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(shore - 0.12f, shore, t));
            return Mathf.Lerp(land, sea, beach);
        }

        float Peaks(float t, bool front)
        {
            int bank = front ? 0 : 40;
            int count = front ? 3 : 4;

            float baseline = (front ? 0.14f : 0.22f)
                             + 0.03f * Wave(t, Range(bank + 7, 2f, 3.4f), Phase(bank + 8));
            float h = baseline;

            for (int k = 0; k < count; k++)
            {
                float centre = (k + Range(bank + 30 + k, 0.15f, 0.85f)) / count;
                float width = Range(bank + 50 + k, 0.09f, 0.16f);
                float height = front ? Range(bank + 60 + k, 0.34f, 0.56f)
                                     : Range(bank + 60 + k, 0.5f, 0.78f);

                h = Mathf.Max(h, baseline + height * Mathf.Max(0f, 1f - Mathf.Abs(t - centre) / width));
            }
            return h;
        }

        static float Wave(float t, float frequency, float phase) =>
            Mathf.Sin(2f * Mathf.PI * (frequency * t + phase));

        static float Sqr(float v) => v * v;

        float Phase(int index) => Hash(index);

        float Range(int index, float min, float max) => min + (max - min) * Hash(index);

        float Hash(int index)
        {
            unchecked
            {
                uint h = (uint)(_seed * 73856093) ^ (uint)((index + 1) * 19349663);
                h ^= h >> 13;
                h *= 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
