# LastGround.Core

Motor bağımsız temel tipler; diğer tüm assembly'ler buna bağlıdır. Başka LastGround assembly'sine **bağımlı olamaz**.

| Klasör | İçerik |
|---|---|
| `Tick/` | `TickLoop` (30 Hz sabit sim adımı + frame fazları), `TickScheduler` (tek `Update`), `TickPhase`, `TickAction` |
| `Run/` | `RunContext`, `RunRole` (Offline/Host/Client), `RunLaunch` |
| `Net/Wire` | `NetWriter` / `NetReader` (bit-pack, varint, hata güvenli okuma), `Quantize` (7.8 mm konum, 6/8 bit yaw) |
| `Net/Protocol` | `NetMsgId` (sabit numaralar), `NetProtocol` (sürüm, portlar), düşük frekanslı mesaj struct'ları |
| `Net/Link` | `IServerLink` / `IClientLink` / `INetLinkFactory` (ham bayt taşıma), `LoopbackNetwork` (testler; gecikme/kayıp/negatif id simülasyonu) |
| `Net/Session` | `ISession`, `NetSession` (handshake, roster, ping/pong saat, istatistik, host-yerel dağıtım), `ISessionService` sözleşmesi |
| `Net/` | `INetClock`, `INetStats`, `ILanDiscovery`, `DiscoveredHost`, `IGameEventStream<T>` — hepsi Mirror'dan bağımsız (D-001) |
| `Input/` | `PlayerInputFrame`, `IPlayerInputSource` — Input ↔ Gameplay sözleşmesi |
| `Events/` | `EventChannel<T>` allocation'sız ring buffer, `EventReader<T>` |
| `Random/` | `DeterministicRandom` (PCG32), `Hash32` — sim'de tek izinli RNG |
| `Ids/` | `PlayerId`, `ZombieHandle`, `PickupId`, `PropId` |
| `Pooling/` | `PoolService`, `ComponentPool<T>`, `RingBuffer<T>`, `IPoolable` |
| `Services/` | `AppServices` — yalnızca App/installer/UI bağlamaları |
| `Logging/` | `Log` (Info release'te derlenmez) |

**Değiştirirsen etkilenenler:** `NetMsgId` numaraları ve mesaj düzenleri wire protokolüdür — değişirse `NetProtocol.Version` artırılır, eski sürümler join'de reddedilir. `DeterministicRandom`/`Hash32` algoritması değişirse host–client desync olur (pinned test `DeterministicRandomTests`). `TickPhase` sırası tüm sistemlerin çalışma sırasını belirler.
