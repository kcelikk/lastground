# LastGround.Gameplay

Player, Weapons, Combat, Zombies, AI, Horde, Boss, Map, Environment, Loot, Upgrades, Events.
**Mirror, UI ve Save'e referans veremez** (TDD_02 §26.1). Bağımlılık: Core, Data, Unity.Mathematics/Collections/Burst.

| Klasör | İçerik |
|---|---|
| `Crowd/` | `CrowdState` (host SoA verisi + `Deaths`/`Hits` event kanalları), `CrowdReplica` (client interpolasyonu + anim durumu + snapshot hit bayrağı → `Hits`), `DummyCrowdSim` (M1 test sürüsü; host artık `Zombies/ZombieWorld` kullanır), `BenchmarkCrowdDriver` (M2 render benchmark yükü), `ICrowdRenderSource`, `CrowdDeath`, `CrowdHit`, `CrowdFlags` |
| `Players/` | `PlayerStateTable` (4 oyuncu, uzaklar interpolasyonlu; vitals + `Hurt` kanalı), `PlayerMotor` (yerel kinematik hareket, duvarda kayma, zombi teması yavaşlatır, nişana döner), `PlayerHealthSystem` (host: can, Alive/Downed/Dead, kaldırma, kan kaybı, dönüş, solo adrenalin, takım yok olması; upgrade'lerle maks. can / hasar azaltma / öldürmede can) |
| `Director/` | `HordeDirector` (dalgasız spawn bütçesi + gerilim döngüsü + desenler + run kişiliği), `IntensityTracker`, `SpawnLocator` (kamera ayak izi dışı), `CameraFootprint`, `PerformanceGovernor`, `RunStatus` (HUD: süre/HORDE/THREAT) |
| `Run/` | `RunOutcome`, `RunResult`, `RunReferee` (takım yok olunca run biter) |
| `Upgrades/` | `TeamProgress` (takım XP, level, teklif kuyruğu), `OfferGenerator`, `PlayerBuild`/`TeamBuilds`, `WeaponStats`, `LocalOffers`, `TeamXp`, `LevelUpPause` |
| `Loot/` | `PickupRegistry` (host: düşme, coin birleştirme, talep doğrulama, süre), `PickupTable` (sahiplik maskesi), `PickupCollector` (yerel toplama), `TeamWallet` |
| `Objectives/` | `ObjectiveSystem` ("Bölgeyi temizle": bölge içi öldürme sayacı, ödül, nefes), `ObjectiveState` |
| `Combat/` | `WeaponController` (yerel silah: atış hızı, şarjör, otomatik reload, claim), `AimResolver` (aim assist, otomatik nişan + ateş), `HitQuery` (ışın vs zombi dairesi, NavGrid duvarları), `ShotRng` (deterministik spread/crit), `HitClaim` + `HitClaimValidator` + `CombatAuthority` (host doğrulama → hasar), `DamageResolver`, `RemoteShotEmitter` (diğer oyuncuların tracer'ları, yalnızca görsel) |
| `Navigation/` | `NavGrid` (bake edilmiş yürünebilirlik ızgarası), `FlowFieldSet` (oyuncu başına BFS flow field, yalnızca hedef hücre değişince yeniden kurulur), `FlowFieldJobs` (Burst) |
| `Zombies/` | `ZombieWorld` (host sürü simülasyonu: SoA + Burst job'lar, `CrowdState`'i doldurur), `ZombieSteeringJob` (+ `SpatialGridBuildJob`: flow field, ayrılma, surround slotuna varış, AI LOD adım atlama), `SurroundSlotSolver` (içten dışa katmanlı halkalar), `ZombieTuning`, `TestHordeSpawner` (M5 Horde Director'ın geçici yerine). Partial'lar: `.Targeting` (hedef seçimi, anti-stuck), `.Combat` (can, windup'lı saldırı, knockback/stagger, respawn itmesi) |

**Değiştirirsen etkilenenler:** `CrowdState` alanları Networking replikasyonu ve Rendering tarafından okunur. `NavGrid` formatı `Data/Map/NavGridAsset` ve Editor `NavGridBaker` ile ortaktır (harita değişince yeniden bake). `ZombieTuning` halka/saldırı mesafeleri `HordeSimulationTests` ve `CombatTests` varsayımlarını etkiler. `HitClaim` alanları `HitClaimSync` wire formatıyla birlikte değişir (`NetProtocol.Version`).
