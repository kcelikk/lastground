using LastGround.Data.Map;
using LastGround.Gameplay.Crowd;
using LastGround.Gameplay.Director;
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
        /// <summary>12 ring segments around the radar (TDD_01 §9.11): direction and size of far hordes; index 0 = east, counter-clockwise.</summary>
        [SerializeField] Image[] _sectors;
        /// <summary>Group size at which a segment is fully lit.</summary>
        [SerializeField] float _sectorFull = 30f;

        static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        static readonly Color32 Zombie = new Color32(220, 40, 30, 255);
        static readonly Color32 Teammate = new Color32(80, 230, 110, 255);

        MapDefinition _definition;
        PlayerStateTable _players;
        ICrowdRenderSource _crowd;
        Texture2D _overlay;
        Color32[] _pixels;
        HordeSummary _horde;
        readonly float[] _sectorSize = new float[12];
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
            SetSectors(0f);
            gameObject.SetActive(map.Minimap != null);
        }

        /// <summary>Far horde groups for the direction ring (VirtualHorde summary, M8).</summary>
        public void BindHorde(HordeSummary horde) => _horde = horde;

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
            UpdateSectors(px, pz);
        }

        void UpdateSectors(float px, float pz)
        {
            if (_sectors == null || _sectors.Length == 0) return;
            for (int s = 0; s < _sectorSize.Length; s++) _sectorSize[s] = 0f;
            if (_horde != null)
            {
                for (int g = 0; g < _horde.Count; g++)
                {
                    float angle = Mathf.Atan2(_horde.Z[g] - pz, _horde.X[g] - px) * Mathf.Rad2Deg;
                    int sector = (int)Mathf.Repeat(Mathf.Round(angle / 30f), 12f);
                    _sectorSize[sector] += _horde.Size[g];
                }
            }
            for (int s = 0; s < _sectors.Length && s < _sectorSize.Length; s++)
            {
                Color c = _sectors[s].color;
                c.a = Mathf.Clamp01(_sectorSize[s] / _sectorFull) * 0.9f;
                _sectors[s].color = c;
            }
        }

        void SetSectors(float alpha)
        {
            if (_sectors == null) return;
            foreach (Image sector in _sectors)
            {
                Color c = sector.color;
                c.a = alpha;
                sector.color = c;
            }
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
