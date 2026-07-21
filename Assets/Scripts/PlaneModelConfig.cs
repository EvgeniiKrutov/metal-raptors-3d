using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// Everything <see cref="LevelController.BuildPlaneModel"/> needs to load and orient one
    /// aircraft model, so a new plane can be dropped in by adding an entry to
    /// <see cref="PlaneModels"/> instead of editing the spawn code. Different FBX exports sit at
    /// different rest orientations, scales, and node names, so those are all data here rather than
    /// the Fokker-specific constants they used to be baked in as.
    ///
    /// The player and enemies pick a config by name (see <see cref="PlaneModels"/>); the same
    /// config drives both the upright player and the mirrored enemy, so the mirror handling stays
    /// in <c>BuildPlaneModel</c> and out of the per-plane data.
    /// </summary>
    public class PlaneModelConfig
    {
        /// <summary>Resources name of the FBX model to load (e.g. "sopwith_camel").</summary>
        public string resourceName;

        /// <summary>
        /// Euler angles that stand the exported (usually flat-lying) model upright with its nose
        /// pointing along the +X heading direction. For the Fokker this is (90, -90, 0): +90° X
        /// tips it upright, -90° Y swings the nose forward. A differently exported model can need a
        /// different rotation, which is exactly why it lives here.
        /// </summary>
        public Vector3 standUpEuler;

        /// <summary>
        /// True if, after <see cref="standUpEuler"/>, the model sits wheels-up and needs a 180° roll
        /// about the nose (the flight axis) to land wheels-down. Applied to the upright player only;
        /// the mirrored enemy drops it (its 180° heading spin flips it belly-down instead).
        /// </summary>
        public bool rollWheelsDown;

        /// <summary>On-screen size in metres of the model's longest side after normalisation.</summary>
        public float onScreenSize;

        /// <summary>
        /// Name of the propeller pivot node (parent of the blades) to spin. Falls back to
        /// <see cref="propBladesNode"/> if this node is missing on the model.
        /// </summary>
        public string propPivotNode;

        /// <summary>Name of the propeller blade node, used to find the muzzle/prop disc and as the
        /// spin fallback when <see cref="propPivotNode"/> is absent.</summary>
        public string propBladesNode;
    }

    /// <summary>
    /// Registry of the aircraft the game can fly. Both models currently share the same FBX
    /// conventions (flat export, same propeller node names), so the two entries differ only in
    /// which mesh they load — but keeping the orientation/scale/node fields per-plane means the next
    /// model that exports differently is a data change, not a code change. Who knows how the next
    /// Sopwith re-export, or a fresh plane, will come out of the modeller.
    /// </summary>
    public static class PlaneModels
    {
        /// <summary>The Fokker Dr.1 triplane. Flown by the enemy fighters.</summary>
        public static readonly PlaneModelConfig Fokker = new PlaneModelConfig
        {
            resourceName   = "fokker_dr1",
            standUpEuler   = new Vector3(90f, -90f, 0f),
            rollWheelsDown = true,
            onScreenSize   = 60f,
            propPivotNode  = "propPivot",
            propBladesNode = "propBlades",
        };

        /// <summary>The Sopwith Camel. Flown by the player.</summary>
        public static readonly PlaneModelConfig Sopwith = new PlaneModelConfig
        {
            resourceName   = "sopwith_camel",
            standUpEuler   = new Vector3(90f, -90f, 0f),
            rollWheelsDown = true,
            onScreenSize   = 60f,
            propPivotNode  = "propPivot",
            propBladesNode = "propBlades",
        };
    }
}
