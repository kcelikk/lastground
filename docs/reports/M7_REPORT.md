# M7 Raporu — Harita, Olaylar ve Çevre

- **Tarih:** 2026-09-19 · **Branch:** `m7-map-events` · **Unity:** 6000.3.24f1 · **Durum:** ✅ Onaylandı (2026-09-19), `main`e merge edildi
- **Cihazlar:** Redmi Pad Pro = MID (USB `368a7a72`) · OnePlus 5T = LOW (USB `3e415066`)

## Çıkış kriterleri (TDD_03 §36 M7)
| Kriter | Sonuç |
|---|---|
| 3 bölgeli harita, portal grafı, mini-harita | ✅ 200 × 200 m sanayi bölgesi: çevre yolu + iki ara sokak (çıkmaz sokak yok), Dökümhane Avlusu, Benzin İstasyonu, Hastane Avlusu (helipad = M8 tahliye alanı). Bölgeler arasında 3 portal, her bölgenin en az iki çıkışı var. Önceden render edilmiş radar mini-harita (zombi/takım noktaları) |
| 6 olay + görevler (D-019) | ✅ Bölgeyi temizle, Erzak düşüşü, Silah deposu, Jeneratör, Kurtarma sinyali, Elite avı; ağırlıklı seçim, art arda aynı bölgeye gitmez. İki cihazda olay paneli, yön oku ve halka senkron |
| Çevre etkileşimlileri | ✅ 10 patlayıcı varil, 3 yakıt tankı (zincirleme patlama, 120 s sonra geri gelir), 4 mermi sandığı (oyuncu başına 45 s cooldown), 2 sağlık istasyonu (şarj havuzu) |
| Anti-kamp | ✅ Takım merkezi 12 m içinde 45 s kalırsa spawn hızı ×1.5, Spitter ağırlığı ×3; hareket edince sıfırlanır |
| Cihazda bütçe | ✅ OnePlus 30.4–30.6 FPS, Redmi 59.7–60 FPS, co-op 0 exception; oyun kodu GC **0 B/kare** (bulunan iki tahsis düzeltildi, bkz. PROFILE) |
| Görsel hedef `docs/reference/maps` | 🟡 Gerçek CC0 doku/prop'lar, şeritli ıslak yollar, paslı çatılar, kirli tuğla, pompa adaları, sodyum/soğuk lamba havuzları. Referanstaki yoğunluk/detay seviyesine henüz ulaşmadı (OPEN ISSUES) |

