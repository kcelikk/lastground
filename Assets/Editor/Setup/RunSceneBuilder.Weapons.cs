using LastGround.Input;
using LastGround.UI.Run;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastGround.EditorTools.Setup
{
    /// <summary>
    /// Weapon controls of the run HUD (TDD_01 §3.5, board-2/3 panel 4): weapon name under the ammo, a round grenade
    /// button with its count and a swap button in an arc above-left of the aim stick, and a "take" button for weapon
    /// pickups at the bottom centre. Swap and grenade are drawn here but hit-tested by the touch input.
    /// </summary>
    static partial class RunSceneBuilder
    {
        static WeaponHud BuildWeaponHud(RectTransform safe, GameObject host, TouchTwinStickInput input)
        {
            Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            TMP_Text weaponName = UiFactory.Label("WeaponName", safe, null, 24, FontStyles.Bold, UiFactory.Muted);
            UiFactory.Place(weaponName.rectTransform, new Vector2(0f, 1f), new Vector2(74f, -128f), new Vector2(420f, 32f));

            RectTransform grenade = UiFactory.Rect("GrenadeButton", safe);
            UiFactory.Place(grenade, new Vector2(1f, 0f), new Vector2(-420f, 70f), new Vector2(128f, 128f));
            var grenadeImage = grenade.gameObject.AddComponent<Image>();
            grenadeImage.sprite = knob;
            grenadeImage.color = new Color(0.08f, 0.09f, 0.11f, 0.8f);
            grenadeImage.raycastTarget = false;
            TMP_Text grenadeIcon = UiFactory.Label("Icon", grenade, "hud.grenade_short", 26, FontStyles.Bold, new Color(1f, 0.6f, 0.2f));
            UiFactory.Place(grenadeIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(120f, 34f));
            grenadeIcon.alignment = TextAlignmentOptions.Center;
            TMP_Text grenadeCount = UiFactory.Label("Count", grenade, null, 34, FontStyles.Bold, Color.white);
            UiFactory.Place(grenadeCount.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(120f, 40f));
            grenadeCount.alignment = TextAlignmentOptions.Center;

            RectTransform swap = UiFactory.Rect("SwapButton", safe);
            UiFactory.Place(swap, new Vector2(1f, 0f), new Vector2(-360f, 250f), new Vector2(280f, 76f));
            var swapImage = swap.gameObject.AddComponent<Image>();
            swapImage.color = new Color(0.08f, 0.09f, 0.11f, 0.8f);
            swapImage.raycastTarget = false;
            TMP_Text swapLabel = UiFactory.Label("Label", swap, null, 24, FontStyles.Bold, Color.white);
            UiFactory.Stretch(swapLabel.rectTransform);
            swapLabel.alignment = TextAlignmentOptions.Center;

            Button take = UiFactory.Button("TakeWeapon", safe, null, new Vector2(460f, 96f), out TMP_Text takeLabel, 30f);
            UiFactory.Place((RectTransform)take.transform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(460f, 96f));
            take.GetComponent<Image>().color = new Color(0.55f, 0.35f, 0.08f, 0.92f);
            takeLabel.alignment = TextAlignmentOptions.Center;
            takeLabel.rectTransform.offsetMin = Vector2.zero;
            take.gameObject.SetActive(false);

            UiFactory.Assign(input, "_switchButton", swap);
            UiFactory.Assign(input, "_grenadeButton", grenade);

            var hud = host.AddComponent<WeaponHud>();
            UiFactory.Assign(hud, "_weaponName", weaponName);
            UiFactory.Assign(hud, "_swapButton", swap.gameObject);
            UiFactory.Assign(hud, "_swapLabel", swapLabel);
            UiFactory.Assign(hud, "_grenadeButton", grenade.gameObject);
            UiFactory.Assign(hud, "_grenadeCount", grenadeCount);
            UiFactory.Assign(hud, "_takeButton", take);
            UiFactory.Assign(hud, "_takeLabel", takeLabel);
            return hud;
        }
    }
}
