using System;
using System.Collections.Generic;
using LastGround.Data.Quality;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LastGround.Rendering.Quality
{
    /// <summary>
    /// Applies a quality tier (TDD_02 §21.9): the Unity quality level (URP asset: render scale, MSAA, HDR, shadows),
    /// the frame-rate target, and exposes the matching presentation preset (LODs, caps) to render systems.
    /// </summary>
    public sealed class QualityService
    {
        /// <summary>Rendering above this internal height costs fill rate without visible gain at phone viewing distance (TDD_02 §21.9).</summary>
        public const float MaxRenderHeight = 1080f;

        readonly QualityPresetDefinition[] _presets;
        readonly Dictionary<UniversalRenderPipelineAsset, float> _authoredScale = new Dictionary<UniversalRenderPipelineAsset, float>();

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
            CapRenderHeight();
            Application.targetFrameRate = Mathf.Min(preset.TargetFps, preferredFps);
            Level = level;
            Current = preset;
            Changed?.Invoke(preset);
        }

        /// <summary>Effective render height of the current tier, for reports.</summary>
        public float RenderScale => QualitySettings.renderPipeline is UniversalRenderPipelineAsset urp ? urp.renderScale : 1f;

        /// <summary>
        /// Limits the URP render scale so the internal resolution never exceeds 1080 lines (2.5K tablets at HIGH).
        /// The authored scale is remembered, so switching tiers or screens never compounds the reduction.
        /// </summary>
        void CapRenderHeight()
        {
            if (!(QualitySettings.renderPipeline is UniversalRenderPipelineAsset urp)) return;
            if (!_authoredScale.TryGetValue(urp, out float authored))
            {
                authored = urp.renderScale;
                _authoredScale[urp] = authored;
            }
            float height = Mathf.Min(Screen.width, Screen.height);
            float cap = height > 0f ? MaxRenderHeight / height : 1f;
#if !UNITY_EDITOR
            // Runtime-only: in the editor this would modify the project asset.
            urp.renderScale = Mathf.Min(authored, cap);
#endif
        }
    }
}
