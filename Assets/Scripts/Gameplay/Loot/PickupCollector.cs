using LastGround.Core.Tick;
using LastGround.Data.Loot;
using LastGround.Data.Upgrades;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Upgrades;

namespace LastGround.Gameplay.Loot
{
    /// <summary>Where the local player's pickup requests go (host: registry; client: network).</summary>
    public interface IPickupClaimSink
    {
        void Claim(int player, int id);
    }

    /// <summary>
    /// The local player walks over loot (TDD_01 §13.2): pickups in radius are requested at once and hidden locally
    /// (instant feel); the host confirms. If no confirmation arrives in time the pickup shows again. Medkits are not
    /// requested at full health.
    /// </summary>
    public sealed class PickupCollector : ITickable
    {
        const float ClaimTimeout = 1.5f;

        readonly PickupTable _table;
        readonly PlayerStateTable _players;
        readonly LootDefinition _loot;
        readonly IPickupClaimSink _sink;

        public PickupCollector(PickupTable table, PlayerStateTable players, LootDefinition loot, IPickupClaimSink sink)
        {
            _table = table;
            _players = players;
            _loot = loot;
            _sink = sink;
        }

        public TeamBuilds Builds { get; set; }

        public void Tick(float dt, uint tick)
        {
            for (int id = 0; id < _table.Capacity; id++)
                if (_table.PendingClaim[id] > 0f) _table.PendingClaim[id] -= dt;

            int me = _players.Local.IsValid ? _players.Local.Value : -1;
            if (me < 0 || !_players.CanAct(me)) return;
            float bonus = Builds != null ? Builds.Of(me).Get(StatId.PickupRadiusPct) / 100f : 0f;
            float radius = _loot.PickupRadius * (1f + bonus);
            float r2 = radius * radius;
            bool fullHealth = _players.Health[me] >= _players.MaxHealth[me] - 0.01f;
            float x = _players.X[me], z = _players.Z[me];
            for (int id = 0; id < _table.Capacity; id++)
            {
                if (!_table.VisibleTo(id, me)) continue;
                if (_table.Type[id] == PickupType.Medkit && fullHealth) continue;
                float dx = _table.X[id] - x, dz = _table.Z[id] - z;
                if (dx * dx + dz * dz > r2) continue;
                _table.PendingClaim[id] = ClaimTimeout;
                _sink.Claim(me, id);
            }
        }
    }
}
