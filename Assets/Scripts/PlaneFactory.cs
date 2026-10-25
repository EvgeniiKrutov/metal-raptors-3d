using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public static class PlaneFactory
    {
        public const int PlaneLayer = 8;

        const float ModelPitchDeg = -10f;

        const float FallbackCubeScale = 30f;

        static readonly int[] BoxTriangles =
        {
            0, 4, 6, 0, 6, 2, 1, 3, 7, 1, 7, 5,
            0, 1, 5, 0, 5, 4, 2, 6, 7, 2, 7, 3,
            0, 2, 3, 0, 3, 1, 4, 5, 7, 4, 7, 6,
        };

        static readonly Dictionary<string, Mesh> Hulls = new Dictionary<string, Mesh>();

        public static Transform BuildPlaneModel(Transform parent, PlaneModelConfig plane,
            bool mirrored = false, PlaneSkin skin = null)
        {
            var prefab = Resources.Load<GameObject>(plane.ResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"PlaneFactory: {plane.ResourcePath} model not found in Resources.");
                var fallback = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                    Vector3.zero, Vector3.one * FallbackCubeScale, Color.white);
                fallback.transform.SetParent(parent, false);
                return fallback.transform;
            }

            var model = Object.Instantiate(prefab);
            model.name = mirrored ? $"{plane.resourceName} (enemy)" : plane.resourceName;
            model.transform.SetParent(parent, false);

            Quaternion wheelsDown = (plane.rollWheelsDown && !mirrored)
                ? Quaternion.Euler(180f, 0f, 0f) : Quaternion.identity;
            Quaternion standUp = wheelsDown * Quaternion.Euler(plane.standUpEuler);
            float nosePitch = ModelPitchDeg + plane.pitchTrimDeg;
            float pitch = mirrored ? -nosePitch : nosePitch;
            model.transform.localRotation = Quaternion.Euler(0f, 0f, pitch) * standUp;

            Vector3 lengthAxis = Quaternion.Inverse(standUp) * Vector3.right;
            NormalizeLength(model.transform, lengthAxis, plane.LengthUnits);
            if (plane.fitWingspan)
                FitSpan(model.transform, Quaternion.Inverse(standUp) * Vector3.forward,
                    plane.OnScreenSize);
            ShiftPivot(model.transform, lengthAxis, plane.pivotFromNose);

            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.On;

            PlaneSkins.Apply(model.transform, skin);
            NightReceivers.Adopt(model, 1f, 1f);
            NightLights.MarkCaster(model);

            AddPlaneCollider(model.transform, plane);
            StartPropeller(model.transform, plane, parent);
            model.AddComponent<ShakeEffect>();
            return model.transform;
        }

        public static Transform MountMuzzle(GameObject body, Transform model, PlaneModelConfig plane,
            out Transform flashPoint)
        {
            const float GunHeightAboveHub = 2.5f;

            Vector3 nose = NoseLocal(body, model, plane);
            bool gunned = GunMuzzleLocal(body, model, plane, out Vector3 gun);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(body.transform, false);
            muzzle.localPosition = gunned ? gun : nose + new Vector3(0f, GunHeightAboveHub, 0f);

            flashPoint = new GameObject("MuzzleFlashPoint").transform;
            flashPoint.SetParent(body.transform, false);
            flashPoint.localPosition = gunned ? gun : nose;
            return muzzle;
        }

        public static bool GunMuzzleLocal(GameObject body, Transform model, PlaneModelConfig plane,
            out Vector3 local)
        {
            local = Vector3.zero;
            Transform gun = FindDeep(model, plane.gunNode);
            MeshFilter filter = gun != null ? gun.GetComponentInChildren<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null) return false;

            Bounds bounds = filter.sharedMesh.bounds;
            Matrix4x4 toBody = body.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Vector3 front = Vector3.zero;
            float reach = 0f;
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 half = Vector3.zero;
                half[axis] = bounds.extents[axis];
                float x = toBody.MultiplyVector(half).x;
                if (Mathf.Abs(x) <= Mathf.Abs(reach)) continue;
                reach = x;
                front = x > 0f ? half : -half;
            }

            local = toBody.MultiplyPoint3x4(bounds.center + front);
            local.z = 0f;
            return true;
        }

        public static Vector3? EngineLocal(GameObject body, Transform model, PlaneModelConfig plane)
        {
            Transform engine = FindDeep(model, plane.engineNode);
            if (engine == null || !RendererBounds(engine, out Bounds bounds)) return null;

            Vector3 local = body.transform.InverseTransformPoint(bounds.center);
            local.z = 0f;
            return local;
        }

        public static Vector3 GunLocal(GameObject body, Transform model, PlaneModelConfig plane,
            float scale, bool mirrored)
        {
            Vector3 nose = NoseLocal(body, model, plane);
            float lift = plane.gunLift * plane.LengthUnits * scale;
            nose.y += mirrored ? -lift : lift;
            return nose;
        }

        public static Vector3 NoseLocal(GameObject body, Transform model, PlaneModelConfig plane)
        {
            const float MuzzleClearance = 2f;

            Transform prop = FindDeep(model, plane.propBladesNode) ?? FindDeep(model, plane.propPivotNode) ?? model;
            var renderers = prop.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers.Length > 0
                ? renderers[0].bounds
                : new Bounds(body.transform.position, Vector3.one);
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            return new Vector3(bounds.max.x + MuzzleClearance - body.transform.position.x,
                bounds.center.y - body.transform.position.y, 0f);
        }

        public static void WingTipsLocal(GameObject body, Transform model,
            out Vector3 near, out Vector3 far, PlaneModelConfig plane = null)
        {
            Transform wing = plane != null ? FindDeep(model, plane.wingNode) : null;
            if (!RendererBounds(wing != null ? wing : model, out Bounds bounds))
            {
                near = far = Vector3.zero;
                return;
            }

            Vector3 origin = body.transform.position;
            float x = bounds.center.x - origin.x;
            float y = bounds.center.y - origin.y;

            near = new Vector3(x, y, bounds.min.z - origin.z);
            far = new Vector3(x, y, bounds.max.z - origin.z);
        }

        static bool RendererBounds(Transform root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            bounds = renderers.Length > 0 ? renderers[0].bounds : default;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return renderers.Length > 0;
        }

        static void ShiftPivot(Transform model, Vector3 lengthAxis, float fromNose)
        {
            if (fromNose <= 0f || !ModelBounds(model, out Bounds bounds)) return;

            float length = ExtentAlong(bounds, lengthAxis);
            float nose = Vector3.Dot(bounds.center, lengthAxis) + length * 0.5f;
            Vector3 shift = lengthAxis * (length * fromNose - nose);
            foreach (Transform child in model) child.localPosition += shift;
        }

        static void NormalizeLength(Transform model, Vector3 lengthAxis, float targetLength)
        {
            if (!ModelBounds(model, out Bounds bounds)) return;

            float length = ExtentAlong(bounds, lengthAxis) * model.localScale.x;
            if (length > 0.0001f)
                model.localScale *= targetLength / length;
        }

        static void FitSpan(Transform model, Vector3 spanAxis, float targetSpan)
        {
            if (!ModelBounds(model, out Bounds bounds)) return;

            var axis = new Vector3(Mathf.Abs(spanAxis.x), Mathf.Abs(spanAxis.y),
                Mathf.Abs(spanAxis.z));
            Vector3 scale = model.localScale;
            float span = ExtentAlong(bounds, axis) * Vector3.Dot(scale, axis);
            if (span > 0.0001f)
                model.localScale = Vector3.Scale(scale,
                    Vector3.one + axis * (targetSpan / span - 1f));
        }

        static bool ModelBounds(Transform model, out Bounds bounds)
        {
            Matrix4x4 toModel = model.worldToLocalMatrix;
            bounds = default;
            bool any = false;

            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                Mesh mesh = r is SkinnedMeshRenderer skinned
                    ? skinned.sharedMesh
                    : r.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                if (mesh == null) continue;

                Bounds local = mesh.bounds;
                Matrix4x4 toRoot = toModel * r.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = toRoot.MultiplyPoint3x4(new Vector3(
                        (corner & 1) == 0 ? local.min.x : local.max.x,
                        (corner & 2) == 0 ? local.min.y : local.max.y,
                        (corner & 4) == 0 ? local.min.z : local.max.z));

                    if (any) bounds.Encapsulate(point);
                    else { bounds = new Bounds(point, Vector3.zero); any = true; }
                }
            }
            return any;
        }

        static float ExtentAlong(Bounds bounds, Vector3 axis) =>
            Mathf.Abs(axis.x) * bounds.size.x
          + Mathf.Abs(axis.y) * bounds.size.y
          + Mathf.Abs(axis.z) * bounds.size.z;

        static void AddPlaneCollider(Transform model, PlaneModelConfig plane)
        {
            Transform hullHost = FindDeep(model, plane.hitboxNode);
            if (hullHost != null)
            {
                AddHullCollider(model, hullHost, plane);
                return;
            }

            MeshFilter biggest = null;
            float biggestSize = 0f;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                float size = mf.sharedMesh.bounds.size.sqrMagnitude;
                if (size > biggestSize) { biggestSize = size; biggest = mf; }
            }
            if (biggest == null) return;

            biggest.gameObject.layer = PlaneLayer;

            if (!biggest.sharedMesh.isReadable)
            {
                Debug.LogWarning($"PlaneFactory: {biggest.sharedMesh.name} is not Read/Write enabled; " +
                                 "falling back to a box hitbox.");
                var box = biggest.gameObject.AddComponent<BoxCollider>();
                box.center = biggest.sharedMesh.bounds.center;
                box.size = biggest.sharedMesh.bounds.size;
                return;
            }

            var col = biggest.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = biggest.sharedMesh;
            col.convex = true;
        }

        static void AddHullCollider(Transform model, Transform host, PlaneModelConfig plane)
        {
            if (!Hulls.TryGetValue(plane.resourceName, out Mesh hull) || hull == null)
                Hulls[plane.resourceName] = hull = BuildHull(model, host, plane);

            host.gameObject.layer = PlaneLayer;
            var col = host.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = hull;
            col.convex = true;
        }

        static Mesh BuildHull(Transform model, Transform host, PlaneModelConfig plane)
        {
            var corners = new List<Vector3>();
            var triangles = new List<int>();
            Matrix4x4 toHost = host.worldToLocalMatrix;

            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;

                Bounds local = mf.sharedMesh.bounds;
                Matrix4x4 toLocal = toHost * mf.transform.localToWorldMatrix;
                int first = corners.Count;
                for (int corner = 0; corner < 8; corner++)
                    corners.Add(toLocal.MultiplyPoint3x4(new Vector3(
                        (corner & 1) == 0 ? local.min.x : local.max.x,
                        (corner & 2) == 0 ? local.min.y : local.max.y,
                        (corner & 4) == 0 ? local.min.z : local.max.z)));

                foreach (int index in BoxTriangles) triangles.Add(first + index);
            }

            if (plane.hitboxFloor > 0f) RaiseFloor(corners, model, host, plane);

            var hull = new Mesh { name = $"{model.name} hull", hideFlags = HideFlags.DontSave };
            hull.SetVertices(corners);
            hull.SetTriangles(triangles, 0);
            return hull;
        }

        static void RaiseFloor(List<Vector3> corners, Transform model, Transform host,
            PlaneModelConfig plane)
        {
            Quaternion standUp = (plane.rollWheelsDown ? Quaternion.Euler(180f, 0f, 0f) : Quaternion.identity)
                               * Quaternion.Euler(plane.standUpEuler);
            Vector3 modelUp = Quaternion.Inverse(standUp) * Vector3.up;
            Vector3 up = host.InverseTransformDirection(model.TransformDirection(modelUp)).normalized;

            float low = float.MaxValue, high = float.MinValue;
            foreach (Vector3 corner in corners)
            {
                float h = Vector3.Dot(corner, up);
                low = Mathf.Min(low, h);
                high = Mathf.Max(high, h);
            }

            float floor = Mathf.Lerp(low, high, plane.hitboxFloor);
            for (int i = 0; i < corners.Count; i++)
            {
                float h = Vector3.Dot(corners[i], up);
                if (h < floor) corners[i] += up * (floor - h);
            }
        }

        static void StartPropeller(Transform model, PlaneModelConfig plane, Transform body)
        {
            Transform spinner = FindDeep(model, plane.propPivotNode) ?? FindDeep(model, plane.propBladesNode);
            if (spinner == null)
            {
                Debug.LogWarning($"PlaneFactory: {plane.resourceName} has no propeller node " +
                                 "(propPivotNode / propBladesNode); its propeller cannot spin.");
                return;
            }

            var spin = spinner.gameObject.AddComponent<PropellerSpin>();
            spin.axisSpace = body;
            spin.axisInSpace = Vector3.right;
            spin.straighten = plane.straightenProp;
        }

        public static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
