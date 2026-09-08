using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class SquadTracers : MonoBehaviour
    {
        const int MaxActive = 64;
        const float Speed = 300f;
        const float Length = 11f;
        const float Width = 0.9f;
        const float Emission = 2.6f;
        const float LifeMax = 2f;

        static readonly Color Round = new Color(1f, 0.83f, 0.46f);

        static Material _material;

        class Shot
        {
            public Transform tr;
            public Vector3 velocity;
            public float life;
        }

        readonly List<Shot> _active = new List<Shot>();
        readonly Stack<Shot> _idle = new Stack<Shot>();

        public static SquadTracers Begin(Transform parent)
        {
            var go = new GameObject("Squad Tracers");
            go.transform.SetParent(parent, false);
            return go.AddComponent<SquadTracers>();
        }

        public void Fire(Vector3 from, Vector3 to)
        {
            if (_active.Count >= MaxActive) return;

            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 1f) return;

            Shot shot = _idle.Count > 0 ? _idle.Pop() : Create();
            if (shot == null) return;

            shot.velocity = delta / distance * Speed;
            shot.life = Mathf.Min(distance / Speed, LifeMax);
            shot.tr.SetPositionAndRotation(from, Quaternion.LookRotation(delta));
            shot.tr.gameObject.SetActive(true);
            _active.Add(shot);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Shot shot = _active[i];
                shot.life -= dt;

                if (shot.life > 0f)
                {
                    shot.tr.position += shot.velocity * dt;
                    continue;
                }

                shot.tr.gameObject.SetActive(false);
                _active.RemoveAt(i);
                _idle.Push(shot);
            }
        }

        Shot Create()
        {
            Material material = RoundMaterial();
            if (material == null) return null;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Tracer";

            var col = go.GetComponent<Collider>();
            if (col != null) { col.enabled = false; Destroy(col); }

            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(Width, Width, Length);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            go.SetActive(false);
            return new Shot { tr = go.transform };
        }

        static Material RoundMaterial()
        {
            if (_material != null) return _material;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;

            _material = new Material(shader) { name = "Tracer" };
            _material.SetColor("_BaseColor", Round);
            _material.SetFloat("_Smoothness", 0f);
            _material.EnableKeyword("_EMISSION");
            _material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            _material.SetColor("_EmissionColor", Round * Emission);
            return _material;
        }
    }
}
