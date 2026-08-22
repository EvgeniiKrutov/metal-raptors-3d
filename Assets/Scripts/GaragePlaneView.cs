using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public class GaragePlaneView : MonoBehaviour
    {
        const float FieldOfView = 30f;
        const float ViewYawDeg = -32f;
        const float ViewPitchDeg = 13f;
        const float FillWidth = 0.93f;
        const float FillHeight = 1.5f;
        const float VerticalMarginFraction = 0.08f;
        const float RegionLeftFraction = 0.328f;
        const float RegionBottomFraction = 0.24f;

        const float BodyYawDeg = 8f;
        const float FallbackNoseUpDeg = 12f;
        const float GroundLineFraction = 0.213f;

        const float GroundSizeFactor = 8f;
        const float ShadowAlpha = 0.4f;

        const float DragDegreesPerScreen = 480f;
        const float MaxDragDeg = 180f;
        const float DragSmoothing = 0.06f;
        const float ReturnSmoothing = 0.85f;

        const float SpinUpSeconds = 0.45f;
        const float SpinHoldSeconds = 1f;
        const float SpinDownSeconds = 1.6f;
        const float SpinPeakDegreesPerSecond = 1100f;

        PlanePreviewRig _rig;
        PlaneModelConfig _plane;
        PlaneSkin _skin;
        GameObject _body;
        Transform _model;
        Transform _ground;
        PropellerSpin _propeller;

        float _spinTime = -1f;

        float _restPitch = FallbackNoseUpDeg;

        bool _dragging;
        float _dragStartX;
        float _dragAnchor;
        float _dragTarget;
        float _dragYaw, _dragVelocity;

        public static GaragePlaneView Build(Transform canvas, PlaneModelConfig plane, PlaneSkin skin)
        {
            var go = new GameObject("Garage Plane View");
            var view = go.AddComponent<GaragePlaneView>();
            view.Compose(canvas, plane, skin);
            return view;
        }

        public void SetPlane(PlaneModelConfig plane, PlaneSkin skin)
        {
            if (plane == _plane) { SetSkin(skin); return; }

            _plane = plane;
            _skin = skin;
            _spinTime = -1f;
            if (_body != null) Destroy(_body);
            _rig.SetPlaneSize(GarageSize(plane));
            BuildBody();
        }

        static float GarageSize(PlaneModelConfig plane) =>
            plane.onScreenSize / Mathf.Max(0.01f, plane.garageZoom);

        public void SetSkin(PlaneSkin skin)
        {
            _skin = skin;
            PlaneSkins.Apply(_model, skin);
        }

        public void PlaySpinUp() => _spinTime = 0f;

        void Compose(Transform canvas, PlaneModelConfig plane, PlaneSkin skin)
        {
            _plane = plane;
            _skin = skin;
            GarageLighting.Apply();
            _rig = new PlanePreviewRig(canvas, transform, new PlanePreviewFraming
            {
                fieldOfView = FieldOfView,
                viewYawDeg = ViewYawDeg,
                viewPitchDeg = ViewPitchDeg,
                fillWidth = FillWidth,
                fillHeight = FillHeight,
                verticalMarginFraction = VerticalMarginFraction,
                regionLeftFraction = RegionLeftFraction,
                regionBottomFraction = RegionBottomFraction,
            }, GarageSize(plane));

            BuildBody();
        }

        void BuildBody()
        {
            _body = new GameObject("Garage Plane");
            _body.transform.position = PlanePreviewRig.Origin;
            _body.transform.rotation = Quaternion.identity;

            Transform model = PlaneFactory.BuildPlaneModel(_body.transform, _plane, skin: _skin);
            _model = model;

            _propeller = model.GetComponentInChildren<PropellerSpin>();
            if (_propeller != null) _propeller.degreesPerSecond = 0f;

            _restPitch = SolveRestingPitch(model);
            ApplyBodyRotation();

            Bounds bounds = MeasureBounds(model);
            float groundY = PlanePreviewRig.Origin.y - GarageSize(_plane) * GroundLineFraction;
            model.position += new Vector3(
                PlanePreviewRig.Origin.x - bounds.center.x,
                groundY - ContactHeight(model),
                PlanePreviewRig.Origin.z - bounds.center.z);

            PlaceGround(MeasureBounds(model), groundY);
        }

        static Bounds MeasureBounds(Transform model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(model.position, Vector3.one);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        float SolveRestingPitch(Transform model)
        {
            List<Vector2> points = ContactProfile(model);
            if (points.Count < 2) return FallbackNoseUpDeg;

            float centroidX = 0f;
            for (int i = 0; i < points.Count; i++) centroidX += points[i].x;
            centroidX /= points.Count;

            List<Vector2> hull = LowerHull(points);
            if (hull.Count < 2) return FallbackNoseUpDeg;

            for (int i = 0; i < hull.Count - 1; i++)
            {
                if (centroidX < hull[i].x || centroidX > hull[i + 1].x) continue;
                return EdgePitch(hull[i], hull[i + 1]);
            }

            return EdgePitch(hull[0], hull[hull.Count - 1]);
        }

        static float EdgePitch(Vector2 a, Vector2 b) =>
            -Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;

        static List<Vector2> LowerHull(List<Vector2> points)
        {
            points.Sort((a, b) => a.x.CompareTo(b.x));

            var hull = new List<Vector2>();
            foreach (Vector2 p in points)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            return hull;
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b) =>
            (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        List<Vector2> ContactProfile(Transform model)
        {
            var points = new List<Vector2>();

            foreach (MeshFilter mf in ContactMeshes(model))
            {
                Matrix4x4 toBody = _body.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (Vector3 v in mf.sharedMesh.vertices)
                {
                    Vector3 local = toBody.MultiplyPoint3x4(v);
                    points.Add(new Vector2(local.x, local.y));
                }
            }
            return points;
        }

        float ContactHeight(Transform model)
        {
            float lowest = float.MaxValue;

            foreach (MeshFilter mf in ContactMeshes(model))
            {
                Matrix4x4 toWorld = mf.transform.localToWorldMatrix;
                foreach (Vector3 v in mf.sharedMesh.vertices)
                    lowest = Mathf.Min(lowest, toWorld.MultiplyPoint3x4(v).y);
            }

            return lowest < float.MaxValue ? lowest : MeasureBounds(model).min.y;
        }

        IEnumerable<MeshFilter> ContactMeshes(Transform model)
        {
            Transform pivot = PlaneFactory.FindDeep(model, _plane.propPivotNode);
            Transform blades = PlaneFactory.FindDeep(model, _plane.propBladesNode);

            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                if (pivot != null && mf.transform.IsChildOf(pivot)) continue;
                if (blades != null && mf.transform.IsChildOf(blades)) continue;
                yield return mf;
            }
        }

        void PlaceGround(Bounds bounds, float groundY)
        {
            if (_ground == null) _ground = CreateGround();

            float size = _plane.onScreenSize * GroundSizeFactor;
            _ground.localScale = new Vector3(size, size, 1f);
            _ground.position = new Vector3(bounds.center.x, groundY, bounds.center.z);
        }

        Transform CreateGround()
        {
            var go = UIFactory.CreatePrimitive3D(PrimitiveType.Quad, PlanePreviewRig.Origin,
                Vector3.one, Color.white, emissive: false, keepCollider: false);
            go.name = "Ground";
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var shader = Shader.Find("Custom/GroundShadowCatcher");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetColor("_ShadowColor", new Color(0f, 0f, 0f, ShadowAlpha));
                renderer.sharedMaterial = mat;
            }
            else
            {
                Debug.LogError("GaragePlaneView: Custom/GroundShadowCatcher shader not found.");
            }

            return go.transform;
        }

        void Update()
        {
            _rig.Update();
            UpdateDrag();
            UpdatePropeller();
        }

        void UpdateDrag()
        {
            ReadDrag();

            if (!_dragging) _dragTarget = 0f;

            _dragYaw = Mathf.SmoothDamp(_dragYaw, _dragTarget, ref _dragVelocity,
                _dragging ? DragSmoothing : ReturnSmoothing);
            ApplyBodyRotation();
        }

        void ReadDrag()
        {
            MenuPointer pointer = MenuInput.ReadPointer();

            if (pointer.PressedThisFrame && OverBand(pointer.Position))
            {
                _dragging = true;
                _dragStartX = pointer.Position.x;
                _dragAnchor = _dragYaw;
            }
            else if (!pointer.Pressed)
            {
                _dragging = false;
            }

            if (!_dragging || Screen.width <= 0) return;

            float travel = (pointer.Position.x - _dragStartX) / Screen.width;
            _dragTarget = Mathf.Clamp(_dragAnchor + travel * DragDegreesPerScreen,
                -MaxDragDeg, MaxDragDeg);
        }

        bool OverBand(Vector2 screenPosition)
        {
            RectTransform rect = _rig.RegionRect;
            return rect != null
                   && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, null);
        }

        void ApplyBodyRotation()
        {
            if (_body == null) return;

            _body.transform.rotation = Quaternion.Euler(0f, BodyYawDeg + _dragYaw, 0f)
                                     * Quaternion.Euler(0f, 0f, _restPitch);
        }

        void UpdatePropeller()
        {
            if (_spinTime < 0f || _propeller == null) return;

            _spinTime += Time.deltaTime;
            float speed;

            if (_spinTime < SpinUpSeconds)
            {
                speed = Mathf.SmoothStep(0f, SpinPeakDegreesPerSecond, _spinTime / SpinUpSeconds);
            }
            else if (_spinTime < SpinUpSeconds + SpinHoldSeconds)
            {
                speed = SpinPeakDegreesPerSecond;
            }
            else
            {
                float t = (_spinTime - SpinUpSeconds - SpinHoldSeconds) / SpinDownSeconds;
                speed = Mathf.SmoothStep(SpinPeakDegreesPerSecond, 0f, Mathf.Clamp01(t));
                if (t >= 1f) _spinTime = -1f;
            }

            _propeller.degreesPerSecond = _spinTime < 0f ? 0f : speed;
        }

        void OnDestroy()
        {
            if (_rig != null) _rig.Release();
            if (_body != null) Destroy(_body);
            if (_ground != null) Destroy(_ground.gameObject);
        }
    }
}
