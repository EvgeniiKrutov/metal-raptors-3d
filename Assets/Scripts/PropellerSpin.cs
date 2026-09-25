using System.Collections.Generic;
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

        public bool straighten;

        const int SpanIterations = 16;

        Vector3 _localCenter = Vector3.zero;

        Vector3 WorldAxis => axisSpace != null ? axisSpace.rotation * axisInSpace : transform.right;

        void Start()
        {
            _localCenter = SolveHub();
            if (straighten) Straighten();
        }

        void Straighten()
        {
            Vector3 axis = WorldAxis;
            Vector3 face = SolveFace(axis);
            if (face == Vector3.zero) return;
            if (Vector3.Dot(face, axis) < 0f) face = -face;

            Quaternion.FromToRotation(face, axis).ToAngleAxis(out float angle, out Vector3 around);
            transform.RotateAround(transform.TransformPoint(_localCenter), around, angle);
        }

        Vector3 SolveFace(Vector3 axis)
        {
            var points = new List<Vector3>();
            foreach (MeshFilter mf in GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                foreach (Vector3 v in mf.sharedMesh.vertices) points.Add(mf.transform.TransformPoint(v));
            }
            if (points.Count < 3) return Vector3.zero;

            Vector3 mean = Vector3.zero;
            foreach (Vector3 p in points) mean += p;
            mean /= points.Count;

            Vector3 far = Vector3.zero;
            foreach (Vector3 p in points)
                if ((p - mean).sqrMagnitude > far.sqrMagnitude) far = p - mean;

            Vector3 span = PrincipalAxis(points, mean, far, Vector3.zero);
            Vector3 chord = PrincipalAxis(points, mean, Vector3.Cross(span, axis), span);
            Vector3 face = Vector3.Cross(span, chord);
            return face.sqrMagnitude > 0.0001f ? face.normalized : Vector3.zero;
        }

        static Vector3 PrincipalAxis(List<Vector3> points, Vector3 mean, Vector3 seed, Vector3 excluded)
        {
            Vector3 axis = Vector3.ProjectOnPlane(seed, excluded).normalized;

            for (int i = 0; i < SpanIterations; i++)
            {
                Vector3 next = Vector3.zero;
                foreach (Vector3 p in points)
                {
                    Vector3 d = Vector3.ProjectOnPlane(p - mean, excluded);
                    next += d * Vector3.Dot(d, axis);
                }
                if (next.sqrMagnitude < 1e-12f) break;
                axis = next.normalized;
            }

            return axis;
        }

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
            transform.RotateAround(worldCenter, WorldAxis, degreesPerSecond * Time.deltaTime);
        }
    }
}
