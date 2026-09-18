namespace LastGround.Core.Net.Link
{
    /// <summary>Creates transport links. Swapping networking frameworks means providing another factory (D-001).</summary>
    public interface INetLinkFactory
    {
        IServerLink CreateServer();
        IClientLink CreateClient();
    }
}
