using System;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Net.Wire;
using LastGround.Core.Tick;
using LastGround.Gameplay.Players;
using UnityEngine;

namespace LastGround.Networking.Replication
{
    /// <summary>
    /// Player movement sync (TDD_02 §15.5, §16): clients send their own state to the host at 30 Hz; the host
    /// validates speed and broadcasts every player's state at 20 Hz. Remote players are displayed interpolated.
    /// A flags byte carries the trigger state so other devices draw that player's tracers.
    /// </summary>
    public sealed class PlayerSync : ITickable, IDisposable
    {
        const float BroadcastInterval = 1f / 20f;
        const float SpeedTolerance = 1.5f;
        const float DistanceSlack = 0.5f;
        const byte FlagFiring = 1 << 0;

        readonly ISession _session;
        readonly PlayerStateTable _table;
        readonly float _maxSpeed;
        readonly NetRawHandler _onInput;
        readonly NetRawHandler _onStates;
        float _broadcastTimer;
        ushort _sequence;
        double _localTime;

        public PlayerSync(ISession session, PlayerStateTable table, float maxSpeed)
        {
            _session = session;
            _table = table;
            _maxSpeed = maxSpeed;
            _onInput = OnInput;
            _onStates = OnStates;
            if (session.IsAuthority) session.Subscribe(NetMsgId.PlayerInput, _onInput);
            else session.Subscribe(NetMsgId.PlayerStates, _onStates);
            session.PlayerLeft += OnPlayerLeft;
        }

        /// <summary>Movement corrections applied by the host (clamped teleports). For tests and diagnostics.</summary>
        public int Corrections { get; private set; }

        /// <summary>NetSend phase (30 Hz).</summary>
        public void Tick(float dt, uint tick)
        {
            _localTime += dt;
            if (_session.IsAuthority)
            {
                _broadcastTimer += dt;
                if (_broadcastTimer >= BroadcastInterval)
                {
                    _broadcastTimer -= BroadcastInterval;
                    BroadcastStates();
                }
                return;
            }

            PlayerId me = _session.LocalPlayer;
            if (!me.IsValid || !_table.Active[me.Value]) return;
            int i = me.Value;
            NetWriter w = _session.Begin(NetMsgId.PlayerInput);
            w.WriteUShort(++_sequence);
            w.WriteUShort(Quantize.Position(_table.X[i]));
            w.WriteUShort(Quantize.Position(_table.Z[i]));
            w.WriteShort(Quantize.Velocity(_table.VelX[i]));
            w.WriteShort(Quantize.Velocity(_table.VelZ[i]));
            w.WriteByte((byte)Quantize.Yaw(_table.Yaw[i], 8));
            w.WriteByte(_table.Firing[i] ? FlagFiring : (byte)0);
            _session.SendToHost(NetChannel.Unreliable);
        }

        /// <summary>Presentation phase: interpolate remote players.</summary>
        public void Interpolate()
        {
            _table.Interpolate(_session.Clock.RenderTime);
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.PlayerInput, _onInput);
            _session.Unsubscribe(NetMsgId.PlayerStates, _onStates);
            _session.PlayerLeft -= OnPlayerLeft;
        }

        void BroadcastStates()
        {
            int count = 0;
            for (int i = 0; i < PlayerStateTable.Max; i++)
                if (_table.Active[i]) count++;
            if (count == 0) return;

            NetWriter w = _session.Begin(NetMsgId.PlayerStates);
            w.WriteVarUInt(NetTime.ToTick(_session.Clock.HostTime));
            w.WriteByte((byte)count);
            for (int i = 0; i < PlayerStateTable.Max; i++)
            {
                if (!_table.Active[i]) continue;
                w.WriteByte((byte)i);
                w.WriteUShort(Quantize.Position(_table.X[i]));
                w.WriteUShort(Quantize.Position(_table.Z[i]));
                w.WriteShort(Quantize.Velocity(_table.VelX[i]));
                w.WriteShort(Quantize.Velocity(_table.VelZ[i]));
                w.WriteByte((byte)Quantize.Yaw(_table.Yaw[i], 8));
                w.WriteByte(_table.Firing[i] ? FlagFiring : (byte)0);
            }
            _session.SendToClients(NetChannel.Unreliable);
        }

        void OnInput(PlayerId sender, ref NetReader r)
        {
            r.ReadUShort(); // sequence: reserved for reconciliation (M4)
            float x = Quantize.Position(r.ReadUShort());
            float z = Quantize.Position(r.ReadUShort());
            float vx = Quantize.Velocity(r.ReadShort());
            float vz = Quantize.Velocity(r.ReadShort());
            float yaw = Quantize.Yaw(r.ReadByte(), 8);
            byte flags = r.ReadByte();
            if (r.Failed || !sender.IsValid || sender.Value >= PlayerStateTable.Max) return;

            int i = sender.Value;
            if (_table.Active[i])
            {
                // Reject teleports: limit the step to what max speed allows since the last accepted update.
                double elapsed = Math.Max(1.0 / 30.0, _localTime - _table.LastUpdate[i]);
                float allowed = (float)(_maxSpeed * SpeedTolerance * elapsed) + DistanceSlack;
                float dx = x - _table.X[i];
                float dz = z - _table.Z[i];
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance > allowed)
                {
                    float k = allowed / distance;
                    x = _table.X[i] + dx * k;
                    z = _table.Z[i] + dz * k;
                    Corrections++;
                }
            }
            _table.PushRemote(sender, x, z, yaw, vx, vz, _session.Clock.HostTime, _localTime);
            _table.Firing[i] = (flags & FlagFiring) != 0 && !_table.Dead[i];
        }

        void OnStates(PlayerId sender, ref NetReader r)
        {
            double time = NetTime.FromTick(r.ReadVarUInt());
            int count = r.ReadByte();
            for (int k = 0; k < count && !r.Failed; k++)
            {
                var id = new PlayerId(r.ReadByte());
                float x = Quantize.Position(r.ReadUShort());
                float z = Quantize.Position(r.ReadUShort());
                float vx = Quantize.Velocity(r.ReadShort());
                float vz = Quantize.Velocity(r.ReadShort());
                float yaw = Quantize.Yaw(r.ReadByte(), 8);
                byte flags = r.ReadByte();
                if (r.Failed || id == _table.Local || id.Value >= PlayerStateTable.Max) continue;
                _table.PushRemote(id, x, z, yaw, vx, vz, time, _localTime);
                _table.Firing[id.Value] = (flags & FlagFiring) != 0;
            }
        }

        void OnPlayerLeft(PlayerId player)
        {
            _table.Remove(player);
        }
    }
}
