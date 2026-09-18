using LastGround.Core.Random;
using LastGround.Data.Director;
using LastGround.Gameplay.Crowd;

namespace LastGround.Gameplay.Director
{
    /// <summary>
    /// Runtime view of the spawn deck (TDD_01 §9.6, §9.8): picks the zombie type for each planned spawn by weight among
    /// the unlocked cards that fit the remaining points and their concurrency cap, and decides elites with pacing
    /// (unlock time, minimum interval, alive cap). Counts alive and queued zombies per type. Allocation-free.
    /// </summary>
    public sealed class SpawnDeck
    {
        readonly SpawnDeckDefinition _definition;
        readonly int[] _alive;
        readonly int[] _queued;
        readonly float[] _weights;
        int _aliveElites;
        float _lastElite = float.NegativeInfinity;

        public SpawnDeck(SpawnDeckDefinition definition)
        {
            _definition = definition;
            int types = 0;
            foreach (SpawnCard card in definition.Cards) types = System.Math.Max(types, card.Zombie.TypeIndex + 1);
            _alive = new int[types];
            _queued = new int[types];
            _weights = new float[definition.Cards.Length];
        }

        public int CardCount => _definition.Cards.Length;
        public ref readonly SpawnCard Card(int index) => ref _definition.Cards[index];

        /// <summary>Recounts alive zombies per type and alive elites (once per plan).</summary>
        public void CountAlive(CrowdState crowd)
        {
            System.Array.Clear(_alive, 0, _alive.Length);
            _aliveElites = 0;
            for (int i = 0; i < crowd.Capacity; i++)
            {
                if (!crowd.AliveSlots[i]) continue;
                if (crowd.Type[i] < _alive.Length) _alive[crowd.Type[i]]++;
                if (crowd.Elite[i] != 0) _aliveElites++;
            }
        }

        /// <summary>
        /// Weighted pick among cards unlocked at <paramref name="runSeconds"/> that cost at most <paramref name="points"/>
        /// and are under their cap. Returns the card index or -1.
        /// </summary>
        public int Pick(float runSeconds, int points, int players, ref DeterministicRandom rng)
        {
            float total = 0f;
            for (int c = 0; c < _definition.Cards.Length; c++)
            {
                ref readonly SpawnCard card = ref _definition.Cards[c];
                float weight = 0f;
                int type = card.Zombie.TypeIndex;
                bool capped = card.MaxConcurrentPerPlayer > 0 && _alive[type] + _queued[type] >= card.MaxConcurrentPerPlayer * players;
                if (runSeconds >= card.MinRunSeconds && card.Cost <= points && !capped)
                    weight = card.Weight != null ? UnityEngine.Mathf.Max(0f, card.Weight.Evaluate((runSeconds - card.MinRunSeconds) / 60f)) : 1f;
                _weights[c] = weight;
                total += weight;
            }
            if (total <= 0f) return -1;
            float roll = rng.NextFloat() * total;
            for (int c = 0; c < _weights.Length; c++)
            {
                roll -= _weights[c];
                if (roll < 0f && _weights[c] > 0f) return c;
            }
            for (int c = _weights.Length - 1; c >= 0; c--) if (_weights[c] > 0f) return c;
            return -1;
        }

        /// <summary>Rolls whether the planned spawn is an elite; returns the modifier id or 0.</summary>
        public byte RollElite(int cardIndex, float runSeconds, int players, ref DeterministicRandom rng)
        {
            var modifiers = _definition.EliteModifiers;
            if (modifiers == null || modifiers.Length == 0 || runSeconds < _definition.EliteMinRunSeconds) return 0;
            if (runSeconds - _lastElite < _definition.EliteMinInterval) return 0;
            if (_aliveElites >= _definition.MaxElitesBase + _definition.MaxElitesPerPlayer * players) return 0;
            if (rng.NextFloat() >= _definition.Cards[cardIndex].EliteChance) return 0;
            _lastElite = runSeconds;
            _aliveElites++;
            return modifiers[rng.Range(0, modifiers.Length)].NetIndex;
        }

        public void OnQueued(byte type) { if (type < _queued.Length) _queued[type]++; }
        public void OnDequeued(byte type) { if (type < _queued.Length && _queued[type] > 0) _queued[type]--; }
    }
}
