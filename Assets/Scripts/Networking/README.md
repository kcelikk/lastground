# LastGround.Networking

Ağ adapterleri ve replikasyon. **Mirror yalnızca `Mirror/` klasöründe** (D-001, TDD_02 §15.11). Bağımlılık: Core, Data, Gameplay, Platform, Mirror, kcp2k.

| Klasör | İçerik |
|---|---|
| `Mirror/` | `MirrorLinkFactory` (KcpTransport, UDP 7777), `MirrorServerLink`, `MirrorClientLink`, `LgPacket` (tek Mirror mesajı, opak payload). Framework değişimi = yalnızca bu klasör |
| `Replication/` | `CrowdReplicationSender` (enter mesajında tip + elite tek baytta; client başına tier 15/6/2 Hz, 60/65 m histerezis, priority accumulator, 600 B/tick bütçe, 7 B snapshot), `CrowdReplicationReceiver`, `PlayerSync` (30 Hz input → host hız doğrulaması → 20 Hz yayın, ateş + elde tutulan slot bayrağı), `HitClaimSync` (client → host claim batch'i, 11 B/claim), `PlayerVitalsSync` (host → client can, maks. can, yaşam durumu, geri sayım, kaldırma, yavaşlama, değişince ≤ 10 Hz), `DirectorInfoSync` (2 Hz + güvenilir duyurular: yeni tip, elite), `CombatFxSync` (projectile spawn/son, patlama; client uçuşu kapalı formdan yeniden oynatır), `LoadoutSync` (host → herkese silah/granat durumu, client → host granat atışı), `RunEndSync`, `ProgressionSync` (takım XP, teklif, seçim, build), `LootSync` (pickup'lar, talep, cüzdan), `ObjectiveSync`, `ReplicationTuning`, `NetTime` |
| `Discovery/` | `UdpLanDiscovery` (UDP 47777, 255.255.255.255 + directed broadcast, unicast cevap, 3 s zaman aşımı), `DiscoveryPacket` |

**Kurallar:** Replikasyon ve discovery Mirror tipi kullanmaz; `LoopbackNetwork` ile EditMode'da test edilir. Bağlantı id'lerinin işareti anlamsızdır (kcp2k negatif id üretebilir).
**Değiştirirsen etkilenenler:** snapshot bit düzeni → `ReplicationTests`, `NetProtocol.Version`.
