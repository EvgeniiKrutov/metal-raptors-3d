using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MetalRaptors
{
    /// <summary>
    /// Controller for the endless campaign levels: the plane flies left to right forever over
    /// terrain streamed in chunks (<see cref="CampaignTerrain"/>), the camera only ever scrolls
    /// forward, and a hard wall at the screen's left edge blocks turning back the way the
    /// ceiling blocks climbing. No enemies spawn yet — the definition's distance-keyed waves
    /// are configuration only. Full design: docs/campaign.md.
    /// </summary>
    public class CampaignLevelController : MonoBehaviour
    {
        [Tooltip("Which campaign level this scene represents; everything else comes from its " +
                 "definition in the CampaignLevels registry.")]
        [SerializeField] int levelNumber = 1;

        const float WorldTop = 900f;           // hard ceiling, as in the fixed levels
        const float CubeHalf = 15f;            // half the plane body's nominal size
        const float CameraDistance = 420f;     // camera sits this far back on -Z of the play plane
        const float PlayPlaneZ = 100f;         // flight plane depth into the land (+Z)
        const float CamZ = PlayPlaneZ - CameraDistance;
        const float StartX = 0f;
        const float SpawnY = 150f;

        CampaignDefinition _level;
        CubeController _cube;
        PlaneShooter _shooter;
        Transform _cubeTr;
        Camera _cam;
        CampaignTerrain _terrain;
        Transform _ceilingBar;

        HealthBar _healthBar;
        Text _distanceText;

        float _halfViewHeight;
        float _halfViewWidth;
        Vector3 _camBasePos;
        bool _gameOver;
        float _furthestX = StartX;
        int _nextWave;

        float Distance => Mathf.Max(0f, _furthestX - StartX);

        void Start()
        {
            _level = CampaignLevels.ForNumber(levelNumber);

            var config = Resources.Load<PlayerConfig>("PlayerConfig");
            if (config == null) config = ScriptableObject.CreateInstance<PlayerConfig>(); // safety fallback

            ConfigureShadows();
            _terrain = CampaignTerrain.Begin(_level.seed, _level.daytime, _level.weather,
                CameraDistance, PlayPlaneZ, StartX);
            SpawnPlayer(config);
            SetupCamera();
            BuildHud();
        }

        /// <summary>Same runtime shadow-distance extension as the fixed levels: the camera sits
        /// far beyond the RP asset's default cutoff, so push it out to keep plane shadows.</summary>
        void ConfigureShadows()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.shadowDistance = Mathf.Max(urp.shadowDistance, CameraDistance + 200f);
        }

        void SpawnPlayer(PlayerConfig config)
        {
            var go = new GameObject("PlayerPlane");
            go.transform.position = new Vector3(StartX, SpawnY, PlayPlaneZ);

            var planeModel = PlaneModels.Sopwith;
            var model = PlaneFactory.BuildPlaneModel(go.transform, planeModel);

            _cube = go.AddComponent<CubeController>();
            _cubeTr = go.transform;
            _cube.OnCrashed += OnCrashed;
            _cube.OnShotDown += OnShotDown;

            // Heading straight right; no side bounds (the world is endless to the right and the
            // hard wall guards the left), ceiling clamped for the plane's size.
            _cube.Initialize(config, 0f, float.MinValue, float.MaxValue,
                WorldTop - CubeHalf, 0f, hardLeftWall: true);

            var muzzle = PlaneFactory.MountMuzzle(go, model, planeModel);
            _shooter = go.AddComponent<PlaneShooter>();
            _shooter.Initialize(config, muzzle, go.GetComponentInChildren<Collider>());
        }

        void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null) return;

            _cam.orthographic = false;
            _cam.transform.rotation = Quaternion.identity; // look straight down +Z at the play plane

            switch (_level.daytime)
            {
                case Daytime.Midday: MiddaySky.Apply(_cam, _level.weather); break;
                case Daytime.Evening: EveningSky.Apply(_cam, _level.weather); break;
                case Daytime.Night: NightSky.Apply(_cam, _level.weather); break;
                default: MorningSky.Apply(_cam, _level.weather); break;
            }
            _cam.farClipPlane = 2200f;

            _halfViewHeight = CameraDistance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            _halfViewWidth = _halfViewHeight * _cam.aspect;

            PositionCamera(instant: true);

            if (_level.clouds != null)
                CloudSystem.Begin(_cam, _level.daytime, _level.weather, _level.clouds, PlayPlaneZ);
        }

        void LateUpdate()
        {
            if (_cubeTr == null) return;

            _furthestX = Mathf.Max(_furthestX, _cubeTr.position.x);
            if (_cam != null) PositionCamera(instant: false);

            // The wall rides the camera's left view edge and only ever advances (SetLeftWall
            // ratchets), so turning back presses the plane against the screen edge.
            if (_cube != null) _cube.SetLeftWall(_camBasePos.x - _halfViewWidth + CubeHalf);

            if (_terrain != null) _terrain.UpdateStreaming(_camBasePos.x);
            if (_ceilingBar != null)
                _ceilingBar.position = new Vector3(_camBasePos.x, WorldTop, PlayPlaneZ);

            UpdateHud();
            CheckWaves();
        }

        void PositionCamera(bool instant)
        {
            Vector3 cubePos = _cubeTr.position;

            // Clamp Y as in the fixed levels; X only ever ratchets forward — flying back leaves
            // the camera (and with it the wall) where it is.
            float minCamY = ProceduralTerrain.CutRevealY + _halfViewHeight;
            float maxCamY = WorldTop - _halfViewHeight;
            if (minCamY > maxCamY) minCamY = maxCamY = WorldTop * 0.5f;
            float targetY = Mathf.Clamp(cubePos.y, minCamY, maxCamY);

            if (instant)
            {
                _camBasePos = new Vector3(Mathf.Max(StartX, cubePos.x), targetY, CamZ);
            }
            else
            {
                float t = 1f - Mathf.Exp(-8f * Time.deltaTime);
                float x = Mathf.Max(_camBasePos.x, Mathf.Lerp(_camBasePos.x, cubePos.x, t));
                _camBasePos = new Vector3(x, Mathf.Lerp(_camBasePos.y, targetY, t), CamZ);
            }
            _cam.transform.position = _camBasePos;
        }

        /// <summary>Waves are configuration only for now: due waves are consumed and noted, and
        /// actual spawning arrives together with the enemies (docs/campaign.md).</summary>
        void CheckWaves()
        {
            if (_gameOver || _level.waves == null) return;
            while (_nextWave < _level.waves.Length && Distance >= _level.waves[_nextWave].distance)
            {
                Debug.Log($"CampaignLevelController: wave at {_level.waves[_nextWave].distance} m " +
                          "due — enemy spawning not implemented yet.");
                _nextWave++;
            }
        }

        // ---------------------------------------------------------------- outcomes

        void OnShotDown()
        {
            if (_shooter != null) _shooter.Stop();
        }

        void OnCrashed()
        {
            if (_gameOver) return;
            _gameOver = true;
            _cube.Stop();
            if (_shooter != null) _shooter.Stop();

            // Freeze immediately, but let the crash explosion play out in full before the fail
            // screen covers it (docs/effects.md).
            StartCoroutine(ShowFailScreenAfter(Explosion.Duration));
        }

        IEnumerator ShowFailScreenAfter(float delay)
        {
            yield return new WaitForSeconds(delay);

            var canvas = UIFactory.CreateCanvas("Campaign Overlay");
            canvas.sortingOrder = 100;
            UIFactory.CreateBackground(canvas.transform, new Color(0.12f, 0f, 0f, 0.82f));
            UIFactory.CreateText(canvas.transform, "MISSION FAILED", 96,
                new Vector2(0, 200), new Vector2(1400, 170), TextAnchor.MiddleCenter, FontStyle.Bold)
                .color = new Color(1f, 0.45f, 0.4f);
            UIFactory.CreateText(canvas.transform, $"DISTANCE FLOWN: {Mathf.FloorToInt(Distance)} m", 40,
                new Vector2(0, 90), new Vector2(1200, 70)).color = new Color(0.9f, 0.85f, 0.8f);

            UIFactory.CreateButton(canvas.transform, "RETRY", new Vector2(0, -20f),
                () => SceneManager.LoadScene(SceneManager.GetActiveScene().name));
            UIFactory.CreateButton(canvas.transform, "BACK TO MENU", new Vector2(0, -130f),
                () => SceneManager.LoadScene(SceneNames.MainMenu));
        }

        // ---------------------------------------------------------------- ui

        void BuildHud()
        {
            var canvas = UIFactory.CreateCanvas("Campaign HUD");

            UIFactory.CreateText(canvas.transform, $"CAMPAIGN — LEVEL {levelNumber}", 52,
                new Vector2(0, 480), new Vector2(1000, 90), TextAnchor.MiddleCenter, FontStyle.Bold);

            UIFactory.CreateText(canvas.transform,
                "A / D to steer  •  F to fire  •  no turning back  •  don't hit the ground", 28,
                new Vector2(0, -500), new Vector2(1600, 50));

            _healthBar = new HealthBar(canvas.transform, new Vector2(-660f, 480f));
            _distanceText = UIFactory.CreateText(canvas.transform, "0 m", 40,
                new Vector2(660f, 480f), new Vector2(500, 60), TextAnchor.MiddleRight, FontStyle.Bold);
            UpdateHud();
        }

        void UpdateHud()
        {
            if (_cube != null && _healthBar != null)
                _healthBar.Set(_cube.CurrentHealth, _cube.MaxHealth);
            if (_distanceText != null)
                _distanceText.text = $"{Mathf.FloorToInt(Distance)} m";
        }
    }
}
