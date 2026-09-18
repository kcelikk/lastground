# M5 Raporu — Endless Loop Core

- **Tarih:** 2026-09-18 · **Branch:** `m5-loop` · **Unity:** 6000.3.24f1 · **Durum:** ✅ Onaylandı (2026-09-18), `main`e merge edildi
- **Cihazlar:** Redmi Pad Pro = MID (host, kablosuz ADB 192.168.1.14) · OnePlus 5T = LOW (client, USB) · Linux PC (yalnızca ağ karşı tarafı)

## Çıkış kriterleri (TDD_03 §36 M5)
| Kriter | Sonuç |
|---|---|
| 3 run'ın yoğunluk grafikleri farklı | ✅ Cihazda 3 run (`docs/benchmarks/M5/director_runs.svg`): tepe (Peak) zamanları farklı — co-op 83/173/351 s, OnePlus solo 45/164/281 s, Redmi solo 58/160/287 s; spawn miktarı farklı (815 / 786). EditMode: 3 seed'in tepeleri ≥ 10 s kayık, spawn hacmi ≥ %5 farklı. Kaynak: tohumdan "run kişiliği" (tempo ±%15, BuildUp uzunluğu ±%30, tepe eşiği ±0.08, desen eğilimi) + rastgele süreler |
| Level-up co-op'ta oyunu durdurmuyor | ✅ PC host + OnePlus: host hiç seçmedi (15 teklif birikti), run 15 dk kesintisiz sürdü. Kural testle sabit (`LevelUpPause`): co-op asla, solo yalnızca panel açıkken + ayar açıkken durur. Cihazda solo durma ve seçimle devam doğrulandı |
| XP/coin tüm client'larda eşit | ✅ Loopback testleri birebir eşitlik (XP, level, build stat'ları, cüzdan). İki telefon 15 dk: 189 örnekte takım XP farkı −3…+7 (ort. 0.5, ~5000 XP içinde), coin farkı −1…+2 (ort. 0.17) — 5 s'lik log satırlarının iki cihazda farklı anda alınmasından; kayma yok |

## Uygulanan
1. **HordeDirector (dalgasız, D-003):** spawn puanı = zaman eğrisi × Threat × oyuncu sayısı × gerilim durumu × host governor. Calm → BuildUp → Peak → PeakHold → Relax döngüsü takım yoğunluğuna (alınan hasar, yakındaki zombi, düşük can) göre; desenler Trickle/Pack/Pincer/Surround; büyük desen için puan biriktirilir. `SpawnLocator`: 22–45 m, yürünebilir, ulaşılabilir, **hiçbir oyuncunun kamera ayak izinde değil** (sabit kameradan hesap). 70 m ötesi zombiler silinir. `PerformanceGovernor` host kare süresine göre bütçeyi 0.5'e kadar düşürür.
2. **Threat + HUD:** `SURVIVAL 08:42 · HORDE: HIGH · THREAT III` (renkli seviye, 2 s histerezis), Threat artışında banner. Tahsis yapmayan `CharLine` tamponu. `DirectorInfo` (2 Hz). Global zombi sayacı yok.
3. **Downed / revive (§14.2):** Alive → Downed (25 s kan kaybı, %30 hızla sürünme, ateş yok) → Dead. Yanında 2 m'de 4 s duran takım arkadaşı kaldırır (%30 can, 2 s dokunulmazlık); kaldıran hasar alırsa yarı hız, bırakılırsa ilerleme yavaşça söner; aynı hayatta 3. düşüşte kan kaybı 2 kat hızlı. Ölü oyuncu takım ayaktayken 60 s sonra %50 canla döner. Solo: run başına 1 "Adrenalin". Zombiler yerdeki oyuncuyu daha az hedefler. Takım yok olunca run biter → **sonuç ekranı** (süre, en yüksek tehdit, öldürme, kaldırma, coin) → ana menü. HUD: yaşam katmanı + kaldırma halkası, takım listesi (isim, can, durum), ekran dışı takım okları.
4. **Takım XP + level-up + 12 upgrade (§7):** tüm öldürmeler tek XP havuzuna; herkes birlikte level atlar, her oyuncu kendi RNG akışıyla 3 kartlık teklif alır (Common 60 / Rare 28 / Epic 10 / Legendary 2, yığın sınırı, tekrarsız). Ağda yalnızca upgrade id + rarity gider; stat'ları her cihaz hesaplar (`WeaponStats`: hasar, atış hızı, şarjör, reload, crit, delme; can, hız, hasar azaltma, toplama alanı, öldürmede can). Host doğrulaması atıcının kendi stat'larını kullanır. Panel alt ortada, küçültülebilir ("SEVİYE +N"), solo'da duraklatır. XP barı + LV.
5. **Takım coin + instanced pickup (§13):** öldürme coin/medkit düşürür; aynı 2 m hücredeki coin'ler 0.5 s içinde tek yığına birleşir. Coin'e kim dokunursa **takım cüzdanı** artar; medkit **oyuncu başına kopya** (`ownerMask`), yalnız alanı iyileştirir, tam canken alınmaz. İstemci yaklaşınca anında gizler, host mesafe/sahiplik doğrular. Instanced çizim, PARA sayacı.
6. **"Bölgeyi temizle" görevi (§12.4, D-019):** gri kutu haritada 4 bölge. Görev bir bölge seçer; yalnızca **bölge içindeki** takım öldürmeleri sayılır (`Zombileri öldür: 12/30`, hedef oyuncu sayısı ve tehditle ölçeklenir). Bitince director nefes verir (Relax) + bölgeye ödül (coin yığını + medkit), 25 s sonra başka bölge. Görev paneli, ekran dışı ok, zeminde parlayan bölge çerçevesi. Sayaç görev kapsamlı; director sayaçtan bağımsız spawn eder.
7. **Dev:** `-lg-autopick`, botun yerdeki arkadaşa yürümesi, director CSV kaydı, telemetri (director, XP, coin, düşme/kaldırma, dokunarak/otomatik seçim sayısı), GC yakalama gecikmesi.

