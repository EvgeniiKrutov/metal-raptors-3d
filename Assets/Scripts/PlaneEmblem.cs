using UnityEngine;
using UnityEngine.UI;

namespace MetalRaptors
{
    public enum EraEmblem { Biplane, Fighter, Jet, Delta }

    public class PlaneEmblem : MaskableGraphic
    {
        struct Shape
        {
            public readonly Vector2[] Points;
            public readonly bool Back;

            public Shape(bool back, params float[] coords)
            {
                Back = back;
                Points = new Vector2[coords.Length / 2];
                for (int i = 0; i < Points.Length; i++)
                    Points[i] = new Vector2(coords[i * 2], coords[i * 2 + 1]);
            }
        }

        EraEmblem _emblem = EraEmblem.Biplane;
        Color _front = Color.black;
        Color _back = Color.gray;

        public static PlaneEmblem Create(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(PlaneEmblem));
            go.transform.SetParent(parent, false);

            var view = go.GetComponent<PlaneEmblem>();
            view.raycastTarget = false;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return view;
        }

        public void SetEmblem(EraEmblem emblem)
        {
            _emblem = emblem;
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
            float scale = Mathf.Min(rect.width, rect.height);
            var centre = new Vector2(rect.center.x, rect.center.y);

            Shape[] shapes = Shapes(_emblem);
            for (int pass = 0; pass < 2; pass++)
            {
                bool back = pass == 0;
                foreach (Shape shape in shapes)
                {
                    if (shape.Back != back) continue;
                    AddShape(vh, shape, centre, scale, back ? _back : _front);
                }
            }
        }

        static void AddShape(VertexHelper vh, Shape shape, Vector2 centre, float scale, Color color)
        {
            int first = vh.currentVertCount;

            foreach (Vector2 point in shape.Points)
                vh.AddVert(new Vector3(centre.x + point.x * scale, centre.y + point.y * scale, 0f),
                    color, Vector2.zero);

            for (int i = 1; i < shape.Points.Length - 1; i++)
                vh.AddTriangle(first, first + i, first + i + 1);
        }

        static Shape[] Shapes(EraEmblem emblem)
        {
            switch (emblem)
            {
                case EraEmblem.Fighter: return Fighter;
                case EraEmblem.Jet: return Jet;
                case EraEmblem.Delta: return Delta;
                default: return Biplane;
            }
        }

        static readonly Shape[] Biplane =
        {
            new Shape(true, 0.03f, -0.46f, 0.25f, -0.46f, 0.25f, 0.46f, 0.03f, 0.46f),
            new Shape(true, 0.44f, -0.28f, 0.48f, -0.28f, 0.48f, 0.28f, 0.44f, 0.28f),
            new Shape(false, -0.46f, -0.05f, -0.46f, 0.05f, 0.22f, 0.075f, 0.40f, 0.05f,
                0.46f, 0f, 0.40f, -0.05f, 0.22f, -0.075f),
            new Shape(false, -0.07f, -0.5f, 0.15f, -0.5f, 0.15f, 0.5f, -0.07f, 0.5f),
            new Shape(false, -0.46f, -0.24f, -0.34f, -0.24f, -0.34f, 0.24f, -0.46f, 0.24f),
        };

        static readonly Shape[] Fighter =
        {
            new Shape(true, 0.44f, -0.3f, 0.49f, -0.3f, 0.49f, 0.3f, 0.44f, 0.3f),
            new Shape(false, -0.46f, -0.045f, -0.46f, 0.045f, 0.20f, 0.07f, 0.38f, 0.055f,
                0.48f, 0f, 0.38f, -0.055f, 0.20f, -0.07f),
            new Shape(false, 0.02f, 0.02f, 0.20f, 0.02f, 0.14f, 0.46f, 0.05f, 0.46f),
            new Shape(false, 0.02f, -0.02f, 0.20f, -0.02f, 0.14f, -0.46f, 0.05f, -0.46f),
            new Shape(false, -0.46f, 0.02f, -0.36f, 0.02f, -0.39f, 0.22f, -0.46f, 0.22f),
            new Shape(false, -0.46f, -0.02f, -0.36f, -0.02f, -0.39f, -0.22f, -0.46f, -0.22f),
        };

        static readonly Shape[] Jet =
        {
            new Shape(true, -0.46f, -0.14f, -0.38f, -0.14f, -0.38f, 0.14f, -0.46f, 0.14f),
            new Shape(false, -0.46f, -0.05f, -0.46f, 0.05f, 0.10f, 0.065f, 0.34f, 0.04f,
                0.50f, 0f, 0.34f, -0.04f, 0.10f, -0.065f),
            new Shape(false, 0.14f, 0.03f, 0.02f, 0.03f, -0.26f, 0.44f, -0.10f, 0.44f),
            new Shape(false, 0.14f, -0.03f, 0.02f, -0.03f, -0.26f, -0.44f, -0.10f, -0.44f),
            new Shape(false, -0.34f, 0.02f, -0.44f, 0.02f, -0.50f, 0.22f, -0.40f, 0.22f),
            new Shape(false, -0.34f, -0.02f, -0.44f, -0.02f, -0.50f, -0.22f, -0.40f, -0.22f),
        };

        static readonly Shape[] Delta =
        {
            new Shape(true, -0.34f, 0.06f, -0.46f, 0.06f, -0.50f, 0.24f, -0.38f, 0.24f),
            new Shape(true, -0.34f, -0.06f, -0.46f, -0.06f, -0.50f, -0.24f, -0.38f, -0.24f),
            new Shape(false, -0.46f, -0.045f, -0.46f, 0.045f, 0.06f, 0.06f, 0.30f, 0.045f,
                0.52f, 0f, 0.30f, -0.045f, 0.06f, -0.06f),
            new Shape(false, 0.16f, 0.03f, -0.16f, 0.40f, -0.32f, 0.40f, -0.32f, 0.03f),
            new Shape(false, 0.16f, -0.03f, -0.16f, -0.40f, -0.32f, -0.40f, -0.32f, -0.03f),
            new Shape(false, 0.30f, 0.04f, 0.20f, 0.04f, 0.10f, 0.22f, 0.22f, 0.22f),
            new Shape(false, 0.30f, -0.04f, 0.20f, -0.04f, 0.10f, -0.22f, 0.22f, -0.22f),
        };
    }
}
