namespace LastGround.Platform.Net
{
    /// <summary>An active IPv4 interface with its directed broadcast address.</summary>
    public readonly struct LocalInterface
    {
        public readonly string Name;
        public readonly string Address;
        public readonly string Broadcast;

        public LocalInterface(string name, string address, string broadcast)
        {
            Name = name;
            Address = address;
            Broadcast = broadcast;
        }
    }
}
