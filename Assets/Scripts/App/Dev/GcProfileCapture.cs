using System.IO;
using LastGround.Core.Logging;
using UnityEngine;
using UnityEngine.Profiling;

namespace LastGround.App.Dev
{
    /// <summary>
    /// Dev: records a few frames of profiler data with allocation call stacks to
    /// persistentDataPath/gc_<scene>.raw, for the editor GcAllocReport tool. Started by -lg-gc-capture.
    /// </summary>
    public sealed class GcProfileCapture : MonoBehaviour
    {
        const float Delay = 8f;
        const int Frames = 120;

        float _timer;
        int _captured = -1;
        string _path;

        void Update()
        {
            if (_captured < 0)
            {
                _timer += Time.unscaledDeltaTime;
                if (_timer < Delay) return;
                _path = Path.Combine(Application.persistentDataPath, "gc_" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ".raw");
                Profiler.logFile = _path;
                Profiler.enableBinaryLog = true;
                Profiler.enableAllocationCallstacks = true;
                Profiler.enabled = true;
                _captured = 0;
                return;
            }

            if (++_captured < Frames) return;
            Profiler.enabled = false;
            Profiler.enableBinaryLog = false;
            Profiler.logFile = string.Empty;
            Log.Info(LogCategory.App, "[GcCapture] wrote " + _path);
            Destroy(this);
        }
    }
}
