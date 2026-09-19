using System;
using LastGround.Core.Events;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Net.Wire;
using LastGround.Core.Tick;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Players;

namespace LastGround.Networking.Replication
{
    /// <summary>
    /// Host → clients crowd stream (TDD_02 §17). Per client: distance tiers decide the send rate, a priority
    /// accumulator picks the most overdue entities, and a byte budget caps each tick, so far entities thin out
    /// gracefully instead of the link saturating. Enter/Exit are reliable batches; positions are 7-byte
    /// bit-packed unreliable entries with absolute values (no baselines).
    /// </summary>
    public sealed class CrowdReplicationSender : ITickable, IDisposable
    {
        sealed class ClientView
        {
            public readonly bool[] Relevant;
            public readonly byte[] KnownGeneration;
            public readonly float[] Priority;
            public readonly int[] Due;
            public readonly float[] DueKey;
            public readonly int[] Enter;
            public readonly int[] Exit;
            public readonly int[] Death;
            public int DeathCount;

            public ClientView(int capacity)
            {
                Relevant = new bool[capacity];
                KnownGeneration = new byte[capacity];
                Priority = new float[capacity];
                Due = new int[capacity];
                DueKey = new float[capacity];
                Enter = new int[capacity];
                Exit = new int[capacity];
                Death = new int[capacity];
            }

            public void Reset()
            {
                Array.Clear(Relevant, 0, Relevant.Length);
                Array.Clear(Priority, 0, Priority.Length);
            }
        }

        readonly ISession _session;
        readonly CrowdState _crowd;
        readonly PlayerStateTable _players;
        readonly ReplicationTuning _tuning;
        readonly ClientView[] _views = new ClientView[PlayerStateTable.Max];
        readonly int _entriesPerTick;
        readonly float[] _deathX;
        readonly float[] _deathZ;
        readonly float[] _deathYaw;
        EventReader<CrowdDeath> _deathReader;

        /// <summary>Zombie type sent at the top rate at any distance (boss body, M8); 255 = none.</summary>
        public byte PriorityType { get; set; } = 255;

        public CrowdReplicationSender(ISession session, CrowdState crowd, PlayerStateTable players, ReplicationTuning tuning)
        {
            if (crowd.Capacity > 1 << ReplicationTuning.SlotBits)
                throw new ArgumentException("Crowd capacity exceeds the snapshot slot range.");
            _session = session;
            _crowd = crowd;
            _players = players;
            _tuning = tuning;
            _entriesPerTick = Math.Max(1, (tuning.SnapshotBytesPerTick - 8) * 8 / ReplicationTuning.SnapshotEntryBits);
            _deathX = new float[crowd.Capacity];
            _deathZ = new float[crowd.Capacity];
            _deathYaw = new float[crowd.Capacity];
            _deathReader = crowd.Deaths.CreateReader();
            _session.PlayerLeft += OnPlayerLeft;
        }

        /// <summary>Entities currently relevant to a player (for stats and tests).</summary>
        public int RelevantCount(PlayerId player)
        {
            ClientView view = _views[player.Value];
            if (view == null) return 0;
            int count = 0;
            for (int i = 0; i < view.Relevant.Length; i++)
                if (view.Relevant[i]) count++;
            return count;
        }

        public void Dispose()
        {
            _session.PlayerLeft -= OnPlayerLeft;
        }

        public void Tick(float dt, uint tick)
        {
            uint stamp = NetTime.ToTick(_session.Clock.HostTime);
            CollectDeaths();
            var players = _session.Players;
            for (int p = 0; p < players.Count; p++)
            {
                PlayerId id = players[p].Id;
                if (id == _session.LocalPlayer || !_players.Active[id.Value]) continue;
                ClientView view = _views[id.Value] ?? (_views[id.Value] = new ClientView(_crowd.Capacity));
                UpdateClient(id, view, _players.X[id.Value], _players.Z[id.Value], dt, stamp);
            }
        }

        void UpdateClient(PlayerId client, ClientView view, float ox, float oz, float dt, uint stamp)
        {
            float a2 = _tuning.TierADistance * _tuning.TierADistance;
            float b2 = _tuning.TierBDistance * _tuning.TierBDistance;
            float c2 = _tuning.TierCDistance * _tuning.TierCDistance;
            float exit2 = _tuning.ExitDistance * _tuning.ExitDistance;
            int enterCount = 0, exitCount = 0, dueCount = 0;

            for (int i = 0; i < _crowd.Capacity; i++)
            {
                if (!_crowd.AliveSlots[i])
                {
                    if (view.Relevant[i])
                    {
                        view.Relevant[i] = false;
                        view.Exit[exitCount++] = i;
                    }
                    continue;
                }

                float dx = _crowd.PosX[i] - ox;
                float dz = _crowd.PosZ[i] - oz;
                float d2 = dx * dx + dz * dz;
                float rate = d2 < a2 ? _tuning.TierARate : d2 < b2 ? _tuning.TierBRate : d2 < c2 ? _tuning.TierCRate : 0f;
                // The boss is big, drives telegraphs and is seen from afar: always relevant at the top rate.
                if (_crowd.Type[i] == PriorityType) rate = _tuning.TierARate;

                if (!view.Relevant[i] || view.KnownGeneration[i] != _crowd.Generation[i])
                {
                    if (rate <= 0f)
                    {
                        if (view.Relevant[i])
                        {
                            view.Relevant[i] = false;
                            view.Exit[exitCount++] = i;
                        }
                        continue;
                    }
                    view.Relevant[i] = true;
                    view.KnownGeneration[i] = _crowd.Generation[i];
                    view.Priority[i] = 0f;
                    view.Enter[enterCount++] = i;
                    continue;
                }

                if (rate <= 0f)
                {
                    if (d2 > exit2)
                    {
                        view.Relevant[i] = false;
                        view.Exit[exitCount++] = i;
                        continue;
                    }
                    rate = _tuning.TierCRate;
                }

                float priority = view.Priority[i] + rate * dt;
                view.Priority[i] = priority;
                if (priority >= 1f)
                {
                    view.Due[dueCount] = i;
                    view.DueKey[dueCount] = -priority;
                    dueCount++;
                }
            }

            if (view.DeathCount > 0) SendDeaths(client, view);
            if (enterCount > 0) SendEnters(client, view, enterCount, stamp);
            if (exitCount > 0) SendExits(client, view, exitCount);
            if (dueCount > 0) SendSnapshot(client, view, dueCount, stamp);
        }

