using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public static class FlandersBoat
    {
        public const int Intact = 0;
        public const int Wreck = 1;
        public const int Models = 2;

        static readonly string[] Resource = { "objects/wooden_boat", "objects/wooden_boat_wreck" };

        const string DebrisPrefix = "Debris";
        const float BleachLift = 1.6f;
        const float ProbeY = -9000f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly GameObject[] Prefabs = new GameObject[Models];
        static readonly float[] LengthYaw = new float[Models];
        static readonly Bounds[] Hulls = new Bounds[Models];
        static readonly Bounds[] Wholes = new Bounds[Models];
        static readonly List<Transform> Debris = new List<Transform>();
        static bool _measured;
        static bool _any;

        public static bool Measure()
        {
            if (_measured) return _any;
            _measured = true;

            for (int i = 0; i < Models; i++)
            {
                var prefab = Resources.Load<GameObject>(Resource[i]);
                if (prefab == null)
                {
                    Debug.LogError($"FlandersBoat: {Resource[i]} not found in Resources.");
                    continue;
                }

                if (!Probe(prefab, 0f, out Bounds hull, out _))
                {
                    Debug.LogError($"FlandersBoat: {Resource[i]} has no hull renderers; it is skipped.");
                    continue;
                }

                float yaw = hull.extents.x > hull.extents.z ? 90f : 0f;
                Probe(prefab, yaw, out hull, out Bounds whole);

                Prefabs[i] = prefab;
                LengthYaw[i] = yaw;
                Hulls[i] = hull;
                Wholes[i] = whole;
                _any = true;
            }

            return _any;
        }

        public static bool Has(int model) =>
            Measure() && model >= 0 && model < Models && Prefabs[model] != null;

        public static Bounds Hull(int model) => Hulls[model];

        public static Bounds Footprint(int model) =>
            new Bounds(Wholes[model].center - Hulls[model].center, Wholes[model].size);

        public static void Place(Transform parent, int model, Vector3 position, Quaternion frame,
            float scale, float roll, bool capsized, float sink, float shade, float bleach)
        {
            if (!Has(model)) return;

            var root = new GameObject("Boat");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, frame);
            root.transform.localScale = Vector3.one * scale;

            var tilt = new GameObject("Tilt").transform;
            tilt.SetParent(root.transform, false);
            var holder = new GameObject("Frame").transform;
            holder.SetParent(tilt, false);
            holder.localRotation = Quaternion.Euler(0f, LengthYaw[model], 0f);

            var view = Object.Instantiate(Prefabs[model], holder, false);
            view.name = Prefabs[model].name;

            Bounds hull = Hulls[model];
            float drop = hull.size.y * sink;
            float radius = hull.extents.x;
            tilt.localPosition = new Vector3(0f, radius - drop, 0f);
            holder.localPosition = -new Vector3(hull.center.x, hull.min.y + radius, hull.center.z);

            Debris.Clear();
            foreach (Transform node in view.GetComponentsInChildren<Transform>(true))
                if (IsDebris(node)) Debris.Add(node);
            foreach (Transform piece in Debris) piece.SetParent(root.transform, true);
            Debris.Clear();

            if (capsized)
            {
                float rad = roll * Mathf.Deg2Rad;
                float lift = hull.extents.y * Mathf.Cos(rad) + hull.extents.x * Mathf.Abs(Mathf.Sin(rad));
                tilt.localPosition = new Vector3(0f, lift - drop, 0f);
                tilt.localRotation = Quaternion.AngleAxis(180f + roll, Vector3.forward);
                holder.localPosition = -hull.center;
            }
            else
            {
                tilt.localRotation = Quaternion.AngleAxis(roll, Vector3.forward);
            }

            NightReceivers.Adopt(root);
            NightLights.MarkCaster(root);
            Weather(root, shade, bleach);
        }

        static bool Probe(GameObject prefab, float yaw, out Bounds hull, out Bounds whole)
        {
            var origin = new Vector3(0f, ProbeY, 0f);
            var holder = new GameObject("Boat Probe");
            holder.transform.SetPositionAndRotation(origin, Quaternion.Euler(0f, yaw, 0f));
            var view = Object.Instantiate(prefab, holder.transform, false);

            hull = whole = default;
            bool anyHull = false, anyWhole = false;
            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>(true))
            {
                Bounds box = renderer.bounds;
                box.center -= origin;

                if (anyWhole) whole.Encapsulate(box);
                else whole = box;
                anyWhole = true;

                if (IsDebris(renderer.transform)) continue;
                if (anyHull) hull.Encapsulate(box);
                else hull = box;
                anyHull = true;
            }

            Object.Destroy(holder);
            return anyHull;
        }

        static bool IsDebris(Transform node) => node.name.StartsWith(DebrisPrefix);

        static void Weather(GameObject root, float shade, float bleach)
        {
            var block = new MaterialPropertyBlock();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null || !slots[i].HasProperty(BaseColorId)) continue;

                    Color source = slots[i].GetColor(BaseColorId);
                    float grey = source.grayscale * BleachLift;
                    Color worn = Color.Lerp(source, new Color(grey, grey, grey), bleach) * shade;
                    worn.a = source.a;

                    block.Clear();
                    block.SetColor(BaseColorId, worn);
                    renderer.SetPropertyBlock(block, i);
                }
            }
        }
    }
}
