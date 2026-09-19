using System;
using LastGround.Core.Events;
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
    /// when more than a second off. Director announcements (new zombie type, elite) go reliable as they happen (3 B).
    /// </summary>
    public sealed class DirectorInfoSync : ITickable, IDisposable
    {
        const float SendInterval = 0.5f;
        const float SnapSeconds = 1f;

        readonly ISession _session;
        readonly RunStatus _status;
        readonly NetRawHandler _onInfo;
        readonly NetRawHandler _onAnnouncement;
        EventReader<DirectorAnnouncement> _announcements;
        float _timer;
        bool _received;

        public DirectorInfoSync(ISession session, RunStatus status)
        {
            _session = session;
            _status = status;
            _onInfo = OnInfo;
            _onAnnouncement = OnAnnouncement;
            _announcements = status.Announcements.CreateReader();
            if (!session.IsAuthority)
            {
                session.Subscribe(NetMsgId.DirectorInfo, _onInfo);
                session.Subscribe(NetMsgId.DirectorAnnouncement, _onAnnouncement);
            }
        }

        /// <summary>Host: NetSend phase. Client: Presentation phase (local clock).</summary>
        public void Tick(float dt, uint tick)
        {
            if (!_session.IsAuthority)
            {
                if (_received) _status.RunSeconds += dt;
                return;
            }
            while (_status.Announcements.TryRead(ref _announcements, out DirectorAnnouncement a))
            {
                NetWriter aw = _session.Begin(NetMsgId.DirectorAnnouncement);
                aw.WriteByte((byte)a.Kind);
                aw.WriteByte(a.ZombieType);
                aw.WriteByte(a.Elite);
                _session.SendToClients(NetChannel.Reliable);
            }
            _timer -= dt;
            if (_timer > 0f) return;
            _timer += SendInterval;
            NetWriter w = _session.Begin(NetMsgId.DirectorInfo);
            w.WriteVarUInt((uint)(_status.RunSeconds * 10f + 0.5f));
            w.WriteByte((byte)_status.Horde);
            w.WriteByte((byte)Math.Min(255, _status.Threat));
            _session.SendToClients(NetChannel.Unreliable);
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.DirectorInfo, _onInfo);
            _session.Unsubscribe(NetMsgId.DirectorAnnouncement, _onAnnouncement);
        }

        void OnAnnouncement(PlayerId sender, ref NetReader r)
        {
            var kind = (AnnouncementKind)r.ReadByte();
            byte type = r.ReadByte();
            byte elite = r.ReadByte();
            if (r.Failed || kind > AnnouncementKind.BossArrived) return;
            _status.Announcements.Publish(new DirectorAnnouncement { Kind = kind, ZombieType = type, Elite = elite });
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
