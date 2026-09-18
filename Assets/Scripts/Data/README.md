# LastGround.Data

ScriptableObject tanımları; runtime'da **salt okunur** (TDD_02 §25). Bağımlılık: Core.

| Klasör | İçerik |
|---|---|
| `Crowd/` | `CrowdAnimationSet` (bake edilmiş gövde: LOD mesh'leri + kemik matris texture'ı + klip tablosu), `CrowdVisualCatalog` (gövdeler, ortak materyal, hedef boy), `CrowdClipId`, `CrowdClip` |
| `Quality/` | `QualityPresetDefinition` (LOD mesafeleri, görünür sürü cap'i, ceset/kan cap'leri, parçacık çarpanı, tracer/hasar sayısı/ses cap'leri) |
| `Weapons/` | `WeaponDefinition` (hasar, atış hızı, şarjör, reload, menzil, spread, crit, delme, knockback, aim assist; `NetIndex` = wire id), `WeaponFireMode` |
| `Zombies/` | `ZombieDefinition` (can, hız aralığı, saldırı hasarı/windup/cooldown, knockback çarpanı) |
| `Players/` | `PlayerDefinition` (can, hız, temas yavaşlaması, ölüm/kalkma) |
| `Presentation/` | `CameraProfile` (pitch, FOV, mesafe, look-ahead, shake) |

Asset'ler: `Assets/Art/Crowd/Baked/` (CrowdBaker üretir), `Assets/Resources/Quality/`, `Assets/ScriptableObjects/{Weapons,Zombies,Players,Presentation}/` (`CombatContentBuilder` eksikleri oluşturur, mevcut değerlere dokunmaz).
