# LastGround.Data

ScriptableObject tanımları; runtime'da **salt okunur** (TDD_02 §25). Bağımlılık: Core.

| Klasör | İçerik |
|---|---|
| `Crowd/` | `CrowdAnimationSet` (bake edilmiş gövde: LOD mesh'leri + kemik matris texture'ı + klip tablosu), `CrowdVisualCatalog` (gövdeler, ortak materyal, hedef boy), `CrowdClipId`, `CrowdClip` |
| `Quality/` | `QualityPresetDefinition` (LOD mesafeleri, görünür sürü cap'i, ceset/kan cap'leri, parçacık çarpanı, tracer/hasar sayısı/ses cap'leri) |
| `Weapons/` | `WeaponDefinition` (hasar, atış hızı, şarjör, reload, menzil, spread, crit, delme, knockback, aim assist, slot, yedek mermi, kamera zoom; `NetIndex` = wire id), `WeaponFireMode`, `WeaponSlot`, `WeaponTags` |
| `Zombies/` | `ZombieDefinition` (can, hız, saldırı, kütle/yarıçap; tipe özel bölümler: Runner lunge, Spitter mesafe + projectile, Exploder sprint/fitil/patlama; `TypeIndex` = wire id), `ZombieBehaviour` |
| `Combat/` | `CombatCatalog` (tüm silah/zombi/projectile/elite, wire id ile indeksli; başlangıç loadout'u, granat, durum efekti süreleri), `ProjectileDefinition`, `ProjectileMotion`, `ExplosionSpec`, `EliteModifierDefinition` |
| `Players/` | `PlayerDefinition` (can, hız, temas yavaşlaması, ölüm/kalkma) |
| `Presentation/` | `CameraProfile` (pitch, FOV, mesafe, look-ahead, shake), `PlayerSlotColors` |
| `Director/` | `DirectorProfile`, `ThreatCurveDefinition`, `PlayerCountScalingProfile`, `SpawnDeckDefinition` + `SpawnCard` (tip maliyeti, açılma zamanı, ağırlık eğrisi, eşzamanlı cap, elite şansı) |
| `Upgrades/` | `UpgradeDefinition`, `UpgradeCatalog` (dizi indeksi = ağ id'si), `LevelCurveDefinition`, `StatId`, `UpgradeRarity` |
| `Loot/` | `LootDefinition` · `Objectives/ObjectiveDefinition` · `Map/MapZoneSet` |

Asset'ler: `Assets/Art/Crowd/Baked/` (CrowdBaker üretir), `Assets/Resources/Quality/`, `Assets/ScriptableObjects/{Weapons,Zombies,Combat,Players,Presentation}/` (`CombatContentBuilder`, `WeaponContentBuilder`, `ZombieContentBuilder` eksikleri oluşturur, mevcut değerlere dokunmaz).

**Değiştirirsen etkilenenler:** `NetIndex`/`TypeIndex` wire id'dir — sırayı değiştirme, yalnızca ekle. `StatId` sırası `PlayerBuild` dizilerini indeksler.
