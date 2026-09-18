# LastGround.Networking

Ağ adapterleri ve replikasyon. **Mirror yalnızca `Mirror/` klasöründe** (D-001, TDD_02 §15.11). Bağımlılık: Core, Data, Gameplay, Platform, Mirror, kcp2k.

| Klasör | İçerik |
|---|---|
| `Mirror/` | `MirrorLinkFactory` (KcpTransport, UDP 7777), `MirrorServerLink`, `MirrorClientLink`, `LgPacket` (tek Mirror mesajı, opak payload). Framework değişimi = yalnızca bu klasör |
| `Replication/` | `CrowdReplicationSender` (client başına tier 15/6/2 Hz, 60/65 m histerezis, priority accumulator, 700 B/tick bütçe, 7 B snapshot), `CrowdReplicationReceiver`, `PlayerSync` (30 Hz input → host hız doğrulaması → 20 Hz yayın), `ReplicationTuning`, `NetTime` |
| `Discovery/` | `UdpLanDiscovery` (UDP 47777, 255.255.255.255 + directed broadcast, unicast cevap, 3 s zaman aşımı), `DiscoveryPacket` |

**Kurallar:** Replikasyon ve discovery Mirror tipi kullanmaz; `LoopbackNetwork` ile EditMode'da test edilir. Bağlantı id'lerinin işareti anlamsızdır (kcp2k negatif id üretebilir).
**Değiştirirsen etkilenenler:** snapshot bit düzeni → `ReplicationTests`, `NetProtocol.Version`.
