using UnityEngine;

namespace MetalRaptors
{
    public class PropellerSpin : MonoBehaviour
    {
        static readonly Vector3[] Corners =
        {
            new Vector3(-1f, -1f, -1f), new Vector3(1f, -1f, -1f),
            new Vector3(-1f,  1f, -1f), new Vector3(1f,  1f, -1f),
            new Vector3(-1f, -1f,  1f), new Vector3(1f, -1f,  1f),
            new Vector3(-1f,  1f,  1f), new Vector3(1f,  1f,  1f),
        };

        [Tooltip("Spin speed in degrees per second about the plane's nose axis.")]
        public float degreesPerSecond = 720f;

        [Tooltip("The plane body the spin axis is read from; PlaneFactory wires it at build time.")]
        public Transform axisSpace;

        [Tooltip("The nose direction in axisSpace's local frame — the body's own +X.")]
        public Vector3 axisInSpace = Vector3.right;

        Vector3 _localCenter = Vector3.zero;

        void Start() => _localCenter = SolveHub();

        Vector3 SolveHub()
        {
            Bounds hub = default;
            bool any = false;

            foreach (MeshFilter mf in GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;

                Bounds mesh = mf.sharedMesh.bounds;
                foreach (Vector3 corner in Corners)
                {
                    Vector3 local = transform.InverseTransformPoint(
                        mf.transform.TransformPoint(mesh.center + Vector3.Scale(mesh.extents, corner)));

                    if (any) hub.Encapsulate(local);
                    else { hub = new Bounds(local, Vector3.zero); any = true; }
                }
            }

            return any ? hub.center : Vector3.zero;
        }

        void Update()
        {
            Vector3 worldCenter = transform.TransformPoint(_localCenter);
            Vector3 worldAxis = axisSpace != null ? axisSpace.rotation * axisInSpace : transform.right;
            transform.RotateAround(worldCenter, worldAxis, degreesPerSecond * Time.deltaTime);
        }
    }
}
