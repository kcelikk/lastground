using System;
using LastGround.Core.Events;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Net.Wire;
using LastGround.Core.Tick;
using LastGround.Data.Upgrades;
using LastGround.Gameplay.Upgrades;
using BuildChangedEvent = LastGround.Gameplay.Upgrades.BuildChanged;

namespace LastGround.Networking.Replication
{
    /// <summary>
    /// Team XP and upgrades over the network (TDD_01 §7.5, TDD_02 §15.5): TeamXp host → all (reliable, on change,
    /// ≤ 4 Hz), UpgradeOffer host → that player, SelectUpgrade player → host, BuildChanged host → all. Only upgrade
    /// ids and rarities travel; every device computes the stats. Host: routes offers (its own player's to the local
    /// panel) and applies picks. Client: forwards its picks and mirrors XP and builds.
    /// </summary>
    public sealed class ProgressionSync : ITickable, IOfferSink, IUpgradeChoiceSink, IDisposable
    {
        const float XpInterval = 0.25f;

        readonly ISession _session;
        readonly TeamXp _xp;
        readonly TeamBuilds _builds;
        readonly LocalOffers _local;
        readonly TeamProgress _progress;
        readonly NetRawHandler _onXp, _onOffer, _onSelect, _onBuild;
        EventReader<BuildChangedEvent> _buildReader;
        int _sentVersion = -1;
        float _xpTimer;

        /// <param name="progress">Host only; null on clients.</param>
        public ProgressionSync(ISession session, TeamXp xp, TeamBuilds builds, LocalOffers local, TeamProgress progress)
        {
            _session = session;
            _xp = xp;
            _builds = builds;
            _local = local;
            _progress = progress;
            _onXp = OnXp;
            _onOffer = OnOffer;
            _onSelect = OnSelect;
            _onBuild = OnBuild;
            _buildReader = builds.Changed.CreateReader();
            if (session.IsAuthority)
            {
                session.Subscribe(NetMsgId.SelectUpgrade, _onSelect);
            }
            else
            {
                session.Subscribe(NetMsgId.TeamXp, _onXp);
                session.Subscribe(NetMsgId.UpgradeOffer, _onOffer);
                session.Subscribe(NetMsgId.BuildChanged, _onBuild);
            }
        }

        public int OffersSent { get; private set; }
        public int PicksReceived { get; private set; }

        /// <summary>Host: NetSend phase.</summary>
        public void Tick(float dt, uint tick)
        {
            if (!_session.IsAuthority || _progress == null) return;
            _xp.Xp = _progress.Xp;
            _xp.Level = _progress.Level;
            _xp.XpToNext = _progress.XpToNext;

            _xpTimer -= dt;
            if (_progress.Version != _sentVersion && _xpTimer <= 0f)
            {
                _xpTimer = XpInterval;
                _sentVersion = _progress.Version;
                NetWriter w = _session.Begin(NetMsgId.TeamXp);
                w.WriteVarUInt((uint)_xp.Xp);
                w.WriteVarUInt((uint)_xp.XpToNext);
                w.WriteByte((byte)Math.Min(255, _xp.Level));
                _session.SendToClients(NetChannel.Reliable);
            }

            while (_builds.Changed.TryRead(ref _buildReader, out BuildChangedEvent change))
            {
                NetWriter w = _session.Begin(NetMsgId.BuildChanged);
                w.WriteByte((byte)change.Player);
                w.WriteByte((byte)change.Upgrade);
                w.WriteByte((byte)change.Rarity);
                _session.SendToClients(NetChannel.Reliable);
            }
        }

        /// <summary>Host: a new offer for a player — the host's own goes to the local panel, others over the network.</summary>
        public void Deliver(int player, in UpgradeOffer offer)
        {
            if (player == _session.LocalPlayer.Value)
            {
                _local.Deliver(player, offer);
                return;
            }
            NetWriter w = _session.Begin(NetMsgId.UpgradeOffer);
            w.WriteUShort(offer.Id);
            w.WriteByte(offer.Count);
            for (int i = 0; i < offer.Count; i++)
            {
                w.WriteByte((byte)offer.UpgradeAt(i));
                w.WriteByte((byte)offer.RarityAt(i));
            }
            _session.SendTo(new PlayerId((byte)player), NetChannel.Reliable);
            OffersSent++;
        }

        /// <summary>Client: the local player's pick goes to the host.</summary>
        public void Choose(int player, ushort offerId, int choice)
        {
            NetWriter w = _session.Begin(NetMsgId.SelectUpgrade);
            w.WriteUShort(offerId);
            w.WriteByte((byte)choice);
            _session.SendToHost(NetChannel.Reliable);
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.SelectUpgrade, _onSelect);
            _session.Unsubscribe(NetMsgId.TeamXp, _onXp);
            _session.Unsubscribe(NetMsgId.UpgradeOffer, _onOffer);
            _session.Unsubscribe(NetMsgId.BuildChanged, _onBuild);
        }

        void OnSelect(PlayerId sender, ref NetReader r)
        {
            ushort offerId = r.ReadUShort();
            int choice = r.ReadByte();
            if (r.Failed || !sender.IsValid) return;
            PicksReceived++;
            _progress?.Choose(sender.Value, offerId, choice);
        }

        void OnXp(PlayerId sender, ref NetReader r)
        {
            int xp = (int)r.ReadVarUInt();
            int toNext = (int)r.ReadVarUInt();
            int level = r.ReadByte();
            if (r.Failed) return;
            _xp.Xp = xp;
            _xp.XpToNext = Math.Max(1, toNext);
            _xp.Level = level;
        }

        void OnOffer(PlayerId sender, ref NetReader r)
        {
            var offer = new UpgradeOffer { Id = r.ReadUShort() };
            int count = Math.Min(UpgradeOffer.Choices, (int)r.ReadByte());
            for (int i = 0; i < count && !r.Failed; i++)
            {
                int upgrade = r.ReadByte();
                var rarity = (UpgradeRarity)r.ReadByte();
                offer.Set(i, upgrade, rarity);
                offer.Count++;
            }
            if (!r.Failed) _local.Deliver(_session.LocalPlayer.Value, offer);
        }

        void OnBuild(PlayerId sender, ref NetReader r)
        {
            int player = r.ReadByte();
            int upgrade = r.ReadByte();
            var rarity = (UpgradeRarity)r.ReadByte();
            if (!r.Failed) _builds.Apply(player, upgrade, rarity);
        }
    }
}
