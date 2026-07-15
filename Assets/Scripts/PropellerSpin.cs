using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// Spins a propeller transform about the axis normal to its blade disc at a constant rate,
    /// giving the parked/flying Fokker Dr.1 a turning prop. Attached to <c>propPivot</c> (which
    /// carries <c>propBlades</c>) by <see cref="LevelController"/> when it spawns the plane. The
    /// disc normal is the nose direction, so the blades turn in their own plane rather than
    /// tumbling. The axis is derived from the blade mesh at startup (see <see cref="Start"/>)
    /// instead of assumed, so it stays correct however the model is oriented.
    /// </summary>
    public class PropellerSpin : MonoBehaviour
    {
        [Tooltip("Spin speed in degrees per second about the blade disc's normal axis.")]
        public float degreesPerSecond = 720f; // ~2 rev/s: individual blades still read as they turn.

        // Local axis to spin about: the blade disc's normal. Set from the mesh in Start; the
        // fallback matches this model's nose direction (local +Y after the stand-up rotation).
        Vector3 _spinAxis = Vector3.up;

        // Local offset from this transform's origin to the blade disc's center, so the blades
        // rotate about their own hub instead of orbiting the (off-center) pivot origin.
        Vector3 _localCenter = Vector3.zero;

        void Start()
        {
            // The blade disc is thin along its normal and wide across its face, so the shortest
            // extent of the propeller mesh bounds is the spin (nose) axis. Deriving it from the
            // geometry keeps the spin in the correct plane regardless of the model's orientation.
            var mf = GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 s = mf.sharedMesh.bounds.size;
                Vector3 meshAxis;
                if (s.x <= s.y && s.x <= s.z) meshAxis = Vector3.right;
                else if (s.y <= s.x && s.y <= s.z) meshAxis = Vector3.up;
                else meshAxis = Vector3.forward;

                // The thin axis is in the mesh's own space; the mesh node may carry FBX-import
                // rotations relative to this pivot, so map it mesh -> world -> this transform's
                // local space before spinning about it.
                _spinAxis = transform.InverseTransformDirection(
                    mf.transform.TransformDirection(meshAxis)).normalized;

                // bounds.center is in the mesh's local space; map it into this transform's space
                // so we can spin around the actual hub of the blades.
                _localCenter = transform.InverseTransformPoint(
                    mf.transform.TransformPoint(mf.sharedMesh.bounds.center));
            }
        }

        void Update()
        {
            // Rotate about the blade disc's center (the hub), not the pivot origin, so the prop
            // spins in place. Recomputing the world axis/center from the transform each frame
            // keeps them following the plane as it banks.
            Vector3 worldCenter = transform.TransformPoint(_localCenter);
            Vector3 worldAxis = transform.TransformDirection(_spinAxis);
            transform.RotateAround(worldCenter, worldAxis, degreesPerSecond * Time.deltaTime);
        }
    }
}
