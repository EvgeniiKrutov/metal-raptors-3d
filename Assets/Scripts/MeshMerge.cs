using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    public static class MeshMerge
    {
        static readonly List<MeshFilter> Filters = new List<MeshFilter>();

        public static bool Collapse(Transform container, IList<GameObject> sources, string name)
        {
            if (container == null || sources == null || sources.Count == 0) return false;

            Filters.Clear();
            foreach (var source in sources)
                if (source != null) Filters.AddRange(source.GetComponentsInChildren<MeshFilter>(true));

            var groups = new Dictionary<string, List<CombineInstance>>();
            var order = new List<Material>();
            ShadowCastingMode shadows = ShadowCastingMode.Off;
            uint layerMask = 0;
            bool any = false;
            Matrix4x4 toLocal = container.worldToLocalMatrix;

            foreach (var filter in Filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !filter.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled) continue;
                if (!mesh.isReadable)
                {
                    Filters.Clear();
                    return false;
                }

                if (!any)
                {
                    shadows = renderer.shadowCastingMode;
                    layerMask = renderer.renderingLayerMask;
                    any = true;
                }

                Material[] mats = renderer.sharedMaterials;
                Matrix4x4 matrix = toLocal * filter.transform.localToWorldMatrix;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    Material mat = sub < mats.Length ? mats[sub] : mats[mats.Length - 1];
                    if (mat == null) continue;
                    if (!groups.TryGetValue(mat.name, out var list))
                    {
                        list = new List<CombineInstance>();
                        groups[mat.name] = list;
                        order.Add(mat);
                    }
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = matrix });
                }
            }

            Filters.Clear();
            if (!any || order.Count == 0) return false;

            var parts = new CombineInstance[order.Count];
            var built = new List<Mesh>(order.Count);
            for (int i = 0; i < order.Count; i++)
            {
                var part = new Mesh { indexFormat = IndexFormat.UInt32 };
                part.CombineMeshes(groups[order[i].name].ToArray(), true, true);
                built.Add(part);
                parts[i] = new CombineInstance { mesh = part, transform = Matrix4x4.identity };
            }

            var merged = new Mesh { name = name + " (merged)", indexFormat = IndexFormat.UInt32 };
            merged.CombineMeshes(parts, false, false);
            merged.RecalculateBounds();
            foreach (var part in built) Object.Destroy(part);

            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = container.gameObject.layer;
            go.transform.SetParent(container, false);
            go.GetComponent<MeshFilter>().sharedMesh = merged;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterials = order.ToArray();
            mr.shadowCastingMode = shadows;
            mr.renderingLayerMask = layerMask;
            go.AddComponent<OwnedMesh>().mesh = merged;

            foreach (var source in sources)
            {
                if (source == null) continue;
                source.SetActive(false);
                Object.Destroy(source);
            }
            return true;
        }
    }

    public class OwnedMesh : MonoBehaviour
    {
        public Mesh mesh;

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
