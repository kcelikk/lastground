# LastGround.Gameplay

Player, Weapons, Combat, Zombies, AI, Horde, Boss, Map, Environment, Loot, Upgrades, Events.
**Mirror, UI ve Save'e referans veremez** (TDD_02 §26.1). Bağımlılık: Core, Data, Unity.Mathematics/Collections/Burst.

| Klasör | İçerik |
|---|---|
| `Crowd/` | `CrowdState` (host SoA verisi + `Deaths` event kanalı), `CrowdReplica` (client interpolasyonu + anim durumu), `DummyCrowdSim` (M1 test sürüsü; host artık `Zombies/ZombieWorld` kullanır), `BenchmarkCrowdDriver` (M2 render benchmark yükü), `ICrowdRenderSource`, `CrowdDeath` |
| `Players/` | `PlayerStateTable` (4 oyuncu, uzaklar interpolasyonlu), `PlayerMotor` (yerel kinematik hareket, `NavGrid` duvarlarında kayar) |
| `Navigation/` | `NavGrid` (bake edilmiş yürünebilirlik ızgarası), `FlowFieldSet` (oyuncu başına BFS flow field, yalnızca hedef hücre değişince yeniden kurulur), `FlowFieldJobs` (Burst) |
| `Zombies/` | `ZombieWorld` (host sürü simülasyonu: SoA + Burst job'lar, `CrowdState`'i doldurur), `ZombieSteeringJob` (+ `SpatialGridBuildJob`: flow field, ayrılma, surround slotuna varış, AI LOD adım atlama), `SurroundSlotSolver` (içten dışa katmanlı halkalar), `ZombieTuning`, `TestHordeSpawner` (M5 Horde Director'ın geçici yerine) |

**Değiştirirsen etkilenenler:** `CrowdState` alanları Networking replikasyonu ve Rendering tarafından okunur. `NavGrid` formatı `Data/Map/NavGridAsset` ve Editor `NavGridBaker` ile ortaktır (harita değişince yeniden bake). `ZombieTuning` halka/saldırı mesafeleri `HordeSimulationTests` varsayımlarını etkiler.
