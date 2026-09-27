using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public static class MaterialLift
    {
        const float LiftGamma = 1f / 2.2f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        static readonly Dictionary<Material, Material> Lifted =
            new Dictionary<Material, Material>();

        public static void Apply(Transform view)
        {
            if (view == null) return;

            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = LiftAll(renderer.sharedMaterials);
        }

        static Material[] LiftAll(Material[] sources)
        {
            for (int i = 0; i < sources.Length; i++) sources[i] = LiftedMaterial(sources[i]);
            return sources;
        }

        static Material LiftedMaterial(Material source)
        {
            if (source == null) return null;
            if (Lifted.TryGetValue(source, out Material cached) && cached != null) return cached;

            var copy = new Material(source) { name = source.name + " (lifted)" };

            if (copy.HasProperty(BaseColorId))
                copy.SetColor(BaseColorId, Lift(copy.GetColor(BaseColorId)));
            else if (copy.HasProperty(ColorId))
                copy.SetColor(ColorId, Lift(copy.GetColor(ColorId)));

            Lifted[source] = copy;
            return copy;
        }

        static Color Lift(Color linear)
        {
            Color gamma = linear.gamma;
            gamma.r = Mathf.Pow(Mathf.Clamp01(gamma.r), LiftGamma);
            gamma.g = Mathf.Pow(Mathf.Clamp01(gamma.g), LiftGamma);
            gamma.b = Mathf.Pow(Mathf.Clamp01(gamma.b), LiftGamma);

            Color raised = gamma.linear;
            raised.a = linear.a;
            return raised;
        }
    }
}
