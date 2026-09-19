using LastGround.Core.Events;
using UnityEngine;

namespace LastGround.Gameplay.Crowd
{
    /// <summary>
    /// Client-side crowd (TDD_02 §17.5, ZombieReplicaWorld): buffers timestamped samples per slot and renders them
    /// at <c>renderTime</c> (host time minus interpolation delay). Beyond the newest sample it extrapolates for up to
    /// 250 ms, then holds. Jumps larger than 3 m snap. No AI runs here.
    /// </summary>
    public sealed class CrowdReplica : ICrowdRenderSource
    {
        public const int SamplesPerSlot = 4;
        const double MaxExtrapolation = 0.25;
        const float SnapDistanceSq = 3f * 3f;
        /// <summary>A hit flag arriving this soon after our own predicted hit on the slot is not shown twice.</summary>
        const double LocalHitSuppression = 0.4;

        readonly bool[] _alive;
        readonly byte[] _generation;
        readonly byte[] _type;
        readonly byte[] _elite;
        readonly float[] _x;
        readonly float[] _z;
        readonly float[] _yaw;
        readonly byte[] _anim;
        readonly byte[] _flags;
        readonly double[] _localHitAt;
        double _now;

        readonly double[] _sampleTime;
        readonly float[] _sampleX;
        readonly float[] _sampleZ;
        readonly float[] _sampleYaw;
        readonly byte[] _sampleCount;
        readonly byte[] _newest;

        /// <summary>Replicated deaths (corpses and blood on clients).</summary>
        public readonly EventChannel<CrowdDeath> Deaths = new EventChannel<CrowdDeath>(256);

        /// <summary>Local predicted hits and other players' hits (from the snapshot hit flag).</summary>
        public readonly EventChannel<CrowdHit> Hits = new EventChannel<CrowdHit>(256);

        public CrowdReplica(int capacity)
        {
            Capacity = capacity;
            _alive = new bool[capacity];
            _generation = new byte[capacity];
            _type = new byte[capacity];
            _elite = new byte[capacity];
            _x = new float[capacity];
            _z = new float[capacity];
            _yaw = new float[capacity];
            _anim = new byte[capacity];
            _flags = new byte[capacity];
            _localHitAt = new double[capacity];
            _sampleTime = new double[capacity * SamplesPerSlot];
            _sampleX = new float[capacity * SamplesPerSlot];
            _sampleZ = new float[capacity * SamplesPerSlot];
            _sampleYaw = new float[capacity * SamplesPerSlot];
            _sampleCount = new byte[capacity];
            _newest = new byte[capacity];
        }

        public int Capacity { get; }
        public int ActiveCount { get; private set; }
        public bool[] Alive => _alive;
        public float[] X => _x;
        public float[] Z => _z;
        public float[] Yaw => _yaw;
        public byte[] AnimState => _anim;
        public byte[] FlagBits => _flags;
        public byte[] Types => _type;
        public byte[] Elites => _elite;

        public byte GenerationOf(int slot) => _generation[slot];

        public void Enter(int slot, byte generation, byte type, float x, float z, float yaw, double time, byte elite = 0)
        {
            if ((uint)slot >= (uint)Capacity) return;
            if (!_alive[slot]) ActiveCount++;
            _alive[slot] = true;
            _generation[slot] = generation;
            _type[slot] = type;
            _elite[slot] = elite;
            _anim[slot] = 1; // walk until the first snapshot says otherwise
            _flags[slot] = 0;
            _localHitAt[slot] = double.MinValue;
            _sampleCount[slot] = 0;
            PushSample(slot, x, z, yaw, time);
            _x[slot] = x;
            _z[slot] = z;
            _yaw[slot] = yaw;
        }

        /// <summary>Applies a snapshot sample. Ignored for unknown slots and for samples older than the newest.</summary>
        public void Update(int slot, float x, float z, float yaw, double time, byte anim = 1, byte flags = 0)
        {
            if ((uint)slot >= (uint)Capacity || !_alive[slot]) return;
            _anim[slot] = anim;
            bool hitEdge = (flags & CrowdFlags.Hit) != 0 && (_flags[slot] & CrowdFlags.Hit) == 0;
            _flags[slot] = flags;
            if (hitEdge && _now - _localHitAt[slot] > LocalHitSuppression)
                Hits.Publish(new CrowdHit { Slot = slot, X = _x[slot], Z = _z[slot] });
            int newest = slot * SamplesPerSlot + _newest[slot];
            if (_sampleCount[slot] > 0 && time <= _sampleTime[newest]) return;

            float dx = x - _x[slot];
            float dz = z - _z[slot];
            if (dx * dx + dz * dz > SnapDistanceSq)
            {
                _sampleCount[slot] = 0;
                _x[slot] = x;
                _z[slot] = z;
            }
            PushSample(slot, x, z, yaw, time);
        }

