# M2 Raporu — Zombie Rendering Benchmark

- **Tarih:** 2026-09-18 · **Branch:** `m2-rendering` · **Unity:** 6000.3.24f1 · **Durum:** APPROVE bekliyor
- **Cihazlar:** OnePlus 5T (SD835 / Adreno 540, 5.7 GB, 2160×1080, Android 10) = LOW · Redmi Pad Pro (SD 7s Gen 2 / Adreno 710, 7.7 GB, 2560×1600, Android 16) = MID. HIGH cihaz yok.

## Çıkış kriterleri (TDD_03 §36 M2)
| Kriter | Sonuç |
|---|---|
| Eldeki tüm cihazlarda CSV | ✅ `docs/benchmarks/M2/` (iki cihaz × LOW/MEDIUM/HIGH) |
| MID: 250 görünürde ≥ 45 FPS | ✅ Redmi MEDIUM **60 FPS** (p99 16.7 ms), 250 hedef → 274 çizim |
| LOW: ≥ 30 FPS'yi tutan görünür cap belirlendi | ✅ OnePlus LOW 250 hedefte **61 FPS** (60 cap ile ölçüldü) → **cap 250 korunuyor**; 30 FPS kilidinde 2× pay |
| Bellek bütçe içinde | ✅ LOW 143 MB (bütçe ≤ 900), MID 257 MB (≤ 1.3 GB), HIGH 286 MB |
| Karar raporu | ✅ bu belge + D-018 |

**Önemli uyarı:** Ölçümler **placeholder** zombilerle (stilize, 1.7–2k vertex LOD0, tek atlas) ve **çevre modeli olmayan** greybox'ta yapıldı. Gerçek zombi asset'i, ortam (≈120k tris), gölge ve post-process ile sayılar düşecek; M7/M11/M13'te aynı benchmark tekrarlanacak.

## Sonuçlar (benchmark: FPS sınırı 60, adım başına 30 s)
Çizilen = görünür sürü + cesetler. Her satır 250 hedef adımı; tam tablolar CSV'lerde.

| Cihaz | Preset | Render scale | 250 hedef FPS / p99 | 300 hedef FPS | Bellek | Sıcaklık (başı→sonu, ~13 dk) |
|---|---|---|---|---|---|---|
| OnePlus 5T | LOW | 0.70 | **61.1** / 16.4 ms | 61.1 | 143 MB | 31.6 → 34.1 °C |
| OnePlus 5T | MEDIUM | 0.85 | **61.1** / 16.4 ms | 61.2 | 169 MB | 34.2 → 36.3 °C |
| OnePlus 5T | HIGH | 1.00 | 53.6 / 32.7 ms | 51.0 | 217 MB | 37.0 → 38.3 °C |
| Redmi Pad Pro | LOW | 0.70 | 60.0 / 16.7 ms | 60.0 | 229 MB | 33 °C sabit |
| Redmi Pad Pro | MEDIUM | 0.68* | **60.0** / 16.7 ms | 60.0 | 257 MB | 33 °C |
| Redmi Pad Pro | HIGH | 0.68* | **60.0** / 16.7 ms | 60.0 | 286 MB | 34 °C |
| Redmi Pad Pro | HIGH (sınırsız) | 1.00 | 53.6 / 33.3 ms | 50.3 | 371 MB | — |

\* 1080 satır render sınırı (TDD_02 §21.9): 2560×1600 tablette 0.68. Sınır olmadan HIGH 2.5K'da 250 hedefte 53.6 FPS'e düşüyordu.

