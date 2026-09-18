using System;
using LastGround.Core.Events;
using LastGround.Core.Input;
using LastGround.Core.Services;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Players;
using LastGround.Gameplay.Run;
using LastGround.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Run
{
    /// <summary>
    /// Combat HUD (TDD_01 §0.9 panel 2): health bar top-left, ammo and reload ring, red hurt vignette, the life-state
    /// overlay (downed: bleedout countdown + revive ring; solo adrenaline; dead: return countdown) and the auto-fire
    /// toggle. Numbers use allocation-free TMP SetText; localized formats are read once.
    /// </summary>
    public sealed class CombatHud : MonoBehaviour
    {
        [SerializeField] Image _healthFill;
        [SerializeField] TMP_Text _healthLabel;
        [SerializeField] TMP_Text _ammoLabel;
        [SerializeField] Image _reloadRing;
        [SerializeField] Image _hurtVignette;
        [SerializeField] GameObject _deathOverlay;
        [SerializeField] TMP_Text _deathLabel;
        [SerializeField] Image _reviveRing;
        [SerializeField] Button _autoFireButton;
        [SerializeField] TMP_Text _autoFireLabel;
        [SerializeField] TMP_Text _coinLabel;

        PlayerStateTable _players;
        IWeaponStatus _weapon;
        AimResolver _aim;
        Action<ControlMode> _modeChanged;
        ILocalizationService _localization;
        EventReader<PlayerHurt> _hurtReader;
        float _maxHealth;
        float _vignette;
        string _ammoFormat;
        string _reloadingText;
        string _downedFormat;
        string _deadFormat;
        string _deadWaiting;
        string _adrenaline;
        PlayerLife _shownLife;
        System.Func<bool> _isSolo;
        RunOutcome _outcome;
        LastGround.Gameplay.Loot.TeamWallet _wallet;
        int _shownCoins = -1;
        string _coinFormat;

        /// <summary>Team run coin shown top-right (gold = money, TDD_01 §0.9).</summary>
        public void SetWallet(LastGround.Gameplay.Loot.TeamWallet wallet) => _wallet = wallet;

        /// <summary>Once the run is over the results screen takes over; the life overlay hides.</summary>
        public void SetOutcome(RunOutcome outcome) => _outcome = outcome;
        int _shownAmmo = -1;
        bool _shownReloading;
        int _shownHealth = -1;
        int _shownCountdown = -1;

        /// <param name="isSolo">True while this is a one-player run (the downed overlay then shows adrenaline).</param>
        public void Bind(PlayerStateTable players, IWeaponStatus weapon, AimResolver aim, float maxHealth,
            System.Func<bool> isSolo, Action<ControlMode> modeChanged)
        {
            _players = players;
            _weapon = weapon;
            _aim = aim;
            _maxHealth = Mathf.Max(1f, maxHealth);
            _isSolo = isSolo;
            _modeChanged = modeChanged;
            _hurtReader = players.Hurt.CreateReader();
            _localization = AppServices.Get<ILocalizationService>();
            _localization.LanguageChanged += OnLanguageChanged;
            _autoFireButton.onClick.AddListener(ToggleAutoFire);
            _deathOverlay.SetActive(false);
            LoadTexts();
        }

        void OnDestroy()
        {
            if (_localization != null) _localization.LanguageChanged -= OnLanguageChanged;
        }

        void Update()
        {
            if (_players == null || !_players.Local.IsValid) return;
            int me = _players.Local.Value;
            float dt = Time.unscaledDeltaTime;

            int health = Mathf.CeilToInt(_players.Health[me]);
            if (health != _shownHealth)
            {
                _shownHealth = health;
                _healthFill.fillAmount = _players.Health[me] / UnityEngine.Mathf.Max(1f, _players.MaxHealth[me]);
                _healthLabel.SetText("{0}", health);
            }

            bool reloading = _weapon.Reloading;
            if (_weapon.Ammo != _shownAmmo || reloading != _shownReloading)
            {
                _shownAmmo = _weapon.Ammo;
                _shownReloading = reloading;
                if (reloading) _ammoLabel.SetText(_reloadingText);
                else _ammoLabel.SetText(_ammoFormat, _weapon.Ammo, _weapon.MagazineSize);
            }
            _reloadRing.fillAmount = reloading ? _weapon.ReloadProgress : 0f;

            while (_players.Hurt.TryRead(ref _hurtReader, out PlayerHurt hurt))
                if (hurt.Player == me) _vignette = hurt.Died ? 1f : Mathf.Min(0.8f, _vignette + 0.35f + hurt.Amount / _maxHealth);
            _vignette = Mathf.Max(0f, _vignette - dt * 1.5f);
            Color c = _hurtVignette.color;
            c.a = _vignette * 0.6f;
            _hurtVignette.color = c;

            UpdateLifeOverlay(me);
            if (_wallet != null && _wallet.Coins != _shownCoins)
            {
                _shownCoins = _wallet.Coins;
                _coinLabel.SetText(_coinFormat, _shownCoins);
            }
        }

        void UpdateLifeOverlay(int me)
        {
            PlayerLife life = _players.Life[me];
            bool show = life != PlayerLife.Alive && (_outcome == null || !_outcome.Ended);
            if (_deathOverlay.activeSelf != show) _deathOverlay.SetActive(show);
            if (life != _shownLife)
            {
                _shownLife = life;
                _shownCountdown = -1;
            }
            if (!show) return;

            _reviveRing.fillAmount = life == PlayerLife.Downed ? _players.ReviveProgress[me] : 0f;
            int countdown = Mathf.CeilToInt(_players.Countdown[me]);
            bool adrenaline = life == PlayerLife.Downed && _isSolo() && _players.ReviveProgress[me] > 0f;
            int key = adrenaline ? -2 : countdown;
            if (key == _shownCountdown) return;
            _shownCountdown = key;
            if (adrenaline) _deathLabel.SetText(_adrenaline);
            else if (life == PlayerLife.Downed) _deathLabel.SetText(_downedFormat, Mathf.Max(0, countdown));
            else if (countdown > 0) _deathLabel.SetText(_deadFormat, countdown);
            else _deathLabel.SetText(_deadWaiting);
        }

        void ToggleAutoFire()
        {
            _aim.Mode = _aim.Mode == ControlMode.AutoAimAutoFire ? ControlMode.Manual : ControlMode.AutoAimAutoFire;
            _modeChanged?.Invoke(_aim.Mode);
            LoadTexts();
        }

        void OnLanguageChanged(string language) => LoadTexts();

        void LoadTexts()
        {
            _ammoFormat = _localization.Get("hud.ammo");
            _reloadingText = _localization.Get("hud.reloading");
            _downedFormat = _localization.Get("hud.downed");
            _deadFormat = _localization.Get("hud.dead");
            _deadWaiting = _localization.Get("hud.dead_waiting");
            _adrenaline = _localization.Get("hud.adrenaline");
            _coinFormat = _localization.Get("hud.coins");
            _shownCoins = -1;
            _autoFireLabel.text = _localization.Get(_aim.Mode == ControlMode.AutoAimAutoFire ? "hud.auto_fire_on" : "hud.auto_fire_off");
            _shownAmmo = -1;
            _shownCountdown = -1;
        }
    }
}
