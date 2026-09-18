using LastGround.Core.Tick;
using LastGround.Gameplay.Loot;
using LastGround.Gameplay.Players;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastGround.Rendering.Loot
{
    /// <summary>
    /// Pickups on the ground (TDD_01 §13.2 PickupRenderSystem): one instanced draw per type, gentle bob and spin,
    /// coin piles scaled by value (1 / 5 / 25 tiers). Each device draws only the pickups its local player owns
    /// (instanced loot) and hides ones it has just claimed. No GameObjects, no allocation.
    /// </summary>
    public sealed class PickupRenderSystem : ITickable
    {
        readonly PickupTable _table;
        readonly PlayerStateTable _players;
        readonly Mesh _coinMesh;
        readonly Mesh _medkitMesh;
        readonly Material _coinMaterial;
        readonly Material _medkitMaterial;
        readonly Matrix4x4[] _coins;
        readonly Matrix4x4[] _medkits;
        float _time;

        public PickupRenderSystem(PickupTable table, PlayerStateTable players, Mesh coinMesh, Material coinMaterial,
            Mesh medkitMesh, Material medkitMaterial)
        {
            _table = table;
            _players = players;
            _coinMesh = coinMesh;
            _coinMaterial = coinMaterial;
            _medkitMesh = medkitMesh;
            _medkitMaterial = medkitMaterial;
            _coins = new Matrix4x4[table.Capacity];
            _medkits = new Matrix4x4[table.Capacity];
        }

        public void Tick(float dt, uint tick)
        {
            _time += dt;
            int me = _players.Local.IsValid ? _players.Local.Value : 0;
            int coins = 0, medkits = 0;
            for (int id = 0; id < _table.Capacity; id++)
            {
                if (!_table.VisibleTo(id, me)) continue;
                float bob = 0.35f + Mathf.Sin(_time * 3f + id) * 0.08f;
                Quaternion spin = Quaternion.Euler(0f, (_time * 90f + id * 37f) % 360f, 0f);
                var position = new Vector3(_table.X[id], bob, _table.Z[id]);
                if (_table.Type[id] == PickupType.Coin)
                {
                    int value = _table.Value[id];
                    float size = value >= 25 ? 0.75f : value >= 5 ? 0.55f : 0.4f;
                    _coins[coins++] = Matrix4x4.TRS(position, spin * Quaternion.Euler(90f, 0f, 0f), new Vector3(size, 0.06f, size));
                }
                else
                {
                    _medkits[medkits++] = Matrix4x4.TRS(position, spin, new Vector3(0.45f, 0.3f, 0.45f));
                }
            }
            Draw(_coinMesh, _coinMaterial, _coins, coins);
            Draw(_medkitMesh, _medkitMaterial, _medkits, medkits);
        }

        static void Draw(Mesh mesh, Material material, Matrix4x4[] matrices, int count)
        {
            if (count == 0 || mesh == null || material == null) return;
            var rp = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, new Vector3(1000f, 20f, 1000f)),
            };
            Graphics.RenderMeshInstanced(rp, mesh, 0, matrices, count);
        }
    }
}
