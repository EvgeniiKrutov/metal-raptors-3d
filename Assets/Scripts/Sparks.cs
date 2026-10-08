using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class Sparks : MonoBehaviour
    {
        const int Capacity = 1024;

        const int BurstCount = 14;
        const float BurstSpeed = 3f;

        const int HitCount = 9;
        const float HitSize = 30f;
        const float HitSpeed = 4.5f;
        const float ConeDeg = 60f;

        const float LifeMin = 0.18f;
        const float LifeMax = 0.4f;
        const float WidthFactor = 0.05f;
        const float DepthSpread = 0.25f;
        const float DepthNudge = 0.5f;
        const float Drag = 3f;
        const float Gravity = 160f;
        const float MaxSpeed = 100000f;
        const float EndWidth = 0.35f;
        const float StretchVelocity = 0.06f;
        const float StretchLength = 2f;

        const float NightFlashIntensity = 0.8f;
        const float NightFlashDuration = 0.06f;
        const float NightFlashPerSize = 0.5f;
        const float NightFlashMin = 10f;
        const float NightFlashGap = 0.03f;
        const int NightFlashPriority = 5;

        static readonly Color HotColor = new Color(1f, 0.93f, 0.62f);
        static readonly Color MidColor = new Color(1f, 0.62f, 0.26f);
        static readonly Color CoolColor = new Color(0.85f, 0.28f, 0.08f);
        static readonly Color NightFlashColor = new Color(1f, 0.72f, 0.38f);

        static readonly int TintId = Shader.PropertyToID("_Tint");

        static Sparks _instance;
        static Material _material;
        static float _lastFlash = float.NegativeInfinity;

        ParticleSystem _system;

        public static void Spawn(Vector3 position, float size)
        {
            var sparks = Emitter();
            if (sparks == null) return;

            Vector3 origin = TowardCamera(position, size);
            for (int i = 0; i < BurstCount; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                sparks.Emit(origin, dir, size, size * BurstSpeed * Random.Range(0.4f, 1f));
            }
            Flash(position, size);
        }

        public static void Hit(Vector3 point, Vector3 travel)
        {
            var sparks = Emitter();
            if (sparks == null) return;

            Vector2 back = -(Vector2)travel;
            if (back.sqrMagnitude < 1e-6f) back = Random.insideUnitCircle;
            float heading = Mathf.Atan2(back.y, back.x);

            Vector3 origin = TowardCamera(point, HitSize);
            for (int i = 0; i < HitCount; i++)
            {
                float ang = heading + Random.Range(-ConeDeg, ConeDeg) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                sparks.Emit(origin, dir, HitSize, HitSize * HitSpeed * Random.Range(0.45f, 1f));
            }
            Flash(point, HitSize);
        }

        void Emit(Vector3 position, Vector2 dir, float size, float speed)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = new Vector3(dir.x, dir.y, Random.Range(-DepthSpread, DepthSpread)) * speed,
                startLifetime = Random.Range(LifeMin, LifeMax),
                startSize = size * WidthFactor * Random.Range(0.7f, 1.2f),
                startColor = HotColor,
            };
            _system.Emit(p, 1);
        }

        static Vector3 TowardCamera(Vector3 position, float size)
        {
            var cam = Camera.main;
            if (cam == null) return position;

            Vector3 to = cam.transform.position - position;
            float dist = to.magnitude;
            if (dist < 1e-3f) return position;
            return position + to / dist * Mathf.Min(size * DepthNudge, dist * 0.5f);
        }

        static void Flash(Vector3 position, float size)
        {
            if (Time.time - _lastFlash < NightFlashGap) return;
            _lastFlash = Time.time;

            NightLights.Flash(position, NightFlashColor, NightFlashIntensity,
                Mathf.Max(NightFlashMin, size * NightFlashPerSize), NightFlashDuration, NightFlashPriority);
        }

        static Sparks Emitter()
        {
            var material = SparkMaterial();
            if (material == null) return null;
            material.SetColor(TintId, Firelight.Warm(Color.white));

            if (_instance != null) return _instance;

            var go = new GameObject("Sparks");
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Capacity;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;

            var emission = system.emission;
            emission.enabled = false;

            var shape = system.shape;
            shape.enabled = false;

            var force = system.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.x = 0f;
            force.y = -Gravity;
            force.z = 0f;

            var limit = system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = MaxSpeed;
            limit.drag = Drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(MidColor, 0.45f),
                    new GradientColorKey(CoolColor, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.55f),
                    new GradientAlphaKey(0f, 1f),
                });
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var width = system.sizeOverLifetime;
            width.enabled = true;
            width.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, EndWidth));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = StretchVelocity;
            renderer.lengthScale = StretchLength;
            renderer.cameraVelocityScale = 0f;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            _instance = go.AddComponent<Sparks>();
            _instance._system = system;
            system.Play();
            return _instance;
        }

        static Material SparkMaterial()
        {
            if (_material != null) return _material;

            var shader = Shader.Find("Hidden/HitSpark");
            if (shader == null) return null;

            _material = new Material(shader) { name = "Hit Spark (runtime)" };
            return _material;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
