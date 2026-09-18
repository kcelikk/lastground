using System;
using LastGround.Core.Events;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Net.Wire;
using LastGround.Core.Tick;
using LastGround.Gameplay.Loot;

namespace LastGround.Networking.Replication
{
    /// <summary>
    /// Pickups and the team wallet over the network (TDD_01 §13.2, TDD_02 §15.5): PickupSpawnBatch host → all
    /// (reliable, batched per tick, 12 B each incl. owner mask), PickupClaim player → host, PickupTaken host → all,
    /// TeamWallet host → all (reliable, on change, ≤ 4 Hz). Instanced loot is one pickup with an owner mask: each
    /// device shows only its own copy.
    /// </summary>
    public sealed class LootSync : ITickable, IPickupClaimSink, IDisposable
    {
        const float WalletInterval = 0.25f;
        const int MaxPerMessage = 60;

        readonly ISession _session;
        readonly PickupTable _table;
        readonly TeamWallet _wallet;
        readonly PickupRegistry _registry;
        readonly NetRawHandler _onSpawn, _onClaim, _onTaken, _onWallet;
        readonly PickupSpawned[] _spawnBuffer = new PickupSpawned[256];
        EventReader<PickupSpawned> _spawnReader;
        EventReader<PickupTaken> _takenReader;
        int _sentWallet = -1;
        float _walletTimer;

        /// <param name="registry">Host only; null on clients.</param>
        public LootSync(ISession session, PickupTable table, TeamWallet wallet, PickupRegistry registry)
        {
            _session = session;
            _table = table;
            _wallet = wallet;
            _registry = registry;
            _onSpawn = OnSpawn;
            _onClaim = OnClaim;
            _onTaken = OnTaken;
            _onWallet = OnWallet;
            _spawnReader = table.Spawned.CreateReader();
            _takenReader = table.Taken.CreateReader();
            if (session.IsAuthority)
            {
                session.Subscribe(NetMsgId.PickupClaim, _onClaim);
            }
            else
            {
                session.Subscribe(NetMsgId.PickupSpawnBatch, _onSpawn);
                session.Subscribe(NetMsgId.PickupTaken, _onTaken);
                session.Subscribe(NetMsgId.TeamWallet, _onWallet);
            }
        }

        /// <summary>Host: NetSend phase.</summary>
        public void Tick(float dt, uint tick)
        {
            if (!_session.IsAuthority) return;
            int count = 0;
            while (count < _spawnBuffer.Length && _table.Spawned.TryRead(ref _spawnReader, out PickupSpawned spawn)) _spawnBuffer[count++] = spawn;
            for (int start = 0; start < count; start += MaxPerMessage)
            {
                int n = Math.Min(MaxPerMessage, count - start);
                NetWriter w = _session.Begin(NetMsgId.PickupSpawnBatch);
                w.WriteVarUInt((uint)n);
                for (int k = 0; k < n; k++)
                {
                    ref PickupSpawned s = ref _spawnBuffer[start + k];
                    w.WriteUShort((ushort)s.Id);
                    w.WriteByte((byte)s.Type);
                    w.WriteUShort(Quantize.Position(s.X));
                    w.WriteUShort(Quantize.Position(s.Z));
                    w.WriteVarUInt((uint)s.Value);
                    w.WriteByte(s.OwnerMask);
                }
                _session.SendToClients(NetChannel.Reliable);
            }

            while (_table.Taken.TryRead(ref _takenReader, out PickupTaken taken))
            {
                NetWriter w = _session.Begin(NetMsgId.PickupTaken);
                w.WriteUShort((ushort)taken.Id);
                w.WriteByte((byte)Math.Min(255, taken.Player));
                _session.SendToClients(NetChannel.Reliable);
            }

            _walletTimer -= dt;
            if (_wallet.Version != _sentWallet && _walletTimer <= 0f)
            {
                _walletTimer = WalletInterval;
                _sentWallet = _wallet.Version;
                NetWriter w = _session.Begin(NetMsgId.TeamWallet);
                w.WriteVarUInt((uint)_wallet.Coins);
                _session.SendToClients(NetChannel.Reliable);
            }
        }

        /// <summary>Client: ask the host for a pickup.</summary>
        public void Claim(int player, int id)
        {
            NetWriter w = _session.Begin(NetMsgId.PickupClaim);
            w.WriteUShort((ushort)id);
            _session.SendToHost(NetChannel.Reliable);
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.PickupClaim, _onClaim);
            _session.Unsubscribe(NetMsgId.PickupSpawnBatch, _onSpawn);
            _session.Unsubscribe(NetMsgId.PickupTaken, _onTaken);
            _session.Unsubscribe(NetMsgId.TeamWallet, _onWallet);
        }

        void OnClaim(PlayerId sender, ref NetReader r)
        {
            int id = r.ReadUShort();
            if (!r.Failed && sender.IsValid) _registry?.Claim(sender.Value, id);
        }

        void OnSpawn(PlayerId sender, ref NetReader r)
        {
            uint count = r.ReadVarUInt();
            for (uint k = 0; k < count && !r.Failed; k++)
            {
                var spawn = new PickupSpawned
                {
                    Id = r.ReadUShort(),
                    Type = (PickupType)r.ReadByte(),
                    X = Quantize.Position(r.ReadUShort()),
                    Z = Quantize.Position(r.ReadUShort()),
                    Value = (int)r.ReadVarUInt(),
                    OwnerMask = r.ReadByte(),
                };
                if (!r.Failed) _table.Put(spawn);
            }
        }

        void OnTaken(PlayerId sender, ref NetReader r)
        {
            int id = r.ReadUShort();
            int player = r.ReadByte();
            if (!r.Failed) _table.Take(id, player);
        }

        void OnWallet(PlayerId sender, ref NetReader r)
        {
            int coins = (int)r.ReadVarUInt();
            if (r.Failed) return;
            _wallet.Coins = coins;
            _wallet.Version++;
        }
    }
}
