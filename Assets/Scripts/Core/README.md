# LastGround.Core

Motor bağımsız temel tipler; diğer tüm assembly'ler buna bağlıdır. Başka LastGround assembly'sine **bağımlı olamaz**.

| Klasör | İçerik |
|---|---|
| `Tick/` | `TickLoop` (30 Hz sabit sim adımı + frame fazları), `TickScheduler` (tek `Update`), `TickPhase` |
| `Run/` | `RunContext`, `RunRole` (Offline/Host/Client) |
| `Net/` | Network sınırı sözleşmeleri: `ISession`, `ICommandSink`, `IGameEventStream<T>`, `INetClock`, `ILanDiscovery` — Mirror'dan bağımsız (D-001) |
| `Events/` | `EventChannel<T>` allocation'sız ring buffer, `EventReader<T>` |
| `Random/` | `DeterministicRandom` (PCG32), `Hash32` — sim'de tek izinli RNG |
| `Ids/` | `PlayerId`, `ZombieHandle`, `PickupId`, `PropId` |
| `Pooling/` | `PoolService`, `ComponentPool<T>`, `RingBuffer<T>`, `IPoolable` |
| `Services/` | `AppServices` — yalnızca App/installer/UI bağlamaları |
| `Logging/` | `Log` (Info release'te derlenmez) |

**Değiştirirsen etkilenenler:** `DeterministicRandom`/`Hash32` algoritması değişirse host–client desync olur (pinned test `DeterministicRandomTests`). `TickPhase` sırası tüm sistemlerin çalışma sırasını belirler.
