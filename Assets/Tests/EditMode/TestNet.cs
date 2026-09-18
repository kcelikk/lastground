using System.Collections.Generic;
using LastGround.Core.Net.Link;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;

namespace LastGround.Tests
{
    /// <summary>Loopback harness: one host plus N clients advanced in fixed time steps.</summary>
    sealed class TestNet
    {
        public readonly LoopbackNetwork Network = new LoopbackNetwork();
        public readonly List<NetSession> Sessions = new List<NetSession>();
        public double Now;

        public NetSession CreateSession(string name, uint contentHash = 42, int maxPlayers = NetProtocol.MaxPlayers)
        {
            var session = new NetSession(Network, new SessionConfig
            {
                ContentHash = contentHash,
                PlayerName = name,
                SessionName = name + "'s game",
                MaxPlayers = maxPlayers,
            });
            session.Tick(Now);
            Sessions.Add(session);
            return session;
        }

        public void Step(double seconds, double dt = 1.0 / 30.0, System.Action<float> perTick = null)
        {
            double end = Now + seconds;
            while (Now < end - 1e-9)
            {
                Now += dt;
                Network.Pump(Now);
                for (int i = 0; i < Sessions.Count; i++) Sessions[i].Tick(Now);
                perTick?.Invoke((float)dt);
            }
        }
    }
}
