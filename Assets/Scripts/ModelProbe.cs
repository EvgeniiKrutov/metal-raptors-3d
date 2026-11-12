using UnityEngine;

namespace MetalRaptors
{
    public static class ModelProbe
    {
        const float ProbeY = -9000f;

        public static readonly Vector3 Origin = new Vector3(0f, ProbeY, 0f);

        public static GameObject Open(GameObject prefab)
        {
            var holder = new GameObject("Model Probe");
            holder.transform.position = Origin;
            var view = Object.Instantiate(prefab, holder.transform, false);
            return view;
        }

        public static void Close(GameObject view)
        {
            if (view == null) return;
            Transform holder = view.transform.parent;
            Object.Destroy(holder != null ? holder.gameObject : view);
        }

        public static bool Bounds(GameObject view, out Bounds box)
        {
            box = default;
            Renderer[] renderers = view.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;

            box = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) box.Encapsulate(renderers[i].bounds);
            box.center -= Origin;
            return true;
        }

        public static Vector3 Local(Vector3 world) => world - Origin;
    }
}
