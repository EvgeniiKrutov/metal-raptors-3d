using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class CampaignLevelController : MonoBehaviour
    {
        const float WorldTop = 650f;
        const float CubeHalf = 15f;
        const float CameraDistance = 420f;
        const float PlayPlaneZ = 100f;
        const float CamZ = PlayPlaneZ - CameraDistance;
        const float StartX = 0f;
        const float SpawnY = 150f;
        const float CamShakeMagnitude = 7f;
        const float CamShakeDuration = 0.3f;

        const float DitchSplashSize = 75f;
        const float SinkSpeed = 26f;
        const float SinkDriftKeep = 0.15f;
        const float SinkDuration = 2f;

        CampaignDefinition _level;
        int _levelNumber;
        CubeController _cube;
        PlaneShooter _shooter;
        PlaneSearchlight _searchlight;
        Transform _cubeTr;
        Camera _cam;
        CampaignTerrain _terrain;
        SeaSurface _sea;
        Transform _ceilingBar;
        SoundSystem _sound;

        HealthBar _healthBar;
        SearchlightIndicator _lightIndicator;
        Text _distanceText;
        GameObject _hud;

        float _halfViewHeight;
        float _halfViewWidth;
        Vector3 _camBasePos;
        float _camShake;
        bool _gameOver;
        float _furthestX = StartX;
        int _nextWave;

        float Distance => Mathf.Max(0f, _furthestX - StartX);

        bool Coast => _level.terrain == TerrainKind.Flanders;

        void Start()
        {
            _levelNumber = CampaignRun.Level;
            _level = CustomBattle.Requested
                ? CampaignLevels.Custom(CustomBattle.Map, CustomBattle.Daytime)
                : CampaignLevels.ForNumber(_levelNumber);

            var config = Resources.Load<PlayerConfig>("PlayerConfig");
            if (config == null) config = ScriptableObject.CreateInstance<PlayerConfig>();

            ConfigureShadows();
            _terrain = CampaignTerrain.Begin(_level.terrain, _level.seed, _level.daytime,
                _level.weather, CameraDistance, PlayPlaneZ, StartX);
            SpawnPlayer(config);
            SetupCamera();

            if (Coast)
            {
                _sea = SeaSurface.Begin(_cam, _level.daytime);
                Battlefield.BeginCoast(_cam, _halfViewWidth, _level.seed,
                    SeaSurface.Level, SeaSurface.NearEdge);
            }
            else
            {
                Battlefield.Begin(_cam, _halfViewWidth, _level.seed, _terrain.InCrater);
            }

            BuildHud();
            _sound = SoundSystem.Begin(_cube, null);
        }

        void ConfigureShadows()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.shadowDistance = Mathf.Max(urp.shadowDistance, CameraDistance + 200f);
        }

        void SpawnPlayer(PlayerConfig config)
        {
            var go = new GameObject("PlayerPlane");
            go.transform.position = new Vector3(StartX, SpawnY, PlayPlaneZ);

            var planeModel = GameManager.CurrentPlane;
            var model = PlaneFactory.BuildPlaneModel(go.transform, planeModel);

            _cube = go.AddComponent<CubeController>();
            _cubeTr = go.transform;
            _cube.OnCrashed += OnCrashed;
            _cube.OnShotDown += OnShotDown;
            _cube.OnDamaged += OnPlayerDamaged;
            _cube.OnScraped += OnPlayerScraped;

            _cube.Initialize(config, 0f, float.MinValue, float.MaxValue,
                WorldTop - CubeHalf, 0f, hardLeftWall: true);

            var muzzle = PlaneFactory.MountMuzzle(go, model, planeModel, out var flashPoint);
            _shooter = go.AddComponent<PlaneShooter>();
            _shooter.Initialize(config, muzzle, flashPoint, go.GetComponentInChildren<Collider>());

            _searchlight = PlaneSearchlight.Mount(go,
                PlaneFactory.NoseLocal(go, model, planeModel), _level.daytime);
        }

        void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null) return;

            _cam.orthographic = false;
            _cam.transform.rotation = Quaternion.identity;

            if (Coast)
            {
                CoastSky.Apply(_cam, _level.daytime, _level.weather);
            }
            else
            {
                switch (_level.daytime)
                {
                    case Daytime.Midday: MiddaySky.Apply(_cam, _level.weather); break;
                    case Daytime.Evening: EveningSky.Apply(_cam, _level.weather); break;
                    case Daytime.Night: NightSky.Apply(_cam, _level.weather); break;
                    default: MorningSky.Apply(_cam, _level.weather); break;
                }
            }
            _cam.farClipPlane = 2600f;

            _halfViewHeight = CameraDistance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            _halfViewWidth = _halfViewHeight * _cam.aspect;

            PositionCamera(instant: true);

            if (_level.clouds == null) return;

            if (Coast)
                CloudSystem.Begin(_cam, CoastSky.CloudColor(_level.daytime),
                    CoastSky.CloudGlow(_level.daytime), _level.clouds, PlayPlaneZ);
            else
                CloudSystem.Begin(_cam, _level.daytime, _level.weather, _level.clouds, PlayPlaneZ);
        }

        void Update()
        {
            if (_gameOver || GameMenu.IsOpen) return;
            if (MenuInput.ReadCancel()) GameMenu.Open(GameMenuKind.Pause, Subtitle, _hud);
        }

        string MapName => TerrainNames.For(_level.terrain);

        string Subtitle => CustomBattle.Requested
            ? $"{CustomBattle.Map.Name} | {DaytimeNames.For(_level.daytime)}"
            : $"level {_levelNumber} | {MapName}";

        string HudTitle => CustomBattle.Requested
            ? $"CUSTOM BATTLE — {MapName.ToUpperInvariant()}"
            : $"CAMPAIGN — LEVEL {_levelNumber}";

        string HudHint => "A / D to steer  •  F to fire  •  no turning back  •  "
            + "don't hit the ground";

        void LateUpdate()
        {
            if (_cubeTr == null) return;

            _furthestX = Mathf.Max(_furthestX, _cubeTr.position.x);
            if (_camShake > 0f)
                _camShake = Mathf.Max(0f, _camShake - Time.deltaTime / CamShakeDuration);
            if (_cam != null) PositionCamera(instant: false);

            if (_cube != null) _cube.SetLeftWall(_camBasePos.x - _halfViewWidth + CubeHalf);

            if (_terrain != null) _terrain.UpdateStreaming(_camBasePos.x);
            if (_ceilingBar != null)
                _ceilingBar.position = new Vector3(_camBasePos.x, WorldTop, PlayPlaneZ);

            if (!_gameOver && _sea != null && PlayPlaneZ >= SeaSurface.NearEdge
                && _cubeTr.position.y <= SeaSurface.Level) Ditch();

            UpdateHud();
            CheckWaves();
        }

        void PositionCamera(bool instant)
        {
            Vector3 cubePos = _cubeTr.position;

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

            Vector3 pos = _camBasePos;
            if (_camShake > 0f)
            {
                Vector2 j = Random.insideUnitCircle * (CamShakeMagnitude * _camShake);
                pos += new Vector3(j.x, j.y, 0f);
            }
            _cam.transform.position = pos;
        }

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

        void Ditch()
        {
            _gameOver = true;

            Vector3 pos = _cubeTr.position;
            WaterSplash.Spawn(new Vector3(pos.x, SeaSurface.Level, pos.z), DitchSplashSize,
                _cam != null ? _cam.transform.position : pos);

            _cube.Sink(SinkSpeed, SinkDriftKeep);
            if (_shooter != null) _shooter.Stop();
            if (_sound != null) _sound.EnterGameOver();

            StartCoroutine(ShowFailScreenAfter(SinkDuration));
        }

        void OnShotDown()
        {
            if (_shooter != null) _shooter.Stop();
            if (_sound != null) _sound.EnterGameOver();
        }

        void OnPlayerDamaged()
        {
            if (_sound != null) _sound.ReportPlayerDamaged();
        }

        void OnPlayerScraped() => _camShake = 1f;

        void OnCrashed()
        {
            if (_gameOver) return;
            _gameOver = true;
            _cube.Stop();
            if (_shooter != null) _shooter.Stop();
            if (_sound != null) _sound.EnterGameOver();

            StartCoroutine(ShowFailScreenAfter(Explosion.Duration));
        }

        IEnumerator ShowFailScreenAfter(float delay)
        {
            yield return new WaitForSeconds(delay);

            GameMenu.Open(GameMenuKind.Failed, Subtitle, _hud);
        }

        void BuildHud()
        {
            var canvas = UIFactory.CreateCanvas("Campaign HUD");
            _hud = canvas.gameObject;

            UIFactory.CreateText(canvas.transform, HudTitle, 52,
                new Vector2(0, 480), new Vector2(1000, 90), TextAnchor.MiddleCenter, FontStyle.Bold);

            UIFactory.CreateText(canvas.transform, HudHint, 28,
                new Vector2(0, -500), new Vector2(1600, 50));

            _healthBar = new HealthBar(canvas.transform, new Vector2(-660f, 480f));
            if (_searchlight != null)
                _lightIndicator = new SearchlightIndicator(canvas.transform, new Vector2(-785f, 435f));
            _distanceText = UIFactory.CreateText(canvas.transform, "0 m", 40,
                new Vector2(660f, 480f), new Vector2(500, 60), TextAnchor.MiddleRight, FontStyle.Bold);
            UpdateHud();
        }

        void UpdateHud()
        {
            if (_lightIndicator != null && _searchlight != null)
                _lightIndicator.Set(_searchlight.IsOn);
            if (_cube != null && _healthBar != null)
                _healthBar.Set(_cube.CurrentHealth, _cube.MaxHealth);
            if (_distanceText != null)
                _distanceText.text = $"{Mathf.FloorToInt(Distance)} m";
        }
    }
}
