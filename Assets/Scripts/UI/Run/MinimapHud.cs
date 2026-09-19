using LastGround.Data.Map;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Players;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Run
{
    /// <summary>
    /// Round radar in the top-right corner (TDD_01 §0.9, reference boards): the map's prerendered top view scrolled so
    /// the local player sits in the centre, zombies as red dots and teammates as green dots drawn into a small overlay
    /// texture at 5 Hz (one SetPixels32, no allocation), and the player arrow turned to the aim/move heading.
    /// </summary>
    public sealed class MinimapHud : MonoBehaviour
    {
        const int DotResolution = 96;
        const float RefreshInterval = 0.2f;

        [SerializeField] RawImage _map;
        [SerializeField] RawImage _dots;
        [SerializeField] RectTransform _arrow;
        /// <summary>World metres across the radar.</summary>
        [SerializeField] float _range = 70f;

        static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        static readonly Color32 Zombie = new Color32(220, 40, 30, 255);
        static readonly Color32 Teammate = new Color32(80, 230, 110, 255);

        MapDefinition _definition;
        PlayerStateTable _players;
        ICrowdRenderSource _crowd;
        Texture2D _overlay;
        Color32[] _pixels;
        float _timer;

        public void Bind(MapDefinition map, PlayerStateTable players, ICrowdRenderSource crowd)
        {
            _definition = map;
            _players = players;
            _crowd = crowd;
            _map.texture = map.Minimap;
            _overlay = new Texture2D(DotResolution, DotResolution, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _pixels = new Color32[DotResolution * DotResolution];
            _dots.texture = _overlay;
            gameObject.SetActive(map.Minimap != null);
        }

        void OnDestroy()
        {
            if (_overlay != null) Destroy(_overlay);
        }

        void Update()
        {
            if (_definition == null || !_players.Local.IsValid) return;
            int me = _players.Local.Value;
            float px = _players.X[me], pz = _players.Z[me];
            Rect world = _definition.MinimapRect;
            float w = _range / world.width, h = _range / world.height;
            _map.uvRect = new Rect((px - world.xMin) / world.width - w * 0.5f, (pz - world.yMin) / world.height - h * 0.5f, w, h);
            _arrow.localRotation = Quaternion.Euler(0f, 0f, -_players.Yaw[me]);

            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = RefreshInterval;
            System.Array.Fill(_pixels, Clear);
            for (int i = 0; i < _crowd.Capacity; i++)
                if (_crowd.Alive[i]) Plot(_crowd.X[i] - px, _crowd.Z[i] - pz, Zombie, 0);
            for (int p = 0; p < PlayerStateTable.Max; p++)
                if (p != me && _players.Active[p]) Plot(_players.X[p] - px, _players.Z[p] - pz, Teammate, 1);
            _overlay.SetPixels32(_pixels);
            _overlay.Apply(false);
        }

        /// <summary>A dot (1 or 3 px) at a player-relative offset, clipped to the round radar.</summary>
        void Plot(float dx, float dz, Color32 color, int radius)
        {
            float half = _range * 0.5f;
            if (dx * dx + dz * dz > half * half) return;
            int cx = (int)((dx / _range + 0.5f) * DotResolution), cy = (int)((dz / _range + 0.5f) * DotResolution);
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                if ((uint)y >= DotResolution) continue;
                for (int x = cx - radius; x <= cx + radius; x++)
                    if ((uint)x < DotResolution) _pixels[y * DotResolution + x] = color;
            }
        }
    }
}