        void SendEnters(PlayerId client, ClientView view, int count, uint stamp)
        {
            for (int start = 0; start < count; start += ReplicationTuning.MaxEntriesPerReliableMessage)
            {
                int n = Math.Min(ReplicationTuning.MaxEntriesPerReliableMessage, count - start);
                NetWriter w = _session.Begin(NetMsgId.ZombieEnter);
                w.WriteVarUInt(stamp);
                w.WriteVarUInt((uint)n);
                for (int k = 0; k < n; k++)
                {
                    int i = view.Enter[start + k];
                    w.WriteUShort((ushort)i);
                    w.WriteByte(_crowd.Generation[i]);
                    // Type in the low nibble, elite modifier in the high nibble (both < 16).
                    w.WriteByte((byte)((_crowd.Type[i] & 0x0F) | (_crowd.Elite[i] << 4)));
                    w.WriteUShort(Quantize.Position(_crowd.PosX[i]));
                    w.WriteUShort(Quantize.Position(_crowd.PosZ[i]));
                    w.WriteByte((byte)Quantize.Yaw(_crowd.Heading[i], 8));
                }
                _session.SendTo(client, NetChannel.Reliable);
            }
        }

        /// <summary>
        /// Deaths are taken from the crowd's event channel before relevance is evaluated: a client that knew the
        /// zombie gets a death (corpse + blood) instead of an exit, and a slot reused in the same tick enters fresh.
        /// </summary>
        void CollectDeaths()
        {
            while (_crowd.Deaths.TryRead(ref _deathReader, out CrowdDeath death))
            {
                _deathX[death.Slot] = death.X;
                _deathZ[death.Slot] = death.Z;
                _deathYaw[death.Slot] = death.Yaw;
                for (int v = 0; v < _views.Length; v++)
                {
                    ClientView view = _views[v];
                    if (view == null || !view.Relevant[death.Slot]) continue;
                    view.Relevant[death.Slot] = false;
                    view.Death[view.DeathCount++] = death.Slot;
                }
            }
        }

        void SendDeaths(PlayerId client, ClientView view)
        {
            for (int start = 0; start < view.DeathCount; start += ReplicationTuning.MaxEntriesPerReliableMessage)
            {
                int n = Math.Min(ReplicationTuning.MaxEntriesPerReliableMessage, view.DeathCount - start);
                NetWriter w = _session.Begin(NetMsgId.ZombieDeath);
                w.WriteVarUInt((uint)n);
                for (int k = 0; k < n; k++)
                {
                    int i = view.Death[start + k];
                    w.WriteUShort((ushort)i);
                    w.WriteUShort(Quantize.Position(_deathX[i]));
                    w.WriteUShort(Quantize.Position(_deathZ[i]));
                    w.WriteByte((byte)Quantize.Yaw(_deathYaw[i], 8));
                }
                _session.SendTo(client, NetChannel.Reliable);
            }
            view.DeathCount = 0;
        }

        void SendExits(PlayerId client, ClientView view, int count)
        {
            for (int start = 0; start < count; start += ReplicationTuning.MaxEntriesPerReliableMessage)
            {
                int n = Math.Min(ReplicationTuning.MaxEntriesPerReliableMessage, count - start);
                NetWriter w = _session.Begin(NetMsgId.ZombieExit);
                w.WriteVarUInt((uint)n);
                for (int k = 0; k < n; k++)
                    w.WriteUShort((ushort)view.Exit[start + k]);
                _session.SendTo(client, NetChannel.Reliable);
            }
        }

        void SendSnapshot(PlayerId client, ClientView view, int dueCount, uint stamp)
        {
            // Most overdue first; the rest keep accumulating priority for the next tick.
            Array.Sort(view.DueKey, view.Due, 0, dueCount);
            int n = Math.Min(dueCount, _entriesPerTick);

            NetWriter w = _session.Begin(NetMsgId.ZombieSnapshot);
            w.WriteVarUInt(stamp);
            w.WriteVarUInt((uint)n);
            for (int k = 0; k < n; k++)
            {
                int i = view.Due[k];
                w.WriteBits((uint)i, ReplicationTuning.SlotBits);
                w.WriteBits(Quantize.Position(_crowd.PosX[i]), 16);
                w.WriteBits(Quantize.Position(_crowd.PosZ[i]), 16);
                w.WriteBits(Quantize.Yaw(_crowd.Heading[i], ReplicationTuning.YawBits), ReplicationTuning.YawBits);
                w.WriteBits(_crowd.Anim[i], ReplicationTuning.AnimBits);
                w.WriteBits(_crowd.Flags[i], ReplicationTuning.FlagBits);
                view.Priority[i] = 0f;
            }
            w.FlushBits();
            _session.SendTo(client, NetChannel.Unreliable);
        }

        void OnPlayerLeft(PlayerId player)
        {
            if (player.IsValid && player.Value < _views.Length)
                _views[player.Value]?.Reset();
        }
    }
}
