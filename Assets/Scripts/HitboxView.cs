using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MetalRaptors
{
    [DefaultExecutionOrder(2000)]
    public class HitboxView : MonoBehaviour
    {
        const string ShaderName = "Hidden/HitboxWire";
        const float LineWidthPixels = 2.5f;
        const float RescanInterval = 0.5f;
        const float MinDepth = 0.01f;

        static readonly Color PlayerColor = new Color(0.30f, 1f, 0.45f, 0.9f);
        static readonly Color EnemyColor = new Color(1f, 0.28f, 0.24f, 0.9f);

        static readonly Vector3[] UnitBox = HullOutline.Box(new Bounds(Vector3.zero, Vector3.one));
        static readonly Dictionary<Mesh, Vector3[]> Outlines = new Dictionary<Mesh, Vector3[]>();

        struct Target
        {
            public Collider Collider;
            public Color Color;
        }

        static HitboxView _instance;

        readonly List<Target> _targets = new List<Target>();
        readonly List<Collider> _found = new List<Collider>();
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _indices = new List<int>();

        Mesh _mesh;
        Material _material;
        float _rescan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("HitboxView");
            go.AddComponent<HitboxView>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"HitboxView: {ShaderName} not found; hitboxes cannot be drawn.");
                enabled = false;
                return;
            }

            _material = new Material(shader) { hideFlags = HideFlags.DontSave };
            _mesh = new Mesh { name = "Hitbox Wire", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }

        void LateUpdate()
        {
            if (!DevCheats.ShowHitboxes)
            {
                _targets.Clear();
                _rescan = 0f;
                return;
            }

            _rescan -= Time.unscaledDeltaTime;
            if (_rescan <= 0f)
            {
                _rescan = RescanInterval;
                Rescan();
            }

            Camera cam = Camera.main;
            if (cam == null) return;

            Build(cam);
            if (_indices.Count == 0) return;

            var draw = new RenderParams(_material)
            {
                camera = cam,
                worldBounds = _mesh.bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderMesh(draw, _mesh, 0, Matrix4x4.identity);
        }

        void Rescan()
        {
            _targets.Clear();

            foreach (CubeController player in FindObjectsByType<CubeController>(FindObjectsSortMode.None))
                Collect(player.gameObject, PlayerColor);

            foreach (EnemyController enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                Collect(enemy.gameObject, EnemyColor);
        }

        void Collect(GameObject root, Color color)
        {
            root.GetComponentsInChildren(false, _found);
            foreach (Collider col in _found)
            {
                if (col.isTrigger || col.gameObject.layer != PlaneFactory.PlaneLayer) continue;
                _targets.Add(new Target { Collider = col, Color = color });
            }
        }

        void Build(Camera cam)
        {
            _vertices.Clear();
            _colors.Clear();
            _indices.Clear();

            Transform view = cam.transform;
            Vector3 eye = view.position;
            Vector3 forward = view.forward;
            bool flat = cam.orthographic;
            float perPixel = flat
                ? 2f * cam.orthographicSize / cam.pixelHeight
                : 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / cam.pixelHeight;
            float half = perPixel * LineWidthPixels * 0.5f;

            foreach (Target target in _targets)
            {
                Collider col = target.Collider;
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;
                if (!TryOutline(col, out Vector3[] edges, out Matrix4x4 toWorld)) continue;

                for (int i = 0; i + 1 < edges.Length; i += 2)
                    AddLine(toWorld.MultiplyPoint3x4(edges[i]), toWorld.MultiplyPoint3x4(edges[i + 1]),
                        target.Color, eye, forward, flat, half);
            }

            _mesh.Clear();
            if (_indices.Count == 0) return;

            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetIndices(_indices, MeshTopology.Triangles, 0);
        }

        void AddLine(Vector3 a, Vector3 b, Color color, Vector3 eye, Vector3 forward, bool flat, float half)
        {
            Vector3 along = b - a;
            float length = along.magnitude;
            if (length < 1e-5f) return;
            along /= length;

            float wa = flat ? half : half * Mathf.Max(MinDepth, Vector3.Dot(a - eye, forward));
            float wb = flat ? half : half * Mathf.Max(MinDepth, Vector3.Dot(b - eye, forward));

            Vector3 sight = flat ? forward : (a + b) * 0.5f - eye;
            Vector3 side = Vector3.Cross(along, sight);
            if (side.sqrMagnitude < 1e-10f) side = Vector3.Cross(along, Vector3.up);
            if (side.sqrMagnitude < 1e-10f) side = Vector3.Cross(along, Vector3.right);
            side.Normalize();

            Vector3 start = a - along * wa;
            Vector3 end = b + along * wb;

            int first = _vertices.Count;
            _vertices.Add(start - side * wa);
            _vertices.Add(start + side * wa);
            _vertices.Add(end + side * wb);
            _vertices.Add(end - side * wb);
            for (int i = 0; i < 4; i++) _colors.Add(color);

            _indices.Add(first);
            _indices.Add(first + 1);
            _indices.Add(first + 2);
            _indices.Add(first);
            _indices.Add(first + 2);
            _indices.Add(first + 3);
        }

        static bool TryOutline(Collider col, out Vector3[] edges, out Matrix4x4 toWorld)
        {
            toWorld = col.transform.localToWorldMatrix;

            if (col is BoxCollider box)
            {
                toWorld *= Matrix4x4.TRS(box.center, Quaternion.identity, box.size);
                edges = UnitBox;
                return true;
            }

            if (col is MeshCollider meshCollider && meshCollider.sharedMesh != null)
            {
                edges = OutlineOf(meshCollider.sharedMesh);
                return edges.Length > 0;
            }

            edges = null;
            return false;
        }

        static Vector3[] OutlineOf(Mesh mesh)
        {
            if (Outlines.TryGetValue(mesh, out Vector3[] edges)) return edges;

            edges = mesh.isReadable ? HullOutline.Build(mesh.vertices) : HullOutline.Box(mesh.bounds);
            Outlines[mesh] = edges;
            return edges;
        }
    }
}
