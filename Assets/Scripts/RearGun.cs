using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class RearGun : MonoBehaviour
    {
        public const float ShotPitch = 0.8409f;

        const float MinElevation = -80f;
        const float MaxElevation = 80f;
        const float StartElevation = 8f;
        const float AimRate = 80f;
        const float AimResponse = 12f;

        const float PivotFromBreech = 0.21f;
        const float PivotHeight = 0.54f;
        const float MuzzleHeight = 0.84f;
        const float MuzzleClearance = 3f;
        const float FallbackBack = 0.3f;

        const float LineStart = 6f;
        const float LineLength = 300f;
        const float LineWidth = 1.6f;
        const float LineDepth = 6f;
        const int LineDashes = 14;
        const float LineDashFill = 0.55f;
        const int LineTextureWidth = 256;
        static readonly Color LineColor = new Color(1f, 0.97f, 0.88f, 0.4f);

        Transform _pivot;
        Quaternion _rest;
        Vector3 _muzzleLocal;
        float _restElevation;
        float _elevation = StartElevation;
        float _rate;
        float _input;

        LineRenderer _line;
        Material _lineMaterial;
        Texture2D _lineTexture;

        public bool Ready { get; set; }

        public Vector3 Muzzle
        {
            get
            {
                Vector3 point = _pivot.TransformPoint(_muzzleLocal);
                point.z = transform.position.z;
                return point;
            }
        }

        public Vector3 Direction
        {
            get
            {
                Vector3 nose = transform.right;
                float angle = Mathf.Atan2(nose.y, nose.x) + Mathf.PI - _elevation * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            }
        }

        public static RearGun Mount(GameObject body, Transform model, PlaneModelConfig plane)
        {
            var gun = body.AddComponent<RearGun>();
            Transform node = PlaneFactory.FindDeep(model, plane.rearGunNode);

            if (node != null && Measure(body.transform, node, out Bounds bounds))
                gun.Hinge(body.transform, model, node, bounds);
            else
                gun.Hinge(body.transform, model, plane.LengthUnits * PlaneModelConfig.LevelScale);

            gun.BuildLine();
            gun.Apply();
            return gun;
        }

        public void SetAimInput(float input) => _input = Mathf.Clamp(input, -1f, 1f);

        void Hinge(Transform body, Transform model, Transform node, Bounds bounds)
        {
            float length = bounds.size.x;
            float height = bounds.size.y;

            var pivot = new Vector3(bounds.max.x - length * PivotFromBreech,
                bounds.min.y + height * PivotHeight, bounds.center.z);
            var muzzle = new Vector3(bounds.min.x - MuzzleClearance,
                bounds.min.y + height * MuzzleHeight, bounds.center.z);

            PlacePivot(body, model, pivot, muzzle);
            node.SetParent(_pivot, true);
        }

        void Hinge(Transform body, Transform model, float planeLength)
        {
            var pivot = Vector3.zero;
            var muzzle = new Vector3(-planeLength * FallbackBack, 0f, 0f);
            PlacePivot(body, model, pivot, muzzle);
        }

        void PlacePivot(Transform body, Transform model, Vector3 pivot, Vector3 muzzle)
        {
            _pivot = new GameObject("Rear Gun Pivot").transform;
            _pivot.SetParent(model, false);
            _pivot.position = body.TransformPoint(pivot);
            _pivot.rotation = body.rotation;
            _rest = _pivot.localRotation;

            _muzzleLocal = _pivot.InverseTransformPoint(body.TransformPoint(muzzle));
            _restElevation = Mathf.Atan2(muzzle.y - pivot.y, pivot.x - muzzle.x) * Mathf.Rad2Deg;
        }

        static bool Measure(Transform body, Transform node, out Bounds bounds)
        {
            bounds = default;

            var filter = node.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return false;

            Bounds local = filter.sharedMesh.bounds;
            bool first = true;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? local.min.x : local.max.x,
                    (corner & 2) == 0 ? local.min.y : local.max.y,
                    (corner & 4) == 0 ? local.min.z : local.max.z);
                Vector3 inBody = body.InverseTransformPoint(node.TransformPoint(point));

                if (first) bounds = new Bounds(inBody, Vector3.zero);
                else bounds.Encapsulate(inBody);
                first = false;
            }
            return bounds.size.x > 0.0001f;
        }

        void BuildLine()
        {
            var go = new GameObject("Rear Gun Aim");
            go.transform.SetParent(transform, false);

            _lineTexture = DashTexture();
            _lineMaterial = LineMaterial(_lineTexture);

            _line = go.AddComponent<LineRenderer>();
            _line.positionCount = 2;
            _line.useWorldSpace = true;
            _line.widthMultiplier = LineWidth;
            _line.textureMode = LineTextureMode.Stretch;
            _line.alignment = LineAlignment.View;
            _line.numCapVertices = 0;
            _line.sharedMaterial = _lineMaterial;
            _line.shadowCastingMode = ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.lightProbeUsage = LightProbeUsage.Off;
            _line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _line.enabled = false;
        }

        static Texture2D DashTexture()
        {
            var texture = new Texture2D(LineTextureWidth, 1, TextureFormat.RGBA32, false)
            {
                name = "Rear Gun Aim Dashes",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            for (int x = 0; x < LineTextureWidth; x++)
            {
                float u = (float)x / (LineTextureWidth - 1);
                bool dash = Mathf.Repeat(u * LineDashes, 1f) < LineDashFill;
                texture.SetPixel(x, 0, new Color(1f, 1f, 1f, dash ? 1f - u : 0f));
            }
            texture.Apply();
            return texture;
        }

        static Material LineMaterial(Texture2D texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

            var mat = new Material(shader) { name = "Rear Gun Aim (runtime)" };
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", LineColor);
            UIFactory.MakeTransparent(mat);
            return mat;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float input = Ready ? Mathf.Clamp(_input + Keys(), -1f, 1f) : 0f;
            _rate = Mathf.Lerp(_rate, input * AimRate, 1f - Mathf.Exp(-AimResponse * dt));
            _elevation += _rate * dt;

            if (_elevation > MaxElevation || _elevation < MinElevation)
            {
                _elevation = Mathf.Clamp(_elevation, MinElevation, MaxElevation);
                _rate = 0f;
            }

            Apply();
        }

        static float Keys()
        {
            if (GameMenu.IsOpen || LevelBriefing.IsOpen) return 0f;

            var kb = Keyboard.current;
            if (kb == null) return 0f;

            float up = kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f;
            float down = kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f;
            return up - down;
        }

        void Apply()
        {
            _pivot.localRotation = _rest * Quaternion.Euler(0f, 0f, _restElevation - _elevation);
        }

        void LateUpdate()
        {
            if (_line == null) return;

            _line.enabled = Ready;
            if (!Ready) return;

            Vector3 from = Muzzle;
            Vector3 dir = Direction;
            from.z -= LineDepth;

            _line.SetPosition(0, from + dir * LineStart);
            _line.SetPosition(1, from + dir * LineLength);
        }

        void OnDestroy()
        {
            if (_lineMaterial != null) Destroy(_lineMaterial);
            if (_lineTexture != null) Destroy(_lineTexture);
        }
    }
}
