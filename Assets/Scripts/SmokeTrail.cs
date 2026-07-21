using System.Collections.Generic;
using UnityEngine;

namespace MetalRaptors
{
    /// <summary>
    /// A code-built damage smoke trail: once <see cref="Arm"/> is called (when a plane's health
    /// drops below the danger threshold, see <see cref="CubeController"/> and
    /// <see cref="EnemyController"/>), it steadily coughs out dark-grey, half-transparent cubes just
    /// behind the plane's tail. Each puff tumbles as it drifts, shrinks toward nothing, and fades
    /// out — so the plane leaves a thinning ribbon of smoke streaming out behind it.
    ///
    /// Lives on the physics body (the top of the plane rig), so it knows the nose axis
    /// (<c>transform.right</c>, the flight heading) and emits from the opposite end. The puffs
    /// themselves are spawned in world space — NOT parented to the plane — so once born they hang in
    /// the air and fall behind as the plane flies on, instead of riding along with it.
    ///
    /// Purely cosmetic: like <see cref="Sparks"/> and <see cref="Explosion"/>, the puffs carry no
    /// collider and no rigidbody, so smoke can never brush the player, soak a bullet, or fail a
    /// level. Each puff animates itself and self-destructs when its life ends.
    ///
    /// Because the puffs are un-parented world-space objects, they don't die with the plane on
    /// their own: the emitter keeps a live-puff list and <see cref="Clear"/>s it — stopping emission
    /// and destroying every outstanding puff at once — when the plane is destroyed or its model is
    /// removed (see <see cref="EnemyController.Explode"/> and <see cref="CubeController.HideModel"/>).
    /// The emitter also clears itself in <see cref="OnDestroy"/>, so tearing down the plane
    /// GameObject takes the trailing smoke with it even without an explicit call.
    /// </summary>
    public class SmokeTrail : MonoBehaviour
    {
        // ---- emitter (on the plane body) ----
        const float EmitInterval = 0.05f;   // seconds between puffs — denser than before, so more cubes

        // ---- per-puff ----
        const float LifeMin = 0.9f;         // seconds a puff lives (randomised per puff)
        const float LifeMax = 1.5f;
        const float StartSizeFactor = 0.28f; // initial puff size relative to the plane's size
        const float StartSizeJitter = 0.4f;  // ± randomisation on the start size, so puffs vary
        const float MinScaleFactor = 0.2f;   // once a puff shrinks below this fraction of its start
                                             // size it's a barely-visible speck, so kill it outright
        const float DriftSpeedFactor = 0.25f; // backward drift speed relative to the plane's size
        const float RiseSpeed = 8f;          // slow upward buoyancy (m/s) as the smoke lifts and dissipates
        const float SpinMin = 40f;           // tumble rate (deg/s), randomised and signed per puff
        const float SpinMax = 140f;
        const float Opacity = 0.6f;          // requested peak opacity of a fresh puff

        // Dark charcoal grey, per the request; alpha carries the 0.6 opacity (needs the transparent material).
        static readonly Color SmokeColor = new Color(0.10f, 0.10f, 0.11f, Opacity);

        bool _armed;
        bool _cleared;      // Clear() has run: stop emitting for good
        float _size;        // the plane's on-screen size, sizing the whole trail
        float _emitTimer;
        // Live puffs this emitter has spawned, so they can all be destroyed when the plane dies.
        // Puffs remove themselves from here when they self-destruct (see UnregisterFromEmitter).
        readonly List<SmokeTrail> _puffs = new List<SmokeTrail>();

        // ---- puff-instance state (each spawned cube carries its own SmokeTrail as the animator) ----
        bool _isPuff;
        SmokeTrail _emitter; // the plane's emitter that spawned this puff, so it can unregister on death
        Vector3 _velocity;
        Vector3 _spinAxis;
        float _spinRate;    // deg/s
        float _age;
        float _life;
        float _startScale;
        Material _mat;

        /// <summary>
        /// Turns the trail on. Idempotent — repeated calls (each frame health stays low) do nothing
        /// after the first, so the plane just keeps smoking. There is no disarm: once a plane is
        /// hurt enough to smoke, it smokes until it is gone.
        /// </summary>
        /// <param name="planeSize">The plane's on-screen size, so the smoke scales to the model.</param>
        public void Arm(float planeSize)
        {
            _armed = true;
            _size = Mathf.Max(1f, planeSize);
        }

