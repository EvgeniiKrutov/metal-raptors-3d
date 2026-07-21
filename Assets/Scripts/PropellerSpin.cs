using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// Spins a propeller transform about the plane's nose axis at a constant rate, so the blades
    /// turn in their own disc plane rather than tumbling. Attached to <c>propPivot</c> (which
    /// carries <c>propBlades</c>) by <see cref="LevelController"/> when it spawns the plane.
    ///
    /// The spin axis is the plane body's forward/nose direction — a known fact of the rig, not a
    /// guess. Every plane is oriented at spawn so its nose points along the heading (the body's
    /// local +X; see <see cref="LevelController.BuildPlaneModel"/> and CubeController's heading-0 =
    /// +X flight), so the nose axis holds for any model regardless of how its propeller mesh is
    /// shaped or exported. (The old code derived the axis from the blade mesh's shortest bounds
    /// extent, which assumed a symmetric disc and picked the wrong axis for a flat two-blade prop
    /// like the Sopwith Camel's, making it sweep a slanted cone.)
    /// </summary>
    public class PropellerSpin : MonoBehaviour
    {
        [Tooltip("Spin speed in degrees per second about the plane's nose axis.")]
        public float degreesPerSecond = 720f; // ~2 rev/s: individual blades still read as they turn.

        // The plane body whose nose (local +X) is the spin axis. This is the top of the rig — the
        // physics body LevelController yaws to the heading — so reading its +X each frame keeps the
        // spin axis following the plane as it banks.
        Transform _body;

        // Local offset from this transform's origin to the blade disc's center, so the blades
        // rotate about their own hub instead of orbiting the (off-center) pivot origin.
        Vector3 _localCenter = Vector3.zero;

        void Start()
        {
            // Walk up to the top of the rig: the physics body that carries the heading. Its local
            // +X is the nose direction the whole model was oriented to point along at spawn.
            _body = transform.root;

            // Center the spin on the blade hub (the mesh bounds center), not the pivot origin, so
            // the prop spins in place instead of orbiting. This is purely positional and doesn't
            // affect the tumble; the axis is what matters and it's the nose direction below.
            var mf = GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                _localCenter = transform.InverseTransformPoint(
                    mf.transform.TransformPoint(mf.sharedMesh.bounds.center));
            }
        }

        void Update()
        {
            // Spin about the nose axis (the body's world +X) through the blade hub, so the disc
            // stays perpendicular to the nose and the blades turn flat in their own plane. Both are
            // recomputed from the transforms each frame so they follow the plane as it banks.
            Vector3 worldCenter = transform.TransformPoint(_localCenter);
            Vector3 worldAxis = _body != null ? _body.right : transform.right;
            transform.RotateAround(worldCenter, worldAxis, degreesPerSecond * Time.deltaTime);
        }
    }
}
