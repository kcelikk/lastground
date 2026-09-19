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
            clips[(int)SfxId.HeavyShot] = Create("sfx_heavy_shot", 0.35f, 5u, HeavyShot);
            clips[(int)SfxId.Explosion] = Create("sfx_explosion", 1.1f, 6u, Explosion);
            clips[(int)SfxId.Spit] = Create("sfx_spit", 0.25f, 7u, Spit);
            clips[(int)SfxId.FuseBeep] = Create("sfx_fuse_beep", 0.12f, 8u, Beep);
            clips[(int)SfxId.Swap] = Create("sfx_swap", 0.12f, 9u, Swap);
            clips[(int)SfxId.BossRoar] = Create("sfx_boss_roar", 1.8f, 10u, Roar);
            clips[(int)SfxId.BossSlam] = Create("sfx_boss_slam", 1.4f, 11u, Slam);
            clips[(int)SfxId.RotorThump] = Create("sfx_rotor", 0.14f, 12u, Rotor);
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

        static float HeavyShot(float t, float noise, ref float lowPass)
        {
            lowPass += (noise - lowPass) * 0.3f;
            float crack = lowPass * Mathf.Exp(-t * 18f);
            float boom = Mathf.Sin(2f * Mathf.PI * 60f * t) * Mathf.Exp(-t * 9f);
            return crack * 1.1f + boom * 0.8f;
        }

        static float Explosion(float t, float noise, ref float lowPass)
        {
            lowPass += (noise - lowPass) * 0.05f;
            float rumble = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(55f, 30f, t) * t) * Mathf.Exp(-t * 3.5f);
            return lowPass * 3.2f * Mathf.Exp(-t * 2.8f) + rumble * 0.8f + noise * 0.4f * Mathf.Exp(-t * 25f);
        }

        static float Spit(float t, float noise, ref float lowPass)
        {
            lowPass += (noise - lowPass) * 0.35f;
            float gurgle = Mathf.Sin(2f * Mathf.PI * (180f + 60f * Mathf.Sin(2f * Mathf.PI * 30f * t)) * t);
            return (lowPass * 0.9f + gurgle * 0.35f) * Mathf.Exp(-t * 12f);
        }

        static float Beep(float t, float noise, ref float state) =>
            Mathf.Sin(2f * Mathf.PI * 1400f * t) * 0.5f * Mathf.Clamp01(t * 200f) * Mathf.Exp(-t * 18f);

        static float Swap(float t, float noise, ref float lowPass)
        {
            lowPass += (noise - lowPass) * 0.6f;
            float click = t < 0.02f || (t > 0.07f && t < 0.09f) ? 1f : 0f;
            return lowPass * click * 0.8f;
        }

        /// <summary>Deep, rough growl rising then falling (the boss's roar).</summary>
        static float Roar(float t, float noise, ref float lowPass)
        {
            lowPass += (noise - lowPass) * 0.12f;
            float envelope = Mathf.Clamp01(t * 4f) * Mathf.Exp(-Mathf.Max(0f, t - 0.9f) * 3f);
            float frequency = 62f + 22f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.6f));
            float growl = Mathf.Sin(2f * Mathf.PI * frequency * t) * (0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 31f * t));
            float rasp = lowPass * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 47f * t));
            return (growl * 0.7f + rasp * 1.3f) * envelope;
        }

        /// <summary>Concrete-cracking impact with a long sub rumble.</summary>
        static float Slam(float t, float noise, ref float lowPass)
        {
            lowPass += (noise - lowPass) * 0.04f;
            float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(48f, 26f, t / 1.4f) * t) * Mathf.Exp(-t * 2.6f);
            float crack = noise * Mathf.Exp(-t * 40f);
            return sub * 1.1f + lowPass * 3.5f * Mathf.Exp(-t * 3.2f) + crack * 0.6f;
        }

        /// <summary>One chopping blade pass: a low, band-limited whump.</summary>
        static float Rotor(float t, float noise, ref float lowPass)
        {
            lowPass += (noise - lowPass) * 0.1f;
            float whump = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.14f));
            return (whump * 0.6f + lowPass * 1.2f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.14f));
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
