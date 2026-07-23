using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    /// <summary>
    /// Builds flyable aircraft rigs — the model under a bare physics body — for every level
    /// type (Air Fights and Campaign). Extracted from LevelController so any controller can
    /// spawn planes; the mirror/orientation rules are documented in that class's history and
    /// docs/campaign.md.
    /// </summary>
    public static class PlaneFactory
    {
        // Dedicated physics layer for every plane's collider; its self-collisions are switched
        // off by the level controllers so planes pass through each other (scrapes are detected
        // by distance instead).
        public const int PlaneLayer = 8;

        // Cosmetic nose-down tilt of the model about the view axis (visual only).
        const float ModelPitchDeg = -10f;

        const float FallbackCubeScale = 30f;

        /// <summary>
        /// Instantiates the aircraft described by <paramref name="plane"/> under
        /// <paramref name="parent"/> (the physics body): stands it upright per the config,
        /// scales it to its on-screen size, adds a tight convex collider on the plane layer,
        /// enables shadows, spins the propeller, and attaches the scrape ShakeEffect. Pass
        /// <paramref name="mirrored"/> for enemies, which fly left instead of right.
        /// </summary>
        public static Transform BuildPlaneModel(Transform parent, PlaneModelConfig plane, bool mirrored = false)
        {
            var prefab = Resources.Load<GameObject>(plane.resourceName);
            if (prefab == null)
            {
                Debug.LogError($"PlaneFactory: {plane.resourceName} model not found in Resources.");
                var fallback = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                    Vector3.zero, Vector3.one * FallbackCubeScale, Color.white);
                fallback.transform.SetParent(parent, false);
                return fallback.transform;
            }

            var model = Object.Instantiate(prefab);
            model.name = mirrored ? $"{plane.resourceName} (enemy)" : plane.resourceName;
            model.transform.SetParent(parent, false);

            // Stand the flat-exported model upright, roll it wheels-down for the right-flying
            // player (the mirrored enemy gets it from the parent's ~180° heading spin instead),
            // and dip the nose toward the flight direction. Visual only: the heading is the
            // parent's rotation.
            Quaternion wheelsDown = (plane.rollWheelsDown && !mirrored)
                ? Quaternion.Euler(180f, 0f, 0f) : Quaternion.identity;
            float pitch = mirrored ? -ModelPitchDeg : ModelPitchDeg;
            model.transform.localRotation = Quaternion.Euler(0f, 0f, pitch)
                                          * wheelsDown
                                          * Quaternion.Euler(plane.standUpEuler);

            NormalizeSize(model.transform, plane.onScreenSize);

            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.On;

            AddPlaneCollider(model.transform);
            StartPropeller(model.transform, plane);
            model.AddComponent<ShakeEffect>();
            return model.transform;
        }

        /// <summary>
        /// Mounts the machine-gun muzzle just ahead of the propeller disc at Spandau height,
        /// derived from the prop's bounds (the models have no gun nodes). Must be called while
        /// the body still has its spawn heading of 0 (identity rotation), so world offsets can
        /// be stored directly as the muzzle's local position.
        /// </summary>
        public static Transform MountMuzzle(GameObject body, Transform model, PlaneModelConfig plane)
        {
            const float MuzzleClearance = 2f;
            const float GunHeightAboveHub = 2.5f;

            Transform prop = FindDeep(model, plane.propBladesNode) ?? FindDeep(model, plane.propPivotNode) ?? model;
            var renderers = prop.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers.Length > 0
                ? renderers[0].bounds
                : new Bounds(body.transform.position, Vector3.one);
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(body.transform, false);
            muzzle.localPosition = new Vector3(
                bounds.max.x + MuzzleClearance - body.transform.position.x,
                bounds.center.y + GunHeightAboveHub - body.transform.position.y,
                0f);
            return muzzle;
        }

        /// <summary>Uniformly scales <paramref name="model"/> so the longest side of its combined
        /// renderer bounds equals <paramref name="targetSize"/>.</summary>
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

        /// <summary>A single convex mesh collider from the largest mesh (the fuselage), on the
        /// plane layer so plane-vs-plane contacts stay off while ground and bullets stay on.</summary>
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

            var col = biggest.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = biggest.sharedMesh;
            col.convex = true;
            biggest.gameObject.layer = PlaneLayer;
        }

        static void StartPropeller(Transform model, PlaneModelConfig plane)
        {
            Transform spinner = FindDeep(model, plane.propPivotNode) ?? FindDeep(model, plane.propBladesNode);
            if (spinner != null) spinner.gameObject.AddComponent<PropellerSpin>();
            else Debug.LogWarning("PlaneFactory: propeller node not found on the plane model.");
        }

        /// <summary>Depth-first search for a descendant transform by name.</summary>
        public static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