## CREATED / CHANGED FILES
| Assembly | Dosyalar |
|---|---|
| Core | `NetMsgId` (+13 RunEnd, 50 DirectorInfo, 60–63 ilerleme, 70–73 loot, 80 görev), `NetProtocol` v4 |
| Data | `Director/` DirectorProfile, ThreatCurveDefinition, PlayerCountScalingProfile · `Upgrades/` StatId, UpgradeRarity, UpgradeDefinition, UpgradeCatalog, LevelCurveDefinition · `Loot/LootDefinition` · `Objectives/ObjectiveDefinition` · `Map/MapZoneSet` · `Presentation/PlayerSlotColors` · PlayerDefinition (düşme/kaldırma), ZombieDefinition (XP, coin) |
| Gameplay | `Director/` HordeDirector (+`.Spawning`), IntensityTracker, SpawnLocator, CameraFootprint, PerformanceGovernor, RunStatus, DirectorState, HordeLevel, HordePattern · `Run/` RunOutcome, RunResult, RunReferee · `Players/` PlayerLife, PlayerHealthSystem (+`.Revive`), PlayerStateTable (yaşam, geri sayım, kaldırma, maks. can) · `Upgrades/` PlayerBuild, TeamBuilds, WeaponStats, UpgradeOffer, OfferGenerator, TeamProgress, TeamXp, LocalOffers, IOfferSink, LevelUpPause · `Loot/` PickupTable, PickupRegistry, PickupCollector, TeamWallet, PickupType · `Objectives/` ObjectiveSystem, ObjectiveState · Combat/Zombies/Motor: upgrade stat'ları, yerdeki oyuncu önceliği |
| Networking | DirectorInfoSync, RunEndSync, ProgressionSync, LootSync, ObjectiveSync, PlayerVitalsSync (yaşam durumu, maks. can), PlayerSync (hız upgrade'i) |
| Rendering | `Loot/PickupRenderSystem`, `Objectives/ZoneMarker`, PlayerViews (yerde/ölü görünüm) |
| UI | `Common/CharLine` · `Run/` RunStatusHud, TeamPanel, TeammateIndicators, ResultsScreen, LevelUpPanel, XpBar, ObjectivePanel, ObjectiveIndicator, CombatHud (yaşam katmanı, coin) |
| Input | TouchTwinStickInput (UI engelleyici alan, dev arkadaşa yürüme) |
| App | RunInstaller (+ director, referee, ilerleme, loot, görev, solo duraklatma), `Dev/` DirectorLog, RunTelemetry, DevAutomation |
| Editor | `Setup/` UpgradeContentBuilder, CombatContentBuilder (director, loot, bölgeler, görev), RunSceneBuilder (+HUD parçaları) |
| İçerik | `Assets/ScriptableObjects/{Director,Upgrades,Loot,Maps,Objectives}`, `M_PickupCoin/Medkit`, `Art/UI/TeammateArrow.png`, EN/TR key'leri (HUD, upgrade'ler, görev, bölge, sonuç) |
| Testler | `DirectorTests` (5), `LifeTests` (7), `ProgressionTests` (7), `LootTests` (5), `ObjectiveTests` (4), `CombatNetworkTests` +1 (RunEnd) |

## UNITY EDITOR ACTIONS
Yok. Tüm asset'ler ve sahne `RebuildScenesBatch` ile üretildi.

## INSPECTOR CONFIGURATION
Yok. Denge değerleri `Assets/ScriptableObjects/*` altında düzenlenebilir (builder mevcut değerlere dokunmaz). Not: M4'ten kalan `PLR_Base.asset` `RespawnDelay` elle 5 → 60 yapıldı.

## ANDROID BUILD STEPS
`BuildScripts.BuildAndroidDevelopment`, artımlı ~1–3 dk, APK ~66 MB. Linux oyuncusu PC host testleri için (`BuildLinuxDevelopment`).

