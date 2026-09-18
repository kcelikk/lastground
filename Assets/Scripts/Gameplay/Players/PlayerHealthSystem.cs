using LastGround.Core.Events;
using LastGround.Core.Tick;
using LastGround.Data.Players;
using LastGround.Data.Upgrades;
using LastGround.Gameplay.Upgrades;

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
        EventReader<BuildChanged> _buildReader;
        TeamBuilds _builds;

        /// <summary>Players getting back up with a push (respawn from dead).</summary>
        public readonly EventChannel<PlayerRespawn> Respawns = new EventChannel<PlayerRespawn>(16);

        public PlayerHealthSystem(PlayerStateTable players, PlayerDefinition definition)
        {
            _players = players;
            _definition = definition;
            _adrenaline = definition.SoloAdrenaline;
            for (int p = 0; p < _sinceHurt.Length; p++) _sinceHurt[p] = float.MaxValue;
        }

        /// <summary>Team builds: max health, damage reduction and heal-on-kill follow upgrades. Optional.</summary>
        public TeamBuilds Builds
        {
            get => _builds;
            set
            {
                _builds = value;
                if (value != null) _buildReader = value.Changed.CreateReader();
            }
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
            if (_builds != null)
                amount *= 1f - UnityEngine.Mathf.Min(0.6f, _builds.Of(player).Get(StatId.DamageReductionPct) / 100f);
            float health = _players.Health[player] - amount;
            bool down = health <= 0f;
            _players.Health[player] = down ? 0f : health;
            if (down) GoDown(player);
            _players.Hurt.Publish(new PlayerHurt { Player = player, Amount = amount, Died = down });
        }

        /// <summary>Heals a standing player (medkit). False when they are down or already at full health.</summary>
        public bool Heal(int player, float amount)
        {
            if ((uint)player >= PlayerStateTable.Max || !_players.CanAct(player)) return false;
            float max = _players.MaxHealth[player];
            if (_players.Health[player] >= max - 0.01f) return false;
            _players.Health[player] = UnityEngine.Mathf.Min(max, _players.Health[player] + amount);
            return true;
        }

        /// <summary>The shooter killed a zombie: heal-on-kill upgrades (TDD_01 §7.2 OnKillEffect).</summary>
        public void OnKill(int player)
        {
            if (_builds == null || (uint)player >= PlayerStateTable.Max || !_players.CanAct(player)) return;
            float heal = _builds.Of(player).Get(StatId.HealOnKill);
            if (heal > 0f) _players.Health[player] = UnityEngine.Mathf.Min(_players.MaxHealth[player], _players.Health[player] + heal);
        }

        public void Tick(float dt, uint tick)
        {
            ApplyBuildChanges();
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

        void ApplyBuildChanges()
        {
            if (_builds == null) return;
            while (_builds.Changed.TryRead(ref _buildReader, out BuildChanged change))
            {
                float max = MaxHealthOf(change.Player);
                float gained = max - _players.MaxHealth[change.Player];
                _players.MaxHealth[change.Player] = max;
                // More max health also heals by the same amount (a pick should feel good right away).
                if (gained > 0f && _players.Life[change.Player] == PlayerLife.Alive) _players.Health[change.Player] += gained;
            }
        }

        float MaxHealthOf(int p) => _definition.MaxHealth + (_builds != null ? _builds.Of(p).Get(StatId.MaxHealth) : 0f);

        void Join(int p)
        {
            _known[p] = true;
            _players.MaxHealth[p] = MaxHealthOf(p);
            _players.Health[p] = _players.MaxHealth[p];
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
