using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    /// <summary>
    /// The player plane's night searchlight: a warm spot light mounted on the cowl, shining
    /// straight along the nose, toggled with T. Mounted only on <see cref="Daytime.Night"/>
    /// levels (see <see cref="Mount"/>) and off until the pilot switches it on.
    ///
    /// Two halves make the effect: a real URP spot light that lights (and shadows) whatever the
    /// cone falls on, and a visible shaft — a flat additive wedge in the play plane, cut short
    /// each frame at whatever the beam hits, so it stops on the ground instead of running
    /// through it and dissolves into nothing when it is aimed at open air. Design notes and
    /// tuning: docs/searchlight.md.
    /// </summary>
    public class PlaneSearchlight : MonoBehaviour
    {
        // How far the beam carries and how wide it opens.
        const float Range = 250f;
        const float ConeAngle = 25f;
        const float InnerConeFraction = 0.5f; // where the cone starts feathering toward its rim

        // URP attenuates an additional light by 1/d², so a raw intensity means nothing without a
        // distance attached: this is the lighting wanted head-on at the far end of the beam, and
        // the intensity is derived from it. It is deliberately way past "white": the beam flies
        // level, so it rakes flat ground at a grazing angle where Lambert throws most of it away,
        // and the night grade lifts the whole frame by 2 EV — a pool that merely matches ambient
        // reads as no pool at all.
        const float BrightnessAtRange = 1.6f;

        // A single light cannot carry that: some platforms (Metal) hold a light's colour as a
        // 16-bit half, which overflows to infinity past 65504. So the beam is however many
        // coincident spots the total needs — URP just adds them, and sharing a position, cone
        // and range means their shadows agree exactly.
        const float MaxLightIntensity = 50000f;

        // The shaft in the air is far dimmer than the pool of light it lands in — it is scattered
        // haze, not a solid object.
        const float ShaftAlpha = 0.35f;
        const int ShaftSegments = 16;

        // The shaft's geometric apex sits this far back from the nose, as a fraction of the
        // nose's own distance from the body centre — i.e. inside the fuselage. The cone is
        // therefore already open where it leaves the nose instead of pinching to a point there.
        // The light itself still sits on the nose, so the airframe is never inside its cone.
        const float ApexInsideFraction = 0.75f;

        static readonly Color BeamColor = new Color(1f, 0.88f, 0.62f);

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int ReachId = Shader.PropertyToID("_Reach");
        static readonly int ApexOffsetId = Shader.PropertyToID("_ApexOffset");

        readonly RaycastHit[] _hits = new RaycastHit[8];

        Transform _owner;   // the plane body, so the airframe's own collider never cuts the beam
        Light[] _lights;    // coincident spots that add up to one beam (see MaxLightIntensity)
        Transform _shaft;
        Material _shaftMat;
        Mesh _shaftMesh;
        float _apexPullback; // metres the shaft's apex sits behind the nose
        bool _on;

        /// <summary>Whether the beam is currently lit (false once the plane is gone).</summary>
        public bool IsOn => _on && isActiveAndEnabled;

        /// <summary>
        /// Mounts the searchlight on <paramref name="body"/> (the physics body, whose +X is the
        /// flight heading) at <paramref name="noseLocal"/> — the cowl point from
        /// <see cref="PlaneFactory.NoseLocal"/>. Returns null on any daytime but night, which is
        /// also how the level controllers know not to build the HUD indicator.
        /// </summary>
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
                // A spot light shines down its own +Z, but the nose is the body's +X.
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
            go.transform.localPosition = new Vector3(-_apexPullback, 0f, 0f); // apex inside the plane

            _shaftMesh = BuildWedge();
            go.GetComponent<MeshFilter>().sharedMesh = _shaftMesh;

            _shaftMat = new Material(shader) { name = "Searchlight Beam (runtime)" };
            _shaftMat.SetColor(ColorId, new Color(BeamColor.r, BeamColor.g, BeamColor.b, ShaftAlpha));
            _shaftMat.SetFloat(ReachId, 1f);
            // Everything the shader fades is measured from the nose, not from the buried apex.
            _shaftMat.SetFloat(ApexOffsetId, _apexPullback / Range);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _shaftMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            _shaft = go.transform;
        }

        /// <summary>
        /// The shaft mesh: a unit-length wedge along +X in the play plane, a fan of triangles from
        /// the nose out to a flat tip. UV x runs 0 (nose) to 1 (tip) and UV y runs -1 to 1 across
        /// the cone, which is all the shader needs to fade it lengthwise and toward the rim.
        /// </summary>
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

        void Update()
        {
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

        /// <summary>
        /// Cuts the shaft at whatever the beam lands on and tells the shader how far down the
        /// light's range that tip sits, so the visible beam stops on the surface at the brightness
        /// it had there. Scaling X and Y together keeps the cone's angle while it shortens; the
        /// length measured runs from the buried apex, so the hit still lands at the tip.
        /// </summary>
        void UpdateShaft()
        {
            if (_shaft == null) return;

            float shaftLength = _apexPullback + MeasureReach();
            _shaft.localScale = new Vector3(shaftLength, shaftLength, 1f);
            if (_shaftMat != null) _shaftMat.SetFloat(ReachId, shaftLength / Range);
        }

        /// <summary>Distance from the nose to the first thing the beam hits, or the full range
        /// when it is pointed at open air. The plane's own airframe and the rounds flying down
        /// the same line are skipped — both sit inside the beam by construction.</summary>
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
