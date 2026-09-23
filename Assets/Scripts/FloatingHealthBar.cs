using UnityEngine;

namespace MetalRaptors
{
    public class FloatingHealthBar
    {
        static readonly Color BackColor = new Color(0.06f, 0.06f, 0.06f);
        static readonly Color FullColor = new Color(0.25f, 0.9f, 0.3f);
        static readonly Color EmptyColor = new Color(0.95f, 0.2f, 0.12f);

        readonly Transform _root;
        readonly Transform _fillPivot;
        readonly Renderer _fill;

        public FloatingHealthBar(string name, float width, float height)
        {
            _root = new GameObject(name).transform;

            var back = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                Vector3.zero, new Vector3(width, height, 0.5f),
                BackColor, emissive: false, keepCollider: false);
            back.name = "Back";
            back.transform.SetParent(_root, false);

            _fillPivot = new GameObject("FillPivot").transform;
            _fillPivot.SetParent(_root, false);
            _fillPivot.localPosition = new Vector3(-width / 2f, 0f, -0.5f);

            var fill = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                Vector3.zero, new Vector3(width - 1f, height - 0.8f, 0.4f),
                FullColor, emissive: true, keepCollider: false);
            fill.name = "Fill";
            fill.transform.SetParent(_fillPivot, false);
            fill.transform.localPosition = new Vector3((width - 1f) / 2f, 0f, 0f);
            _fill = fill.GetComponent<Renderer>();
        }

        public void Set(float fraction)
        {
            if (_fillPivot == null || _fill == null) return;

            float frac = Mathf.Clamp01(fraction);
            Vector3 s = _fillPivot.localScale;
            s.x = frac;
            _fillPivot.localScale = s;

            Color color = Color.Lerp(EmptyColor, FullColor, frac);
            Material mat = _fill.sharedMaterial;
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_EmissionColor", color * 2f);
        }

        public void Place(Vector3 position)
        {
            if (_root != null) _root.position = position;
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
        }
    }
}
