using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class HealFlash : MonoBehaviour
    {
        public const float Seconds = 0.9f;

        const int Pulses = 3;
        const float TintStrength = 0.8f;
        const float GlowStrength = 1.6f;

        static readonly Color Tint = new Color(0.30f, 1f, 0.42f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        readonly List<Renderer> _renderers = new List<Renderer>();
        readonly List<Color> _baseColors = new List<Color>();
        MaterialPropertyBlock _block;
        float _age;

        public static void Play(Transform model)
        {
            if (model == null) return;

            var flash = model.GetComponent<HealFlash>();
            if (flash == null) flash = model.gameObject.AddComponent<HealFlash>();
            flash.Restart();
        }

        void Restart()
        {
            if (_block == null) _block = new MaterialPropertyBlock();

            _renderers.Clear();
            _baseColors.Clear();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;

                _renderers.Add(renderer);
                Material source = renderer.sharedMaterial;
                _baseColors.Add(source != null && source.HasProperty(BaseColorId)
                    ? source.GetColor(BaseColorId)
                    : Color.white);
            }

            _age = 0f;
            enabled = true;
        }

        void Update()
        {
            _age += Time.deltaTime;

            float t = Mathf.Clamp01(_age / Seconds);
            bool done = t >= 1f;
            float wave = done ? 0f
                : Mathf.Abs(Mathf.Sin(t * Pulses * Mathf.PI)) * (1f - t);

            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId,
                    Color.Lerp(_baseColors[i], Tint, wave * TintStrength));
                _block.SetColor(EmissionColorId, Tint * (wave * GlowStrength));
                renderer.SetPropertyBlock(_block);
            }

            if (done) enabled = false;
        }
    }
}