        /// <summary>
        /// Stops the trail and removes every puff it has spawned so far, immediately. Called when the
        /// plane is destroyed or its model is removed, so a killed plane leaves no smoke hanging in
        /// the air. Idempotent and safe to call whether or not the trail was ever armed.
        /// </summary>
        public void Clear()
        {
            _armed = false;
            _cleared = true;
            for (int i = _puffs.Count - 1; i >= 0; i--)
                if (_puffs[i] != null) Destroy(_puffs[i].gameObject);
            _puffs.Clear();
        }

        void Update()
        {
            if (_isPuff) { AnimatePuff(); return; }
            if (!_armed || _cleared) return;

            _emitTimer -= Time.deltaTime;
            if (_emitTimer <= 0f)
            {
                _emitTimer = EmitInterval;
                EmitPuff();
            }
        }

        /// <summary>Tears the trailing smoke down with the plane even without an explicit
        /// <see cref="Clear"/> — e.g. when the enemy GameObject is destroyed on Explode.</summary>
        void OnDestroy()
        {
            if (!_isPuff) Clear();     // emitter: take its outstanding puffs with it
            else if (_emitter != null) _emitter.Unregister(this); // puff: drop out of the emitter's list
        }

        /// <summary>A puff removes itself from the emitter's live list as it self-destructs, so the
        /// list never holds destroyed puffs.</summary>
        void Unregister(SmokeTrail puff) => _puffs.Remove(puff);

        /// <summary>Spawns one world-space puff from the middle of the plane.</summary>
        void EmitPuff()
        {
            // Smoke billows out from the plane's centre (its position). The body's local +X is the
            // nose (flight heading), so its opposite is the way the smoke drifts off behind the plane.
            Vector3 back = -transform.right;
            Vector3 spawn = transform.position;

            float scale = _size * StartSizeFactor * Random.Range(1f - StartSizeJitter, 1f + StartSizeJitter);
            var go = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                spawn, Vector3.one * scale, SmokeColor, emissive: false, keepCollider: false);
            go.name = "Smoke";
            // Make the base material honour the 0.6 alpha (CreatePrimitive3D's is opaque by default).
            var renderer = go.GetComponent<Renderer>();
            UIFactory.MakeTransparent(renderer.sharedMaterial); // unique per puff, see CreatePrimitive3D
            // Start at a random tumble so the cube doesn't read as axis-aligned.
            go.transform.rotation = Random.rotation;

            var puff = go.AddComponent<SmokeTrail>();
            puff._isPuff = true;
            puff._emitter = this;
            puff._startScale = scale;
            puff._life = Random.Range(LifeMin, LifeMax);
            puff._mat = renderer.sharedMaterial;
            _puffs.Add(puff); // track it so the plane's death can sweep it up
            // Drift back along the tail (in the play plane, Z flat like everything else) and rise a
            // little, so the trail streams out behind the plane and lifts as it thins.
            Vector2 drift = new Vector2(back.x, back.y).normalized * (_size * DriftSpeedFactor)
                            * Random.Range(0.7f, 1.3f);
            puff._velocity = new Vector3(drift.x, drift.y + RiseSpeed, 0f);
            puff._spinAxis = Random.onUnitSphere;
            puff._spinRate = Random.Range(SpinMin, SpinMax) * (Random.value < 0.5f ? -1f : 1f);
        }

        /// <summary>Per-frame animation of one puff: drift, tumble, shrink, and fade, then die.</summary>
        void AnimatePuff()
        {
            _age += Time.deltaTime;
            if (_age >= _life)
            {
                Destroy(gameObject);
                return;
            }

            float t = _age / _life; // 0 -> 1 over the puff's life

            transform.position += _velocity * Time.deltaTime;
            transform.Rotate(_spinAxis, _spinRate * Time.deltaTime, Space.World);
            // Shrink as it trails off.
            float scale = Mathf.Lerp(_startScale, _startScale * 0.1f, t);
            // Once it's shrunk to a tiny speck, destroy it outright rather than letting it linger.
            if (scale <= _startScale * MinScaleFactor)
            {
                Destroy(gameObject);
                return;
            }
            transform.localScale = Vector3.one * scale;

            // Fade the alpha out over the life so the ribbon dissolves into the air.
            if (_mat != null)
            {
                var c = SmokeColor;
                c.a = Opacity * (1f - t);
                _mat.SetColor("_BaseColor", c);
            }
        }
    }
}
