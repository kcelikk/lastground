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
    /// Player vitals, host → clients (TDD_02 §15.5 PlayerVitals: reliable, on change, ≤ 10 Hz, 8 B per player):
    /// health, life state (alive/downed/dead), invulnerability, countdown (bleedout or return), revive progress.
    /// Clients write them into their PlayerStateTable and raise PlayerHurt for health drops and going down, so
    /// camera shake, haptics and sound work the same everywhere.
    /// </summary>
    public sealed class PlayerVitalsSync : ITickable, IDisposable
    {
        const float MinInterval = 0.1f;
        const float HealthScale = 10f;
        const byte FlagInvulnerable = 1 << 0;

        readonly ISession _session;
        readonly PlayerStateTable _players;
        readonly NetRawHandler _onVitals;
        readonly ushort[] _sentHealth = new ushort[PlayerStateTable.Max];
        readonly byte[] _sentFlags = new byte[PlayerStateTable.Max];
        readonly byte[] _sentLife = new byte[PlayerStateTable.Max];
        readonly ushort[] _sentCountdown = new ushort[PlayerStateTable.Max];
        readonly byte[] _sentRevive = new byte[PlayerStateTable.Max];
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
                if (active != _sentActive[p] || (active && (Health(p) != _sentHealth[p] || Flags(p) != _sentFlags[p]
                    || (byte)_players.Life[p] != _sentLife[p] || Countdown(p) != _sentCountdown[p] || Revive(p) != _sentRevive[p]))) changed = true;
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
                _sentLife[p] = (byte)_players.Life[p];
                _sentCountdown[p] = Countdown(p);
                _sentRevive[p] = Revive(p);
                w.WriteByte((byte)p);
                w.WriteUShort(_sentHealth[p]);
                w.WriteByte(_sentLife[p]);
                w.WriteByte(_sentFlags[p]);
                w.WriteUShort(_sentCountdown[p]);
                w.WriteByte(_sentRevive[p]);
            }
            _session.SendToClients(NetChannel.Reliable);
            _sinceSent = 0f;
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.PlayerVitals, _onVitals);
        }

        ushort Health(int p) => (ushort)Math.Min(ushort.MaxValue, Math.Max(0f, _players.Health[p]) * HealthScale + 0.5f);

        byte Flags(int p) => _players.Invulnerable[p] ? FlagInvulnerable : (byte)0;

        /// <summary>Tenths of a second, whole seconds only change the message when dead (a 60 s wait).</summary>
        ushort Countdown(int p)
        {
            float seconds = Math.Max(0f, _players.Countdown[p]);
            return _players.Life[p] == PlayerLife.Dead ? (ushort)(Math.Ceiling(seconds) * 10) : (ushort)Math.Min(ushort.MaxValue, seconds * 10f + 0.5f);
        }

        byte Revive(int p) => (byte)(Math.Min(1f, Math.Max(0f, _players.ReviveProgress[p])) * 255f);

        void OnVitals(PlayerId sender, ref NetReader r)
        {
            int count = r.ReadByte();
            for (int k = 0; k < count && !r.Failed; k++)
            {
                int p = r.ReadByte();
                float health = r.ReadUShort() / HealthScale;
                var life = (PlayerLife)r.ReadByte();
                byte flags = r.ReadByte();
                float countdown = r.ReadUShort() / 10f;
                float revive = r.ReadByte() / 255f;
                if (r.Failed || p >= PlayerStateTable.Max) return;

                float lost = _players.Health[p] - health;
                bool wentDown = life == PlayerLife.Downed && _players.Life[p] == PlayerLife.Alive;
                _players.Health[p] = health;
                _players.Life[p] = life;
                _players.Invulnerable[p] = (flags & FlagInvulnerable) != 0;
                _players.Countdown[p] = countdown;
                _players.ReviveProgress[p] = revive;
                if (lost > 0.05f || wentDown) _players.Hurt.Publish(new PlayerHurt { Player = p, Amount = Math.Max(0f, lost), Died = wentDown });
            }
        }
    }
}
