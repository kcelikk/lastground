# LastGround.Editor

Yalnızca Editor. Menü: **LastGround/…**

- `Setup/ProjectSetup` — idempotent proje ayarı: Player (IL2CPP, ARM64, API 26, Vulkan+GLES3, ASTC, Input System), URP LOW/MEDIUM/HIGH, kalite seviyeleri, sahneler. Ayar değişikliği bu dosyada yapılır.
- `Setup/SceneBuilder` — Boot/Menu sahnelerini üretir; **mevcut sahnenin üzerine yazmaz** (yeniden üretmek için sahneyi sil).
- `Build/BuildScripts` — `BuildAndroidDevelopment` → `Builds/Android/LastGround-dev.apk`.
- `Localization/LocalizationValidator` — eksik/fazla key, boş değer, `{0}` uyuşmazlığı.

- `Scenery/EnvironmentImport` — CC0 kaynaklarını (`Tools/Environment/fetch_cc0.py`, git dışı) mobil bütçeli prop/malzemeye çevirir → `Assets/Art/Environment`.
- `Scenery/IndustrialMapBuilder` (+ `.Regions`) — 200 m sanayi haritası: yollar, 3 bölge, lambalar, çapalar, etkileşimliler, nav grid, `MAP_Industrial`. `SceneryKit` yapı taşları.
- `Scenery/MinimapBaker` — mini-harita render'ı (grafik gerekir, `-nographics` olmadan); `-lgReview <klasör>` bölge görüntüleri.
- `Scenery/ConceptArtImport` — `docs/reference/maps` görsellerini menü arka planı ve bölge kartlarına küçültür → `Assets/Art/UI/Concept`.
- `Setup/MetaContentBuilder` — meta kataloğu (`Resources/Meta/META_Catalog`) ve öğeleri (`ScriptableObjects/Meta`). Oyuncu gövdeleri `Crowd/CrowdBodySource` içinde (`player_*`); çok malzemeli gövdeler `MaterialTiles` ile atlas karolarına dağıtılır.
- `Setup/BossContentBuilder` — `BOSS_MutantBrute`, 4 saldırı (`BAT_*`), `EXT_Rules`; boss gövdesi `ZMB_Brute` (`ZombieContentBuilder`, TypeIndex 5).
- `Setup/ObjectiveContentBuilder` — `OBJ_*` olay tanımları ve `MAP_Interactables`.

**Değiştirirsen etkilenenler:** harita düzeni değişince çapaların yürünebilir kalması `MapEventTests` ile doğrulanır; `MinimapBaker` ve `RebuildScenesBatch` yeniden çalıştırılmalı.

Komutlar: kök `AGENTS.md` §5.