        public void Exit(int slot)
        {
            if ((uint)slot >= (uint)Capacity || !_alive[slot]) return;
            _alive[slot] = false;
            ActiveCount--;
        }

        /// <summary>The host reports a death: remove the replica and publish a corpse at the death position.</summary>
        public void Die(int slot, float x, float z, float yaw)
        {
            Exit(slot);
            if ((uint)slot < (uint)Capacity) Deaths.Publish(new CrowdDeath { Slot = slot, X = x, Z = z, Yaw = yaw, Type = _type[slot] });
        }

        public void Clear()
        {
            System.Array.Clear(_alive, 0, Capacity);
            System.Array.Clear(_sampleCount, 0, Capacity);
            ActiveCount = 0;
        }

        /// <summary>The local player's predicted hit: shown immediately, and the host's hit flag for it is not shown again.</summary>
        public void PublishLocalHit(in CrowdHit hit)
        {
            if ((uint)hit.Slot >= (uint)Capacity) return;
            _localHitAt[hit.Slot] = _now;
            Hits.Publish(hit);
        }

        /// <summary>Computes render positions for all live slots at the given host time.</summary>
        public void Interpolate(double renderTime)
        {
            _now = renderTime;
            for (int slot = 0; slot < Capacity; slot++)
            {
                if (!_alive[slot] || _sampleCount[slot] == 0) continue;
                Evaluate(slot, renderTime);
            }
        }

        void Evaluate(int slot, double t)
        {
            int count = _sampleCount[slot];
            int baseIndex = slot * SamplesPerSlot;
            int newestIndex = _newest[slot];
            int newest = baseIndex + newestIndex;

            if (count == 1 || t >= _sampleTime[newest])
            {
                if (count == 1)
                {
                    Set(slot, _sampleX[newest], _sampleZ[newest], _sampleYaw[newest]);
                    return;
                }
                int previous = baseIndex + (newestIndex + SamplesPerSlot - 1) % SamplesPerSlot;
                double span = _sampleTime[newest] - _sampleTime[previous];
                double ahead = System.Math.Min(t - _sampleTime[newest], MaxExtrapolation);
                if (span <= 0) span = 1e-3;
                float k = (float)(ahead / span);
                Set(slot,
                    _sampleX[newest] + (_sampleX[newest] - _sampleX[previous]) * k,
                    _sampleZ[newest] + (_sampleZ[newest] - _sampleZ[previous]) * k,
                    _sampleYaw[newest]);
                return;
            }

            // Walk back from the newest sample to find the pair around t.
            for (int step = 0; step < count - 1; step++)
            {
                int b = baseIndex + (newestIndex - step + SamplesPerSlot) % SamplesPerSlot;
                int a = baseIndex + (newestIndex - step - 1 + SamplesPerSlot) % SamplesPerSlot;
                if (t < _sampleTime[a]) continue;
                double span = _sampleTime[b] - _sampleTime[a];
                float k = span > 0 ? (float)((t - _sampleTime[a]) / span) : 1f;
                Set(slot,
                    Mathf.Lerp(_sampleX[a], _sampleX[b], k),
                    Mathf.Lerp(_sampleZ[a], _sampleZ[b], k),
                    Mathf.LerpAngle(_sampleYaw[a], _sampleYaw[b], k));
                return;
            }

            int oldest = baseIndex + (newestIndex - (count - 1) + SamplesPerSlot) % SamplesPerSlot;
            Set(slot, _sampleX[oldest], _sampleZ[oldest], _sampleYaw[oldest]);
        }

        void Set(int slot, float x, float z, float yaw)
        {
            _x[slot] = x;
            _z[slot] = z;
            _yaw[slot] = yaw;
        }

        void PushSample(int slot, float x, float z, float yaw, double time)
        {
            int next = _sampleCount[slot] == 0 ? 0 : (_newest[slot] + 1) % SamplesPerSlot;
            int index = slot * SamplesPerSlot + next;
            _sampleTime[index] = time;
            _sampleX[index] = x;
            _sampleZ[index] = z;
            _sampleYaw[index] = yaw;
            _newest[slot] = (byte)next;
            if (_sampleCount[slot] < SamplesPerSlot) _sampleCount[slot]++;
        }
    }
}
