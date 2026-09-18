using System;
using UnityEngine;

namespace LastGround.Platform
{
    /// <summary>
    /// First-launch quality tier (D-006) from RAM and GPU family. Draft (TDD_03 §36 M2): the in-menu silent
    /// benchmark replaces the GPU heuristics in M13. 0 = LOW, 1 = MEDIUM, 2 = HIGH.
    /// </summary>
    public static class DeviceTierDetector
    {
        public const int Low = 0;
        public const int Medium = 1;
        public const int High = 2;

        public static int Detect() => Detect(SystemInfo.systemMemorySize, SystemInfo.graphicsDeviceName);

        public static int Detect(int systemMemoryMb, string gpuName)
        {
            int byMemory = systemMemoryMb < 6000 ? Low : systemMemoryMb < 8000 ? Medium : High;
            return Math.Min(byMemory, GpuCeiling(gpuName ?? string.Empty));
        }

        /// <summary>Caps tiers for GPU families that are known to be weak regardless of RAM.</summary>
        static int GpuCeiling(string gpu)
        {
            if (gpu.IndexOf("Adreno", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int series = ParseNumberAfter(gpu, "Adreno");
                if (series > 0 && series < 600) return Low;      // Adreno 5xx and older
                if (series >= 600 && series < 700) return Medium;
                return High;
            }
            if (gpu.IndexOf("Mali-G5", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gpu.IndexOf("Mali-T", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gpu.IndexOf("PowerVR", StringComparison.OrdinalIgnoreCase) >= 0)
                return Low;
            if (gpu.IndexOf("Mali-G6", StringComparison.OrdinalIgnoreCase) >= 0 ||
                gpu.IndexOf("Mali-G7", StringComparison.OrdinalIgnoreCase) >= 0)
                return Medium;
            return High;
        }

        static int ParseNumberAfter(string text, string token)
        {
            int start = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return 0;
            int value = 0;
            bool any = false;
            for (int i = start + token.Length; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= '0' && c <= '9')
                {
                    value = value * 10 + (c - '0');
                    any = true;
                }
                else if (any)
                {
                    break;
                }
            }
            return value;
        }
    }
}
