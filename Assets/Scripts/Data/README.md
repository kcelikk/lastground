# LastGround.Data

ScriptableObject tanımları; runtime'da **salt okunur** (TDD_02 §25). Bağımlılık: Core.

| Klasör | İçerik |
|---|---|
| `Crowd/` | `CrowdAnimationSet` (bake edilmiş gövde: LOD mesh'leri + kemik matris texture'ı + klip tablosu), `CrowdVisualCatalog` (gövdeler, ortak materyal, hedef boy), `CrowdClipId`, `CrowdClip` |
| `Quality/` | `QualityPresetDefinition` (LOD mesafeleri, görünür sürü cap'i, ceset/kan cap'leri, parçacık çarpanı) |

Asset'ler: `Assets/Art/Crowd/Baked/` (CrowdBaker üretir), `Assets/ScriptableObjects/Quality/`.