## TEST RESULTS
EditMode **129 / 129** (M4'e göre +28). Öne çıkanlar: 6 dakikalık başsız director simülasyonu (tüm gerilim durumları, **ekranda doğan zombi 0**), 3 seed'in farklı tepe zamanları, solo adrenalin ve takım yok olması, 2 m'de 4 s kaldırma, ateş altında yavaş kaldırma, kan kaybı → ölüm → dönüş, XP/build'lerin host ve client'ta birebir aynı olması, coin yığını birleşme, instanced medkit kopyaları, görevde yalnız bölge içi öldürmeler, tüm yeni mesajların loopback replikasyonu.

## PROFILE
| Test | Cihaz | FPS | En kötü kare | Not |
|---|---|---|---|---|
| **İki telefon 15.4 dk (Redmi host, OnePlus client)** | Redmi (MID host) | **59.98** ort., min 59.2 | 116.7 ms (tek) | 0 hata, 4794 öldürme, level 18, 1191 coin, Threat V, 300 zombiye kadar |
| 〃 | OnePlus (LOW client) | **30.6** sabit | 196 ms (tek) | 0 hata |
| Co-op kaldırma testi 10 dk (Redmi + OnePlus) | Redmi / OnePlus | 60.0 / 30.6 | — | 4 düşme, **3 kaldırma**, 0 ölüm, 1723 öldürme |
| PC host + OnePlus 15 dk (host seçim yapmadı) | OnePlus | 30.6 | — | level 15, 513 coin, 5 düşme, 3 kaldırma, oyun durmadı |
| Solo 5 dk, sabit duran oyuncu | OnePlus (LOW) | 30.6 | 34 ms | 602 öldürme, adrenalin → ikinci düşüş → sonuç ekranı |
| GC yakalama (solo savaş, 300 kare) | OnePlus | | | **oyun kodu 0 B/kare**; Mirror IMGUI 365 B/f (dev) + dev telemetrisi (5 s'de bir ~10–14 KB, HUD'daki "GC max" tepelerinin kaynağı) |

## DECISIONS / DEVIATIONS
| Konu | TDD | Uygulanan | Gerekçe |
|---|---|---|---|
| VirtualHorde / `HordeGroupSummary` | M5 (TDD_02 §G) | **M7'ye ertelendi** | Bölge/portal grafı ve mini-map ile anlamlı; şimdilik 22–45 m spawn + 70 m temizlik |
| Elite / anti-kamp (§9.8) | Director parçası | Yok | Elite modifier'lar M6 kapsamında; anti-kamp bölge verisi ister (M7) |
| Kaldırma hedef önceliği | "%50 daha az" | Mesafe² × 2 (≈ 1.4 kat uzak sayılır) | Basit ve kararlı; testle doğrulandı |
| Ölüden dönüş | Rescue Signal veya 60 s | 60 s (takım ayaktayken), olduğu yerde + itme | Rescue Signal event'i M7 |
| Ölü oyuncu | Spectate | Kamera kendi bedeninde kalır | Takım arkadaşları arası geçiş M10 |
| Upgrade zaman aşımı | Ayar: kapalı/5/10 s | Yok (varsayılan "kapalı") | Ayarlar ekranı M9 |
| Upgrade etkileri | Stat + silah modifier + yetenek + on-hit | 12 tek-stat upgrade (delme ve öldürmede can dahil) | MVP 12 upgrade hedefi; efekt çeşitliliği M6 |
| Pickup toplama | "Oyuncuya uçma" animasyonu | Anında gizleme (1.5 s içinde onay gelmezse geri görünür) | Görsel cila M11 |
| Sonuç ekranı | Scrap'e çeviri | Yalnızca istatistik | Meta ilerleme M9 |
| Görev bölgeleri | `MapEventDefinition` içinde | `MapZoneSet` (gri kutu haritada 4 dikdörtgen) | Gerçek bölgeler M7 haritasıyla |

## OPEN ISSUES
1. **Denge:** botlarla HORDE etiketi çoğunlukla LOW (co-op'ta 921 s'nin 671'i); yoğunluk erken oyunda düşük. İnsan oyuncuyla playtest gerekiyor; eşikler `DIR_Default.asset`'te.
2. **Redmi host "picks=17":** host otomatik seçimle başlatılmamıştı ama her level'da seçim yapıldı; kod yolunda otomatik seçim yok — tablete dokunulmuş olabilir. Dokunarak/otomatik seçim sayaçları eklendi (sonraki solo testlerde dokunma 0, otomatik 8–9 — tutarlı).
3. Level-up paneli, HUD ve sonuç ekranı placeholder görünümde (UI art M11). Alt ortadaki dev istatistikleri paneli kısmen örtüyor (yalnızca dev build).
4. Dev telemetrisi 5 s'de bir ~10–14 KB ayırıyor (release'te yok) — HUD'daki GC tepelerini kirletiyor; M13'te tamponlu hale getirilebilir.
5. Önceki milestone'lardan: Mirror/kcp2k client ~27 B/f, hotspot testi (B5), zombi placeholder (D1).
6. Redmi'nin kablosuz hata ayıklaması ekran kilitlenince kapanıyor; testlerde "Uyanık kal" + şarj önerilir.
7. Sonraki: **M6 — Combat Content I** (6 silah, grenade/projectile, Runner/Tank/Spitter/Exploder, elite modifier'lar, durum efektleri).
