using LastGround.Core.Events;
using LastGround.Core.Tick;
using LastGround.Data.Players;

namespace LastGround.Gameplay.Players
{
    /// <summary>
    /// Host-authoritative player health (TDD_02 §16). M4: at 0 HP a player is dead for RespawnDelay seconds, then
    /// gets back up in place with full health, brief invulnerability and a push that clears nearby zombies.
    /// Downed/revive replaces this in M5. Writes the vitals in <see cref="PlayerStateTable"/>; PlayerVitals
    /// replication copies them to clients.
    /// </summary>
    public sealed class PlayerHealthSystem : ITickable, IPlayerDamageSink
    {
        readonly PlayerStateTable _players;
        readonly PlayerDefinition _definition;
        readonly float[] _deadTimer = new float[PlayerStateTable.Max];
        readonly float[] _invulnerableTimer = new float[PlayerStateTable.Max];
        readonly bool[] _known = new bool[PlayerStateTable.Max];

        /// <summary>Players getting back up this tick (zombie push-back).</summary>
        public readonly EventChannel<PlayerRespawn> Respawns = new EventChannel<PlayerRespawn>(16);

        public PlayerHealthSystem(PlayerStateTable players, PlayerDefinition definition)
        {
            _players = players;
            _definition = definition;
        }

        public int Deaths { get; private set; }

        public void Damage(int player, float amount)
        {
            if (player < 0 || player >= PlayerStateTable.Max || !_players.Active[player]) return;
            if (_players.Dead[player] || _players.Invulnerable[player] || amount <= 0f) return;
            float health = _players.Health[player] - amount;
            bool died = health <= 0f;
            _players.Health[player] = died ? 0f : health;
            if (died)
            {
                _players.Dead[player] = true;
                _deadTimer[player] = _definition.RespawnDelay;
                Deaths++;
            }
            _players.Hurt.Publish(new PlayerHurt { Player = player, Amount = amount, Died = died });
        }

        public void Tick(float dt, uint tick)
        {
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_players.Active[p])
                {
                    _known[p] = false;
                    continue;
                }
                if (!_known[p])
                {
                    // Joined (or re-joined): start healthy.
                    _known[p] = true;
                    _players.Health[p] = _definition.MaxHealth;
                    _players.Dead[p] = false;
                    _players.Invulnerable[p] = false;
                }

                if (_players.Dead[p])
                {
                    _deadTimer[p] -= dt;
                    if (_deadTimer[p] <= 0f) Respawn(p);
                    continue;
                }
                if (_invulnerableTimer[p] > 0f)
                {
                    _invulnerableTimer[p] -= dt;
                    if (_invulnerableTimer[p] <= 0f) _players.Invulnerable[p] = false;
                }
            }
        }

        void Respawn(int p)
        {
            _players.Dead[p] = false;
            _players.Health[p] = _definition.MaxHealth;
            _players.Invulnerable[p] = true;
            _invulnerableTimer[p] = _definition.RespawnInvulnerability;
            Respawns.Publish(new PlayerRespawn
            {
                Player = p, X = _players.X[p], Z = _players.Z[p],
                PushRadius = _definition.RespawnPushRadius, PushSpeed = _definition.RespawnPushSpeed,
            });
        }
    }
}
