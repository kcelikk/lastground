using System;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Net.Wire;
using LastGround.Core.Tick;
using LastGround.Gameplay.Combat;

namespace LastGround.Networking.Replication
{
    /// <summary>
    /// Hit claims over the network (TDD_02 §15.5 HitClaimBatch, reliable, batched per tick, 11 B per claim).
    /// Client: the local weapon's claims are buffered and flushed in NetSend. Host: incoming claims are stamped
    /// with the sender (a client cannot claim for someone else) and handed to the CombatAuthority.
    /// </summary>
    public sealed class HitClaimSync : ITickable, IHitClaimSink, IDisposable
    {
        const int MaxPerMessage = 80;
        const int BufferCapacity = 256;

        readonly ISession _session;
        readonly IHitClaimSink _authority;
        readonly NetRawHandler _onBatch;
        readonly HitClaim[] _buffer = new HitClaim[BufferCapacity];
        int _count;

        /// <param name="authority">Host: where received claims go. Null on clients.</param>
        public HitClaimSync(ISession session, IHitClaimSink authority)
        {
            _session = session;
            _authority = authority;
            _onBatch = OnBatch;
            if (session.IsAuthority) session.Subscribe(NetMsgId.HitClaimBatch, _onBatch);
        }

        public int Sent { get; private set; }
        public int Received { get; private set; }
        public int Overflowed { get; private set; }

        /// <summary>Client: queue a claim for the next NetSend.</summary>
        public void Submit(in HitClaim claim)
        {
            if (_count >= BufferCapacity)
            {
                Overflowed++;
                return;
            }
            _buffer[_count++] = claim;
        }

        public void Tick(float dt, uint tick)
        {
            if (_count == 0) return;
            for (int start = 0; start < _count; start += MaxPerMessage)
            {
                int n = Math.Min(MaxPerMessage, _count - start);
                NetWriter w = _session.Begin(NetMsgId.HitClaimBatch);
                w.WriteVarUInt((uint)n);
                for (int k = 0; k < n; k++)
                {
                    ref HitClaim c = ref _buffer[start + k];
                    w.WriteUShort(c.ShotSeq);
                    w.WriteByte(c.Weapon);
                    w.WriteByte((byte)((c.Pellet & 0x0F) << 4 | (c.Pierce & 0x0F)));
                    w.WriteUShort(c.Slot);
                    w.WriteByte(c.Generation);
                    w.WriteUShort(Quantize.Position(c.HitX));
                    w.WriteUShort(Quantize.Position(c.HitZ));
                }
                _session.SendToHost(NetChannel.Reliable);
            }
            Sent += _count;
            _count = 0;
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.HitClaimBatch, _onBatch);
        }

        void OnBatch(PlayerId sender, ref NetReader r)
        {
            if (!sender.IsValid || _authority == null) return;
            uint count = r.ReadVarUInt();
            for (uint k = 0; k < count && !r.Failed; k++)
            {
                var claim = new HitClaim { Shooter = sender.Value };
                claim.ShotSeq = r.ReadUShort();
                claim.Weapon = r.ReadByte();
                byte pelletPierce = r.ReadByte();
                claim.Pellet = (byte)(pelletPierce >> 4);
                claim.Pierce = (byte)(pelletPierce & 0x0F);
                claim.Slot = r.ReadUShort();
                claim.Generation = r.ReadByte();
                claim.HitX = Quantize.Position(r.ReadUShort());
                claim.HitZ = Quantize.Position(r.ReadUShort());
                if (r.Failed) return;
                _authority.Submit(claim);
                Received++;
            }
        }
    }
}
