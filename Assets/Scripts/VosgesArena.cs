using UnityEngine;

namespace MetalRaptors
{
    public class VosgesArena
    {
        const float U = PlaneModelConfig.UnitsPerMeter;

        public const string ModelResource = "objects/villages/village_arena";
        public const float WorldWidth = 2100f;
        public const int MarginChunks = 3;
        public const float HillMargin = 12f * U;

        const string RoadNode = "road_main";
        const float HillRamp = 50f * U;
        const float MarginCap = 3f * U;
        const float SolidTop = 0.3f;

        static readonly Quaternion Turn = Quaternion.Euler(0f, 180f, 0f);

        ModelCover _cover;
        ModelCover _solid;
        Matrix4x4 _toLocal;

        public GameObject Prefab { get; private set; }
        public Vector3 Origin { get; private set; }
        public Quaternion Rotation => Turn;
        public float Scale => U;
        public Rect Footprint { get; private set; }
        public float RoadZ { get; private set; }
        public float RoadHalfWidth { get; private set; }
        public float RoadLift { get; private set; }
        public WalkGraph Graph { get; private set; }

        public static VosgesArena Load(float worldWidth, float roadZ, float groundY)
        {
            var prefab = Resources.Load<GameObject>(ModelResource);
            if (prefab == null)
            {
                Debug.LogError($"VosgesArena: {ModelResource} not found in Resources.");
                return null;
            }

            GameObject view = ModelProbe.Open(prefab);
            try
            {
                if (!ModelProbe.Bounds(view, out Bounds box))
                {
                    Debug.LogError($"VosgesArena: {ModelResource} has no renderers.");
                    return null;
                }

                Transform road = PlaneFactory.FindDeep(view.transform, RoadNode);
                if (road == null || !road.TryGetComponent(out Renderer roadRenderer))
                {
                    Debug.LogError($"VosgesArena: {ModelResource} has no {RoadNode} mesh.");
                    return null;
                }

                ModelCover cover = ModelCover.Build(view, box);
                ModelCover solid = ModelCover.Build(view, box, SolidTop);
                if (cover == null || solid == null)
                {
                    Debug.LogError($"VosgesArena: {ModelResource} meshes are not readable. "
                                   + "Turn on Read/Write in its import settings.");
                    return null;
                }

                Bounds roadBox = roadRenderer.bounds;
                Vector3 roadCentre = ModelProbe.Local(roadBox.center);
                Vector3 origin = new Vector3(worldWidth * 0.5f + box.center.x * U, groundY,
                    roadZ + roadCentre.z * U);

                var arena = new VosgesArena
                {
                    _cover = cover,
                    _solid = solid,
                    _toLocal = Matrix4x4.TRS(origin, Turn, Vector3.one * U).inverse,
                    Prefab = prefab,
                    Origin = origin,
                    Footprint = Rect.MinMaxRect(origin.x - box.max.x * U, origin.z - box.max.z * U,
                        origin.x - box.min.x * U, origin.z - box.min.z * U),
                    RoadZ = roadZ,
                    RoadHalfWidth = roadBox.extents.z * U,
                    RoadLift = Mathf.Max(0f, ModelProbe.Local(roadBox.max).y * U),
                    Graph = VosgesVillage.BuildGraph(view, RoadNode),
                };
                return arena;
            }
            finally
            {
                ModelProbe.Close(view);
            }
        }

        public bool Covered(float x, float z) => Hit(_cover, x, z);

        public bool Occupied(float x, float z, float margin)
        {
            float reach = Mathf.Min(margin, MarginCap);
            if (Mathf.Abs(z - RoadZ) < RoadHalfWidth + reach) return true;

            return Solid(x, z, margin);
        }

        public bool Solid(float x, float z, float margin)
        {
            float reach = Mathf.Min(margin, MarginCap);
            return Hit(_solid, x, z) || Hit(_solid, x + reach, z) || Hit(_solid, x - reach, z)
                   || Hit(_solid, x, z + reach) || Hit(_solid, x, z - reach);
        }

        bool Hit(ModelCover cover, float x, float z)
        {
            if (!Footprint.Contains(new Vector2(x, z))) return false;
            return cover.Covered(_toLocal.MultiplyPoint3x4(new Vector3(x, Origin.y, z)));
        }

        public float HillMask(float x, float z)
        {
            float outside = Mathf.Max(Footprint.xMin - x, x - Footprint.xMax);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HillMargin, HillMargin + HillRamp, outside));
        }
    }
}
