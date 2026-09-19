using LastGround.Data.Zombies;
using Unity.Mathematics;

namespace LastGround.Gameplay.Zombies
{
    /// <summary>
    /// Driven bodies (M8 boss, D-021): a crowd slot whose type has <see cref="ZombieBehaviour.Boss"/>. The steering
    /// job, melee attacks, stun and slow leave it alone; a controller moves it and picks its animation state. Health,
    /// hits, kill credit, replication and rendering stay the ordinary crowd paths.
    /// </summary>
    public sealed partial class ZombieWorld
    {
        /// <summary>Scales damage to driven bodies (weak point, invulnerability).</summary>
        public IZombieDamageModifier DamageModifier { get; set; }

        public bool IsDriven(int slot) => _types[_type[slot]].Behaviour == ZombieBehaviour.Boss;

        /// <summary>Places a driven body: position, heading (degrees) and animation state (CrowdClipId).</summary>
        public void Drive(int slot, float2 position, float heading, byte anim)
        {
            if ((uint)slot >= (uint)_capacity || _alive[slot] == 0) return;
            _position[slot] = position;
            _velocity[slot] = float2.zero;
            _heading[slot] = heading;
            _crowd.PosX[slot] = position.x;
            _crowd.PosZ[slot] = position.y;
            _crowd.Heading[slot] = heading;
            _crowd.Anim[slot] = anim;
        }

        /// <summary>Sets a driven body's health (scaled by player count, M8).</summary>
        public void SetHealth(int slot, float health)
        {
            if ((uint)slot >= (uint)_capacity || _alive[slot] == 0) return;
            _health[slot] = health;
        }

        float DrivenDamageScale(int slot, float2 direction, float amount, int sourcePlayer) =>
            DamageModifier != null && IsDriven(slot) ? DamageModifier.Scale(slot, direction, amount, sourcePlayer) : 1f;
    }
}
