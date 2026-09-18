using LastGround.Core.Events;

namespace LastGround.Gameplay.Upgrades
{
    /// <summary>
    /// The local player's pending upgrade offers for the level-up panel (TDD_01 §7.5): a small queue; choosing sends
    /// the pick to the host (or straight to TeamProgress on the host). The panel can be minimised; offers wait.
    /// </summary>
    public sealed class LocalOffers : IOfferSink
    {
        const int Capacity = 8;

        readonly UpgradeOffer[] _queue = new UpgradeOffer[Capacity];
        readonly int _localPlayer;
        int _head;
        int _count;

        public LocalOffers(int localPlayer)
        {
            _localPlayer = localPlayer;
        }

        /// <summary>Where the pick goes: TeamProgress on the host, the network on clients.</summary>
        public IUpgradeChoiceSink Choices { get; set; }

        public int Count => _count;
        public bool HasOffer => _count > 0;
        public UpgradeOffer Current => _queue[_head];

        /// <summary>A new offer arrived (panel pop-up, sound).</summary>
        public readonly EventChannel<UpgradeOffer> Arrived = new EventChannel<UpgradeOffer>(8);

        public void Deliver(int player, in UpgradeOffer offer)
        {
            if (player != _localPlayer || _count >= Capacity) return;
            _queue[(_head + _count) % Capacity] = offer;
            _count++;
            Arrived.Publish(offer);
        }

        public void Choose(int choice)
        {
            if (_count == 0) return;
            UpgradeOffer offer = _queue[_head];
            _head = (_head + 1) % Capacity;
            _count--;
            Choices?.Choose(_localPlayer, offer.Id, choice);
        }
    }
}
