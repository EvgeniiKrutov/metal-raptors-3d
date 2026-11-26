using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MetalRaptors
{
    public class DevStats : MonoBehaviour
    {
        const float RefreshInterval = 0.25f;

        const int ColumnCount = 3;
        const float ColumnWidth = 280f;
        const float ColumnGap = 28f;
        const float PadX = 22f;
        const float PadY = 18f;
        const float PanelWidth = ColumnCount * ColumnWidth + (ColumnCount - 1) * ColumnGap + 2f * PadX;

        const int HeaderSize = 16;
        const float HeaderHeight = 22f;
        const float HeaderToColumns = 18f;

        const int TitleSize = 15;
        const float TitleRowHeight = 20f;
        const float TitleToRows = 12f;
        const float SubTitleTop = 6f;

        const int RowSize = 18;
        const float RowHeight = 24f;
        const float RowGap = 10f;
        const float MeterHeight = 3f;
        const float MeterTop = 5f;
        const float LabelWidth = 52f;

        const float MeterWarn = 0.7f;

        const float ButtonHeight = 30f;
        const float ButtonGap = 8f;
        const int ButtonFontSize = 14;
        const float ButtonStep = ButtonHeight + ButtonGap;

        const float BoxSize = 16f;
        const float BoxStroke = 2f;
        const float BoxInset = 4f;
        const float BoxLeft = 10f;
        const float CheckTextLeft = 36f;

        const string IdleStatus = "TAB";
        const string FrozenStatus = "FROZEN   TAB";
        const string EmptyCaption = "--";

        static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.07f, 0.90f);
        static readonly Color TitleColor = new Color(0.52f, 0.58f, 0.66f, 1f);
        static readonly Color LabelColor = new Color(0.60f, 0.66f, 0.74f, 1f);
        static readonly Color ValueColor = new Color(0.92f, 0.95f, 1f, 1f);
        static readonly Color MeterTrackColor = new Color(1f, 1f, 1f, 0.10f);
        static readonly Color MeterLow = new Color(0.36f, 0.80f, 0.52f, 1f);
        static readonly Color MeterMid = new Color(0.94f, 0.76f, 0.30f, 1f);
        static readonly Color MeterHigh = new Color(0.90f, 0.32f, 0.26f, 1f);
        static readonly Color SpawnPendingColor = new Color(0.94f, 0.76f, 0.30f, 1f);
        static readonly Color BoxHoleColor = new Color(0.06f, 0.07f, 0.09f, 1f);

        struct Row
        {
            public Text Value;
            public RectTransform Fill;
            public Image FillImage;
        }

        class Block
        {
            public RectTransform Rt;
            public float Height;
            public Func<bool> Visible;
            public bool Shown = true;
        }

        class Check
        {
            public Func<bool> Get;
            public Image Mark;
            public bool On;
        }

        class SpawnAction
        {
            public Action Release;
            public string Caption;
            public Button Button;
            public Text Label;
            public float Remaining;
        }

        static DevStats _instance;

        ProfilerRecorder _mainThread;
        ProfilerRecorder _gpuFrame;
        ProfilerRecorder _systemMemory;
        ProfilerRecorder _gcMemory;
        ProfilerRecorder _triangles;

        readonly Dictionary<Mesh, long> _meshFaces = new Dictionary<Mesh, long>();

        readonly FrameTiming[] _timings = new FrameTiming[1];
        bool _hasTiming;

        GameObject _panel;
        RectTransform _panelRt;
        Text _status;
        bool _statusFrozen;

        readonly List<Block>[] _blocks = new List<Block>[ColumnCount];
        readonly RectTransform[] _columns = new RectTransform[ColumnCount];
        readonly RectTransform[] _dividers = new RectTransform[ColumnCount - 1];
        readonly List<Check> _checks = new List<Check>();

        Row _cpu;
        Row _gpu;
        Row _tris;
        Row _ram;
        Row _fps;
        Row _wave;

        Text _skipLabel;
        Text _jumpLabel;
        Text _finalLabel;
        Text _sceneLabel;

        SpawnAction _scoutSpawn;
        SpawnAction _fighterSpawn;
        SpawnAction _duelistSpawn;
        SpawnAction _truckSpawn;
        SpawnAction _tankSpawn;
        SpawnAction _zeppelinSpawn;
        bool _spawnShown;
        bool _zeppelinShown;

        double _frameSum;
        double _cpuSum;
        double _gpuSum;
        double _trisSum;
        int _gpuSamples;
        int _trisSamples;
        int _samples;
        float _elapsed;
        bool _visible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("DevStats");
            go.AddComponent<DevStats>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;

            StartRecorders();
            Build();
            SetVisible(false);
        }

        void StartRecorders()
        {
            _mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            _gpuFrame = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time");
            _systemMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
            _gcMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => _meshFaces.Clear();

        void OnDestroy()
        {
            if (_instance == this) _instance = null;

            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_mainThread.Valid) _mainThread.Dispose();
            if (_gpuFrame.Valid) _gpuFrame.Dispose();
            if (_systemMemory.Valid) _systemMemory.Dispose();
            if (_gcMemory.Valid) _gcMemory.Dispose();
            if (_triangles.Valid) _triangles.Dispose();
        }

        void Build()
        {
            Canvas canvas = UIFactory.CreateCanvas("Dev Stats");
            canvas.sortingOrder = 500;
            canvas.transform.SetParent(transform, false);

            var go = new GameObject("Panel", typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            _panel = go;

            var background = go.GetComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = true;

            _panelRt = background.rectTransform;
            _panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRt.pivot = new Vector2(0.5f, 0.5f);
            _panelRt.anchoredPosition = Vector2.zero;

            Transform content = CreateContent(_panelRt);

            UIFactory.CreateLabel(content, "DEV MENU", HeaderSize, 0f, HeaderHeight,
                ValueColor, UIFactory.BoldFont);

            _status = UIFactory.CreateLabel(content, IdleStatus, TitleSize, 0f, HeaderHeight,
                TitleColor, UIFactory.MediumFont);
            _status.alignment = TextAnchor.MiddleRight;

            UIFactory.CreateRule(content, -(HeaderHeight + HeaderToColumns * 0.5f),
                new Vector2(PanelWidth - 2f * PadX, 1f), MeterTrackColor);

            float top = -(HeaderHeight + HeaderToColumns);
            for (int i = 0; i < ColumnCount; i++)
            {
                _blocks[i] = new List<Block>();
                _columns[i] = CreateColumn(content, i, top);
            }

            for (int i = 0; i < ColumnCount - 1; i++) _dividers[i] = CreateDivider(content, i, top);

            BuildPerformance();
            BuildLevel();
            BuildPlane();
            Layout();
        }

        static Transform CreateContent(RectTransform panel)
        {
            var go = new GameObject("Content", typeof(RectTransform));
            go.transform.SetParent(panel, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(PadX, PadY);
            rt.offsetMax = new Vector2(-PadX, -PadY);
            return go.transform;
        }

        static RectTransform CreateColumn(Transform content, int index, float top)
        {
            var go = new GameObject($"Column {index}", typeof(RectTransform));
            go.transform.SetParent(content, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(ColumnWidth, 0f);
            rt.anchoredPosition = new Vector2(index * (ColumnWidth + ColumnGap), top);
            return rt;
        }

        static RectTransform CreateDivider(Transform content, int index, float top)
        {
            var go = new GameObject("Divider", typeof(Image));
            go.transform.SetParent(content, false);

            var img = go.GetComponent<Image>();
            img.color = MeterTrackColor;
            img.raycastTarget = false;

            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2((index + 1) * ColumnWidth + index * ColumnGap + ColumnGap * 0.5f, top);
            return rt;
        }

        Block AddBlock(int column, string name, float height, Func<bool> visible = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_columns[column], false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);

            var block = new Block { Rt = rt, Height = height, Visible = visible };
            _blocks[column].Add(block);
            return block;
        }

        void AddTitle(int column, string caption)
        {
            Block block = AddBlock(column, caption, TitleRowHeight + TitleToRows);
            UIFactory.CreateLabel(block.Rt, caption, TitleSize, 0f, TitleRowHeight,
                TitleColor, UIFactory.MediumFont);
        }

        void AddSubTitle(int column, string caption, Func<bool> visible)
        {
            Block block = AddBlock(column, caption, SubTitleTop + TitleRowHeight + ButtonGap, visible);
            UIFactory.CreateLabel(block.Rt, caption, TitleSize, -SubTitleTop, TitleRowHeight,
                TitleColor, UIFactory.MediumFont);
        }

        void BuildPerformance()
        {
            AddTitle(0, "PERFORMANCE");

            Block block = AddBlock(0, "Stats", 0f);
            float y = 0f;
            _cpu = CreateRow(block.Rt, "CPU", ref y, true);
            _gpu = CreateRow(block.Rt, "GPU", ref y, true);
            _tris = CreateRow(block.Rt, "TRIS", ref y, false);
            _ram = CreateRow(block.Rt, "RAM", ref y, false);
            _fps = CreateRow(block.Rt, "FPS", ref y, false);
            _wave = CreateRow(block.Rt, "WAVE", ref y, false);
            block.Height = -y;
        }

        void BuildLevel()
        {
            AddTitle(1, "LEVEL");

            Block empty = AddBlock(1, "Empty", ButtonStep, () => !AnyLevelTool);
            UIFactory.CreateLabel(empty.Rt, EmptyCaption, RowSize, 0f, ButtonHeight,
                TitleColor, UIFactory.MediumFont);

            _skipLabel = AddLevelButton("Skip", () => DevSkip.Available, DevSkip.Run);
            _jumpLabel = AddLevelButton("Jump", () => DevJump.Available, DevJump.Run);
            _finalLabel = AddLevelButton("Final", () => DevFinalJump.Available, DevFinalJump.Run);
            _sceneLabel = AddLevelButton("Scene", () => DevSceneJump.Available, DevSceneJump.Run);

            AddCheck(1, "AERODROME FIRE", () => Aerodrome.Current != null && Aerodrome.Current.Burning,
                SetFire, () => Aerodrome.Current != null);

            AddSubTitle(1, "SPAWN", () => DevSpawn.Available);

            Block row = AddBlock(1, "Spawn Row 1", ButtonStep, () => DevSpawn.Available);
            _scoutSpawn = CreateSpawnButton(row.Rt, "SCOUT", 0, () => DevSpawn.Spawn(EnemyRole.Scout));
            _fighterSpawn = CreateSpawnButton(row.Rt, "FIGHTER", 1, () => DevSpawn.Spawn(EnemyRole.Fighter));

            row = AddBlock(1, "Spawn Row 2", ButtonStep, () => DevSpawn.Available);
            _duelistSpawn = CreateSpawnButton(row.Rt, "DUELIST", 0, () => DevSpawn.Spawn(EnemyRole.Duelist));
            _truckSpawn = CreateSpawnButton(row.Rt, "TRUCK", 1, DevSpawn.SpawnTruck);

            row = AddBlock(1, "Spawn Row 3", ButtonStep, () => DevSpawn.Available);
            _tankSpawn = CreateSpawnButton(row.Rt, "TANK", 0, DevSpawn.SpawnTank);
            _zeppelinSpawn = CreateSpawnButton(row.Rt, "ZEPPELIN", 1, DevSpawn.SpawnZeppelin);
            _zeppelinSpawn.Button.gameObject.SetActive(false);

            Block career = AddBlock(1, "Career", ButtonStep, () => DevCareerReset.Available);
            Button reset = CreateButton(career.Rt, "RESET CAREER");
            reset.onClick.AddListener(() => RunAndClose(DevCareerReset.Run));
            reset.GetComponentInChildren<Text>().color = ValueColor;
        }

        void BuildPlane()
        {
            AddTitle(2, "PLANE");
            AddCheck(2, "INVULNERABLE", () => DevCheats.Invulnerable, on => DevCheats.Invulnerable = on);
            AddCheck(2, "HIGH DAMAGE   ×10", () => DevCheats.HighDamage, on => DevCheats.HighDamage = on);
            AddCheck(2, "SPEED   ×1.5", () => DevCheats.FastSpeed, on => DevCheats.FastSpeed = on);
            AddCheck(2, "FAST RELOAD", () => DevCheats.FastReload, on => DevCheats.FastReload = on);
            AddCheck(2, "HITBOXES", () => DevCheats.ShowHitboxes, on => DevCheats.ShowHitboxes = on);
        }

        static bool AnyLevelTool => DevSkip.Available || DevJump.Available || DevFinalJump.Available
            || DevSceneJump.Available
            || Aerodrome.Current != null || DevSpawn.Available || DevCareerReset.Available;

        Text AddLevelButton(string name, Func<bool> visible, Action run)
        {
            Block block = AddBlock(1, name, ButtonStep, visible);
            Button button = CreateButton(block.Rt, string.Empty);
            button.onClick.AddListener(() => RunAndClose(run));

            Text label = button.GetComponentInChildren<Text>();
            label.color = ValueColor;
            return label;
        }

        void AddCheck(int column, string caption, Func<bool> get, Action<bool> set,
            Func<bool> visible = null)
        {
            Block block = AddBlock(column, caption, ButtonStep, visible);
            Button button = CreateButton(block.Rt, caption);

            Text label = button.GetComponentInChildren<Text>();
            label.color = ValueColor;
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.offsetMin = new Vector2(CheckTextLeft, 0f);

            Image frame = CreateSquare(button.transform, BoxSize, LabelColor);
            RectTransform frameRt = frame.rectTransform;
            frameRt.anchorMin = new Vector2(0f, 0.5f);
            frameRt.anchorMax = new Vector2(0f, 0.5f);
            frameRt.anchoredPosition = new Vector2(BoxLeft + BoxSize * 0.5f, 0f);

            CreateSquare(frame.transform, BoxSize - 2f * BoxStroke, BoxHoleColor);
            Image mark = CreateSquare(frame.transform, BoxSize - 2f * BoxInset, MeterLow);
            mark.enabled = false;

            _checks.Add(new Check { Get = get, Mark = mark });
            button.onClick.AddListener(() => set(!get()));
        }

        static Image CreateSquare(Transform parent, float size, Color color)
        {
            var go = new GameObject("Box", typeof(Image));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
            return img;
        }

        static Button CreateButton(Transform parent, string caption)
        {
            Button button = UIFactory.CreateButton(parent, caption, Vector2.zero, null,
                new Vector2(0f, ButtonHeight), fontSize: ButtonFontSize);

            var rt = (RectTransform)button.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, ButtonHeight);
            rt.anchoredPosition = Vector2.zero;
            return button;
        }

        static Button CreateHalfButton(Transform parent, string caption, int side)
        {
            Button button = CreateButton(parent, caption);

            var rt = (RectTransform)button.transform;
            rt.anchorMin = new Vector2(side * 0.5f, 1f);
            rt.anchorMax = new Vector2(side * 0.5f + 0.5f, 1f);
            rt.sizeDelta = new Vector2(-ButtonGap * 0.5f, ButtonHeight);
            rt.anchoredPosition = new Vector2(side == 0 ? -ButtonGap * 0.25f : ButtonGap * 0.25f, 0f);
            return button;
        }

        SpawnAction CreateSpawnButton(Transform parent, string caption, int side, Action release)
        {
            Button button = CreateHalfButton(parent, caption, side);

            var action = new SpawnAction
            {
                Release = release,
                Caption = caption,
                Button = button,
                Label = button.GetComponentInChildren<Text>(),
            };
            button.onClick.AddListener(() => BeginSpawn(action));
            return action;
        }

        static Row CreateRow(Transform content, string label, ref float y, bool withMeter)
        {
            Text caption = UIFactory.CreateLabel(content, label, RowSize, y, RowHeight,
                LabelColor, UIFactory.MediumFont);
            caption.rectTransform.anchorMax = new Vector2(0f, 1f);
            caption.rectTransform.pivot = new Vector2(0f, 1f);
            caption.rectTransform.sizeDelta = new Vector2(LabelWidth, RowHeight);

            Text value = UIFactory.CreateLabel(content, "--", RowSize, y, RowHeight,
                ValueColor, UIFactory.MediumFont);
            value.alignment = TextAnchor.MiddleRight;

            var row = new Row { Value = value };
            y -= RowHeight;

            if (withMeter)
            {
                y -= MeterTop;

                var trackGo = new GameObject($"Meter ({label})", typeof(Image));
                trackGo.transform.SetParent(content, false);

                var track = trackGo.GetComponent<Image>();
                track.color = MeterTrackColor;
                track.raycastTarget = false;

                RectTransform trackRt = track.rectTransform;
                trackRt.anchorMin = new Vector2(0f, 1f);
                trackRt.anchorMax = new Vector2(1f, 1f);
                trackRt.pivot = new Vector2(0.5f, 1f);
                trackRt.sizeDelta = new Vector2(0f, MeterHeight);
                trackRt.anchoredPosition = new Vector2(0f, y);

                var fillGo = new GameObject("Fill", typeof(Image));
                fillGo.transform.SetParent(trackGo.transform, false);

                row.FillImage = fillGo.GetComponent<Image>();
                row.FillImage.color = MeterLow;
                row.FillImage.raycastTarget = false;

                row.Fill = row.FillImage.rectTransform;
                row.Fill.anchorMin = Vector2.zero;
                row.Fill.anchorMax = new Vector2(0f, 1f);
                row.Fill.pivot = new Vector2(0f, 0.5f);
                row.Fill.offsetMin = Vector2.zero;
                row.Fill.offsetMax = Vector2.zero;

                y -= MeterHeight;
            }

            y -= RowGap;
            return row;
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame)
            {
                if (_visible) Close();
                else Open();
            }

            if (DevFreeze.Active && (!_visible || GameMenu.IsOpen || !DevFreeze.CanFreeze)) Close();

            TickSpawn();
            TickNight(kb);

            if (!_visible) return;

            TickBlocks();
            TickLabels();
            Sample();

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < RefreshInterval) return;

            Refresh();
        }

        void Open()
        {
            SetVisible(true);
            if (DevFreeze.CanFreeze) DevFreeze.Begin();
            TickBlocks();
            TickLabels();
        }

        void Close()
        {
            SetVisible(false);
            DevFreeze.End();
        }

        void RunAndClose(Action run)
        {
            Close();
            run();
        }

        void SetVisible(bool visible)
        {
            _visible = visible;
            if (_panel != null) _panel.SetActive(visible);

            _frameSum = 0d;
            _cpuSum = 0d;
            _gpuSum = 0d;
            _trisSum = 0d;
            _samples = 0;
            _gpuSamples = 0;
            _trisSamples = 0;
            _elapsed = 0f;
        }

        void TickBlocks()
        {
            bool changed = false;
            for (int c = 0; c < ColumnCount; c++)
            {
                List<Block> blocks = _blocks[c];
                for (int i = 0; i < blocks.Count; i++)
                {
                    Block block = blocks[i];
                    bool shown = block.Visible == null || block.Visible();
                    if (shown == block.Shown) continue;

                    block.Shown = shown;
                    changed = true;
                }
            }

            if (changed) Layout();
        }

        void Layout()
        {
            float tallest = 0f;
            for (int c = 0; c < ColumnCount; c++)
            {
                float y = 0f;
                List<Block> blocks = _blocks[c];
                for (int i = 0; i < blocks.Count; i++)
                {
                    Block block = blocks[i];
                    block.Rt.gameObject.SetActive(block.Shown);
                    if (!block.Shown) continue;

                    block.Rt.anchoredPosition = new Vector2(0f, y);
                    block.Rt.sizeDelta = new Vector2(0f, block.Height);
                    y -= block.Height;
                }

                tallest = Mathf.Max(tallest, -y);
            }

            float columns = Mathf.Max(0f, tallest - ButtonGap);
            for (int i = 0; i < _dividers.Length; i++) _dividers[i].sizeDelta = new Vector2(1f, columns);

            _panelRt.sizeDelta = new Vector2(PanelWidth,
                2f * PadY + HeaderHeight + HeaderToColumns + columns);
        }

        void TickLabels()
        {
            SetCaption(_skipLabel, DevSkip.Available, DevSkip.Caption);
            SetCaption(_jumpLabel, DevJump.Available, DevJump.Caption);
            SetCaption(_finalLabel, DevFinalJump.Available, DevFinalJump.Caption);
            SetCaption(_sceneLabel, DevSceneJump.Available, DevSceneJump.Caption);

            bool frozen = DevFreeze.Active;
            if (frozen != _statusFrozen)
            {
                _statusFrozen = frozen;
                _status.text = frozen ? FrozenStatus : IdleStatus;
                _status.color = frozen ? SpawnPendingColor : TitleColor;
            }

            for (int i = 0; i < _checks.Count; i++)
            {
                Check check = _checks[i];
                bool on = check.Get();
                if (on == check.On) continue;

                check.On = on;
                check.Mark.enabled = on;
            }
        }

        static void SetCaption(Text label, bool shown, string caption)
        {
            if (!shown) return;

            string text = caption ?? string.Empty;
            if (label.text != text) label.text = text;
        }

        static void SetFire(bool on)
        {
            Aerodrome field = Aerodrome.Current;
            if (field != null) field.SetBurning(on);
        }

        static void TickNight(Keyboard kb)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (kb == null) return;
            if (kb.f7Key.wasPressedThisFrame)
                NightLightingController.OverlayEnabled = !NightLightingController.OverlayEnabled;
            if (kb.f8Key.wasPressedThisFrame) NightLightingController.DevToggle();
