using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// A code-built explosion: an emissive orange flash sphere that balloons and vanishes, a
    /// handful of ballistic debris cubes, and one of the sibling repo's explosion_N.wav clips.
    /// Spawn with <see cref="Spawn"/>; the component itself just animates the flash. Debris has
    /// no colliders, so wreckage can never crash the player or soak a bullet.
    /// </summary>
    public class Explosion : MonoBehaviour
    {
        const float FlashLife = 0.45f;   // seconds the flash sphere lives
        const int DebrisCount = 8;
        const float DebrisLife = 2.2f;
        const float SoundVolume = 0.55f; // 2D playback; 3D rolloff would mute it at ~420 m

        float _age;
        float _startScale, _endScale;

        /// <param name="size">Rough size of the thing exploding; scales the whole effect.</param>
        public static void Spawn(Vector3 position, float size)
        {
            // Flash: an emissive sphere that grows from half the body size to ~2.2x and pops.
            var flash = UIFactory.CreatePrimitive3D(PrimitiveType.Sphere,
                position, Vector3.one * size * 0.5f,
                new Color(1f, 0.55f, 0.12f), emissive: true, keepCollider: false);
            flash.name = "Explosion";
            var fx = flash.AddComponent<Explosion>();
            fx._startScale = size * 0.5f;
            fx._endScale = size * 2.2f;

            SpawnDebris(position, size);
            PlaySound(position);
        }

        static void SpawnDebris(Vector3 position, float size)
        {
            for (int i = 0; i < DebrisCount; i++)
            {
                var chunk = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                    position + Random.insideUnitSphere * size * 0.25f,
                    Vector3.one * size * Random.Range(0.08f, 0.16f),
                    new Color(0.15f, 0.13f, 0.11f), emissive: false, keepCollider: false);
                chunk.name = "Debris";

                var rb = chunk.AddComponent<Rigidbody>();
                rb.linearVelocity = Random.onUnitSphere * Random.Range(0.6f, 1.6f) * size
                                    + Vector3.up * size * 0.8f;
                rb.angularVelocity = Random.onUnitSphere * 8f;

                Destroy(chunk, DebrisLife);
            }
        }

        static void PlaySound(Vector3 position)
        {
            var clip = Resources.Load<AudioClip>($"Sounds/explosion_{Random.Range(1, 4)}");
            if (clip == null) return;

            // Own carrier GameObject so the sound outlives the short flash.
            var go = new GameObject("ExplosionSound");
            go.transform.position = position;
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            audio.PlayOneShot(clip, SoundVolume);
            Destroy(go, clip.length + 0.1f);
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= FlashLife)
            {
                Destroy(gameObject);
                return;
            }
            // Ease-out growth: fast bloom that slows as it fades from view.
            float t = Mathf.Sqrt(_age / FlashLife);
            transform.localScale = Vector3.one * Mathf.Lerp(_startScale, _endScale, t);
        }
    }
}
