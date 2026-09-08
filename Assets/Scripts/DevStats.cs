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

        const float PanelWidth = 300f;
        const float PanelInset = 24f;
        const float PadX = 18f;
        const float PadY = 16f;

        const int TitleSize = 15;
        const float TitleRowHeight = 20f;
        const float TitleToRows = 14f;

        const int RowSize = 18;
        const float RowHeight = 24f;
        const float RowGap = 10f;
        const float MeterHeight = 3f;
        const float MeterTop = 5f;
        const float LabelWidth = 52f;

        const float MeterWarn = 0.7f;

        const float SpawnRuleGap = 12f;
        const float SpawnCaptionGap = 8f;
        const float SpawnButtonHeight = 30f;
        const float SpawnButtonGap = 8f;
        const int SpawnFontSize = 14;

        static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.07f, 0.84f);
        static readonly Color TitleColor = new Color(0.52f, 0.58f, 0.66f, 1f);
        static readonly Color LabelColor = new Color(0.60f, 0.66f, 0.74f, 1f);
        static readonly Color ValueColor = new Color(0.92f, 0.95f, 1f, 1f);
        static readonly Color MeterTrackColor = new Color(1f, 1f, 1f, 0.10f);
        static readonly Color MeterLow = new Color(0.36f, 0.80f, 0.52f, 1f);
        static readonly Color MeterMid = new Color(0.94f, 0.76f, 0.30f, 1f);
        static readonly Color MeterHigh = new Color(0.90f, 0.32f, 0.26f, 1f);
        static readonly Color SpawnPendingColor = new Color(0.94f, 0.76f, 0.30f, 1f);

        struct Row
        {
            public Text Value;
            public RectTransform Fill;
            public Image FillImage;
        }

        class SpawnAction
        {
            public EnemyRole Role;
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
        Row _cpu;
        Row _gpu;
        Row _tris;
        Row _ram;
        Row _fps;

        GameObject _spawnSection;
        SpawnAction _scoutSpawn;
        SpawnAction _fighterSpawn;
        float _statsHeight;
        float _spawnPanelHeight;
        bool _spawnShown;

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
            background.raycastTarget = false;

            var rt = background.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-PanelInset, -PanelInset);

            Transform content = CreateContent(rt);

            UIFactory.CreateLabel(content, "DEV STATS", TitleSize, 0f, TitleRowHeight,
                TitleColor, UIFactory.BoldFont);

            UIFactory.CreateLabel(content, "TAB", TitleSize, 0f, TitleRowHeight,
                TitleColor, UIFactory.MediumFont).alignment = TextAnchor.MiddleRight;

            float y = -(TitleRowHeight + TitleToRows);
            _cpu = CreateRow(content, "CPU", ref y, true);
            _gpu = CreateRow(content, "GPU", ref y, true);
            _tris = CreateRow(content, "TRIS", ref y, false);
            _ram = CreateRow(content, "RAM", ref y, false);
            _fps = CreateRow(content, "FPS", ref y, false);

            _panelRt = rt;
            _statsHeight = -y - RowGap + 2f * PadY;
            BuildSpawnSection(content, y);
            rt.sizeDelta = new Vector2(PanelWidth, _statsHeight);
        }

        void BuildSpawnSection(Transform content, float y)
        {
            var go = new GameObject("Spawn", typeof(RectTransform));
            go.transform.SetParent(content, false);
            _spawnSection = go;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            UIFactory.CreateRule(go.transform, y, new Vector2(PanelWidth - 2f * PadX, 1f),
                MeterTrackColor);
            y -= SpawnRuleGap;

            UIFactory.CreateLabel(go.transform, "SPAWN", TitleSize, y, TitleRowHeight,
                TitleColor, UIFactory.MediumFont);
            y -= TitleRowHeight + SpawnCaptionGap;

            _scoutSpawn = CreateSpawnButton(go.transform, "SPAWN SCOUT", EnemyRole.Scout, ref y);
            _fighterSpawn = CreateSpawnButton(go.transform, "SPAWN FIGHTER", EnemyRole.Fighter,
                ref y);

            _spawnPanelHeight = -y - SpawnButtonGap + 2f * PadY;
            go.SetActive(false);
        }

        SpawnAction CreateSpawnButton(Transform parent, string caption, EnemyRole role, ref float y)
        {
            Button button = UIFactory.CreateButton(parent, caption, Vector2.zero, null,
                new Vector2(0f, SpawnButtonHeight), fontSize: SpawnFontSize);

            var rt = (RectTransform)button.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, SpawnButtonHeight);
            rt.anchoredPosition = new Vector2(0f, y);
            y -= SpawnButtonHeight + SpawnButtonGap;

            var action = new SpawnAction
            {
                Role = role,
                Caption = caption,
                Button = button,
                Label = button.GetComponentInChildren<Text>(),
            };
            button.onClick.AddListener(() => BeginSpawn(action));
            return action;
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
            if (kb != null && kb.tabKey.wasPressedThisFrame) SetVisible(!_visible);

            TickSpawn();

            if (!_visible) return;

            Sample();

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < RefreshInterval) return;

            Refresh();
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

        void TickSpawn()
        {
            bool available = DevSpawn.Available;
            if (available != _spawnShown) ShowSpawnSection(available);
            if (!available) return;

            Countdown(_scoutSpawn);
            Countdown(_fighterSpawn);
        }

        void ShowSpawnSection(bool shown)
        {
            _spawnShown = shown;
            if (_spawnSection != null) _spawnSection.SetActive(shown);
            if (_panelRt != null)
                _panelRt.sizeDelta =
                    new Vector2(PanelWidth, shown ? _spawnPanelHeight : _statsHeight);

            if (shown) return;

            ResetSpawn(_scoutSpawn);
            ResetSpawn(_fighterSpawn);
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
            if (action == null || action.Remaining <= 0f) return;

            action.Remaining -= Time.unscaledDeltaTime;
            if (action.Remaining > 0f)
            {
                action.Label.text = $"{action.Caption}   {action.Remaining:0.0}";
                return;
            }

            ResetSpawn(action);
            DevSpawn.Spawn(action.Role);
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
