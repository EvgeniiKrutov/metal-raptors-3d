using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    public class UboatFlotilla : MonoBehaviour
    {
        const string ModelResource = "objects/machines/uboat_uc_1";
        const string BowNode = "bow_plane_port";
        const float M = PlaneModelConfig.UnitsPerMeter;

        const int Boats = 3;
        const float Spacing = 150f;
        const float YawDeg = -135f;
        const float ShoreClear = 45f;

        const float HullLength = 40f, HullBeam = 4.4f;
        const float HullSink = 0.3f;
        const float TowerLength = 5f, TowerHigh = 3.4f, TowerDeep = 2f, TowerAhead = 3f;
        const float GunAhead = 9f, GunHigh = 1.1f;
        const float ModelSink = 0.25f;
        const float DeckLift = 0.15f;

        static readonly Color HullColor = new Color(0.22f, 0.24f, 0.25f);

        readonly List<Bounds> _hulls = new List<Bounds>();
        float _unit = M;

        public float Length => HullLength * _unit;

        public static UboatFlotilla Begin(float x, float cameraZ, float planeDistance)
        {
            var flotilla = new GameObject("U-Boat Flotilla").AddComponent<UboatFlotilla>();
            flotilla._unit = Unit(cameraZ, planeDistance);
            flotilla.Build(x);
            return flotilla;
        }

        static float Unit(float cameraZ, float planeDistance)
        {
            if (planeDistance <= 0f) return M;

            float k = M * PlaneModelConfig.LevelScale / planeDistance;
            float slant = HullLength * 0.5f * Mathf.Abs(Mathf.Sin(YawDeg * Mathf.Deg2Rad));
            float near = FlandersTerrain.BeachZMax + ShoreClear - cameraZ;
            float grow = 1f - k * slant;
            return grow > 0.05f ? k * near / grow : M;
        }

        void Build(float x)
        {
            float reach = Length * 0.5f * Mathf.Abs(Mathf.Sin(YawDeg * Mathf.Deg2Rad));
            float spacing = Spacing * _unit / M;
            float z = FlandersTerrain.BeachZMax + ShoreClear + reach;
            var prefab = Resources.Load<GameObject>(ModelResource);

            for (int i = 0; i < Boats; i++)
            {
                var root = new GameObject("U-Boat");
                root.layer = BattlefieldProps.Layer;
                root.transform.SetParent(transform, false);
                root.transform.position = new Vector3(x + (i - (Boats - 1) * 0.5f) * spacing,
                    SeaSurface.Level, z);

                Transform view = prefab != null ? Model(prefab, root.transform) : Placeholder(root.transform);
                root.transform.rotation = Quaternion.Euler(0f, YawDeg, 0f);

                NightReceivers.Adopt(view.gameObject, 1f, 1f);
                NightLights.MarkCaster(view.gameObject);
                _hulls.Add(WorldBounds(view));
            }
        }

        public Vector3 DeckNear(float x)
        {
            Bounds best = _hulls[0];
            foreach (Bounds hull in _hulls)
                if (Mathf.Abs(hull.center.x - x) < Mathf.Abs(best.center.x - x)) best = hull;

            return new Vector3(best.center.x, SeaSurface.Level + best.size.y * DeckLift, best.center.z);
        }

        Transform Model(GameObject prefab, Transform root)
        {
            var holder = new GameObject(prefab.name).transform;
            holder.SetParent(root, false);
            GameObject view = Instantiate(prefab, holder, false);

            Bounds raw = WorldBounds(holder);
            holder.localRotation = Quaternion.Euler(0f, BowYaw(view.transform, raw), 0f);
            float longest = Mathf.Max(raw.size.x, raw.size.z);
            holder.localScale = Vector3.one * (longest > 0.001f ? Length / longest : _unit);

            Bounds box = WorldBounds(holder);
            holder.position += new Vector3(root.position.x - box.center.x,
                SeaSurface.Level - ModelSink * box.size.y - box.min.y,
                root.position.z - box.center.z);
            return holder;
        }

        static float BowYaw(Transform view, Bounds hull)
        {
            Transform node = PlaneFactory.FindDeep(view, BowNode);
            Renderer bow = node != null ? node.GetComponentInChildren<Renderer>(true) : null;
            if (bow == null) return hull.size.z > hull.size.x ? 90f : 0f;

            Vector3 ahead = bow.bounds.center - hull.center;
            return Mathf.Abs(ahead.x) >= Mathf.Abs(ahead.z)
                ? (ahead.x <= 0f ? 0f : 180f)
                : (ahead.z >= 0f ? -90f : 90f);
        }

        Transform Placeholder(Transform root)
        {
            var view = new GameObject("U-Boat (placeholder)").transform;
            view.SetParent(root, false);
            view.position = new Vector3(root.position.x, SeaSurface.Level, root.position.z);

            float hullY = -HullSink * HullBeam;
            GameObject hull = UIFactory.CreatePrimitive3D(PrimitiveType.Capsule, Vector3.zero,
                Vector3.one, HullColor, keepCollider: false);
            hull.name = "Hull";
            hull.transform.SetParent(view, false);
            hull.transform.localPosition = new Vector3(0f, hullY, 0f) * _unit;
            hull.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            hull.transform.localScale = new Vector3(HullBeam, HullLength * 0.5f, HullBeam) * _unit;

            float deck = hullY + HullBeam * 0.5f;
            GameObject tower = UIFactory.CreatePrimitive3D(PrimitiveType.Cube, Vector3.zero,
                Vector3.one, HullColor, keepCollider: false);
            tower.name = "Conning Tower";
            tower.transform.SetParent(view, false);
            tower.transform.localPosition = new Vector3(-TowerAhead, deck + TowerHigh * 0.5f, 0f) * _unit;
            tower.transform.localScale = new Vector3(TowerLength, TowerHigh, TowerDeep) * _unit;

            GameObject gun = UIFactory.CreatePrimitive3D(PrimitiveType.Cylinder, Vector3.zero,
                Vector3.one, HullColor, keepCollider: false);
            gun.name = "Deck Gun";
            gun.transform.SetParent(view, false);
            gun.transform.localPosition = new Vector3(-GunAhead, deck + GunHigh * 0.5f, 0f) * _unit;
            gun.transform.localScale = new Vector3(0.6f, GunHigh * 0.5f, 0.6f) * _unit;
            return view;
        }

        static Bounds WorldBounds(Transform view)
        {
            var renderers = view.GetComponentsInChildren<Renderer>(true);
            var box = new Bounds(view.position, Vector3.one);
            bool any = false;

            foreach (Renderer renderer in renderers)
            {
                if (any) box.Encapsulate(renderer.bounds);
                else box = renderer.bounds;
                any = true;
            }
            return box;
        }
    }
}
