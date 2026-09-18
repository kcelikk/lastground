using LastGround.Core.Tick;
using LastGround.Data.Presentation;
using LastGround.Gameplay.Players;
using UnityEngine;

namespace LastGround.Rendering
{
    /// <summary>
    /// One GameObject per player slot (max 4), created once at run start and shown/hidden by the table.
    /// Slot colours double as the team ring colour (TDD_01 §0.6). Downed players lie down and pulse red, dead ones
    /// lie down greyed out, invulnerable ones blink.
    /// </summary>
    public sealed class PlayerViews : ITickable
    {
        readonly PlayerStateTable _table;
        readonly Transform[] _views = new Transform[PlayerStateTable.Max];
        readonly MeshRenderer[] _renderers = new MeshRenderer[PlayerStateTable.Max];
        readonly MaterialPropertyBlock[] _blocks = new MaterialPropertyBlock[PlayerStateTable.Max];
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color DownedColor = new Color(0.9f, 0.1f, 0.08f);
        static readonly Color DeadColor = new Color(0.25f, 0.25f, 0.27f);
        float _time;

        public PlayerViews(PlayerStateTable table, Transform parent, Mesh mesh, Material material)
        {
            _table = table;
            for (int i = 0; i < _views.Length; i++)
            {
                var go = new GameObject("Player_" + i, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                var block = new MaterialPropertyBlock();
                block.SetColor(BaseColorId, PlayerSlotColors.Of(i));
                renderer.SetPropertyBlock(block);
                _blocks[i] = block;
                go.SetActive(false);
                _views[i] = go.transform;
                _renderers[i] = renderer;
            }
        }

        public Transform Get(int index) => _views[index];

        public void Tick(float dt, uint tick)
        {
            _time += dt;
            for (int i = 0; i < _views.Length; i++)
            {
                bool active = _table.Active[i];
                if (_views[i].gameObject.activeSelf != active) _views[i].gameObject.SetActive(active);
                if (!active) continue;
                _table.GetDisplay(i, out float x, out float z, out float yaw);
                PlayerLife life = _table.Life[i];
                if (life != PlayerLife.Alive)
                    _views[i].SetPositionAndRotation(new Vector3(x, 0.5f, z), Quaternion.Euler(90f, yaw, 0f));
                else
                    _views[i].SetPositionAndRotation(new Vector3(x, 1f, z), Quaternion.Euler(0f, yaw, 0f));
                Color color = life == PlayerLife.Alive ? PlayerSlotColors.Of(i)
                    : life == PlayerLife.Dead ? DeadColor
                    : Color.Lerp(PlayerSlotColors.Of(i), DownedColor, 0.5f + 0.5f * Mathf.Sin(_time * 8f));
                _blocks[i].SetColor(BaseColorId, color);
                _renderers[i].SetPropertyBlock(_blocks[i]);
                bool visible = !_table.Invulnerable[i] || Mathf.Repeat(_time * 8f, 1f) < 0.6f;
                if (_renderers[i].enabled != visible) _renderers[i].enabled = visible;
            }
        }
    }
}
