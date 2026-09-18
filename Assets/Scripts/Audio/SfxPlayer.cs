using UnityEngine;

namespace LastGround.Audio
{
    /// <summary>
    /// Pooled 2D voices (TDD_02 §20.2 Audio voice, TDD_02 §23): a fixed set of AudioSources created at run start.
    /// Per-sound throttling (minimum interval, maximum simultaneous voices) keeps 300-zombie fights from turning into
    /// noise and caps CPU. Distance attenuation and pan are computed from the local player, not the camera, because
    /// the top-down camera is 22 m away from the action.
    /// </summary>
    public sealed class SfxPlayer : System.IDisposable
    {
        const float AudibleDistance = 35f;

        readonly AudioSource[] _voices;
        readonly SfxId[] _voiceSound;
        readonly AudioClip[] _clips;
        readonly float[] _lastPlayed = new float[(int)SfxId.Count];
        readonly float[] _minInterval = new float[(int)SfxId.Count];
        readonly int[] _maxVoices = new int[(int)SfxId.Count];
        readonly GameObject _root;

        public SfxPlayer(Transform parent, int voices, AudioClip[] clips)
        {
            _clips = clips;
            _root = new GameObject("[Sfx]");
            _root.transform.SetParent(parent, false);
            voices = Mathf.Max(4, voices);
            _voices = new AudioSource[voices];
            _voiceSound = new SfxId[voices];
            for (int i = 0; i < voices; i++)
            {
                var source = _root.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _voices[i] = source;
            }
            Limit(SfxId.Gunshot, 0.035f, voices / 2);
            Limit(SfxId.ZombieHit, 0.05f, 4);
            Limit(SfxId.ZombieDeath, 0.09f, 4);
            Limit(SfxId.PlayerHurt, 0.15f, 1);
            for (int i = 0; i < _lastPlayed.Length; i++) _lastPlayed[i] = float.MinValue;
        }

        /// <summary>Master SFX volume (Settings).</summary>
        public float Volume { get; set; } = 1f;

        public int Played { get; private set; }
        public int Throttled { get; private set; }

        /// <summary>Plays a sound at a world position heard from <paramref name="listener"/> (XZ).</summary>
        public void Play(SfxId id, Vector2 position, Vector2 listener, float volume, float pitch = 1f)
        {
            float now = Time.unscaledTime;
            int index = (int)id;
            if (now - _lastPlayed[index] < _minInterval[index])
            {
                Throttled++;
                return;
            }
            Vector2 offset = position - listener;
            float distance = offset.magnitude;
            if (distance > AudibleDistance) return;

            int active = 0, free = -1, oldest = -1;
            float longestPlaying = -1f;
            for (int i = 0; i < _voices.Length; i++)
            {
                AudioSource v = _voices[i];
                if (!v.isPlaying)
                {
                    if (free < 0) free = i;
                    continue;
                }
                if (_voiceSound[i] == id)
                {
                    active++;
                    if (v.time > longestPlaying)
                    {
                        longestPlaying = v.time;
                        oldest = i;
                    }
                }
            }
            int slot = active >= _maxVoices[index] ? oldest : free;
            if (slot < 0)
            {
                Throttled++;
                return;
            }

            float attenuation = 1f - distance / AudibleDistance;
            AudioSource source = _voices[slot];
            source.Stop();
            source.clip = _clips[index];
            source.volume = volume * attenuation * attenuation * Volume;
            source.pitch = pitch;
            source.panStereo = Mathf.Clamp(offset.x / 20f, -0.8f, 0.8f);
            source.Play();
            _voiceSound[slot] = id;
            _lastPlayed[index] = now;
            Played++;
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
            for (int i = 0; i < _clips.Length; i++)
                if (_clips[i] != null) Object.Destroy(_clips[i]);
        }

        void Limit(SfxId id, float minInterval, int maxVoices)
        {
            _minInterval[(int)id] = minInterval;
            _maxVoices[(int)id] = Mathf.Max(1, maxVoices);
        }
    }
}
