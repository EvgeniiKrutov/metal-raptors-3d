using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MetalRaptors
{
    public class ArenaLevelController : MonoBehaviour, ICampaignScriptHost, IBossHost,
        IDangerHost
    {
        const float CameraDistance = 420f;
        const float PlayPlaneZ = 100f;
        const float CamZ = PlayPlaneZ - CameraDistance;
        const float EyeY = 500f;

        const float SideMargin = 40f;
        const float TopMargin = 28f;
        const float FloorSkim = 0.03f;
        const float EnemyFloorDrop = 110f;

        const float CloudSpeedFactor = 1.2f;
        const float HoverSpeedFactor = 1.17f;
        const float PlaneScale = 1.4f;
        const float RollSpinScale = 1.3f;

        const float BossEntryMargin = 180f;
        const float BossAhead = 115f;
        const float BossAbove = 55f;
        const float BossPad = 30f;
        const float BossLaneResponse = 4f;
        const float BossStartSpeed = 150f;
        const float BossTopSpeed = 420f;
        const float BossAccel = 260f;
        const float BossBrake = 300f;
        const float BossArrive = 1f;
        const float BossSpeedFactor = 1.125f;

        const float EntryMargin = 120f;
        const float HoldFraction = 0.5f;
        const float SettleSec = 0.7f;

        const float CamShakeMagnitude = 7f;
        const float CamShakeDuration = 0.3f;
        const float ScrapeShakeCooldown = 1.2f;

        const float FailMaxSec = 3.5f;
        const float FailDrop = 140f;

        const float OutroExitMaxSec = 6f;
        const float OutroExitMargin = 120f;
        const float OutroFadeSec = 1.2f;

        const string Objective = "no way out  •  clear the sky";

        CampaignDefinition _level;
        int _levelNumber;

        Camera _cam;
        Vector3 _camPos;
        float _halfW;
        float _halfH;
        float _floorY;
        float _ceilingY;
        float _minX;
        float _maxX;

        CubeController _cube;
        Transform _cubeTr;
        PlaneShooter _shooter;
        PlaneBarrelRoll _roll;
        float _cruise;
        float _hoverSpeed;

        GameObject _hud;
        LevelHud _hudView;
        HudCurtain _curtain;
        DialogueBar _dialogue;
        SoundSystem _sound;
        ArenaClouds _clouds;
        CampaignEnemies _enemies;
        CampaignScriptRunner _runner;

        EnemyController _boss;
        BossHealthBar _bossBar;
        CinematicBars _entryBars;
        float _bossMax = 1f;
        bool _bossEntry;
        bool _bossReady;
        string _bossName;

        bool _entering = true;
        bool _gameOver;
        bool _outro;
        bool _failed;
        float _camShake;
        float _lastScrapeShake = -999f;

        public bool IsOver => _gameOver;

        public int EnemiesAlive => _enemies != null ? _enemies.AliveCount : 0;

        public int CountAlive(EnemyKind kind, PlaneModelConfig plane) =>
            kind == EnemyKind.Plane && _enemies != null ? _enemies.CountAlive(plane) : 0;

        public bool CompanionReady => true;

        public bool ZeppelinHanging => false;

        bool Cinematic => _entering || _outro || _bossEntry || CinematicBars.AnyShowing;

        public bool BossReady => _bossReady;

        string Subtitle => _level.title.ToLowerInvariant();

        public static ArenaLevelController Begin(GameObject owner, CampaignDefinition level,
            int levelNumber)
        {
            var arena = owner.AddComponent<ArenaLevelController>();
            arena._level = level;
            arena._levelNumber = levelNumber;
            arena.Build();
            return arena;
        }

        void Build()
        {
            CampaignCheckpoint.InCareer = true;

            var config = Resources.Load<PlayerConfig>("PlayerConfig");
            if (config == null) config = ScriptableObject.CreateInstance<PlayerConfig>();

            Firelight.Clear();
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.shadowDistance = Mathf.Max(urp.shadowDistance, CameraDistance + 200f);

            SetupCamera();

            _clouds = ArenaClouds.Begin(_camPos, _halfW, _halfH, CameraDistance, PlayPlaneZ,
                CoastSky.CloudColor(_level.daytime), CoastSky.CloudGlow(_level.daytime),
                DuelPlane.CruiseSpeed * CloudSpeedFactor);

            _floorY = _clouds.FloorY + FloorSkim * _halfH;
            _ceilingY = _camPos.y + _halfH - TopMargin;
            _minX = _camPos.x - _halfW + SideMargin;
            _maxX = _camPos.x + _halfW - SideMargin;

            SpawnPlayer(config);
            PlaneScrapes.DisablePlanePlaneCollisions();
            BuildHud();

            _sound = SoundSystem.Begin(_cube, null);
            _enemies = new CampaignEnemies(_cube.GetComponent<Rigidbody>(),
                _floorY - EnemyFloorDrop, _camPos.y + _halfH, _level);
            _enemies.ModelScale = PlaneScale;
            _sound.Track(_enemies.Live, boss: true);

            StartCoroutine(FlyIn());
        }

        void OnDestroy() => CampaignCheckpoint.InCareer = false;

        void SetupCamera()
        {
            _camPos = new Vector3(0f, EyeY, CamZ);
            _cam = Camera.main;

            if (_cam == null)
            {
                _halfH = CameraDistance * Mathf.Tan(LevelCamera.BaseFieldOfView * 0.5f * Mathf.Deg2Rad);
                _halfW = _halfH * LevelCamera.ReferenceAspect;
                return;
            }

            _cam.orthographic = false;
            _cam.transform.rotation = Quaternion.identity;
            CoastSky.Apply(_cam, _level.daytime, _level.weather);
            CoastSky.ApplyFog(_level.daytime, CameraDistance, PlayPlaneZ);
            _cam.farClipPlane = 2600f;

            LevelCamera.Frame(_cam, CameraDistance, out _halfW, out _halfH);
            CutsceneBlur.Focus(CameraDistance);
            _cam.transform.position = _camPos;
        }

        void SpawnPlayer(PlayerConfig config)
        {
            var go = new GameObject("PlayerPlane");
            go.transform.position = new Vector3(_camPos.x - _halfW - EntryMargin,
                (_floorY + _ceilingY) * 0.5f, PlayPlaneZ);

            var planeModel = GameManager.CurrentPlane;
            var model = PlaneFactory.BuildPlaneModel(go.transform, planeModel,
                skin: GameManager.CurrentSkin);
            model.localScale *= PlaneScale;

            PlayerConfig flight = PlaneLoadout.Build(config, planeModel);
            _cruise = flight.flySpeed;
            _hoverSpeed = _cruise * HoverSpeedFactor;

            _cube = go.AddComponent<CubeController>();
            _cubeTr = go.transform;
            _cube.OnCrashed += OnCrashed;
            _cube.OnShotDown += OnShotDown;
            _cube.OnDamaged += OnPlayerDamaged;
            _cube.OnScraped += OnPlayerScraped;

            _cube.Initialize(flight, 0f, float.MinValue, float.MaxValue, _ceilingY, 0f,
                hardWalls: true);
            _cube.SetControlled(false);

            var muzzle = PlaneFactory.MountMuzzle(go, model, planeModel, out var flashPoint);
            var hitbox = go.GetComponentInChildren<Collider>();

            _shooter = go.AddComponent<PlaneShooter>();
            _shooter.Initialize(flight, muzzle, flashPoint, hitbox);
            _shooter.Stop();

            _roll = go.AddComponent<PlaneBarrelRoll>();
            _roll.Initialize(flight, _cube, model, RollSpinScale, streaks: false);
            _roll.Stop();
        }

        void BuildHud()
        {
            var canvas = UIFactory.CreateCanvas("Arena HUD");
            _hud = canvas.gameObject;

            _hudView = new LevelHud(canvas.transform, Objective, _cube, _shooter, null, null, _roll,
                null, null, TryPause, arena: true);

            _curtain = HudCurtain.Attach(_hud);
            _curtain.Set(false);
        }

        IEnumerator FlyIn()
        {
            float holdX = _camPos.x - _halfW * HoldFraction;
            while (_cubeTr != null && !_gameOver && _cubeTr.position.x < holdX) yield return null;
            if (_cube == null || _gameOver) yield break;

            _cube.SetWalls(_minX, _maxX);
            _cube.BeginHover(_hoverSpeed, _floorY);

            for (float t = 0f; t < SettleSec; t += Time.deltaTime) yield return null;
            if (_gameOver) yield break;

            _entering = false;
            _cube.SetControlled(true);
            if (_shooter != null) _shooter.Resume();
            if (_roll != null) _roll.Resume();
            BeginScript();
        }

        void BeginScript()
        {
            if (string.IsNullOrEmpty(_level.arenaScript)) return;

            CampaignScript script = CampaignScript.Load(_level.arenaScript);
            if (script == null) return;

            _dialogue = new DialogueBar(_hud.transform);
            _runner = CampaignScriptRunner.Begin(gameObject, script, this, _dialogue);
        }

        void Update()
        {
            if (MenuInput.ReadCancel()) TryPause();
        }

        void TryPause()
        {
            if (_gameOver || GameMenu.IsOpen || LevelBriefing.IsOpen || ScreenFade.IsBusy) return;
            GameMenu.Open(GameMenuKind.Pause, Subtitle, _hud);
        }

        void FixedUpdate()
        {
            if (_gameOver || _cube == null || _bossEntry) return;
            PlaneScrapes.Check(_cube, _cubeTr, _enemies != null ? _enemies.Live : null, null);
        }

        void LateUpdate()
        {
            if (_cubeTr == null) return;

            if (_camShake > 0f)
                _camShake = Mathf.Max(0f, _camShake - Time.unscaledDeltaTime / CamShakeDuration);
            PlaceCamera();

            if (_cube != null) _cube.SetCinematic(Cinematic);
            if (_curtain != null) _curtain.Set(!Cinematic);
            if (!_outro && _hudView != null) _hudView.Tick();
            if (_enemies != null) _enemies.SetWindow(_camPos.x, _halfW);

            if (_entryBars != null && _dialogue != null && _dialogue.IsReady)
            {
                Destroy(_entryBars.gameObject);
                _entryBars = null;
            }
            if (_bossBar != null) _bossBar.Set(_boss != null ? _boss.CurrentHealth : 0f, _bossMax);
        }

        void PlaceCamera()
        {
            if (_cam == null) return;

            Vector3 pos = _camPos;
            if (_camShake > 0f)
            {
                Vector2 j = Random.insideUnitCircle * (CamShakeMagnitude * _camShake);
                pos += new Vector3(j.x, j.y, 0f);
            }
            _cam.transform.position = pos;
        }

        public void SpawnWave(EnemyGroup[] groups, bool spread = false)
        {
            if (_gameOver || groups == null || _enemies == null) return;

            if (spread) _enemies.SpawnSpread(groups, _camPos, _halfW, _halfH);
            else _enemies.Spawn(groups, _camPos.x, _halfW);
        }

        public float WarnIncoming(int planes)
        {
            if (_gameOver || _hud == null || planes <= 0) return 0f;

            EnemyWarning warning = EnemyWarning.Show(_hud.transform, planes);
            if (_curtain != null) _curtain.Adopt(warning.gameObject);
            return EnemyWarning.Seconds;
        }

        public float WarnDanger(ScreenEdge edge, float seconds)
        {
            if (_gameOver || _hud == null) return 0f;

            DangerSign sign = DangerSign.Show(_hud.transform, edge, seconds, () => DangerAlong(edge));
            if (_curtain != null) _curtain.Adopt(sign.gameObject);
            return sign.Seconds;
        }

        float DangerAlong(ScreenEdge edge)
        {
            Vector2 target = BossTarget();
            return edge == ScreenEdge.Left || edge == ScreenEdge.Right
                ? (target.y - (_camPos.y - _halfH)) / (2f * _halfH)
                : (target.x - (_camPos.x - _halfW)) / (2f * _halfW);
        }

        public void ArmSupply(int crates) { }

        public void SetCompanionFoe(PlaneModelConfig plane) { }

        public bool SpawnZeppelin() => false;

        public void GatherFormation() { }

        public bool FormationReady => true;

        public void EnterArena() { }

        public void EnterBoss(PlaneModelConfig plane, string skin, float health, string name)
        {
            if (_gameOver || _enemies == null || _cube == null || plane == null || _boss != null)
            {
                _bossReady = true;
                return;
            }

            _bossEntry = true;
            _bossReady = false;
            _bossName = name;
            _cube.SetControlled(false);
            if (_shooter != null) _shooter.Stop();
            if (_roll != null) _roll.Stop();

            _entryBars = CinematicBars.Create(_hud.transform);
            _entryBars.Raise();

            var start = new Vector2(_camPos.x - _halfW - BossEntryMargin, BossTarget().y);
            _boss = _enemies.SpawnBoss(plane, PlaneSkins.ById(plane, skin), health, start,
                _camPos.x, _halfW, _cruise * BossSpeedFactor);
            if (_boss == null)
            {
                _bossReady = true;
                return;
            }

            _bossMax = _boss.MaxHealth;
            _boss.HideHealthBar();
            _boss.BeginScript();
            _boss.ScriptTo(start, 0f);
            StartCoroutine(FlyBossIn(start));
        }

        Vector2 BossTarget()
        {
            Vector3 player = _cubeTr != null
                ? _cubeTr.position
                : new Vector3(_camPos.x, (_floorY + _ceilingY) * 0.5f, 0f);

            float x = Mathf.Min(player.x + BossAhead, _maxX);
            float y = player.y + BossAbove;
            if (y > _ceilingY - BossPad) y = Mathf.Max(_floorY + BossPad, player.y - BossAbove);
            return new Vector2(x, y);
        }

        IEnumerator FlyBossIn(Vector2 start)
        {
            Vector2 pos = start;
            float speed = BossStartSpeed;

            while (_boss != null && !_gameOver)
            {
                float dt = Time.deltaTime;
                Vector2 target = BossTarget();
                float left = target.x - pos.x;
                if (left <= BossArrive) break;

                float cap = Mathf.Min(BossTopSpeed, Mathf.Sqrt(2f * BossBrake * left));
                speed = Mathf.MoveTowards(speed, cap, (cap > speed ? BossAccel : BossBrake) * dt);
                pos.x = Mathf.Min(target.x, pos.x + speed * dt);
                pos.y += (target.y - pos.y) * (1f - Mathf.Exp(-BossLaneResponse * dt));

                _boss.ScriptTo(pos, 0f);
                yield return null;
            }

            _bossReady = true;
        }

        public void BeginDuel()
        {
            if (_gameOver) return;

            _bossEntry = false;
            if (_entryBars != null)
            {
                Destroy(_entryBars.gameObject);
                _entryBars = null;
            }

            if (_cube != null) _cube.SetControlled(true);
            if (_shooter != null) _shooter.Resume();
            if (_roll != null) _roll.Resume();
            if (_boss == null) return;

            _boss.EndScript();
            _bossBar = new BossHealthBar(_hud.transform, _hudView.BossSlot, _bossName);
            _bossBar.Set(_boss.CurrentHealth, _bossMax);
        }

        public void Checkpoint(int step, bool warnedFirst, bool warnedPair) { }

        public void CompleteLevel()
        {
            if (_gameOver) return;
            _gameOver = true;
            _outro = true;

            if (_shooter != null) _shooter.Stop();
            if (_roll != null) _roll.Stop();
            if (_enemies != null) _enemies.StandDown();
            if (_dialogue != null) _dialogue.Hide();
            if (_cube != null)
            {
                _cube.ClearWalls();
                _cube.SetControlled(false);
                _cube.SetHoverInput(Vector2.right);
            }

            CampaignProgress.Complete(_levelNumber);
            CampaignCheckpoint.Clear();

            StartCoroutine(FlyOut());
        }

        IEnumerator FlyOut()
        {
            float left = OutroExitMaxSec;
            while (left > 0f && !PlaneGone)
            {
                left -= Time.deltaTime;
                yield return null;
            }

            if (_sound != null) _sound.FadeOut(OutroFadeSec);
            ScreenFade.Swap(ShowGroundScene, OutroFadeSec);
        }

        bool PlaneGone => _cubeTr == null
            || _cubeTr.position.x > _camPos.x + _halfW + OutroExitMargin;

        void ShowGroundScene()
        {
            if (_cube != null) _cube.Stop();
            CampaignFinale.Play(_level, _levelNumber, _hud, Subtitle);
        }

        void StopScript()
        {
            if (_runner != null) _runner.Stop();
            if (_enemies != null) _enemies.StandDown();
        }

        void OnShotDown()
        {
            if (_gameOver) return;
            _gameOver = true;

            StopScript();
            if (_shooter != null) _shooter.Stop();
            if (_roll != null) _roll.Stop();
            if (_sound != null) _sound.EnterGameOver();

            StartCoroutine(FailWhenGone());
        }

        IEnumerator FailWhenGone()
        {
            float left = FailMaxSec;
            while (left > 0f && _cubeTr != null && _cubeTr.position.y > _floorY - FailDrop)
            {
                left -= Time.deltaTime;
                yield return null;
            }

            Fail();
        }

        void OnCrashed()
        {
            bool first = !_gameOver;
            _gameOver = true;

            StopScript();
            if (_cube != null) _cube.Stop();
            if (_shooter != null) _shooter.Stop();
            if (_roll != null) _roll.Stop();
            if (first && _sound != null) _sound.EnterGameOver();

            StartCoroutine(FailAfter(Explosion.Duration));
        }

        IEnumerator FailAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            Fail();
        }

        void Fail()
        {
            if (_failed || _outro) return;
            _failed = true;
            GameMenu.Open(GameMenuKind.Failed, Subtitle, _hud);
        }

        void OnPlayerDamaged()
        {
            if (_sound != null) _sound.ReportPlayerDamaged();
        }

        void OnPlayerScraped()
        {
            if (Time.time - _lastScrapeShake < ScrapeShakeCooldown) return;
            _lastScrapeShake = Time.time;
            _camShake = 1f;
        }
    }
}
