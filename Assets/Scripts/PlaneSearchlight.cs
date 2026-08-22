using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class PlaneSearchlight : MonoBehaviour
    {
        const float Range = 250f;
        const float ConeAngle = 25f;
        const float InnerConeFraction = 0.5f;

        const float BrightnessAtRange = 1.6f;

        const float MaxLightIntensity = 50000f;

        const float ShaftAlpha = 0.35f;
        const int ShaftSegments = 16;

        const float ApexInsideFraction = 0.75f;

        static readonly Color BeamColor = new Color(1f, 0.88f, 0.62f);

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int ReachId = Shader.PropertyToID("_Reach");
        static readonly int ApexOffsetId = Shader.PropertyToID("_ApexOffset");

        readonly RaycastHit[] _hits = new RaycastHit[8];

        Transform _owner;
        Light[] _lights;
        Transform _shaft;
        Material _shaftMat;
        Mesh _shaftMesh;
        float _apexPullback;
        bool _on;

        public bool IsOn => _on && isActiveAndEnabled;

        public static PlaneSearchlight Mount(GameObject body, Vector3 noseLocal, Daytime daytime)
        {
            if (daytime != Daytime.Night) return null;

            var go = new GameObject("Searchlight");
            go.transform.SetParent(body.transform, false);
            go.transform.localPosition = noseLocal;

            var searchlight = go.AddComponent<PlaneSearchlight>();
            searchlight._owner = body.transform;
            searchlight._apexPullback = Mathf.Max(0f, noseLocal.x) * ApexInsideFraction;
            searchlight.Build();
            searchlight.SetOn(false);
            return searchlight;
        }

        void Build()
        {
            float total = BrightnessAtRange * Range * Range;
            int count = Mathf.Max(1, Mathf.CeilToInt(total / MaxLightIntensity));
            _lights = new Light[count];

            for (int i = 0; i < count; i++)
            {
                var lightGo = new GameObject(count > 1 ? $"Beam Light {i + 1}" : "Beam Light");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = BeamColor;
                light.range = Range;
                light.spotAngle = ConeAngle;
                light.innerSpotAngle = ConeAngle * InnerConeFraction;
                light.intensity = total / count;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.85f;
                light.shadowNormalBias = 0.5f;
                light.renderMode = LightRenderMode.ForcePixel;
                _lights[i] = light;
            }

            BuildShaft();
        }

        void BuildShaft()
        {
            var shader = Shader.Find("Custom/SearchlightBeam");
            if (shader == null)
            {
                Debug.LogWarning("PlaneSearchlight: Custom/SearchlightBeam not found; " +
                                 "the beam will light surfaces but show no shaft.");
                return;
            }

            var go = new GameObject("Beam Shaft", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(-_apexPullback, 0f, 0f);

            _shaftMesh = BuildWedge();
            go.GetComponent<MeshFilter>().sharedMesh = _shaftMesh;

            _shaftMat = new Material(shader) { name = "Searchlight Beam (runtime)" };
            _shaftMat.SetColor(ColorId, new Color(BeamColor.r, BeamColor.g, BeamColor.b, ShaftAlpha));
            _shaftMat.SetFloat(ReachId, 1f);
            _shaftMat.SetFloat(ApexOffsetId, _apexPullback / Range);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _shaftMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            _shaft = go.transform;
        }

        static Mesh BuildWedge()
        {
            float halfWidth = Mathf.Tan(ConeAngle * 0.5f * Mathf.Deg2Rad);

            var verts = new Vector3[ShaftSegments + 2];
            var uvs = new Vector2[ShaftSegments + 2];
            var tris = new int[ShaftSegments * 3];

            verts[0] = Vector3.zero;
            uvs[0] = Vector2.zero;

            for (int i = 0; i <= ShaftSegments; i++)
            {
                float lateral = Mathf.Lerp(-1f, 1f, (float)i / ShaftSegments);
                verts[i + 1] = new Vector3(1f, halfWidth * lateral, 0f);
                uvs[i + 1] = new Vector2(1f, lateral);
            }

            for (int i = 0; i < ShaftSegments; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }

            var mesh = new Mesh { name = "Searchlight Beam" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        public void Toggle()
        {
            if (GameMenu.IsOpen || LevelBriefing.IsOpen) return;
            SetOn(!_on);
        }

        void Update()
        {
            if (GameMenu.IsOpen || LevelBriefing.IsOpen) return;

            var kb = Keyboard.current;
            if (kb != null && kb.tKey.wasPressedThisFrame) SetOn(!_on);
            if (_on) UpdateShaft();
        }

        void SetOn(bool on)
        {
            _on = on;
            if (_lights != null)
                foreach (var light in _lights)
                    if (light != null) light.enabled = on;
            if (_shaft != null) _shaft.gameObject.SetActive(on);
            if (on) UpdateShaft();
        }

        void UpdateShaft()
        {
            if (_shaft == null) return;

            float shaftLength = _apexPullback + MeasureReach();
            _shaft.localScale = new Vector3(shaftLength, shaftLength, 1f);
            if (_shaftMat != null) _shaftMat.SetFloat(ReachId, shaftLength / Range);
        }

        float MeasureReach()
        {
            int count = Physics.RaycastNonAlloc(transform.position, transform.right, _hits,
                Range, ~0, QueryTriggerInteraction.Ignore);

            float nearest = Range;
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.collider == null || hit.distance >= nearest) continue;
                if (_owner != null && hit.transform.IsChildOf(_owner)) continue;
                if (hit.collider.GetComponentInParent<Bullet>() != null) continue;
                nearest = hit.distance;
            }
            return Mathf.Max(1f, nearest);
        }

        void OnDestroy()
        {
            if (_shaftMat != null) Destroy(_shaftMat);
            if (_shaftMesh != null) Destroy(_shaftMesh);
        }
    }
}
