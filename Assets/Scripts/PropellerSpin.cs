using UnityEngine;

namespace MetalRaptors
{
    public class PropellerSpin : MonoBehaviour
    {
        [Tooltip("Spin speed in degrees per second about the plane's nose axis.")]
        public float degreesPerSecond = 720f;

        Transform _body;

        Vector3 _localCenter = Vector3.zero;

        void Start()
        {
            _body = transform.root;

            var mf = GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                _localCenter = transform.InverseTransformPoint(
                    mf.transform.TransformPoint(mf.sharedMesh.bounds.center));
            }
        }

        void Update()
        {
            Vector3 worldCenter = transform.TransformPoint(_localCenter);
            Vector3 worldAxis = _body != null ? _body.right : transform.right;
            transform.RotateAround(worldCenter, worldAxis, degreesPerSecond * Time.deltaTime);
        }
    }
}
