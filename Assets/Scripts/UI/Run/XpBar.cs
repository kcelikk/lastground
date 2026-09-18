using LastGround.Core.Services;
using LastGround.Gameplay.Upgrades;
using LastGround.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Run
{
    /// <summary>Team XP bar and level under the health bar (TDD_01 §0.9: orange = XP/level). Shared by everyone (D-002).</summary>
    public sealed class XpBar : MonoBehaviour
    {
        [SerializeField] Image _fill;
        [SerializeField] TMP_Text _level;

        TeamXp _xp;
        string _format;
        int _shownLevel = -1;
        float _flash;

        public void Bind(TeamXp xp)
        {
            _xp = xp;
            _format = AppServices.Get<ILocalizationService>().Get("hud.level");
        }

        void Update()
        {
            if (_xp == null) return;
            _fill.fillAmount = Mathf.Clamp01(_xp.Xp / (float)Mathf.Max(1, _xp.XpToNext));
            if (_xp.Level != _shownLevel)
            {
                if (_shownLevel > 0) _flash = 1f;
                _shownLevel = _xp.Level;
                _level.SetText(_format, _xp.Level);
            }
            if (_flash <= 0f) return;
            _flash = Mathf.Max(0f, _flash - Time.unscaledDeltaTime * 1.5f);
            float scale = 1f + _flash * 0.35f;
            _level.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
