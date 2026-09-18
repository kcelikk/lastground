using System;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Net.Wire;
using LastGround.Core.Tick;
using LastGround.Gameplay.Director;

namespace LastGround.Networking.Replication
{
    /// <summary>
    /// Run status host → clients (TDD_02 §15.5 DirectorInfo, unreliable 2 Hz, 5–7 B): survival time, HORDE label,
    /// THREAT level. Clients advance the clock locally every frame and ease towards the host's value, snapping only
    /// when more than a second off.
    /// </summary>
    public sealed class DirectorInfoSync : ITickable, IDisposable
    {
        const float SendInterval = 0.5f;
        const float SnapSeconds = 1f;

        readonly ISession _session;
        readonly RunStatus _status;
        readonly NetRawHandler _onInfo;
        float _timer;
        bool _received;

        public DirectorInfoSync(ISession session, RunStatus status)
        {
            _session = session;
            _status = status;
            _onInfo = OnInfo;
            if (!session.IsAuthority) session.Subscribe(NetMsgId.DirectorInfo, _onInfo);
        }

        /// <summary>Host: NetSend phase. Client: Presentation phase (local clock).</summary>
        public void Tick(float dt, uint tick)
        {
            if (!_session.IsAuthority)
            {
                if (_received) _status.RunSeconds += dt;
                return;
            }
            _timer -= dt;
            if (_timer > 0f) return;
            _timer += SendInterval;
            NetWriter w = _session.Begin(NetMsgId.DirectorInfo);
            w.WriteVarUInt((uint)(_status.RunSeconds * 10f));
            w.WriteByte((byte)_status.Horde);
            w.WriteByte((byte)Math.Min(255, _status.Threat));
            _session.SendToClients(NetChannel.Unreliable);
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.DirectorInfo, _onInfo);
        }

        void OnInfo(PlayerId sender, ref NetReader r)
        {
            float seconds = r.ReadVarUInt() / 10f;
            byte horde = r.ReadByte();
            byte threat = r.ReadByte();
            if (r.Failed) return;
            float error = seconds - _status.RunSeconds;
            if (!_received || Math.Abs(error) > SnapSeconds) _status.RunSeconds = seconds;
            else _status.RunSeconds += error * 0.2f;
            _received = true;
            _status.Horde = (HordeLevel)Math.Min(horde, (byte)HordeLevel.Extreme);
            _status.Threat = Math.Max(1, (int)threat);
        }
    }
}
