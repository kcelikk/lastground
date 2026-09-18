using System;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Net.Wire;
using LastGround.Core.Tick;
using LastGround.Gameplay.Players;

namespace LastGround.Networking.Replication
{
    /// <summary>
    /// Player health and life state, host → clients (TDD_02 §15.5 PlayerVitals: reliable, on change, ≤ 10 Hz).
    /// Clients write the values into their PlayerStateTable and raise PlayerHurt for health drops, so camera shake,
    /// haptics and sound work the same everywhere.
    /// </summary>
    public sealed class PlayerVitalsSync : ITickable, IDisposable
    {
        const float MinInterval = 0.1f;
        const float HealthScale = 10f;
        const byte FlagDead = 1 << 0;
        const byte FlagInvulnerable = 1 << 1;

        readonly ISession _session;
        readonly PlayerStateTable _players;
        readonly NetRawHandler _onVitals;
        readonly ushort[] _sentHealth = new ushort[PlayerStateTable.Max];
        readonly byte[] _sentFlags = new byte[PlayerStateTable.Max];
        readonly bool[] _sentActive = new bool[PlayerStateTable.Max];
        float _sinceSent = MinInterval;

        public PlayerVitalsSync(ISession session, PlayerStateTable players)
        {
            _session = session;
            _players = players;
            _onVitals = OnVitals;
            if (!session.IsAuthority) session.Subscribe(NetMsgId.PlayerVitals, _onVitals);
        }

        public void Tick(float dt, uint tick)
        {
            if (!_session.IsAuthority) return;
            _sinceSent += dt;
            if (_sinceSent < MinInterval) return;

            bool changed = false;
            int count = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                bool active = _players.Active[p];
                if (active) count++;
                if (active != _sentActive[p] || (active && (Health(p) != _sentHealth[p] || Flags(p) != _sentFlags[p]))) changed = true;
            }
            if (!changed || count == 0) return;

            NetWriter w = _session.Begin(NetMsgId.PlayerVitals);
            w.WriteByte((byte)count);
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                _sentActive[p] = _players.Active[p];
                if (!_players.Active[p]) continue;
                _sentHealth[p] = Health(p);
                _sentFlags[p] = Flags(p);
                w.WriteByte((byte)p);
                w.WriteUShort(_sentHealth[p]);
                w.WriteByte(_sentFlags[p]);
            }
            _session.SendToClients(NetChannel.Reliable);
            _sinceSent = 0f;
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.PlayerVitals, _onVitals);
        }

        ushort Health(int p) => (ushort)Math.Min(ushort.MaxValue, Math.Max(0f, _players.Health[p]) * HealthScale + 0.5f);

        byte Flags(int p) => (byte)((_players.Dead[p] ? FlagDead : 0) | (_players.Invulnerable[p] ? FlagInvulnerable : 0));

        void OnVitals(PlayerId sender, ref NetReader r)
        {
            int count = r.ReadByte();
            for (int k = 0; k < count && !r.Failed; k++)
            {
                int p = r.ReadByte();
                float health = r.ReadUShort() / HealthScale;
                byte flags = r.ReadByte();
                if (r.Failed || p >= PlayerStateTable.Max) return;

                bool dead = (flags & FlagDead) != 0;
                float lost = _players.Health[p] - health;
                bool died = dead && !_players.Dead[p];
                _players.Health[p] = health;
                _players.Dead[p] = dead;
                _players.Invulnerable[p] = (flags & FlagInvulnerable) != 0;
                if (lost > 0.05f || died) _players.Hurt.Publish(new PlayerHurt { Player = p, Amount = Math.Max(0f, lost), Died = died });
            }
        }
    }
}
