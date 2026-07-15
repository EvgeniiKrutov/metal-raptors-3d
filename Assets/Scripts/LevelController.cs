using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MetalRaptors
{
    /// <summary>
    /// Shared controller for the playable "Air Fights" levels (Level 1, Level 2). The scene
    /// sets <see cref="levelNumber"/> in the inspector so one script serves both.
    ///
    /// Builds a worldWidth x 1000 m flight arena at runtime: ground at the bottom (either the
    /// flat placeholder slab or the procedural Verdun-style land, see
    /// <see cref="ProceduralTerrain"/>), a hard ceiling at the top, horizontal wrap-around on
    /// the sides, and a glowing goal near the top. The player cube (coloured by the Garage
    /// selection) flies with the sibling repo's physics via <see cref="CubeController"/>. A
    /// perspective camera follows the cube, giving a 2.5D platformer feel. Touching the ground
    /// fails the level; reaching the goal completes it.
    /// </summary>
    public class LevelController : MonoBehaviour
    {
        [Tooltip("Which level this scene represents (1 for Level1, 2 for Level2, ...).")]
        [SerializeField] int levelNumber = 1;

        [Tooltip("Playable width of the arena in metres (the wrap-around distance).")]
        [SerializeField] float worldWidth = 1000f;

        [Tooltip("Replace the flat placeholder ground with the procedurally generated terrain.")]
        [SerializeField] bool proceduralTerrain;

        [Tooltip("Seed for the procedural terrain; the same seed always builds the same land.")]
        [SerializeField] int terrainSeed = 1916;

        // ---- World geometry (metres). X is centred on 0; Y runs from the ground up. ----
        const float WorldHeight = 1000f;
        const float GroundY = 0f;              // top surface of the flat ground
        const float WorldTop = WorldHeight;    //  1000, the hard ceiling
        float MinX => -worldWidth / 2f;
        float MaxX => worldWidth / 2f;

        const float CubeScale = 30f;
        const float CubeHalf = CubeScale / 2f;
        const float CameraDistance = 420f;     // camera sits this far back on -Z of the play plane
        const float PlayPlaneZ = 100f;         // the flight plane sits this far into the land (+Z), so a
                                               // falling cube lands on the land, not on its front cut edge
        const float CamZ = PlayPlaneZ - CameraDistance; // world Z the camera rides at
        const float BackdropZ = PlayPlaneZ + 150f; // shadow-receiving wall sits this far behind the play plane

        CubeController _cube;
        Transform _cubeTr;
        Transform _goal;
        Camera _cam;

        float _halfViewHeight;   // half the world height visible on screen (for camera clamping)
        float _lastCubeX;
        bool _gameOver;

        void Start()
        {
            var config = Resources.Load<CubeConfig>("CubeConfig");
            if (config == null) config = ScriptableObject.CreateInstance<CubeConfig>(); // safety fallback

            ConfigureShadows();
            BuildWorld();
            SpawnPlayer(config);
            SetupCamera();
            BuildHud();
        }

        /// <summary>
        /// The active URP asset ships with a 50 m shadow distance, but the camera sits
        /// <see cref="CameraDistance"/> (~420 m) back from the play plane, so the cube is far
        /// beyond that cutoff and never casts a visible shadow. Push the shadow distance out to
        /// cover the whole camera-to-play-plane depth (plus margin) so the main directional light
        /// actually shadows the cube. This is set at runtime so we don't disturb the shared RP
        /// asset used by every other scene.
        /// </summary>
        void ConfigureShadows()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                // Distance from the camera to the play plane, with headroom for the cube's climb.
                urp.shadowDistance = Mathf.Max(urp.shadowDistance, CameraDistance + 200f);
            }
        }

        // ---------------------------------------------------------------- world

        void BuildWorld()
        {
            if (proceduralTerrain)
            {
                // Verdun-style land; its TerrainCollider drives the fail on contact.
                ProceduralTerrain.Build(terrainSeed, worldWidth, CameraDistance, PlayPlaneZ);
            }
            else
            {
                // Solid flat ground at the bottom (its collider drives the fail on contact).
                UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                    new Vector3(0f, GroundY - 10f, 0f),
                    new Vector3(worldWidth + 200f, 20f, 400f),
                    new Color(0.20f, 0.22f, 0.16f));

                // Backdrop wall a little behind the play plane. The main light shines into +Z,
                // so it projects the cube's silhouette onto this wall, giving Level 1 a clearly
                // visible drop-shadow. Purely visual: it receives shadows but casts none and has
                // no collider (the camera looks straight down +Z, so it never occludes the cube).
                var backdrop = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                    new Vector3(0f, WorldHeight / 2f, BackdropZ),
                    new Vector3(worldWidth + 400f, WorldHeight + 200f, 10f),
                    new Color(0.16f, 0.17f, 0.20f), keepCollider: false);
                var backdropRenderer = backdrop.GetComponent<Renderer>();
                if (backdropRenderer != null)
                    backdropRenderer.shadowCastingMode = ShadowCastingMode.Off; // receive only
            }

            // Ceiling bar (visual only) so the player can see the hard cap.
            UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                new Vector3(0f, WorldTop, PlayPlaneZ),
                new Vector3(worldWidth + 200f, 8f, 60f),
                new Color(0.55f, 0.6f, 0.7f), emissive: true, keepCollider: false);

            // Glowing goal near the top; its X differs per level so Level 2 needs more steering.
            float goalX = levelNumber >= 2 ? 260f : 0f;
            var goalGo = UIFactory.CreatePrimitive3D(PrimitiveType.Sphere,
                new Vector3(goalX, WorldTop - 90f, PlayPlaneZ),
                Vector3.one * 90f,
                new Color(1f, 0.85f, 0.15f), emissive: true);
            goalGo.name = "Goal";
            var goalCol = goalGo.GetComponent<Collider>();
            if (goalCol != null) goalCol.isTrigger = true; // trigger -> OnTriggerEnter, not a crash
            _goal = goalGo.transform;
        }

        void SpawnPlayer(CubeConfig config)
        {
            var color = GameManager.Instance != null ? GameManager.Instance.SelectedColor : Color.white;

            var go = UIFactory.CreatePrimitive3D(PrimitiveType.Cube,
                new Vector3(0f, 150f, PlayPlaneZ), Vector3.one * CubeScale, color);
            go.name = "PlayerCube";

            // Make sure the cube throws a shadow onto whatever is below it: the procedural
            // terrain in Level 1, or the flat ground / backdrop wall in the placeholder levels.
            // See ConfigureShadows(), which extends the shadow distance so it actually shows.
            var cubeRenderer = go.GetComponent<Renderer>();
            if (cubeRenderer != null) cubeRenderer.shadowCastingMode = ShadowCastingMode.On;

            _cube = go.AddComponent<CubeController>();
            _cubeTr = go.transform;
            _cube.OnCrashed += OnCrashed;
            _cube.OnReachedGoal += OnReachedGoal;

            // Start heading straight up (velocity +Y => angle π/2), ceiling clamped for the cube's size.
            _cube.Initialize(config, Mathf.PI / 2f, MinX, MaxX, WorldTop - CubeHalf);
            _lastCubeX = _cubeTr.position.x;
        }

        void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null) return;

            _cam.orthographic = false; // perspective, per the chosen 2.5D look
            _cam.transform.rotation = Quaternion.identity; // look straight down +Z at the play plane

            if (proceduralTerrain)
            {
                // Overcast war-haze sky matching the fog, and enough draw distance to reach
                // the terrain's fogged far edge.
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = ProceduralTerrain.HazeColor;
                _cam.farClipPlane = 2200f;
            }

            // How much world height fits on screen at the play plane (z = 0), for vertical clamping.
            _halfViewHeight = CameraDistance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

            PositionCamera(instant: true);
        }

        // ---------------------------------------------------------------- loop

        void LateUpdate()
        {
            if (_goal != null) _goal.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
            if (_cam != null && _cubeTr != null) PositionCamera(instant: false);
        }

        void PositionCamera(bool instant)
        {
            Vector3 cubePos = _cubeTr.position;

            // Follow X directly, but snap (no smoothing) when the cube wraps around an edge.
            bool wrapped = Mathf.Abs(cubePos.x - _lastCubeX) > worldWidth * 0.5f;
            _lastCubeX = cubePos.x;

            // Clamp Y so the view never shows past the ground or the ceiling. With terrain,
            // the camera may sink low enough to reveal the dirt cut below the surface line.
            float minCamY = (proceduralTerrain ? ProceduralTerrain.CutRevealY : GroundY) + _halfViewHeight;
            float maxCamY = WorldTop - _halfViewHeight;
            if (minCamY > maxCamY) minCamY = maxCamY = (GroundY + WorldTop) * 0.5f;
            float targetY = Mathf.Clamp(cubePos.y, minCamY, maxCamY);

            var target = new Vector3(cubePos.x, targetY, CamZ);
            Vector3 cur = _cam.transform.position;

            if (instant || wrapped)
            {
                _cam.transform.position = target;
            }
            else
            {
                // Smooth follow (matches the sibling's camera lerp feel).
                float t = 1f - Mathf.Exp(-8f * Time.deltaTime);
                _cam.transform.position = new Vector3(
                    Mathf.Lerp(cur.x, target.x, t),
                    Mathf.Lerp(cur.y, target.y, t),
                    CamZ);
            }
        }

        // ---------------------------------------------------------------- outcomes

        void OnReachedGoal()
        {
            if (_gameOver) return;
            _gameOver = true;
            _cube.Stop();

            if (GameManager.Instance != null)
                GameManager.Instance.UnlockLevel(levelNumber + 1);

            var canvas = NewOverlay(new Color(0f, 0.08f, 0.02f, 0.8f));
            UIFactory.CreateText(canvas.transform, "MISSION COMPLETE", 90,
                new Vector2(0, 200), new Vector2(1400, 160), TextAnchor.MiddleCenter, FontStyle.Bold)
                .color = new Color(0.6f, 1f, 0.6f);

            float y = 0f;
            if (levelNumber == 1)
            {
                UIFactory.CreateButton(canvas.transform, "NEXT LEVEL", new Vector2(0, y),
                    () => SceneManager.LoadScene(SceneNames.Level2));
                y -= 110f;
            }
            UIFactory.CreateButton(canvas.transform, "BACK TO MENU", new Vector2(0, y),
                () => SceneManager.LoadScene(SceneNames.MainMenu));
        }

        void OnCrashed()
        {
            if (_gameOver) return;
            _gameOver = true;
            _cube.Stop();

            var canvas = NewOverlay(new Color(0.12f, 0f, 0f, 0.82f));
            UIFactory.CreateText(canvas.transform, "MISSION FAILED", 96,
                new Vector2(0, 200), new Vector2(1400, 170), TextAnchor.MiddleCenter, FontStyle.Bold)
                .color = new Color(1f, 0.45f, 0.4f);

            UIFactory.CreateButton(canvas.transform, "RETRY", new Vector2(0, 0f),
                () => SceneManager.LoadScene(SceneManager.GetActiveScene().name));
            UIFactory.CreateButton(canvas.transform, "BACK TO MENU", new Vector2(0, -110f),
                () => SceneManager.LoadScene(SceneNames.MainMenu));
        }

        // ---------------------------------------------------------------- ui

        void BuildHud()
        {
            var canvas = UIFactory.CreateCanvas($"Level{levelNumber} HUD");

            UIFactory.CreateText(canvas.transform, $"LEVEL {levelNumber}", 52,
                new Vector2(0, 480), new Vector2(1000, 90), TextAnchor.MiddleCenter, FontStyle.Bold);

            string mech = GameManager.Instance != null ? GameManager.Instance.SelectedMech : "Unknown";
            UIFactory.CreateText(canvas.transform, $"Piloting: {mech}", 30,
                new Vector2(0, 420), new Vector2(1200, 50));

            UIFactory.CreateText(canvas.transform,
                "A / D to steer  •  reach the goal  •  don't hit the ground", 28,
                new Vector2(0, -500), new Vector2(1600, 50));
        }

        Canvas NewOverlay(Color dimColor)
        {
            var canvas = UIFactory.CreateCanvas($"Level{levelNumber} Overlay");
            canvas.sortingOrder = 100;
            UIFactory.CreateBackground(canvas.transform, dimColor);
            return canvas;
        }
    }
}
