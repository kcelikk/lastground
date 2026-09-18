using System;
using LastGround.Data.Quality;
using UnityEngine;

namespace LastGround.Rendering.Quality
{
    /// <summary>
    /// Applies a quality tier (TDD_02 §21.9): the Unity quality level (URP asset: render scale, MSAA, HDR, shadows),
    /// the frame-rate target, and exposes the matching presentation preset (LODs, caps) to render systems.
    /// </summary>
    public sealed class QualityService
    {
        readonly QualityPresetDefinition[] _presets;

        public QualityService(QualityPresetDefinition[] presets)
        {
            if (presets == null || presets.Length == 0) throw new ArgumentException("No quality presets.");
            _presets = presets;
        }

        public QualityPresetDefinition Current { get; private set; }
        public int Level { get; private set; } = -1;
        public int LevelCount => _presets.Length;

        public event Action<QualityPresetDefinition> Changed;

        /// <summary>LOW is locked to 30 FPS; higher tiers use the player's preferred rate (30 or 60).</summary>
        public void Apply(int level, int preferredFps)
        {
            level = Mathf.Clamp(level, 0, _presets.Length - 1);
            QualityPresetDefinition preset = _presets[level];
            QualitySettings.SetQualityLevel(preset.QualityLevel, true);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Mathf.Min(preset.TargetFps, preferredFps);
            Level = level;
            Current = preset;
            Changed?.Invoke(preset);
        }
    }
}
