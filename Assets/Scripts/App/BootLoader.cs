using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastGround.App
{
    /// <summary>Boot scene entry: services are already up (AppRoot), so continue to the menu.</summary>
    public sealed class BootLoader : MonoBehaviour
    {
        void Start()
        {
            SceneManager.LoadScene(SceneNames.Menu);
        }
    }
}
