using UnityEngine;

namespace MetalRaptors
{
    public class Tracer : MonoBehaviour
    {
        const float Life = 1.3f;
        const float Thickness = 3.2f;
        const float Length = 5.2f;
        const float Emission = 1.1f;

        static readonly Color Round = new Color(0.96f, 0.76f, 0.38f);

        static GameObject _template;

        Vector3 _velocity;
        float _age;

        public static void Spawn(Vector3 position, Vector3 direction, float speed)
        {
            GameObject template = Template();
            if (template == null) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var go = Instantiate(template, position, Quaternion.Euler(0f, 0f, angle - 90f));
            go.name = "Tracer";
            go.SetActive(true);
            go.GetComponent<Tracer>()._velocity = direction.normalized * speed;
        }

        static GameObject Template()
        {
            if (_template != null) return _template;

            _template = UIFactory.CreatePrimitive3D(PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(Thickness, Length, Thickness), Round,
                emissive: true, keepCollider: false);

            var rend = _template.GetComponent<Renderer>();
            if (rend != null && rend.sharedMaterial != null)
            {
                var mat = rend.sharedMaterial;
                mat.SetFloat("_Metallic", 0.9f);
                mat.SetFloat("_Smoothness", 0.55f);
                mat.SetColor("_EmissionColor", Round * Emission);
            }

            _template.AddComponent<Tracer>();
            _template.SetActive(false);
            _template.name = "TracerTemplate";
            return _template;
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age > Life) { Destroy(gameObject); return; }

            transform.position += _velocity * Time.deltaTime;
        }
    }
}
