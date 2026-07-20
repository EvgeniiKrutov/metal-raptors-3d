using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// A code-built shower of sparks: a handful of small emissive-yellow motes that spray out
    /// from a point, fade from bright to dark, and vanish. Spawn with <see cref="Spawn"/> at the
    /// plane's position when a scrape shakes it (see <see cref="CubeController.Scrape"/>), to sell
    /// the metal-on-metal contact.
    ///
    /// Purely cosmetic: the motes carry no <see cref="Collider"/> and no <see cref="Rigidbody"/>,
    /// so they can never brush the player, soak a bullet, or deal damage — they just drift on their
    /// spawn velocity and fade. Each mote animates itself and self-destructs when its short life ends.
    /// </summary>
    public class Sparks : MonoBehaviour
    {
        const int SparkCount = 14;
        const float LifeMin = 0.25f;      // seconds a mote lives (randomised per mote)
        const float LifeMax = 0.5f;
        const float SpeedFactor = 3.0f;   // spray speed relative to the effect size
        const float Drag = 2.5f;          // how quickly a mote slows as it flies out
        const float SizeFactor = 0.06f;   // mote size relative to the effect size

        // Bright spark yellow-orange, cooling to a dim ember as it dies.
        static readonly Color HotColor = new Color(1f, 0.85f, 0.35f);
        static readonly Color CoolColor = new Color(0.5f, 0.18f, 0.05f);

        Vector3 _velocity;
        Material _mat;
        float _age;
        float _life;
        float _startScale;

        /// <param name="position">Where the sparks originate — the plane's position on a scrape.</param>
        /// <param name="size">Rough size of the thing scraping; scales the whole effect.</param>
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
                // Spray outward in the play plane (Z stays flat, like everything else in the level).
                Vector2 dir = Random.insideUnitCircle.normalized;
                spark._velocity = new Vector3(dir.x, dir.y, 0f)
                                  * size * SpeedFactor * Random.Range(0.4f, 1f);
                spark._life = Random.Range(LifeMin, LifeMax);
                spark._startScale = mote;
                spark._mat = go.GetComponent<Renderer>().sharedMaterial; // unique per spark, see CreatePrimitive3D
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

            float t = _age / _life; // 0 -> 1 over the spark's life

            // Drift outward, slowing as it goes, and shrink to nothing.
            transform.position += _velocity * Time.deltaTime;
            _velocity *= Mathf.Max(0f, 1f - Drag * Time.deltaTime);
            transform.localScale = Vector3.one * Mathf.Lerp(_startScale, 0f, t);

            // Cool from hot yellow to a dim ember as it fades.
            if (_mat != null)
            {
                var c = Color.Lerp(HotColor, CoolColor, t);
                _mat.SetColor("_BaseColor", c);
                _mat.SetColor("_EmissionColor", c * 2f);
            }
        }
    }
}
