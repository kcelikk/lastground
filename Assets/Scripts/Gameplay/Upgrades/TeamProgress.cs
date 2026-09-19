using LastGround.Core.Events;
using LastGround.Core.Tick;
using LastGround.Data.Upgrades;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Players;

namespace LastGround.Gameplay.Upgrades
{
    /// <summary>
    /// Host: team XP and levels (TDD_01 §7.5, §13.1, D-002). Every kill adds XP to one shared pool; the whole team
    /// levels up together and each active player gets their own offer. Offers queue per player (several level-ups
    /// in a row are fine); a choice is validated against the head of that player's queue, applied to their build,
    /// and announced through <see cref="TeamBuilds.Changed"/>. The game never waits for anyone to choose.
    /// </summary>
    public sealed class TeamProgress : ITickable, IUpgradeChoiceSink
    {
        const int QueueCapacity = 8;

        readonly CrowdState _crowd;
        readonly PlayerStateTable _players;
        readonly TeamBuilds _builds;
        readonly LevelCurveDefinition _curve;
        readonly int _xpPerKill;
        readonly OfferGenerator _generator;
        readonly UpgradeOffer[] _queue = new UpgradeOffer[PlayerStateTable.Max * QueueCapacity];
        readonly int[] _queueHead = new int[PlayerStateTable.Max];
        readonly int[] _queueCount = new int[PlayerStateTable.Max];
        EventReader<CrowdDeath> _deaths;
        ushort _nextOfferId = 1;

        public TeamProgress(CrowdState crowd, PlayerStateTable players, TeamBuilds builds, LevelCurveDefinition curve,
            int xpPerKill, uint runSeed)
        {
            _crowd = crowd;
            _players = players;
            _builds = builds;
            _curve = curve;
            _xpPerKill = xpPerKill;
            _generator = new OfferGenerator(builds.Catalog, runSeed);
            _deaths = crowd.Deaths.CreateReader();
            Level = 1;
        }

        /// <summary>XP per kill by zombie type (M6: Runners, Tanks… are worth more). Null = the flat value for every kill.</summary>
        public Data.Zombies.ZombieDefinition[] ZombieTypes { get; set; }

        /// <summary>Receives new offers (local UI queue for the host's player, network for the others).</summary>
        public IOfferSink Offers { get; set; }

        public int Xp { get; private set; }
        public int Level { get; private set; }
        public int TotalXp { get; private set; }
        public int XpToNext => _curve.XpForLevel(Level, ActivePlayers());

        /// <summary>Changes whenever XP or level change (replication dirty check).</summary>
        public int Version { get; private set; }

        public int PendingFor(int player) => _queueCount[player];

        public void Tick(float dt, uint tick)
        {
            int gained = 0;
            while (_crowd.Deaths.TryRead(ref _deaths, out CrowdDeath death)) gained += XpOf(death.Type);
            if (gained > 0) AddXp(gained);
        }

        int XpOf(byte type) =>
            ZombieTypes != null && type < ZombieTypes.Length && ZombieTypes[type] != null ? ZombieTypes[type].Xp : _xpPerKill;

        public void AddXp(int amount)
        {
            if (amount <= 0) return;
            Xp += amount;
            TotalXp += amount;
            Version++;
            while (Level < _curve.MaxLevel && Xp >= XpToNext)
            {
                Xp -= XpToNext;
                Level++;
                for (int p = 0; p < PlayerStateTable.Max; p++)
                    if (_players.Active[p]) Offer(p);
            }
        }

        public void Choose(int player, ushort offerId, int choice)
        {
            if ((uint)player >= PlayerStateTable.Max || _queueCount[player] == 0) return;
            ref UpgradeOffer head = ref _queue[player * QueueCapacity + _queueHead[player]];
            if (head.Id != offerId || (uint)choice >= head.Count) return;
            int upgrade = head.UpgradeAt(choice);
            UpgradeRarity rarity = head.RarityAt(choice);
            _queueHead[player] = (_queueHead[player] + 1) % QueueCapacity;
            _queueCount[player]--;
            _builds.Apply(player, upgrade, rarity);
        }

        /// <summary>Objective reward (TDD_01 §12.2 "Legendary teklif"): one extra offer for every player.</summary>
        public void GrantBonusOffer(UpgradeRarity minRarity)
        {
            for (int p = 0; p < PlayerStateTable.Max; p++)
                if (_players.Active[p]) Offer(p, minRarity);
        }

        void Offer(int player, UpgradeRarity minRarity = UpgradeRarity.Common)
        {
            if (_queueCount[player] >= QueueCapacity) return;
            UpgradeOffer offer = _generator.Generate(_builds.Of(player), player, _nextOfferId++, minRarity);
            if (offer.Count == 0) return;
            _queue[player * QueueCapacity + (_queueHead[player] + _queueCount[player]) % QueueCapacity] = offer;
            _queueCount[player]++;
            Offers?.Deliver(player, offer);
        }

        int ActivePlayers()
        {
            int count = 0;
            for (int p = 0; p < PlayerStateTable.Max; p++) if (_players.Active[p]) count++;
            return count < 1 ? 1 : count;
        }
    }
}
