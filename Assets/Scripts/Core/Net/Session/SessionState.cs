namespace LastGround.Core.Net.Session
{
    public enum SessionState : byte
    {
        Idle = 0,
        /// <summary>Offline or host: session is running locally.</summary>
        Hosting = 1,
        /// <summary>Client: transport connecting or waiting for JoinAccepted.</summary>
        Connecting = 2,
        /// <summary>Client: accepted by the host.</summary>
        Connected = 3,
    }
}
