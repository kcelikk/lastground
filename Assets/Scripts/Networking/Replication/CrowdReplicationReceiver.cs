using System;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Net.Wire;
using LastGround.Core.Tick;
using LastGround.Gameplay.Crowd;

namespace LastGround.Networking.Replication
{
    /// <summary>Client side of the crowd stream: applies Enter/Snapshot/Exit to the replica and interpolates it each frame.</summary>
    public sealed class CrowdReplicationReceiver : ITickable, IDisposable
    {
        readonly ISession _session;
        readonly CrowdReplica _replica;
        readonly NetRawHandler _onEnter;
        readonly NetRawHandler _onSnapshot;
        readonly NetRawHandler _onExit;
        readonly NetRawHandler _onDeath;

        public CrowdReplicationReceiver(ISession session, CrowdReplica replica)
        {
            _session = session;
            _replica = replica;
            _onEnter = OnEnter;
            _onSnapshot = OnSnapshot;
            _onExit = OnExit;
            _onDeath = OnDeath;
            _session.Subscribe(NetMsgId.ZombieEnter, _onEnter);
            _session.Subscribe(NetMsgId.ZombieSnapshot, _onSnapshot);
            _session.Subscribe(NetMsgId.ZombieExit, _onExit);
            _session.Subscribe(NetMsgId.ZombieDeath, _onDeath);
        }

        /// <summary>Presentation phase: move replicas to the render time.</summary>
        public void Tick(float dt, uint tick)
        {
            _replica.Interpolate(_session.Clock.RenderTime);
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.ZombieEnter, _onEnter);
            _session.Unsubscribe(NetMsgId.ZombieSnapshot, _onSnapshot);
            _session.Unsubscribe(NetMsgId.ZombieExit, _onExit);
            _session.Unsubscribe(NetMsgId.ZombieDeath, _onDeath);
        }

        void OnEnter(PlayerId sender, ref NetReader r)
        {
            double time = NetTime.FromTick(r.ReadVarUInt());
            uint count = r.ReadVarUInt();
            for (uint k = 0; k < count && !r.Failed; k++)
            {
                int slot = r.ReadUShort();
                byte generation = r.ReadByte();
                byte type = r.ReadByte();
                float x = Quantize.Position(r.ReadUShort());
                float z = Quantize.Position(r.ReadUShort());
                float yaw = Quantize.Yaw(r.ReadByte(), 8);
                if (!r.Failed) _replica.Enter(slot, generation, type, x, z, yaw, time);
            }
        }

        void OnSnapshot(PlayerId sender, ref NetReader r)
        {
            double time = NetTime.FromTick(r.ReadVarUInt());
            uint count = r.ReadVarUInt();
            for (uint k = 0; k < count && !r.Failed; k++)
            {
                int slot = (int)r.ReadBits(ReplicationTuning.SlotBits);
                float x = Quantize.Position((ushort)r.ReadBits(16));
                float z = Quantize.Position((ushort)r.ReadBits(16));
                float yaw = Quantize.Yaw(r.ReadBits(ReplicationTuning.YawBits), ReplicationTuning.YawBits);
                byte anim = (byte)r.ReadBits(ReplicationTuning.AnimBits);
                byte flags = (byte)r.ReadBits(ReplicationTuning.FlagBits);
                if (!r.Failed) _replica.Update(slot, x, z, yaw, time, anim, flags);
            }
        }

        void OnDeath(PlayerId sender, ref NetReader r)
        {
            uint count = r.ReadVarUInt();
            for (uint k = 0; k < count && !r.Failed; k++)
            {
                int slot = r.ReadUShort();
                float x = Quantize.Position(r.ReadUShort());
                float z = Quantize.Position(r.ReadUShort());
                float yaw = Quantize.Yaw(r.ReadByte(), 8);
                if (!r.Failed) _replica.Die(slot, x, z, yaw);
            }
        }

        void OnExit(PlayerId sender, ref NetReader r)
        {
            uint count = r.ReadVarUInt();
            for (uint k = 0; k < count && !r.Failed; k++)
                _replica.Exit(r.ReadUShort());
        }
    }
}
