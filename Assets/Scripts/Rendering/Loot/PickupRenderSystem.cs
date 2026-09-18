using LastGround.Core.Tick;
using LastGround.Gameplay.Loot;
using LastGround.Gameplay.Players;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastGround.Rendering.Loot
{
    /// <summary>How one pickup type is drawn: mesh, material, size and whether it lies flat (coins).</summary>
    public struct PickupLook
    {
        public Mesh Mesh;
        public Material Material;
        public Vector3 Size;
        public bool Flat;
    }

    /// <summary>
    /// Pickups on the ground (TDD_01 §13.2 PickupRenderSystem): one instanced draw per type, gentle bob and spin,
    /// coin piles scaled by value (1 / 5 / 25 tiers). Each device draws only the pickups its local player owns
    /// (instanced loot) and hides ones it has just claimed. No GameObjects, no allocation.
    /// </summary>
    public sealed class PickupRenderSystem : ITickable
    {
        readonly PickupTable _table;
        readonly PlayerStateTable _players;
        readonly PickupLook[] _looks;
        readonly Matrix4x4[][] _matrices;
        readonly int[] _counts;
        float _time;

        /// <param name="looks">Indexed by <see cref="PickupType"/>; types without a mesh are not drawn.</param>
        public PickupRenderSystem(PickupTable table, PlayerStateTable players, PickupLook[] looks)
        {
            _table = table;
            _players = players;
            _looks = looks;
            _matrices = new Matrix4x4[looks.Length][];
            for (int i = 0; i < looks.Length; i++) _matrices[i] = new Matrix4x4[table.Capacity];
            _counts = new int[looks.Length];
        }

        public void Tick(float dt, uint tick)
        {
            _time += dt;
            int me = _players.Local.IsValid ? _players.Local.Value : 0;
            System.Array.Clear(_counts, 0, _counts.Length);
            for (int id = 0; id < _table.Capacity; id++)
            {
                if (!_table.VisibleTo(id, me)) continue;
                int type = (int)_table.Type[id];
                if (type >= _looks.Length || _looks[type].Mesh == null) continue;
                float bob = 0.35f + Mathf.Sin(_time * 3f + id) * 0.08f;
                Quaternion spin = Quaternion.Euler(0f, (_time * 90f + id * 37f) % 360f, 0f);
                var position = new Vector3(_table.X[id], bob, _table.Z[id]);
                Vector3 size = _looks[type].Size;
                if (_looks[type].Flat)
                {
                    int value = _table.Value[id];
                    float tier = value >= 25 ? 1.9f : value >= 5 ? 1.4f : 1f;
                    size = new Vector3(size.x * tier, size.y, size.z * tier);
                    spin *= Quaternion.Euler(90f, 0f, 0f);
                }
                _matrices[type][_counts[type]++] = Matrix4x4.TRS(position, spin, size);
            }
            for (int type = 0; type < _looks.Length; type++) Draw(_looks[type], _matrices[type], _counts[type]);
        }

        static void Draw(in PickupLook look, Matrix4x4[] matrices, int count)
        {
            if (count == 0 || look.Mesh == null || look.Material == null) return;
            var rp = new RenderParams(look.Material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, new Vector3(1000f, 20f, 1000f)),
            };
            Graphics.RenderMeshInstanced(rp, look.Mesh, 0, matrices, count);
        }
    }
}
