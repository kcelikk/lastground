using LastGround.Core.Events;
using LastGround.Gameplay.Players;

namespace LastGround.Gameplay.Loot
{
    /// <summary>A pickup appeared (host: registry; clients: PickupSpawn).</summary>
    public struct PickupSpawned
    {
        public int Id;
        public PickupType Type;
        public float X;
        public float Z;
        public int Value;
        public byte OwnerMask;
    }

    /// <summary>A pickup was taken by a player (255 = expired). Instanced pickups may be taken once per owner.</summary>
    public struct PickupTaken
    {
        public int Id;
        public int Player;
        public PickupType Type;
        public int Value;
    }

    /// <summary>
    /// Every device's view of the pickups on the ground (TDD_01 §13.2 PickupRegistry): fixed slots indexed by id,
    /// owner mask per slot (a player sees and can take an instanced pickup only while their bit is set). The host
    /// changes it through <see cref="PickupRegistry"/>; clients through replication.
    /// </summary>
    public sealed class PickupTable
    {
        public readonly bool[] Active;
        public readonly PickupType[] Type;
        public readonly float[] X;
        public readonly float[] Z;
        public readonly int[] Value;
        public readonly byte[] OwnerMask;

        /// <summary>Client: hidden locally while a claim is waiting for the host (seconds left).</summary>
        public readonly float[] PendingClaim;

        public readonly EventChannel<PickupSpawned> Spawned = new EventChannel<PickupSpawned>(128);
        public readonly EventChannel<PickupTaken> Taken = new EventChannel<PickupTaken>(128);

        public PickupTable(int capacity)
        {
            Capacity = capacity;
            Active = new bool[capacity];
            Type = new PickupType[capacity];
            X = new float[capacity];
            Z = new float[capacity];
            Value = new int[capacity];
            OwnerMask = new byte[capacity];
            PendingClaim = new float[capacity];
        }

        public int Capacity { get; }

        /// <summary>True when <paramref name="player"/> sees this pickup (their owner bit is set).</summary>
        public bool VisibleTo(int id, int player) => Active[id] && (OwnerMask[id] & (1 << player)) != 0 && PendingClaim[id] <= 0f;

        public void Put(in PickupSpawned spawn)
        {
            if ((uint)spawn.Id >= (uint)Capacity) return;
            Active[spawn.Id] = true;
            Type[spawn.Id] = spawn.Type;
            X[spawn.Id] = spawn.X;
            Z[spawn.Id] = spawn.Z;
            Value[spawn.Id] = spawn.Value;
            OwnerMask[spawn.Id] = spawn.OwnerMask;
            PendingClaim[spawn.Id] = 0f;
            Spawned.Publish(spawn);
        }

        /// <summary>Applies a take: team loot and expiries remove the pickup; instanced loot clears the taker's bit.</summary>
        public void Take(int id, int player)
        {
            if ((uint)id >= (uint)Capacity || !Active[id]) return;
            PickupType type = Type[id];
            bool team = type == PickupType.Coin || player >= PlayerStateTable.Max;
            if (team) OwnerMask[id] = 0;
            else OwnerMask[id] &= (byte)~(1 << player);
            if (OwnerMask[id] == 0) Active[id] = false;
            PendingClaim[id] = 0f;
            Taken.Publish(new PickupTaken { Id = id, Player = player, Type = type, Value = Value[id] });
        }
    }
}
