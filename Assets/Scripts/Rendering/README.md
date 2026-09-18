# LastGround.Rendering

Çizim sistemleri; Gameplay'i yalnızca okur. Bağımlılık: Core, Data, Gameplay, URP.

- `CrowdRenderer` — `ICrowdRenderSource` → `Graphics.RenderMeshInstanced` (1023'lük batch). M1: kapsül; M2: VAT + LOD + footprint culling + tint (TDD_02 §21.3).
- `PlayerViews` — 4 oyuncu slotu için GameObject, slot rengi.
- `FollowCamera` — sabit 55°/0° yaw, FOV 35, 22 m. M4'te `TopDownCameraRig`.
