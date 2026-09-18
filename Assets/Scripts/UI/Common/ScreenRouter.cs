using UnityEngine;

namespace LastGround.UI.Common
{
    /// <summary>Shows one menu panel at a time (TDD_02 §28 ScreenRouter).</summary>
    public sealed class ScreenRouter : MonoBehaviour
    {
        [SerializeField] GameObject[] _screens;

        public void Show(GameObject screen)
        {
            for (int i = 0; i < _screens.Length; i++)
            {
                if (_screens[i] != null) _screens[i].SetActive(_screens[i] == screen);
            }
        }
    }
}
