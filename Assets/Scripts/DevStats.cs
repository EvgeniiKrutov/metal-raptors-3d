using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
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

        static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.07f, 0.84f);
        static readonly Color TitleColor = new Color(0.52f, 0.58f, 0.66f, 1f);
        static readonly Color LabelColor = new Color(0.60f, 0.66f, 0.74f, 1f);
        static readonly Color ValueColor = new Color(0.92f, 0.95f, 1f, 1f);
        static readonly Color MeterTrackColor = new Color(1f, 1f, 1f, 0.10f);
        static readonly Color MeterLow = new Color(0.36f, 0.80f, 0.52f, 1f);
        static readonly Color MeterMid = new Color(0.94f, 0.76f, 0.30f, 1f);
        static readonly Color MeterHigh = new Color(0.90f, 0.32f, 0.26f, 1f);

        struct Row
        {
            public Text Value;
            public RectTransform Fill;
            public Image FillImage;
        }

        static DevStats _instance;

        ProfilerRecorder _mainThread;
        ProfilerRecorder _gpuFrame;
        ProfilerRecorder _systemMemory;
        ProfilerRecorder _gcMemory;

        readonly FrameTiming[] _timings = new FrameTiming[1];
        bool _hasTiming;

        GameObject _panel;
        Row _cpu;
        Row _gpu;
        Row _ram;
        Row _fps;

        double _frameSum;
        double _cpuSum;
        double _gpuSum;
        int _gpuSamples;
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
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;

            if (_mainThread.Valid) _mainThread.Dispose();
            if (_gpuFrame.Valid) _gpuFrame.Dispose();
            if (_systemMemory.Valid) _systemMemory.Dispose();
            if (_gcMemory.Valid) _gcMemory.Dispose();
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
            _ram = CreateRow(content, "RAM", ref y, false);
            _fps = CreateRow(content, "FPS", ref y, false);

            rt.sizeDelta = new Vector2(PanelWidth, -y - RowGap + 2f * PadY);
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
            _samples = 0;
            _gpuSamples = 0;
            _elapsed = 0f;
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

            _ram.Value.text = $"{Megabytes(TotalMemory())} MB   gc {Megabytes(ManagedMemory())} MB";

            double fps = frameMs > 0d ? 1000d / frameMs : 0d;
            _fps.Value.text = $"{fps:0}   {frameMs:0.0} ms";

            _frameSum = 0d;
            _cpuSum = 0d;
            _gpuSum = 0d;
            _samples = 0;
            _gpuSamples = 0;
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