## Uygulanan
- **D-018 — kemik matris texture'ı + GPU instancing:** `CrowdBaker` 4 gövdeyi tek mesh'e birleştirir, 3 LOD üretir (≈1.9k / 0.8k / 0.3–0.55k vertex), 7 klibi (Idle, Walk, Run, Attack, Hit, Crawl, Death) 30 fps kemik texture'ına bake eder (gövde başına ~250 KB). Unity 4-kemik skinning'e karşı **0.0 mm** doğrulama. `LG/CrowdInstanced` vertex shader'da skinning + frame interpolasyonu + tint + rim + lighting grid.
- **ZombieRenderSystem:** kameranın zemin dörtgeni ile culling, odak noktasına mesafe ile LOD, slot hash'inden gövde/tint/ölçek (±%8)/animasyon hızı ve fazı (her cihazda aynı görünüm, ağa veri gitmez), preset görünür cap'i (en yakınlar önce), cesetler (ölüm klibi → son kare → 0.5 m batarak kaybolma). Frame başına allocation yok.
- **Kan:** L1 paylaşımlı `ParticleSystem.Emit`, L2 instanced zemin lekeleri (ring buffer, solma; LOW'da kapalı). L3 birikim M11.
- **Lighting grid prototipi:** 256² tepeden ışık haritası (sodyum turuncusu havuzlar); sürü shader'ı ve zemin quad'ı örnekler — gerçek ışık yok.
- **QualityService + preset'ler** (`Resources/Quality/LOW|MEDIUM|HIGH`), **1080 satır render sınırı**, **DeviceTierDetector** (RAM + GPU ailesi; OnePlus → LOW, Redmi → MEDIUM doğru), `DeviceThermals` (pil °C).
- **Benchmark:** `BenchmarkCrowdDriver` (kamera karesinde oyuncuya akan sürü, yakındakiler saldırır, 2 ölüm/s) + `PerfBenchmarkRunner` (CSV). `-lg-bench [SN]`, `-lg-quality N`.
- **GC teşhisi:** `-lg-gc-capture` (cihazda call stack'li profiler kaydı) + `GcAllocReport` (editörde tahsis yeri raporu).

## GC bulgusu
Dev build'de sabit ~360–380 B/frame: **Mirror `KcpTransport.OnGUI`** (yalnızca `DEBUG`/Editor'de derlenir) her frame IMGUI başlatıyor. Oyun kodu run sırasında (300 zombi, ceset, kan) **0 B/frame**; tek istisna dev-only `RunTelemetry` (5 s'de bir log satırı). Release build'de bu `OnGUI` yok. M4 "0 B/frame" kapısı `GcAllocReport` ile ölçülecek.

## İki cihazlı co-op (M1 açık maddesi B4)
OnePlus host + Redmi client, Wi-Fi, 3 dk: client downstream ~6–7 KB/s, RTT 28–31 ms, client 60 FPS, host 30.6 FPS (LOW kilidi); client zombileri GPU skinning ile animasyonlu. **Hotspot testi (B5) hâlâ açık.**

## CREATED / CHANGED FILES
| Alan | Dosyalar |
|---|---|
| Data | `Crowd/` CrowdAnimationSet, CrowdVisualCatalog, CrowdClip, CrowdClipId · `Quality/` QualityPresetDefinition |
| Gameplay | CrowdState (Deaths kanalı, `Despawn(died)`), CrowdReplica (anim durumu), CrowdDeath, BenchmarkCrowdDriver |
| Rendering | `Crowd/` ZombieRenderSystem, GroundFootprint, CorpseBuffer, CrowdVariety · `Carnage/` BloodSystem · `Lighting/` LightingGrid · `Quality/` QualityService. Eski `CrowdRenderer` kaldırıldı |
| Platform | DeviceTierDetector, DeviceThermals |
| App | RunInstaller (render/kan/lighting/benchmark), AppRoot (QualityService), SessionService.StartBenchmark, `Dev/` PerfBenchmarkRunner, GcProfileCapture; `QualityDefaults` kaldırıldı |
| Editor | `Crowd/` CrowdBaker, CrowdBodySource, CrowdCatalogBuilder · `Setup/` QualityPresetBuilder, RunSceneBuilder (catalog, kan materyalleri, ışık havuzu), QualityLevels (4-kemik skin) · `Tools/` AssetInspector, GcAllocReport |
| Shader | LG_CrowdInstanced, LG_GroundDecal, LG_FX_AlphaBlend, LG_LightPoolGround |
| İçerik | Quaternius Zombie Kit (CC0) → `Art/Crowd/Baked/*` (4 gövde), `CrowdCatalog`, `M_Crowd`; `Resources/Quality/*`; UnityMeshSimplifier (MIT, UPM git) |
| Testler | 73 EditMode (M1'e göre +7): tier tespiti, ceset tamponu, benchmark sürücüsü, footprint, çeşitlilik |

## UNITY EDITOR ACTIONS
Yok. Gövdeler `CrowdBaker.BakeAllBatch`, sahneler `RebuildScenesBatch` ile üretildi.

## ANDROID BUILD STEPS
APK 63.0 MB; artımlı build ~1 dk. Redmi'ye kurulum kablosuz ADB ile (HyperOS "Install via USB" + onay; "Remember my choice" işaretliyse onaysız).

## DECISIONS / DEVIATIONS
| Konu | TDD | Uygulanan | Gerekçe |
|---|---|---|---|
| Animasyon (D-018) | Vertex-VAT (varsayılan) | Kemik matris texture'ı | Bellek (~1 MB vs 24–48 MB), LOD'lar ortak |
| Zombi asset'i | Asset Store (D1) | Quaternius CC0 placeholder | Bütçe kararı yok; pipeline asset'ten bağımsız, `CrowdBodySource` tablosu değişir |
| Mixamo | Animasyon kaynağı | Kullanılmadı | Asset kendi klipleriyle geliyor; Mixamo giriş gerektiriyor |
| Benchmark FPS | Preset hedefi | Ölçümde 60 sınırı | LOW'un 30 kilidi kapasiteyi gizliyordu |
| Kan L3, ışık bake | M2'de "basit" | L3 yok; lighting grid runtime | M11 kapsamı |
| Culling | Burst job | C# döngü | 300 varlıkta ölçülebilir maliyet yok; Burst M3 sim ile |

## OPEN ISSUES
1. **Sayılar iyimser** (placeholder, ortam yok). Görünür cap'ler (250/250/300) şimdilik değişmedi; ortam ve nihai zombi sonrası yeniden ölçülecek.
2. **Uzun termal test** yapılmadı (en uzun 13 dk, +3–4 °C). 30 dk soak M13'te; OnePlus LOW'un 2× payı iyi işaret.
3. Client tarafında ceset/kan yok — ölüm replikasyonu (`ZombieDeathBatch`) M3'te.
4. HIGH preset'i Redmi'de de 60 FPS; HIGH sınıf cihaz olmadığı için HIGH hedefi doğrulanmadı (B2).
5. Hotspot testi (B5).
6. Sonraki: **M3 — Horde Simulation** (Burst SoA ZombieWorld, flow field, surround, AI LOD, greybox harita, ölüm replikasyonu).
