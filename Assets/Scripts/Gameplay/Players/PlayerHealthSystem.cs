using LastGround.Core.Events;
using LastGround.Core.Tick;
using LastGround.Data.Players;

namespace LastGround.Gameplay.Players
{
    /// <summary>
    /// Host-authoritative health and life states (TDD_01 §14.2, TDD_02 §16):
    /// Alive → (0 HP) → Downed: bleedout timer, crawl, no shooting. A standing teammate within ReviveRadius fills
    /// revive progress (slower while that teammate is being hit; it fades when nobody helps) → Alive with part of
    /// their health and brief invulnerability. Bleedout runs out → Dead; a dead player returns after RespawnDelay while
    /// any teammate is still standing. Solo: a limited self-revive ("Adrenaline"), then going down ends the run.
    /// Team wipe (nobody standing) raises <see cref="TeamWiped"/>. Writes the vitals in <see cref="PlayerStateTable"/>.
    /// </summary>
    public sealed partial class PlayerHealthSystem : ITickable, IPlayerDamageSink
    {
        /// <summary>A reviver hit within this many seconds counts as "under fire".</summary>
        const float RecentHurtWindow = 1f;

        readonly PlayerStateTable _players;
        readonly PlayerDefinition _definition;
        readonly float[] _invulnerableTimer = new float[PlayerStateTable.Max];
        readonly float[] _sinceHurt = new float[PlayerStateTable.Max];
        readonly int[] _downsThisLife = new int[PlayerStateTable.Max];
        readonly bool[] _known = new bool[PlayerStateTable.Max];
        int _adrenaline;
        bool _wipeReported;

        /// <summary>Players getting back up with a push (respawn from dead).</summary>
        public readonly EventChannel<PlayerRespawn> Respawns = new EventChannel<PlayerRespawn>(16);

        public PlayerHealthSystem(PlayerStateTable players, PlayerDefinition definition)
        {
            _players = players;
            _definition = definition;
            _adrenaline = definition.SoloAdrenaline;
            for (int p = 0; p < _sinceHurt.Length; p++) _sinceHurt[p] = float.MaxValue;
        }

        public int Downs { get; private set; }
        public int Deaths { get; private set; }
        public int Revives { get; private set; }
        public int AdrenalineLeft => _adrenaline;

        /// <summary>Set once when every active player is downed or dead.</summary>
        public bool TeamWiped { get; private set; }

        public void Damage(int player, float amount)
        {
            if (player < 0 || player >= PlayerStateTable.Max || !_players.Active[player]) return;
            if (_players.Life[player] != PlayerLife.Alive || _players.Invulnerable[player] || amount <= 0f) return;
            _sinceHurt[player] = 0f;
            float health = _players.Health[player] - amount;
            bool down = health <= 0f;
            _players.Health[player] = down ? 0f : health;
            if (down) GoDown(player);
            _players.Hurt.Publish(new PlayerHurt { Player = player, Amount = amount, Died = down });
        }

        public void Tick(float dt, uint tick)
        {
            int active = 0, standing = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_players.Active[p])
                {
                    _known[p] = false;
                    continue;
                }
                if (!_known[p]) Join(p);
                active++;
                _sinceHurt[p] += dt;
                if (_players.Life[p] == PlayerLife.Alive) standing++;
            }

            for (int p = 0; p < PlayerStateTable.Max; p++)
            {
                if (!_players.Active[p]) continue;
                switch (_players.Life[p])
                {
                    case PlayerLife.Alive:
                        TickInvulnerability(p, dt);
                        break;
                    case PlayerLife.Downed:
                        TickDowned(p, dt, active);
                        break;
                    case PlayerLife.Dead:
                        TickDead(p, dt, standing);
                        break;
                }
            }

            // Wiped = nobody standing and nobody able to get up (a solo player with adrenaline still can).
            bool canSelfRevive = active == 1 && _adrenaline > 0;
            if (active > 0 && !_wipeReported && CountStanding() == 0 && !canSelfRevive)
            {
                _wipeReported = true;
                TeamWiped = true;
            }
        }

        void Join(int p)
        {
            _known[p] = true;
            _players.Health[p] = _definition.MaxHealth;
            _players.Life[p] = PlayerLife.Alive;
            _players.Invulnerable[p] = false;
            _players.Countdown[p] = 0f;
            _players.ReviveProgress[p] = 0f;
            _downsThisLife[p] = 0;
        }

        void GoDown(int p)
        {
            _downsThisLife[p]++;
            Downs++;
            _players.Life[p] = PlayerLife.Downed;
            _players.ReviveProgress[p] = 0f;
            float bleedout = _definition.BleedoutTime;
            if (_downsThisLife[p] >= _definition.FastBleedoutFromDown) bleedout *= _definition.FastBleedoutScale;
            _players.Countdown[p] = bleedout;
        }

        void TickInvulnerability(int p, float dt)
        {
            if (_invulnerableTimer[p] <= 0f) return;
            _invulnerableTimer[p] -= dt;
            if (_invulnerableTimer[p] <= 0f) _players.Invulnerable[p] = false;
        }

        int CountStanding()
        {
            int standing = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++) if (_players.CanAct(p)) standing++;
            return standing;
        }

        void MakeInvulnerable(int p, float seconds)
        {
            _players.Invulnerable[p] = true;
            _invulnerableTimer[p] = seconds;
        }
    }
}
