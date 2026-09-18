using LastGround.Core.Ids;
using LastGround.Gameplay.Crowd;

namespace LastGround.Gameplay.Players
{
    /// <summary>
    /// Latest known state of up to four players, indexed by <see cref="PlayerId"/>. The local player is written
    /// directly each frame; remote players arrive as samples and are displayed through the same interpolation as
    /// the crowd (TDD_02 §17.5).
    /// </summary>
    public sealed class PlayerStateTable
    {
        public const int Max = 4;

        public readonly bool[] Active = new bool[Max];
        public readonly float[] X = new float[Max];
        public readonly float[] Z = new float[Max];
        public readonly float[] Yaw = new float[Max];
        public readonly float[] VelX = new float[Max];
        public readonly float[] VelZ = new float[Max];

        /// <summary>Local time of the last accepted update (host: movement validation).</summary>
        public readonly double[] LastUpdate = new double[Max];

        readonly CrowdReplica _display = new CrowdReplica(Max);

        public PlayerId Local { get; set; } = PlayerId.Invalid;

        public void SetLocal(float x, float z, float yaw, float velX, float velZ)
        {
            if (!Local.IsValid) return;
            int i = Local.Value;
            Active[i] = true;
            X[i] = x;
            Z[i] = z;
            Yaw[i] = yaw;
            VelX[i] = velX;
            VelZ[i] = velZ;
        }

        /// <summary>Stores a remote player's state stamped with host time.</summary>
        public void PushRemote(PlayerId id, float x, float z, float yaw, float velX, float velZ, double hostTime, double localTime)
        {
            if (!id.IsValid || id.Value >= Max || id == Local) return;
            int i = id.Value;
            if (!Active[i])
            {
                Active[i] = true;
                _display.Enter(i, 1, 0, x, z, yaw, hostTime);
            }
            else
            {
                _display.Update(i, x, z, yaw, hostTime);
            }
            X[i] = x;
            Z[i] = z;
            Yaw[i] = yaw;
            VelX[i] = velX;
            VelZ[i] = velZ;
            LastUpdate[i] = localTime;
        }

        public void Remove(PlayerId id)
        {
            if (!id.IsValid || id.Value >= Max) return;
            Active[id.Value] = false;
            _display.Exit(id.Value);
        }

        public void Interpolate(double renderTime)
        {
            _display.Interpolate(renderTime);
        }

        /// <summary>Position to draw: raw for the local player, interpolated for others.</summary>
        public void GetDisplay(int index, out float x, out float z, out float yaw)
        {
            if (index == Local.Value || !_display.Alive[index])
            {
                x = X[index];
                z = Z[index];
                yaw = Yaw[index];
                return;
            }
            x = _display.X[index];
            z = _display.Z[index];
            yaw = _display.Yaw[index];
        }
    }
}