#endif
        }

        void TickSpawn()
        {
            bool available = DevSpawn.Available;
            bool zeppelin = DevSpawn.ZeppelinOffered;
            if (available != _spawnShown || zeppelin != _zeppelinShown) ShowSpawn(available, zeppelin);
            if (!available) return;

            Countdown(_scoutSpawn);
            Countdown(_fighterSpawn);
            Countdown(_duelistSpawn);
            Countdown(_truckSpawn);
            Countdown(_tankSpawn);
            Countdown(_zeppelinSpawn);

            if (zeppelin && _zeppelinSpawn.Remaining <= 0f)
                _zeppelinSpawn.Button.interactable = DevSpawn.ZeppelinReady;
        }

        void ShowSpawn(bool shown, bool zeppelin)
        {
            _spawnShown = shown;
            _zeppelinShown = zeppelin;
            _zeppelinSpawn.Button.gameObject.SetActive(zeppelin);

            if (!zeppelin) ResetSpawn(_zeppelinSpawn);
            if (shown) return;

            ResetSpawn(_scoutSpawn);
            ResetSpawn(_fighterSpawn);
            ResetSpawn(_duelistSpawn);
            ResetSpawn(_truckSpawn);
            ResetSpawn(_tankSpawn);
        }

        void BeginSpawn(SpawnAction action)
        {
            if (action.Remaining > 0f || !DevSpawn.Available) return;

            action.Remaining = DevSpawn.Delay;
            action.Button.interactable = false;
            action.Label.color = SpawnPendingColor;
            action.Label.text = $"{action.Caption}   {action.Remaining:0.0}";
        }

        void Countdown(SpawnAction action)
        {
            if (action == null || action.Remaining <= 0f || DevFreeze.Active) return;

            action.Remaining -= Time.unscaledDeltaTime;
            if (action.Remaining > 0f)
            {
                action.Label.text = $"{action.Caption}   {action.Remaining:0.0}";
                return;
            }

            ResetSpawn(action);
            action.Release?.Invoke();
        }

        static void ResetSpawn(SpawnAction action)
        {
            if (action == null) return;

            action.Remaining = 0f;
            action.Button.interactable = true;
            action.Label.color = ValueColor;
            action.Label.text = action.Caption;
        }

        void Sample()
        {
            CaptureTiming();

            double frameMs = Math.Max(Time.unscaledDeltaTime, 0.0001f) * 1000d;
            _frameSum += frameMs;
            _samples++;

            _cpuSum += ReadCpuMs(frameMs);

            double gpuMs = ReadGpuMs();
            if (gpuMs > 0d)
            {
                _gpuSum += gpuMs;
                _gpuSamples++;
            }

            if (_triangles.Valid && _triangles.LastValue > 0)
            {
                _trisSum += _triangles.LastValue;
                _trisSamples++;
            }
        }

        void CaptureTiming()
        {
            FrameTimingManager.CaptureFrameTimings();
            _hasTiming = FrameTimingManager.GetLatestTimings(1, _timings) > 0;
        }

        double ReadCpuMs(double frameMs)
        {
            if (_mainThread.Valid && _mainThread.LastValue > 0) return _mainThread.LastValue * 1e-6;
            if (_hasTiming && _timings[0].cpuMainThreadFrameTime > 0d) return _timings[0].cpuMainThreadFrameTime;
            return frameMs;
        }

        double ReadGpuMs()
        {
            if (_gpuFrame.Valid && _gpuFrame.LastValue > 0) return _gpuFrame.LastValue * 1e-6;
            if (_hasTiming) return _timings[0].gpuFrameTime;
            return 0d;
        }

        void Refresh()
        {
            double frameMs = _samples > 0 ? _frameSum / _samples : 0d;
            double cpuMs = _samples > 0 ? _cpuSum / _samples : 0d;
            double gpuMs = _gpuSamples > 0 ? _gpuSum / _gpuSamples : 0d;
            double budgetMs = BudgetMs();

            SetMetric(_cpu, $"{cpuMs:0.0} ms   {Percent(cpuMs, budgetMs)}", (float)(cpuMs / budgetMs));

            if (_gpuSamples > 0)
                SetMetric(_gpu, $"{gpuMs:0.0} ms   {Percent(gpuMs, budgetMs)}", (float)(gpuMs / budgetMs));
            else
                SetMetric(_gpu, "n/a", 0f);

            _tris.Value.text = _trisSamples > 0
                ? Count(_trisSum / _trisSamples)
                : $"~{Count(VisibleFaces())} faces";

            _ram.Value.text = $"{Megabytes(TotalMemory())} MB   gc {Megabytes(ManagedMemory())} MB";

            double fps = frameMs > 0d ? 1000d / frameMs : 0d;
            _fps.Value.text = $"{fps:0}   {frameMs:0.0} ms";

            _wave.Value.text = CampaignScriptRunner.WaveStatus ?? "--";

            _frameSum = 0d;
            _cpuSum = 0d;
            _gpuSum = 0d;
            _trisSum = 0d;
            _samples = 0;
            _gpuSamples = 0;
            _trisSamples = 0;
            _elapsed = 0f;
        }

        static void SetMetric(Row row, string text, float fraction)
        {
            row.Value.text = text;
            if (row.Fill == null) return;

            float clamped = Mathf.Clamp01(fraction);
            row.Fill.anchorMax = new Vector2(clamped, 1f);
            row.FillImage.color = clamped < MeterWarn ? MeterLow : clamped < 1f ? MeterMid : MeterHigh;
        }

        static string Percent(double value, double budget) =>
            budget > 0d ? $"{value / budget * 100d:0}%" : "--";

        static string Megabytes(long bytes) => (bytes / (1024d * 1024d)).ToString("0");

        static string Count(double value)
        {
            if (value >= 1e6d) return $"{value / 1e6d:0.00}M";
            if (value >= 1e3d) return $"{value / 1e3d:0.0}k";
            return value.ToString("0");
        }

        long VisibleFaces()
        {
            long total = 0L;
            Renderer[] renderers =
                FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (!renderer.enabled || !renderer.isVisible) continue;

                Mesh mesh = MeshOf(renderer);
                if (mesh != null) total += FaceCount(mesh);
            }

            return total;
        }

        static Mesh MeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;

            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        long FaceCount(Mesh mesh)
        {
            if (_meshFaces.TryGetValue(mesh, out long cached)) return cached;

            long faces = 0L;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
                faces += (long)mesh.GetIndexCount(sub) / IndicesPerFace(mesh.GetTopology(sub));

            _meshFaces[mesh] = faces;
            return faces;
        }

        static int IndicesPerFace(MeshTopology topology) => topology switch
        {
            MeshTopology.Quads => 4,
            MeshTopology.Lines => 2,
            MeshTopology.LineStrip => 2,
            MeshTopology.Points => 1,
            _ => 3,
        };

        static double BudgetMs()
        {
            int target = Application.targetFrameRate;
            if (target > 0) return 1000d / target;

            double hz = Screen.currentResolution.refreshRateRatio.value;
            return hz > 1d ? 1000d / hz : 1000d / 60d;
        }

        long TotalMemory()
        {
            if (_systemMemory.Valid && _systemMemory.LastValue > 0) return _systemMemory.LastValue;

            long allocated = Profiler.GetTotalAllocatedMemoryLong();
            return allocated > 0 ? allocated : GC.GetTotalMemory(false);
        }

        long ManagedMemory()
        {
            if (_gcMemory.Valid && _gcMemory.LastValue > 0) return _gcMemory.LastValue;

            long mono = Profiler.GetMonoUsedSizeLong();
            return mono > 0 ? mono : GC.GetTotalMemory(false);
        }
    }
}
