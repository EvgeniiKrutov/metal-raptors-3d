using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class WingStreaks : MonoBehaviour
    {
        const float Width = 2.1f;
        const float Life = 0.55f;
        const float Alpha = 0.8f;
        const float MinStep = 1f;
        const float SweepBack = 0.12f;

        const float HoldPoint = 0.6f;
        const float HoldWidth = 0.8f;

        TrailRenderer[] _trails;
        Material _material;

        public static WingStreaks Mount(GameObject body, Transform model, Color? tint = null)
        {
            PlaneFactory.WingTipsLocal(body, model, out Vector3 near, out Vector3 far);

            var go = new GameObject("Wing Streaks");
            go.transform.SetParent(body.transform, false);

            var trails = go.AddComponent<WingStreaks>();
            trails._material = BuildMaterial(tint ?? Color.white);
            trails._trails = new[]
            {
                trails.Streak(near, "Wingtip Near"),
                trails.Streak(far, "Wingtip Far"),
            };
            return trails;
        }

        public void SetEmitting(bool on)
        {
            if (_trails == null) return;

            foreach (var trail in _trails)
                if (trail != null) trail.emitting = on;
        }

        TrailRenderer Streak(Vector3 tipLocal, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = tipLocal - new Vector3(Mathf.Abs(tipLocal.z) * SweepBack, 0f, 0f);

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = Life;
            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(HoldPoint, HoldWidth),
                new Keyframe(1f, 0f));
            trail.widthMultiplier = Width;
            trail.minVertexDistance = MinStep;
            trail.autodestruct = false;
            trail.emitting = false;
            trail.alignment = LineAlignment.View;
            trail.numCapVertices = 0;
            trail.sharedMaterial = _material;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.lightProbeUsage = LightProbeUsage.Off;
            trail.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return trail;
        }

        static Material BuildMaterial(Color tint)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

            var mat = new Material(shader) { name = "Wing Streak (runtime)" };
            mat.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, Alpha));
            UIFactory.MakeTransparent(mat);
            return mat;
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
