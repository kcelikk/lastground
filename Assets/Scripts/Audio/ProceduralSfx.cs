using LastGround.Core.Random;
using UnityEngine;

namespace LastGround.Audio
{
    /// <summary>
    /// Placeholder combat sounds synthesised at run start (M4 "basic audio"): filtered noise bursts and short
    /// sweeps, no asset import. Replaced by recorded SFX in M12 (TDD_02 §23); the SfxId table stays.
    /// </summary>
    public static class ProceduralSfx
    {
        const int SampleRate = 22050;

        public static AudioClip[] CreateAll()
        {
            var clips = new AudioClip[(int)SfxId.Count];
            clips[(int)SfxId.Gunshot] = Create("sfx_gunshot", 0.16f, 1u, Gunshot);
            clips[(int)SfxId.ZombieHit] = Create("sfx_zombie_hit", 0.07f, 2u, Hit);
            clips[(int)SfxId.ZombieDeath] = Create("sfx_zombie_death", 0.4f, 3u, Death);
            clips[(int)SfxId.PlayerHurt] = Create("sfx_player_hurt", 0.22f, 4u, Hurt);
            return clips;
        }

        delegate float Sample(float t, float noise, ref float state);

        static AudioClip Create(string name, float seconds, uint seed, Sample sample)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            var rng = new DeterministicRandom(seed);
            float state = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                data[i] = Mathf.Clamp(sample(t, rng.NextFloat() * 2f - 1f, ref state), -1f, 1f);
            }
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Gunshot(float t, float noise, ref float lowPass)
        {
            lowPass += (noise - lowPass) * 0.45f;
            float crack = lowPass * Mathf.Exp(-t * 38f);
            float thump = Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t * 22f);
            return (crack * 0.9f + thump * 0.7f) * 0.9f;
        }

        static float Hit(float t, float noise, ref float lowPass)
        {
            lowPass += (noise - lowPass) * 0.25f;
            return (lowPass * 1.4f + Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.5f) * Mathf.Exp(-t * 55f);
        }

        static float Death(float t, float noise, ref float lowPass)
        {
            lowPass += (noise - lowPass) * 0.08f;
            float frequency = Mathf.Lerp(150f, 55f, t / 0.4f);
            float growl = Mathf.Sin(2f * Mathf.PI * frequency * t) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 23f * t));
            return (growl * 0.55f + lowPass * 1.6f) * Mathf.Exp(-t * 7f);
        }

        static float Hurt(float t, float noise, ref float lowPass)
        {
            float frequency = Mathf.Lerp(230f, 140f, t / 0.22f);
            float square = Mathf.Sin(2f * Mathf.PI * frequency * t) > 0f ? 0.5f : -0.5f;
            lowPass += (square - lowPass) * 0.2f;
            return lowPass * Mathf.Exp(-t * 11f) + noise * 0.05f * Mathf.Exp(-t * 30f);
        }
    }
}
