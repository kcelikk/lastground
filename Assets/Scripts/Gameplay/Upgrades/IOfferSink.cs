namespace LastGround.Gameplay.Upgrades
{
    /// <summary>Where offers for a player go (host: local queue or network) and where their choice goes back.</summary>
    public interface IOfferSink
    {
        void Deliver(int player, in UpgradeOffer offer);
    }

    /// <summary>Receives a player's pick (host: TeamProgress; client: network sender).</summary>
    public interface IUpgradeChoiceSink
    {
        void Choose(int player, ushort offerId, int choice);
    }
}
