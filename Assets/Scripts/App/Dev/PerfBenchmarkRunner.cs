using System;
using System.Globalization;
using System.IO;
using System.Text;
using LastGround.Core.Logging;
using LastGround.Gameplay.Crowd;
using LastGround.Platform;
using LastGround.Rendering.Crowd;
using LastGround.Rendering.Quality;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LastGround.App.Dev
{
    /// <summary>
    /// Stepped crowd benchmark (TDD_02 §22.6, TDD_03 §36 M2): 20 → 50 → 100 → 150 → 200 → 250 → 300 entities,
    /// a short warm-up then a measured window per step. Writes a CSV to persistentDataPath/bench and logs each row
    /// as "[Bench]" for logcat. Editor numbers are not performance data.
    /// The frame cap is raised to 60 during the benchmark so a tier's headroom is visible even when it normally
    /// runs locked at 30 (LOW).
    /// </summary>
    public sealed class PerfBenchmarkRunner : MonoBehaviour
    {
        static readonly int[] Steps = { 20, 50, 100, 150, 200, 250, 300 };
        const float WarmupSeconds = 4f;
        const int BenchmarkFpsCap = 60;

        BenchmarkCrowdDriver _driver;
        ZombieRenderSystem _renderer;
        QualityService _quality;
        float _stepSeconds;
        bool _quitWhenDone;

        int _step = -1;
        float _stepTime;
        bool _measuring;
        float[] _frameMs;
        int _frames;
        long _gcSum;
        long _gcMax;
        int _drawnSum;
        float _tempStart;
        ProfilerRecorder _gc;
        ProfilerRecorder _memory;
        readonly StringBuilder _csv = new StringBuilder();

        public void Bind(BenchmarkCrowdDriver driver, ZombieRenderSystem renderer, QualityService quality, float stepSeconds, bool quitWhenDone)
        {
            _driver = driver;
            _renderer = renderer;
            _quality = quality;
            _stepSeconds = Mathf.Max(5f, stepSeconds);
            _quitWhenDone = quitWhenDone;
            _frameMs = new float[Mathf.CeilToInt(_stepSeconds * 200f)];
            Application.targetFrameRate = BenchmarkFpsCap;
            _csv.AppendLine("device,gpu,graphics_api,os,ram_mb,quality,render_scale,fps_cap,target,drawn_avg,frames,avg_fps,avg_ms,p50_ms,p99_ms,max_ms,gc_avg_b,gc_max_b,mem_mb,temp_start_c,temp_end_c");
            NextStep();
        }

        void OnEnable()
        {
            _gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _memory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        }

        void OnDisable()
        {
            _gc.Dispose();
            _memory.Dispose();
        }

        void Update()
        {
            if (_driver == null || _step >= Steps.Length) return;
            float dt = Time.unscaledDeltaTime;
            _stepTime += dt;

            if (!_measuring)
            {
                if (_stepTime < WarmupSeconds) return;
                _measuring = true;
                _stepTime = 0f;
                _tempStart = DeviceThermals.BatteryTemperatureC();
                return;
            }

            if (_frames < _frameMs.Length) _frameMs[_frames++] = dt * 1000f;
            long gc = _gc.Valid ? _gc.LastValue : 0;
            _gcSum += gc;
            if (gc > _gcMax) _gcMax = gc;
            _drawnSum += _renderer.DrawnLastFrame;

            if (_stepTime >= _stepSeconds) FinishStep();
        }

        void NextStep()
        {
            _step++;
            if (_step >= Steps.Length)
            {
                Finish();
                return;
            }
            _driver.TargetCount = Steps[_step];
            _stepTime = 0f;
            _measuring = false;
            _frames = 0;
            _gcSum = 0;
            _gcMax = 0;
            _drawnSum = 0;
        }

        void FinishStep()
        {
            int n = Mathf.Max(1, _frames);
            double total = 0;
            float max = 0f;
            for (int i = 0; i < _frames; i++)
            {
                total += _frameMs[i];
                if (_frameMs[i] > max) max = _frameMs[i];
            }
            Array.Sort(_frameMs, 0, _frames);
            float p50 = _frameMs[Mathf.Clamp(n / 2, 0, n - 1)];
            float p99 = _frameMs[Mathf.Clamp((int)(n * 0.99f), 0, n - 1)];
            double avgMs = total / n;
            float renderScale = QualitySettings.renderPipeline is UniversalRenderPipelineAsset urp ? urp.renderScale : 1f;

            string row = string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5},{6:0.00},{20},{7},{8:0},{9},{10:0.0},{11:0.00},{12:0.00},{13:0.00},{14:0.00},{15:0},{16},{17:0},{18:0.0},{19:0.0}",
                Clean(SystemInfo.deviceModel), Clean(SystemInfo.graphicsDeviceName), SystemInfo.graphicsDeviceType,
                Clean(SystemInfo.operatingSystem), SystemInfo.systemMemorySize, _quality.Current.Id, renderScale,
                Steps[_step], (float)_drawnSum / n, _frames, 1000.0 / avgMs, avgMs, p50, p99, max,
                (double)_gcSum / n, _gcMax, _memory.Valid ? _memory.LastValue / (1024.0 * 1024.0) : 0.0,
                _tempStart, DeviceThermals.BatteryTemperatureC(), Application.targetFrameRate);
            _csv.AppendLine(row);
            Debug.Log("[Bench] " + row);
            NextStep();
        }

        void Finish()
        {
            string dir = Path.Combine(Application.persistentDataPath, "bench");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, string.Format(CultureInfo.InvariantCulture, "bench_{0:yyyyMMdd_HHmmss}_{1}.csv", DateTime.Now, _quality.Current.Id));
            File.WriteAllText(file, _csv.ToString());
            Log.Info(LogCategory.App, "[Bench] done: " + file);
            if (_quitWhenDone) Application.Quit();
        }

        static string Clean(string text) => (text ?? string.Empty).Replace(',', ' ');
    }
}
