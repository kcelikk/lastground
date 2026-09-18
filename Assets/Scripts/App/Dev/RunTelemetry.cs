using System.Globalization;
using LastGround.Core.Net;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Zombies;
using UnityEngine;

namespace LastGround.App.Dev
{
    /// <summary>
    /// Development builds: one log line every 5 s with network and frame stats, so soak tests can be read from
    /// logcat / Player.log (TDD_02 §17.9 CSV-style). Allocates a string every 5 s; not present in release builds.
    /// </summary>
    public sealed class RunTelemetry : MonoBehaviour
    {
        const float Interval = 5f;

        ISession _session;
        ICrowdRenderSource _crowd;
        ZombieWorld _world;
        float _simMsSum;
        float _simMsMax;
        int _simSamples;
        float _timer;
        int _frames;
        float _maxDt;
        float _elapsed;

        public void Bind(ISession session, ICrowdRenderSource crowd, ZombieWorld world = null)
        {
            _session = session;
            _crowd = crowd;
            _world = world;
        }

        void Update()
        {
            if (_session == null) return;
            float dt = Time.unscaledDeltaTime;
            _timer += dt;
            _elapsed += dt;
            _frames++;
            if (dt > _maxDt) _maxDt = dt;
            if (_world != null)
            {
                _simMsSum += _world.LastTickMs;
                if (_world.LastTickMs > _simMsMax) _simMsMax = _world.LastTickMs;
                _simSamples++;
            }
            if (_timer < Interval) return;

            INetStats s = _session.Stats;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[NetStats] t={0:0} role={1} players={2} rtt={3:0}ms inKBps={4:0.00} outKBps={5:0.00} inMsg={6} outMsg={7} " +
                "snapKBps={8:0.00} crowd={9} fps={10:0.0} maxFrameMs={11:0.0} simMs={12:0.00} simMaxMs={13:0.00} unstuck={14}",
                _elapsed, _session.Role, _session.Players.Count, _session.Clock.Rtt * 1000.0,
                s.InBytesPerSecond / 1024f, s.OutBytesPerSecond / 1024f, s.InMessagesPerSecond, s.OutMessagesPerSecond,
                (s.InBytesFor(NetMsgId.ZombieSnapshot) + s.OutBytesFor(NetMsgId.ZombieSnapshot)) / 1024f,
                _crowd.ActiveCount, _frames / _timer, _maxDt * 1000f,
                _simSamples > 0 ? _simMsSum / _simSamples : 0f, _simMsMax, _world != null ? _world.Unstuck : 0));
            _simMsSum = 0f;
            _simMsMax = 0f;
            _simSamples = 0;
            _timer = 0f;
            _frames = 0;
            _maxDt = 0f;
        }
    }
}
