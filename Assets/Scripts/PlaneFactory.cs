using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public static class PlaneFactory
    {
        public const int PlaneLayer = 8;

        const float ModelPitchDeg = -10f;

        const float FallbackCubeScale = 30f;

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
            float pitch = mirrored ? -ModelPitchDeg : ModelPitchDeg;
            model.transform.localRotation = Quaternion.Euler(0f, 0f, pitch)
                                          * wheelsDown
                                          * Quaternion.Euler(plane.standUpEuler);

            NormalizeSize(model.transform, plane.onScreenSize);

            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.On;

            PlaneSkins.Apply(model.transform, skin);

            AddPlaneCollider(model.transform);
            StartPropeller(model.transform, plane);
            model.AddComponent<ShakeEffect>();
            return model.transform;
        }

        public static Transform MountMuzzle(GameObject body, Transform model, PlaneModelConfig plane,
            out Transform flashPoint)
        {
            const float GunHeightAboveHub = 2.5f;

            Vector3 nose = NoseLocal(body, model, plane);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(body.transform, false);
            muzzle.localPosition = nose + new Vector3(0f, GunHeightAboveHub, 0f);

            flashPoint = new GameObject("MuzzleFlashPoint").transform;
            flashPoint.SetParent(body.transform, false);
            flashPoint.localPosition = nose;
            return muzzle;
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
            out Vector3 near, out Vector3 far)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                near = far = Vector3.zero;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            Vector3 origin = body.transform.position;
            float x = bounds.center.x - origin.x;
            float y = bounds.center.y - origin.y;

            near = new Vector3(x, y, bounds.min.z - origin.z);
            far = new Vector3(x, y, bounds.max.z - origin.z);
        }

        static void NormalizeSize(Transform model, float targetSize)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (longest > 0.0001f)
                model.localScale *= targetSize / longest;
        }

        static void AddPlaneCollider(Transform model)
        {
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

        static void StartPropeller(Transform model, PlaneModelConfig plane)
        {
            Transform spinner = FindDeep(model, plane.propPivotNode) ?? FindDeep(model, plane.propBladesNode);
            if (spinner != null) spinner.gameObject.AddComponent<PropellerSpin>();
            else Debug.LogWarning("PlaneFactory: propeller node not found on the plane model.");
        }

        public static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
