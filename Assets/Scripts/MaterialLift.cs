using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public static class MaterialLift
    {
        const float LiftGamma = 1f / 2.2f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        static readonly Dictionary<(Material, float), Material> Lifted =
            new Dictionary<(Material, float), Material>();

        public static void Apply(Transform view, float strength = 1f)
        {
            if (view == null) return;

            strength = Mathf.Clamp01(strength);
            if (strength <= 0f) return;

            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = LiftAll(renderer.sharedMaterials, strength);
        }

        static Material[] LiftAll(Material[] sources, float strength)
        {
            for (int i = 0; i < sources.Length; i++) sources[i] = LiftedMaterial(sources[i], strength);
            return sources;
        }

        static Material LiftedMaterial(Material source, float strength)
        {
            if (source == null) return null;
            if (Lifted.TryGetValue((source, strength), out Material cached) && cached != null)
                return cached;

            var copy = new Material(source) { name = source.name + " (lifted)" };

            if (copy.HasProperty(BaseColorId))
                copy.SetColor(BaseColorId, Lift(copy.GetColor(BaseColorId), strength));
            else if (copy.HasProperty(ColorId))
                copy.SetColor(ColorId, Lift(copy.GetColor(ColorId), strength));

            Lifted[(source, strength)] = copy;
            return copy;
        }

        static Color Lift(Color linear, float strength)
        {
            Color gamma = linear.gamma;
            Color lifted = gamma;
            lifted.r = Mathf.Pow(Mathf.Clamp01(gamma.r), LiftGamma);
            lifted.g = Mathf.Pow(Mathf.Clamp01(gamma.g), LiftGamma);
            lifted.b = Mathf.Pow(Mathf.Clamp01(gamma.b), LiftGamma);

            Color raised = Color.Lerp(gamma, lifted, strength).linear;
            raised.a = linear.a;
            return raised;
        }
    }
}
