using UnityEngine;

namespace MetalRaptors
{
    public class MuzzleFlash : MonoBehaviour
    {
        const float Life = 0.07f;
        const float CoreDiameter = 0.18f;
        const float SpikeLength = 0.32f;
        const float SpikeWidth = 0.05f;
        const int SpikeCount = 4;
        const float SpikeSpreadDeg = 28f;
        const float EmissionStrength = 3f;

        static readonly Color HotColor = new Color(1f, 0.96f, 0.75f);
        static readonly Color FlameColor = new Color(1f, 0.7f, 0.25f);

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        struct Piece
        {
            public Material mat;
            public Color color;
        }

        Piece[] _pieces;
        float _age;

        public static void Spawn(Vector3 position, Vector3 direction, float size)
        {
            var root = new GameObject("MuzzleFlash");
            root.transform.position = position;
            float ang = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            root.transform.rotation = Quaternion.Euler(0f, 0f, ang);

            var fx = root.AddComponent<MuzzleFlash>();
            fx._pieces = new Piece[SpikeCount + 1];

            float core = size * CoreDiameter * Random.Range(0.85f, 1.15f);
            fx._pieces[0] = BuildPiece(root.transform, PrimitiveType.Sphere,
                Vector3.zero, Quaternion.identity, Vector3.one * core, HotColor);

            for (int i = 0; i < SpikeCount; i++)
            {
                float frac = SpikeCount > 1 ? (float)i / (SpikeCount - 1) : 0.5f;
                float spikeAng = Mathf.Lerp(-SpikeSpreadDeg, SpikeSpreadDeg, frac) + Random.Range(-4f, 4f);
                float len = size * SpikeLength * Random.Range(0.55f, 1f);
                float wid = size * SpikeWidth * Random.Range(0.8f, 1.2f);

                var rot = Quaternion.Euler(0f, 0f, spikeAng);
                var pos = rot * new Vector3(len * 0.5f, 0f, 0f);
                fx._pieces[i + 1] = BuildPiece(root.transform, PrimitiveType.Cube,
                    pos, rot, new Vector3(len, wid, wid), FlameColor);
            }
        }

        static Piece BuildPiece(Transform parent, PrimitiveType type, Vector3 localPos,
            Quaternion localRot, Vector3 scale, Color color)
        {
            var go = UIFactory.CreatePrimitive3D(type, Vector3.zero, scale, color,
                emissive: true, keepCollider: false);
            go.name = "Flash";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            return new Piece
            {
                mat = go.GetComponent<Renderer>().sharedMaterial,
                color = color,
            };
        }

        void Update()
        {
            _age += Time.deltaTime;
            float t = _age / Life;
            if (t >= 1f) { Destroy(gameObject); return; }

            transform.localScale = Vector3.one * (1f - t * t);
            float emission = EmissionStrength * (1f - t);
            for (int i = 0; i < _pieces.Length; i++)
                if (_pieces[i].mat != null)
                    _pieces[i].mat.SetColor(EmissionColorId, _pieces[i].color * emission);
        }

        void OnDestroy()
        {
            if (_pieces == null) return;
            foreach (var p in _pieces)
                if (p.mat != null) Destroy(p.mat);
        }
    }
}
