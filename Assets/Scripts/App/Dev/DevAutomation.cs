using System;
using System.Collections.Generic;
using System.Globalization;
using LastGround.Core.Logging;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastGround.App.Dev
{
    /// <summary>
    /// Development-only automation for LAN tests without touching the UI.
    /// Desktop: command-line arguments. Android: <c>adb shell am start … -e lgargs "-lg-join 192.168.1.10"</c>.
    ///   -lg-host              host a game from the menu
    ///   -lg-start-at N        host: start the run when N players are in the lobby (default 2)
    ///   -lg-join ADDRESS      join a host by IP
    ///   -lg-solo              start a solo run
    ///   -lg-quit-after SEC    quit after SEC seconds (soak tests)
    ///   -lg-wander            local player wanders in circles (soak tests)
    /// </summary>
    public sealed class DevAutomation : MonoBehaviour
    {
        SessionService _service;
        bool _host;
        bool _solo;
        string _joinAddress;
        int _startAt = 2;
        float _quitAfter = -1f;
        bool _acted;
        bool _runRequested;

        public static void Install(GameObject root, SessionService service)
        {
            List<string> args = ReadArguments();
            if (args.Count == 0) return;
            var automation = root.AddComponent<DevAutomation>();
            automation._service = service;
            automation.Parse(args);
            Log.Info(LogCategory.App, "Dev automation: " + string.Join(" | ", args) + " (start-at " + automation._startAt + ")");
        }

        void Parse(List<string> args)
        {
            for (int i = 0; i < args.Count; i++)
            {
                switch (args[i])
                {
                    case "-lg-host": _host = true; break;
                    case "-lg-solo": _solo = true; break;
                    case "-lg-wander": LastGround.Input.TouchMoveInput.DevWander = true; break;
                    case "-lg-join" when i + 1 < args.Count: _joinAddress = args[++i]; break;
                    case "-lg-start-at" when i + 1 < args.Count:
                        if (int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int startAt))
                            _startAt = startAt;
                        break;
                    case "-lg-quit-after" when i + 1 < args.Count:
                        if (float.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out float quitAfter))
                            _quitAfter = quitAfter;
                        break;
                }
            }
        }

        void Update()
        {
            if (_quitAfter > 0f && Time.realtimeSinceStartup > _quitAfter)
            {
                Log.Info(LogCategory.App, "Dev automation: quit-after reached");
                _quitAfter = -1f;
                Application.Quit();
                return;
            }

            if (!_acted && SceneManager.GetActiveScene().name == SceneNames.Menu)
            {
                _acted = true;
                if (_solo) _service.StartSolo();
                else if (_host) _service.HostGame();
                else if (!string.IsNullOrEmpty(_joinAddress)) _service.Join(_joinAddress, NetProtocol.DefaultGamePort);
            }

            if (_host && !_runRequested && _service.Session.State == SessionState.Hosting &&
                _service.Session.Players.Count >= _startAt)
            {
                _runRequested = true;
                _service.StartRun();
            }
        }

        static List<string> ReadArguments()
        {
            var result = new List<string>();
            string[] commandLine = Environment.GetCommandLineArgs();
            for (int i = 1; i < commandLine.Length; i++)
            {
                if (commandLine[i].StartsWith("-lg-", StringComparison.Ordinal) || (i > 1 && commandLine[i - 1].StartsWith("-lg-", StringComparison.Ordinal)))
                    result.Add(commandLine[i]);
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject intent = activity.Call<AndroidJavaObject>("getIntent"))
                {
                    string extra = intent.Call<string>("getStringExtra", "lgargs");
                    if (!string.IsNullOrEmpty(extra))
                        result.AddRange(extra.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                }
            }
            catch (AndroidJavaException e)
            {
                Debug.LogWarning("[App] Could not read lgargs: " + e.Message);
            }
#endif
            return result;
        }
    }
}
