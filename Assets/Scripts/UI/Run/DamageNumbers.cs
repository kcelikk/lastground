using LastGround.Core.Events;
using LastGround.Core.Net;
using LastGround.Core.Tick;
using LastGround.Gameplay.Crowd;
using TMPro;
using UnityEngine;

namespace LastGround.UI.Run
{
    /// <summary>
    /// Floating damage numbers for the local player's hits (TDD_01 §5.7): a fixed pool of world-space TextMeshPro
    /// objects created at run start (cap from the quality preset), recycled oldest-first. Crits are larger and gold.
    /// Presentation phase; no allocation per hit.
    /// </summary>
    public sealed class DamageNumbers : ITickable
    {
        const float Life = 0.6f;
        const float Rise = 1.2f;
        const float Height = 2.1f;
        static readonly Color NormalColor = new Color(1f, 1f, 1f, 1f);
        static readonly Color CritColor = new Color(1f, 0.78f, 0.2f, 1f);

        readonly IGameEventStream<CrowdHit> _hits;
        readonly Camera _camera;
        readonly TextMeshPro[] _texts;
        readonly float[] _bornAt;
        readonly Vector3[] _origin;
        readonly bool[] _crit;
        EventReader<CrowdHit> _reader;
        int _next;
        float _time;

        public DamageNumbers(IGameEventStream<CrowdHit> hits, Camera camera, Transform parent, int cap)
        {
            _hits = hits;
            _reader = hits.CreateReader();
            _camera = camera;
            cap = Mathf.Max(4, cap);
            _texts = new TextMeshPro[cap];
            _bornAt = new float[cap];
            _origin = new Vector3[cap];
            _crit = new bool[cap];
            for (int i = 0; i < cap; i++)
            {
                var go = new GameObject("DamageNumber_" + i);
                go.transform.SetParent(parent, false);
                var text = go.AddComponent<TextMeshPro>();
                text.alignment = TextAlignmentOptions.Center;
                text.fontStyle = FontStyles.Bold;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.rectTransform.sizeDelta = new Vector2(4f, 1f);
                go.SetActive(false);
                _texts[i] = text;
                _bornAt[i] = float.MinValue;
            }
        }

        public void Tick(float dt, uint tick)
        {
            _time += dt;
            while (_hits.TryRead(ref _reader, out CrowdHit hit))
                if (hit.Local && hit.Damage > 0f) Spawn(hit);

            Quaternion facing = _camera.transform.rotation;
            for (int i = 0; i < _texts.Length; i++)
            {
                TextMeshPro text = _texts[i];
                if (!text.gameObject.activeSelf) continue;
                float age = (_time - _bornAt[i]) / Life;
                if (age >= 1f)
                {
                    text.gameObject.SetActive(false);
                    continue;
                }
                text.transform.SetPositionAndRotation(_origin[i] + Vector3.up * (Rise * age), facing);
                Color c = _crit[i] ? CritColor : NormalColor;
                c.a = age < 0.6f ? 1f : 1f - (age - 0.6f) / 0.4f;
                text.color = c;
            }
        }

        void Spawn(in CrowdHit hit)
        {
            int i = _next;
            _next = (_next + 1) % _texts.Length;
            TextMeshPro text = _texts[i];
            _bornAt[i] = _time;
            _crit[i] = hit.Crit;
            // Small sideways jitter from the slot so numbers from a burst do not stack exactly.
            float jitter = ((hit.Slot * 37 + i * 11) % 9 - 4) * 0.08f;
            _origin[i] = new Vector3(hit.X + jitter, Height, hit.Z);
            text.fontSize = hit.Crit ? 7f : 5f;
            text.SetText("{0}", Mathf.RoundToInt(hit.Damage));
            text.gameObject.SetActive(true);
        }
    }
}
