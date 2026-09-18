using System.Globalization;
using System.IO;
using System.Text;
using LastGround.Core.Logging;
using LastGround.Gameplay.Director;
using UnityEngine;

namespace LastGround.App.Dev
{
    /// <summary>
    /// Development builds (host): one director sample per second into a preallocated buffer, written as CSV to
    /// persistentDataPath/director_&lt;seed&gt;.csv when the run ends — the M5 check that runs breathe differently
    /// (TDD_03 §36 M5: "3 run'ın yoğunluk grafikleri farklı").
    /// </summary>
    public sealed class DirectorLog : MonoBehaviour
    {
        const int Capacity = 7200;

        struct Sample
        {
            public float Time;
            public float Intensity;
            public byte State;
            public byte Horde;
            public byte Threat;
            public short Alive;
            public short MaxAlive;
            public float Rate;
        }

        readonly Sample[] _samples = new Sample[Capacity];
        RunStatus _status;
        HordeDirector _director;
        uint _seed;
        int _count;
        float _next;
        bool _written;

        public void Bind(RunStatus status, HordeDirector director, uint seed)
        {
            _status = status;
            _director = director;
            _seed = seed;
        }

        void Update()
        {
            if (_status == null || _count >= Capacity || _status.RunSeconds < _next) return;
            _next = _status.RunSeconds + 1f;
            _samples[_count++] = new Sample
            {
                Time = _status.RunSeconds, Intensity = _status.Intensity, State = (byte)_status.State, Horde = (byte)_status.Horde,
                Threat = (byte)_status.Threat, Alive = (short)_status.Alive, MaxAlive = (short)_status.MaxAlive, Rate = _status.SpawnRate,
            };
        }

        void OnApplicationQuit() => Write();
        void OnDestroy() => Write();

        void Write()
        {
            if (_written || _count == 0) return;
            _written = true;
            var csv = new StringBuilder(_count * 40);
            csv.AppendLine("time,intensity,state,horde,threat,alive,maxAlive,rate");
            for (int i = 0; i < _count; i++)
            {
                Sample s = _samples[i];
                csv.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:0.0},{1:0.000},{2},{3},{4},{5},{6},{7:0.00}",
                    s.Time, s.Intensity, (DirectorState)s.State, (HordeLevel)s.Horde, s.Threat, s.Alive, s.MaxAlive, s.Rate));
            }
            string path = Path.Combine(Application.persistentDataPath, "director_" + _seed.ToString(CultureInfo.InvariantCulture) + ".csv");
            File.WriteAllText(path, csv.ToString());
            Log.Info(LogCategory.App, "[DirectorLog] wrote " + path + " (" + _count + " samples, spawned " + _director.Spawned + ")");
        }
    }
}
