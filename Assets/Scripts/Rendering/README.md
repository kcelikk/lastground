# LastGround.Rendering

Çizim sistemleri; Gameplay'i yalnızca okur. Bağımlılık: Core, Data, Gameplay, URP.

| Klasör | İçerik |
|---|---|
| `Crowd/` | `ZombieRenderSystem` (kemik texture skinning + instancing, zemin footprint culling, mesafe LOD'u, görünür cap, cesetler), `GroundFootprint`, `CorpseBuffer`, `CrowdVariety` (slot hash → gövde/tint/ölçek/faz) |
| `Carnage/` | `BloodSystem` — L1 paylaşımlı parçacık `Emit`, L2 instanced zemin lekesi (L3 birikim M11) |
| `Lighting/` | `LightingGrid` — tepeden ışık haritası (sürü shader'ı + `LG/LightPoolGround` örnekler). M2: runtime; M7/M11: editörde bake |
| `Quality/` | `QualityService` — kalite seviyesi + FPS hedefi + 1080 satır render sınırı; preset'ler `Resources/Quality` |
| (kök) | `PlayerViews`, `FollowCamera` (M4'te `TopDownCameraRig`) |

Shader'lar `Assets/Art/Shaders`: `LG/CrowdInstanced`, `LG/GroundDecal`, `LG/FX_AlphaBlend`, `LG/LightPoolGround`.
**Değiştirirsen etkilenenler:** `LG/CrowdInstanced` instance verisi (`_Anim`) ve kemik texture düzeni `CrowdBaker` (Editor) ile birlikte değişir.