## Uygulanan
1. **CC0 çevre içeriği (D-020):** Poly Haven'dan 28 prop (araba, bariyer, varil, vinç, çit, lamba, jeneratör, sandık…) ve ambientCG'den 17 yüzey. `Tools/Environment/fetch_cc0.py` indirir (kaynaklar git dışı), `EnvironmentImport` prop başına bütçeye indirger (1200–6000 üçgen), birim/yön düzeltir, 512 px albedo/normal (ASTC) üretir → `Assets/Art/Environment` (46 MB).
2. **Harita üreticisi:** `IndustrialMapBuilder` + `SceneryKit` (zemin, şeritli yol, bina, konteyner, decal, prop). Referansa göre malzemeler: yolda şerit çizgili ıslak asfalt, kavşak yamaları, benzinlikte ıslak çatlak asfalt + iki pompa adası (referansta kanopi yok), paslı koyu sac çatılar, kirli tuğla, dökümhanede konteyner + iki ayak üzerinde gantri vinç, hastanede helipad (üretilmiş aşınmış "H" decal'i). 38 lamba, 10 olay çapası, 19 etkileşimli; nav grid 202 × 202 bake edilir.
3. **Mini-harita:** `MinimapBaker` 1024² tepeden render (`-lgReview` ile bölge kontrol görüntüleri). `MinimapHud` sağ üstte dairesel radar; harita kayar, noktalar 96² dokuya 5 Hz'de yazılır (tahsissiz).
4. **Olaylar (`ObjectiveSystem` + `.Kinds`):**
   | Olay | Açılış | Kural | Ödül |
   |---|---|---|---|
   | Bölgeyi temizle | başlangıç | bölge içi öldürme sayacı | coin + medkit |
   | Erzak düşüşü | 1:00 | geri sayımla iner, 3 s başında dur (yokken ilerleme söner), 75 s | coin, medkit, silah, 2 granat |
   | Jeneratör | 2:00 | 8 s tut (ilerleme korunur), tutarken baskı ×1.6, 90 s | 60 s nöbetçi taret |
   | Kurtarma sinyali | 3:00 | 60 s savun, 150 s | nadir teklif, ölüleri geri getirir |
   | Silah deposu | 3:20 | önce elite bekçiyi öldür, sonra 2 s aç, 120 s | coin + silah |
   | Elite avı | 4:00 | işaretli elite'i bul (çapa onu izler), 120 s | epik teklif |
   Bekçi/avlanan zombiler "pinli": director onları uzaklaştırıp geri dönüştürmez.
5. **Etkileşimliler:** host `InteractableSystem` (vuruş talebi menzil + LOS doğrulamalı, patlama `ExplosionSystem` üzerinden, zincir `IBlastListener` ile), `LocalStations` (mermi sandığı cihazda), `InteractableViews` (patlayanı gizler). Mermi ışını varile çarparsa zombiye geçmez.
6. **Referans görsellerin oyunda kullanımı (D-020):** `ConceptArtImport` `docs/reference/maps` görsellerini küçültür → menü arka planı (sanayi bölgesi genel görünümü, karartılmış) ve **bölge kartı** (oyuncu yeni bölgeye girince konsept görsel + ad + "Tehlike seviyesi N", 3.5 s).
7. **Ağ:** `ObjectiveSync` genişledi (tür, çapa, aşama, geri sayım, taret), `InteractableSync` (47 vuruş, 48 durum). `NetProtocol.Version` 6.
8. **Teşhis:** `NetSession` dağıtımına mesaj başına profiler işaretleyicisi (`LG.Msg.<id>`), GC yakalamalarında hangi işleyicinin tahsis yaptığını gösterir.

## CREATED / CHANGED FILES
| Assembly | Dosyalar |
|---|---|
| Core | `NetMsgId` (+47, 48), `NetProtocol` v6, `NetSession` (profiler işaretleyicileri) |
| Data | `Map/` MapDefinition (lamba, çapa, portal, etkileşimli, mini-harita), MapMeshBundle, InteractableProfile, MapZoneSet (tehlike, konsept) · `Objectives/` ObjectiveKind, ObjectiveDefinition (tür alanları, ödüller) · DirectorProfile (anti-kamp), ExplosionKind (+Barrel) |
| Gameplay | `Objectives/` ObjectiveSystem(.Kinds), ObjectiveState, SentryTurret, RemoteTurretEmitter · `Interactables/` InteractableTable, InteractableSystem, LocalStations, IInteractableHitSink · `Director/` HordeDirector(.Camp, .Spawning), SpawnDeck · `Projectiles/` IBlastListener, ExplosionSystem · ZombieWorld (pin), PickupRegistry, OfferGenerator, TeamProgress, PlayerHealthSystem.Revive, WeaponController(.Slots) |
| Networking | `Replication/` InteractableSync, ObjectiveSync · `Discovery/` UdpLanDiscovery (tahsissiz yoklama) |
| Rendering | `Interactables/` InteractableViews · `Objectives/` ZoneMarker (olay halkası) · `Lighting/` LightingGrid.FromMap |
| UI | `Run/` MinimapHud, RegionCard, ObjectivePanel, ObjectiveIndicator |
| App | RunInstaller (+ .Presentation): harita, olaylar, etkileşimliler, mini-harita, bölge kartı |
| Editor | `Scenery/` EnvironmentImport, SceneryKit, IndustrialMapBuilder(.Regions), MinimapBaker, ConceptArtImport · `Setup/` ObjectiveContentBuilder, RunSceneBuilder(.Map), MenuSceneBuilder, ProjectSetup |
| Tests | MapEventTests (yeni), InteractableTests (yeni), DirectorTests (anti-kamp) |
| İçerik | `Art/Environment` (28 prop, 17 yüzey), `Art/Maps` (mesh paketi, nav grid, mini-harita, helipad), `Art/UI/Concept` (4 görsel), `ScriptableObjects/Maps` (MAP_Industrial, bölgeler, etkileşimli profili), `ScriptableObjects/Objectives` (5 yeni olay), sahneler, EN/TR key'leri |
| Araçlar | `Tools/Environment/fetch_cc0.py`, `.gitignore` (CC0 kaynakları) |

## UNITY EDITOR ACTIONS
Yok — her şey batch komutlarıyla üretildi. Harita değişirse: `MinimapBaker.BakeBatch` (grafikli, `-nographics` olmadan) → `ProjectSetup.RebuildScenesBatch`.

## INSPECTOR CONFIGURATION
Yok (sahne kurucuları atar).

## ANDROID BUILD STEPS
`BuildScripts.BuildAndroidDevelopment`, ~4 dk (tam), APK **88.5 MB** (M6: 80.7 MB; +8 MB çevre dokuları, prop'lar, konsept görseller).

## TEST RESULTS
- EditMode: **163 / 163** (M6: 154). Yeni: Erzak düşüşü (duyuru → iniş → ilerleme söner → tamam + ödül), Jeneratör (ilerleme korunur, süre dolunca başarısız, ödül yok), ardışık olaylar farklı bölgede, harita varlığı (çapalar ve oyuncu doğuşları yürünebilir ve doğru bölgede, her olay türünün ≥ 2 bölgede çapası), varil zinciri + geri gelme, uzak/düşmüş oyuncu vuruşu reddi, sağlık istasyonu havuzu, anti-kamp başlama/sıfırlanma, olay/istasyon/director tick'lerinde 0 B tahsis.
- Cihaz: iki telefon co-op (Redmi host, OnePlus client ve tersi), 0 exception; Silah deposu olayı iki cihazda aynı anda "Bekçiyi öldür".

## PROFILE
| Ölçüm | Cihaz | Sonuç |
|---|---|---|
| Co-op 5 dk (Redmi host) | Redmi / OnePlus | ort. **59.9 / 30.5 FPS**, min 54.4 / 26.3; tek takılma yükleme anında (t=5 s: 433 / 719 ms), sonrası yok. 276 öldürme, 6 patlama, level 7 |
| Co-op 2 dk (OnePlus host) | OnePlus / Redmi | 30.4 / 59.7 FPS, 0 exception |
| Bellek (HUD) | Redmi / OnePlus | 336 MB / 268 MB |
| GC yakalama (301 kare, solo host) | OnePlus | Oyun kodu **0 B/kare**. İlk ölçümde `AppRoot.Update` 32 B/kare → `UdpLanDiscovery` her karede `IPEndPoint` oluşturuyordu (host koşuda da ilan veriyor); düzeltildi |
| GC yakalama (co-op) | Redmi client | NetworkLoop 10 B/kare (M5: 27) |
| GC yakalama (co-op) | OnePlus host | NetworkLoop **~940 B/kare** — `LG.Msg` işaretleyicilerinin dışında, kcp2k `KcpServer.RawReceiveFrom` gelen her pakette endpoint ayırıyor (üçüncü taraf, bu Mirror sürümünde `KcpServerNonAlloc` boş). Oyun kodu değil; OPEN ISSUES |

## DECISIONS / DEVIATIONS
- **Bölge culling (CullingGroup) yapılmadı:** tepeden kamera + Unity'nin renderer başına frustum culling'i yeterli; OnePlus 30 FPS sabit. Harita 7 bölgeye büyüyünce ölçülüp eklenecek.
- **Lighting grid bake yerine runtime:** lamba listesi `MapDefinition`'dan, açılışta 256² doku (bir kez). Bake'e gerek görülmedi.
- **Benzinlikte kanopi kaldırıldı:** referans görselde (`maps/03`) kanopi yok; iki pompa adası + lamba havuzları.
- **VirtualHorde (M5'ten ertelenen)** bu milestone'a sığmadı → M8 (tahliye ile birlikte büyük harita akışı).
- Olay açılış süreleri ve ödüller hipotez (TDD balance kuralı); playtest gerekli.
- MSAA: kullanıcı kararıyla MEDIUM kalite seviyesi 2x; `QualityLevels.Level.Msaa` ile `ProjectSetup` artık bunu korur.

## OPEN ISSUES
- **Görsel yoğunluk:** referanstaki çöp/moloz/bitki, çatı klimaları, kırık araba, yol çatlakları ve ıslak zemin yansımaları henüz az; bina cepheleri düz kutu (pencere/kapı detayı yok). M11 sanat geçişi veya ek CC0 set gerekli.
- Oyuncu hâlâ kapsül (M6'dan); tablette üst satır can barıyla çakışıyor (M11).
- **Host kcp2k tahsisi ~0.9 KB/kare** (üçüncü taraf): Mirror/kcp2k güncellemesi veya NonAlloc sunucu — M13 performans geçişinde.
- VirtualHorde → M8. Harita 3 → 7 bölge sonraki harita milestone'unda.
