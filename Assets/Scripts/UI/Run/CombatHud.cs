using System;
using LastGround.Core.Events;
using LastGround.Core.Input;
using LastGround.Core.Services;
using LastGround.Gameplay.Combat;
using LastGround.Gameplay.Players;
using LastGround.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.UI.Run
{
    /// <summary>
    /// Combat HUD (TDD_01 §0.9 panel 2, M4 subset): health bar top-left, ammo and reload ring near the aim stick,
    /// red hurt vignette, "down — back in N" overlay, and the auto-fire toggle. Numbers use allocation-free
    /// TMP SetText; localized formats are read once.
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
        [SerializeField] Button _autoFireButton;
        [SerializeField] TMP_Text _autoFireLabel;

        PlayerStateTable _players;
        IWeaponStatus _weapon;
        AimResolver _aim;
        Action<ControlMode> _modeChanged;
        ILocalizationService _localization;
        EventReader<PlayerHurt> _hurtReader;
        float _maxHealth;
        float _respawnDelay;
        float _deadFor;
        float _vignette;
        string _ammoFormat;
        string _reloadingText;
        string _deathFormat;
        int _shownAmmo = -1;
        bool _shownReloading;
        int _shownHealth = -1;
        int _shownCountdown = -1;

        public void Bind(PlayerStateTable players, IWeaponStatus weapon, AimResolver aim, float maxHealth, float respawnDelay,
            Action<ControlMode> modeChanged)
        {
            _players = players;
            _weapon = weapon;
            _aim = aim;
            _maxHealth = Mathf.Max(1f, maxHealth);
            _respawnDelay = respawnDelay;
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
                _healthFill.fillAmount = _players.Health[me] / _maxHealth;
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

            UpdateDeathOverlay(_players.Dead[me], dt);
        }

        void UpdateDeathOverlay(bool dead, float dt)
        {
            if (_deathOverlay.activeSelf != dead)
            {
                _deathOverlay.SetActive(dead);
                _deadFor = 0f;
                _shownCountdown = -1;
            }
            if (!dead) return;
            _deadFor += dt;
            int countdown = Mathf.Max(1, Mathf.CeilToInt(_respawnDelay - _deadFor));
            if (countdown == _shownCountdown) return;
            _shownCountdown = countdown;
            _deathLabel.SetText(_deathFormat, countdown);
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
            _deathFormat = _localization.Get("hud.down");
            _autoFireLabel.text = _localization.Get(_aim.Mode == ControlMode.AutoAimAutoFire ? "hud.auto_fire_on" : "hud.auto_fire_off");
            _shownAmmo = -1;
            _shownCountdown = -1;
        }
    }
}
