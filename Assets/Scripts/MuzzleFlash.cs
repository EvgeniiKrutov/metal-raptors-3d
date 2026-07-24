using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// A code-built muzzle flash: a brief, bright burst at the gun's muzzle the instant a round
    /// is fired — a hot core plus a few flame spikes fanning forward along the barrel. Spawn with
    /// <see cref="Spawn"/> from both sides' guns: the player's <see cref="PlaneShooter"/> and the
    /// enemy's <see cref="EnemyController"/>. See docs/effects.md.
    ///
    /// Purely cosmetic: the pieces carry no <see cref="Collider"/> and no <see cref="Rigidbody"/>,
    /// so the flash can never brush a plane, soak a bullet, or deal damage. The whole effect lives
    /// only a few frames — it pops to full size, then shrinks and dims to nothing.
    /// </summary>
    public class MuzzleFlash : MonoBehaviour
    {
        const float Life = 0.07f;         // seconds the whole flash lives — a quick pop
        const float CoreDiameter = 0.18f; // core size relative to the plane's body radius
        const float SpikeLength = 0.32f;  // longest spike relative to the body radius
        const float SpikeWidth = 0.05f;   // spike thickness relative to the body radius
        const int SpikeCount = 4;
        const float SpikeSpreadDeg = 28f; // half-angle the spikes fan across, around the barrel
        const float EmissionStrength = 3f;

        // Hot near-white core cooling to a yellow-orange flame.
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

        /// <param name="position">The muzzle — where the round leaves the barrel.</param>
        /// <param name="direction">Firing direction in the play plane; the flash points along it.</param>
        /// <param name="size">The firing plane's body radius; scales the whole flash.</param>
        public static void Spawn(Vector3 position, Vector3 direction, float size)
        {
            var root = new GameObject("MuzzleFlash");
            root.transform.position = position;
            float ang = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            root.transform.rotation = Quaternion.Euler(0f, 0f, ang); // root-local +X = firing direction

            var fx = root.AddComponent<MuzzleFlash>();
            fx._pieces = new Piece[SpikeCount + 1];

            // Central hot core, sitting right at the muzzle.
            float core = size * CoreDiameter * Random.Range(0.85f, 1.15f);
            fx._pieces[0] = BuildPiece(root.transform, PrimitiveType.Sphere,
                Vector3.zero, Quaternion.identity, Vector3.one * core, HotColor);

            // Flame spikes fanning forward along +X (the barrel), each a varied length and angle.
            for (int i = 0; i < SpikeCount; i++)
            {
                float frac = SpikeCount > 1 ? (float)i / (SpikeCount - 1) : 0.5f;
                float spikeAng = Mathf.Lerp(-SpikeSpreadDeg, SpikeSpreadDeg, frac) + Random.Range(-4f, 4f);
                float len = size * SpikeLength * Random.Range(0.55f, 1f);
                float wid = size * SpikeWidth * Random.Range(0.8f, 1.2f);

                var rot = Quaternion.Euler(0f, 0f, spikeAng);
                // A cube pivots at its centre, so push it out along the spike by half its length.
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
                mat = go.GetComponent<Renderer>().sharedMaterial, // unique per piece, see CreatePrimitive3D
                color = color,
            };
        }

        void Update()
        {
            _age += Time.deltaTime;
            float t = _age / Life;
            if (t >= 1f) { Destroy(gameObject); return; }

            // Pop to full instantly, then shrink and dim to nothing over the (very short) life.
            // Scaling the root keeps the whole composition (core + spikes) collapsing together.
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
