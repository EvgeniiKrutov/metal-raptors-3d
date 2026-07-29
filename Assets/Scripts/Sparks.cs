using UnityEngine;

namespace MetalRaptors
{
    public class Sparks : MonoBehaviour
    {
        const int SparkCount = 14;
        const float LifeMin = 0.25f;
        const float LifeMax = 0.5f;
        const float SpeedFactor = 3.0f;
        const float Drag = 2.5f;
        const float SizeFactor = 0.06f;

        static readonly Color HotColor = new Color(1f, 0.85f, 0.35f);
        static readonly Color CoolColor = new Color(0.5f, 0.18f, 0.05f);

        Vector3 _velocity;
        Material _mat;
        float _age;
        float _life;
        float _startScale;

        public static void Spawn(Vector3 position, float size)
        {
            for (int i = 0; i < SparkCount; i++)
            {
                float mote = size * SizeFactor * Random.Range(0.6f, 1.2f);
                var go = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                    position, Vector3.one * mote,
                    HotColor, emissive: true, keepCollider: false);
                go.name = "Spark";

                var spark = go.AddComponent<Sparks>();
                Vector2 dir = Random.insideUnitCircle.normalized;
                spark._velocity = new Vector3(dir.x, dir.y, 0f)
                                  * size * SpeedFactor * Random.Range(0.4f, 1f);
                spark._life = Random.Range(LifeMin, LifeMax);
                spark._startScale = mote;
                spark._mat = go.GetComponent<Renderer>().sharedMaterial;
            }
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _life)
            {
                Destroy(gameObject);
                return;
            }

            float t = _age / _life;

            transform.position += _velocity * Time.deltaTime;
            _velocity *= Mathf.Max(0f, 1f - Drag * Time.deltaTime);
            transform.localScale = Vector3.one * Mathf.Lerp(_startScale, 0f, t);

            if (_mat != null)
            {
                var c = Color.Lerp(HotColor, CoolColor, t);
                _mat.SetColor("_BaseColor", c);
                _mat.SetColor("_EmissionColor", c * 2f);
            }
        }
    }
}
