# LastGround.Gameplay

Player, Weapons, Combat, Zombies, AI, Horde, Boss, Map, Environment, Loot, Upgrades, Events.
**Mirror, UI ve Save'e referans veremez** (TDD_02 §26.1). Bağımlılık: Core, Data, Unity.Mathematics/Collections/Burst.

| Klasör | İçerik |
|---|---|
| `Crowd/` | `CrowdState` (host SoA verisi + `Deaths` event kanalı), `CrowdReplica` (client interpolasyonu + anim durumu), `DummyCrowdSim` (M1 test sürüsü — M3'te ZombieWorld ile değişir), `BenchmarkCrowdDriver` (M2 render benchmark yükü), `ICrowdRenderSource`, `CrowdDeath` |
| `Players/` | `PlayerStateTable` (4 oyuncu, uzaklar interpolasyonlu), `PlayerMotor` (yerel kinematik hareket) |

**Değiştirirsen etkilenenler:** `CrowdState` alanları Networking replikasyonu ve Rendering tarafından okunur.
