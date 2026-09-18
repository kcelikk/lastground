namespace LastGround.Core.Net.Session
{
    public enum DisconnectReason : byte
    {
        None = 0,
        /// <summary>The local user left.</summary>
        Left = 1,
        /// <summary>Connection to the host was lost or the host closed the session.</summary>
        HostLost = 2,
        /// <summary>The transport could not connect.</summary>
        ConnectFailed = 3,
        /// <summary>The host refused the join; see the reject reason.</summary>
        Rejected = 4,
        /// <summary>No answer to the join request in time.</summary>
        Timeout = 5,
    }
}
