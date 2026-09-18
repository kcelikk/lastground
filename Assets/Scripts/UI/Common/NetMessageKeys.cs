using LastGround.Core.Net.Protocol;
using LastGround.Core.Net.Session;

namespace LastGround.UI.Common
{
    /// <summary>Maps session end reasons to localization keys for user-facing messages.</summary>
    public static class NetMessageKeys
    {
        public static string For(DisconnectReason reason, JoinRejectReason reject)
        {
            if (reason == DisconnectReason.Rejected)
            {
                switch (reject)
                {
                    case JoinRejectReason.VersionMismatch: return "net.reject.version";
                    case JoinRejectReason.Full: return "net.reject.full";
                    case JoinRejectReason.InProgress: return "net.reject.in_progress";
                    default: return "net.reject.refused";
                }
            }
            switch (reason)
            {
                case DisconnectReason.HostLost: return "net.disconnect.host_lost";
                case DisconnectReason.ConnectFailed: return "net.disconnect.connect_failed";
                case DisconnectReason.Timeout: return "net.disconnect.timeout";
                default: return null;
            }
        }
    }
}
