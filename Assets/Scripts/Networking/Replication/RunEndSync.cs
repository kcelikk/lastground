using System;
using LastGround.Core.Ids;
using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;
using LastGround.Core.Net.Wire;
using LastGround.Core.Tick;
using LastGround.Gameplay.Run;

namespace LastGround.Networking.Replication
{
    /// <summary>
    /// Run over, host → clients (TDD_02 §15.5 RunResults, reliable, once): survival time, max threat, kills,
    /// revives, coins, extracted. Clients end their run with the host's result.
    /// </summary>
    public sealed class RunEndSync : ITickable, IDisposable
    {
        readonly ISession _session;
        readonly RunOutcome _outcome;
        readonly NetRawHandler _onEnd;
        bool _sent;

        public RunEndSync(ISession session, RunOutcome outcome)
        {
            _session = session;
            _outcome = outcome;
            _onEnd = OnEnd;
            if (!session.IsAuthority) session.Subscribe(NetMsgId.RunEnd, _onEnd);
        }

        /// <summary>Host: NetSend phase.</summary>
        public void Tick(float dt, uint tick)
        {
            if (!_session.IsAuthority || _sent || !_outcome.Ended) return;
            _sent = true;
            RunResult result = _outcome.Result;
            NetWriter w = _session.Begin(NetMsgId.RunEnd);
            w.WriteVarUInt((uint)(result.SurvivalSeconds * 10f + 0.5f));
            w.WriteByte((byte)Math.Min(255, result.MaxThreat));
            w.WriteVarUInt((uint)result.Kills);
            w.WriteVarUInt((uint)result.Revives);
            w.WriteVarUInt((uint)result.Coins);
            w.WriteBool(result.Extracted);
            _session.SendToClients(NetChannel.Reliable);
        }

        public void Dispose()
        {
            _session.Unsubscribe(NetMsgId.RunEnd, _onEnd);
        }

        void OnEnd(PlayerId sender, ref NetReader r)
        {
            var result = new RunResult
            {
                SurvivalSeconds = r.ReadVarUInt() / 10f,
                MaxThreat = r.ReadByte(),
                Kills = (int)r.ReadVarUInt(),
                Revives = (int)r.ReadVarUInt(),
                Coins = (int)r.ReadVarUInt(),
                Extracted = r.ReadBool(),
            };
            if (!r.Failed) _outcome.End(result);
        }
    }
}
