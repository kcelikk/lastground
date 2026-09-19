# LastGround.Rendering

Çizim sistemleri; Gameplay'i yalnızca okur. Bağımlılık: Core, Data, Gameplay, URP.

| Klasör | İçerik |
|---|---|
| `Crowd/` | `ZombieRenderSystem` (kemik texture skinning + instancing, zemin footprint culling, mesafe LOD'u, görünür cap, cesetler), `GroundFootprint`, `CorpseBuffer`, `CrowdVariety` (slot hash → gövde/tint/ölçek/faz) |
| `Carnage/` | `BloodSystem` — L1 paylaşımlı parçacık `Emit` (ölüm + isabet), L2 instanced zemin lekesi (L3 birikim M11) |
| `Combat/` | `TracerSystem` — tracer + namlu alevi, instanced additive quad'lar, ring buffer (preset `TracerCap`) |
| `Lighting/` | `LightingGrid` — tepeden ışık haritası (sürü shader'ı + `LG/LightPoolGround` örnekler). M7: lambalar `MapDefinition.Lamps`'ten (`FromMap`) |
| `Interactables/` | `InteractableViews` — patlayan varil/tankı gizler, geri gelince gösterir (sahnedeki `Interactable_<id>` nesneleri) |
| `Objectives/` | `ZoneMarker` — bölge kenarı (Bölgeyi temizle) veya çapalı olay halkası (instanced additive) |
| `Quality/` | `QualityService` — kalite seviyesi + FPS hedefi + 1080 satır render sınırı; preset'ler `Resources/Quality` |
| (kök) | `PlayerViews` (ölü yatar, dokunulmaz yanıp söner), `TopDownCameraRig` (takip, nişan/hareket look-ahead, trauma shake) |

Shader'lar `Assets/Art/Shaders`: `LG/CrowdInstanced` (hit flash `_Anim.w` kesirinde), `LG/GroundDecal`, `LG/FX_AlphaBlend`, `LG/FX_Additive`, `LG/LightPoolGround`.
**Değiştirirsen etkilenenler:** `LG/CrowdInstanced` instance verisi (`_Anim`) ve kemik texture düzeni `CrowdBaker` (Editor) ile birlikte değişir.
