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
    /// Emotes (TDD_02 §15 <c>EmoteMessage{playerId, emoteId}</c>, M9): a client asks the host (1 B), the host checks
    /// the index and a per-player cooldown and relays it to everyone (2 B); every device publishes it on
    /// <see cref="PlayerStateTable.Emotes"/> for the bubble and the animation.
    /// </summary>
    public sealed class EmoteSync : ITickable, IDisposable
    {
        const float Cooldown = 2f;

        readonly ISession _session;
        readonly PlayerStateTable _players;
        readonly int _emoteCount;
        readonly float[] _cooldown = new float[PlayerStateTable.Max];
        readonly NetRawHandler _onRequest;
        readonly NetRawHandler _onPlayed;

        public EmoteSync(ISession session, PlayerStateTable players, int emoteCount)
        {
            _session = session;
            _players = players;
            _emoteCount = emoteCount;
            _onRequest = OnRequest;
            _onPlayed = OnPlayed;
            if (session.IsAuthority) session.Subscribe(NetMsgId.EmoteRequest, _onRequest);
            else session.Subscribe(NetMsgId.EmotePlayed, _onPlayed);
        }

        /// <summary>The local player plays an emote.</summary>
        public void Play(byte emote)
        {
            if (_session.IsAuthority)
            {
                Accept(_session.LocalPlayer.Value, emote);
                return;
            }
            NetWriter w = _session.Begin(NetMsgId.EmoteRequest);
            w.WriteByte(emote);
            _session.SendToHost(NetChannel.Reliable);
        }

        public void Tick(float dt, uint tick)
        {
            for (int p = 0; p < _cooldown.Length; p++) if (_cooldown[p] > 0f) _cooldown[p] -= dt;
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.EmoteRequest, _onRequest);
            _session.Unsubscribe(NetMsgId.EmotePlayed, _onPlayed);
        }

        void OnRequest(PlayerId sender, ref NetReader r)
        {
            byte emote = r.ReadByte();
            if (r.Failed || !sender.IsValid) return;
            Accept(sender.Value, emote);
        }

        void Accept(int player, byte emote)
        {
            if ((uint)player >= PlayerStateTable.Max || emote >= _emoteCount || _cooldown[player] > 0f) return;
            _cooldown[player] = Cooldown;
            _players.Emotes.Publish(new PlayerEmote { Player = player, Emote = emote });
            NetWriter w = _session.Begin(NetMsgId.EmotePlayed);
            w.WriteByte((byte)player);
            w.WriteByte(emote);
            _session.SendToClients(NetChannel.Reliable);
        }

        void OnPlayed(PlayerId sender, ref NetReader r)
        {
            int player = r.ReadByte();
            byte emote = r.ReadByte();
            if (r.Failed || player >= PlayerStateTable.Max || emote >= _emoteCount) return;
            _players.Emotes.Publish(new PlayerEmote { Player = player, Emote = emote });
        }
    }
}
