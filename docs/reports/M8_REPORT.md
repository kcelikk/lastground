# M8 Raporu — Boss ve Tahliye

- **Tarih:** 2026-09-19 · **Branch:** `m8-boss-extraction` · **Unity:** 6000.3.24f1 · **Durum:** ⏳ Onay bekliyor
- **Cihazlar:** Redmi Pad Pro = MID (USB `368a7a72`) · OnePlus 5T = LOW (USB `3e415066`)
- **Kararlar:** D-021 (boss = Mixamo Mutant 3.5×, helikoptersiz iniş alanı, VirtualHorde M8'de)

## Çıkış kriterleri (TDD_03 §36 M8)
| Kriter | Sonuç |
|---|---|
| Boss 2–4P'de senkron | ✅ İki telefonda boss fazı, canı, saldırı telegraph'ları ve ölümü aynı (host ve client telemetrisinde aynı HP: 755/6800). 0 exception |
| Boss 2–4P'de adil | 🟡 Mekanik olarak doğru (telegraph ≥ 0.8 s, hasar yalnızca telegraph bitince — test), can oyuncu sayısıyla ölçekleniyor (1P 4000, 2P 6800). Ama kaçmayan botlarla bir denemede boss öldü, diğerinde takım 61. saniyede düştü → insan playtest'i gerekli (OPEN ISSUES) |
| Extract / devam kararı çalışıyor | ✅ Boss ölünce iniş alanı açılıyor, 45 s tutulunca iki cihazda "TAHLİYE EDİLDİ" + sonuç ekranında tahliye bonusu ve kasaya giren; kaçırılan pencerede run sürüyor ve 7 dk sonra yenisi geliyor (test) |
| Threat ile boss tekrarı | ✅ İlk boss tehdit IV (12:00), sonra VI'dan itibaren her seviyede; her dönüşte +%35 can, −%10 bekleme, +1 çağrılan zombi (test) |
| Ödül dönüşümü | ✅ Tahliye: run coin'i + oyuncu başı 15 × tehdit; alanda olmayan düşmüş/ölü takım arkadaşı coin'in tamamı (bonus yok); takım düşerse %60 (TDD_01 §2.2) |
| Cihazda bütçe | ✅ Boss dövüşü sırasında OnePlus 30.6 FPS, Redmi 59.8–60 FPS; oyun kodu GC **0 B/kare** (boss dövüşü sırasında 301 karelik yakalama) |

## Uygulanan
1. **Mutant Brute (TDD_01 §10, D-021):** Mixamo Mutant gövdesi, kendi klip tablosu (yumruk = Ground Slam, savurma = Prop Throw, kükreme = giriş/çağırma/hücum hazırlığı, koşu = hücum); referans paftaya göre gri et + kırmızı büyümeler; 3.5× ölçek, klipler durum değişince baştan oynar (telegraph ile uyumlu).
2. **Boss kontrolcüsü (host):** ZombieWorld'de "sürülen" bir slot (steering, melee, stun, slow atlanır; hasar, isabet, replikasyon ve render sıradan kalabalık hattından). Kükreyen, hasar almayan 3 s giriş; aggro (verilen hasar + yakınlık) ve 8–12 s'de zorunlu hedef değişimi; faz 1 → faz 2 (≤ %60) → Enraged (≤ %25), geri dönmez.
   | Saldırı | Telegraph | Etki | Faz |
   |---|---|---|---|
   | Ground Slam | 1.1 s, 6 m daire | 35 hasar, sonra 12 m'ye yayılan şok halkası (15) | hepsi |
   | Charge | 1.0 s, 18 m şerit | 30 hasar; duvara çarparsa 2 s sersem; yoldaki variller patlar | 2, Enraged |
   | Prop Throw | 1.4 s, hedefte 3 m daire | enkaz iner, 25 hasar | hepsi |
   | Summon Scream | 1.2 s kükreme | 5 Runner (+ oyuncu sayısı + dönüş) | hepsi |
   | Frenzy | — | Enraged'da slam'in ardından hemen charge | Enraged |
   Zayıf nokta: sırta gelen atış ×2. Boss yaşarken director spawn hızı %40; ölünce ödül yığını (oyuncu başı 40 coin, 2 medkit, silah, 2 granat), "nefes" ve tahliye.
3. **Tahliye (TDD_01 §2.2):** `ExtractionController` pencereyi boss ölümünden 6 s sonra ve kaçırılan pencereden 7 dk sonra açar; üç bölgede de iniş alanı var (dökümhane, benzinlik, hastane helipad'i), takımın bölgesi dışından seçilir. Kimse yokken 90 s geri sayar; alanda ayakta biri varken tutma ilerler (ilerleme korunur) ve sürü baskısı artar. Harita olayları boss ve açık pencere sırasında yenisini başlatmaz.
4. **VirtualHorde (TDD_02 §17.7):** Peak'teki bazı paket/kuşatma desenleri 75–95 m ötede yürüyen grup olarak başlar (%50 daha büyük, geç gelir); 70 m'den uzakta kalan zombiler yok olmak yerine gruba katılır; grup 48 m'ye girince ekran dışında sırayla gerçek zombiye dönüşür (cap'e uyar). 1 Hz `HordeSummary` → mini-harita çevresinde 12 dilimli yön halkası.
5. **Sunum:** boss barı (ad, can, faz işaretleri, sersemken yanıp söner), telegraph'lar (dolan kırmızı daire + şok halkası, hücum şeridi, enkaz dairesi + alçak yay), parlayan tümör, kamera sarsıntısı; mavi iniş alanı (halka, 4 fişek, dönen iniş ışıkları, tutma halkası, tutulurken büyüyen rotor gölgesi); ikinci durum satırı `TAHLİYE 01:12 → HASTANE AVLUSU` / `TAHLİYE 16 / 45 sn`, tutma çubuğu, mavi kenar oku; boss geliş bannerı ("Sırtındaki tümöre ateş et"); sonuç ekranında tahliye bonusu ve kasaya giren.
6. **Ses:** prosedürel boss kükremesi, yere vurma, rotor (tutma ilerledikçe hızlanır ve yükselir).
7. **Ağ:** `BossSync` (durum ≤ 10 Hz, saldırı olayı güvenilir), `ExtractionSync`, `HordeSummarySync`, RunEnd'e ödül alanları; boss gövdesi her mesafede en yüksek snapshot hızında. `NetProtocol.Version` 7.
8. **Dev:** `-lg-boss SEC`, `-lg-extract SEC` (bot açık iniş alanına nav grid üzerinde BFS ile yol bularak gider), telemetride boss/tahliye/sanal sürü sayaçları.

## CREATED / CHANGED FILES
| Assembly | Dosyalar |
|---|---|
| Core | `NetMsgId` (+52, 81, 90, 91), `NetProtocol` v7 |
| Data | `Boss/` BossDefinition, BossAttackDefinition, BossAttackKind, BossAttackShape, ExtractionRulesDefinition · ZombieBehaviour (+Boss), CrowdTypeLook (ClipsFromStateStart), DirectorProfile (sanal sürü) |
| Gameplay | `Boss/` BossController(.Attacks), BossState, BossPhase, BossAttackStarted · `Extraction/` ExtractionController, ExtractionState, ExtractionPhase · `Director/` HordeDirector(.Virtual, dış hız çarpanı), HordeSummary, DirectorAnnouncement (+BossArrived) · `Zombies/` ZombieWorld(.Driven), IZombieDamageModifier, steering (boss atlanır) · `Combat/` HitQuery (tip başına isabet yarıçapı), WeaponController · `Run/` RunReferee, RunResult · ObjectiveSystem (Paused) |
| Networking | BossSync, ExtractionSync, HordeSummarySync, RunEndSync, CrowdReplicationSender (öncelikli tip), DirectorInfoSync |
| Rendering | `Boss/` BossView, ExtractionZoneView · ZombieRenderSystem (durum başından klip) |
| UI | BossHealthBar, ExtractionHud, MinimapHud (sürü halkası), ResultsScreen (ödül), RunStatusHud (boss bannerı) |
| Audio | BossAudio, SfxId/ProceduralSfx (+3 ses) |
| App | RunInstaller(.Boss), DevAutomation (-lg-boss, -lg-extract), DevPathSeeker, RunTelemetry |
| Editor | BossContentBuilder, ZombieContentBuilder (ZMB_Brute), CrowdBodySource/BodyTextureBaker/CrowdCatalogBuilder (boss gövdesi), RunSceneBuilder(.Boss), IndustrialMapBuilder (+2 iniş alanı çapası), QualityLevels (MSAA 2x kalıcı) |
| Tests | BossTests (6), ExtractionTests (3), DirectorTests (sanal sürü), CombatContentTests |
| İçerik | `ScriptableObjects/Boss` (boss, 4 saldırı, tahliye kuralları), `ZMB_Brute`, `Art/Crowd/Baked/boss_brute*`, sahneler, EN/TR key'leri |

## UNITY EDITOR ACTIONS
Yok.

## INSPECTOR CONFIGURATION
Yok (sahne kurucuları atar).

## ANDROID BUILD STEPS
`BuildScripts.BuildAndroidDevelopment`, ~4 dk, APK **89.4 MB** (M7: 88.5 MB; +1 MB boss gövdesi).

## TEST RESULTS
- EditMode: **173 / 173** (M7: 163). Yeni: boss tehdit IV'te ekran dışında ve ölçekli canla gelir + duyuru; girişte hasar almaz, sırt ×2; fazlar ileri gider; telegraph ≥ 0.8 s ve hasar telegraph bitmeden gelmez; summon + yenilgi + daha güçlü dönüş; boss dövüşü tick'i 0 B; tahliye penceresi boss sonrası başka bölgede açılır, kaçırılınca periyodik döner; tutma → tahliye + bonus + geride kalanın payı; takım düşerse %60; uzak zombi gruba katılır, geri yürür, ekran dışında yeniden doğar.
- Cihaz: co-op boss (Redmi host): 18 saldırı, 18 çağrılan zombi, boss öldü, iniş alanı açıldı, iki cihazda aynı durum, 0 exception. Co-op tahliye: iki bot helipad'i 45 s tuttu, iki cihazda "EXTRACTED" + bonus, 0 exception.

## PROFILE
| Ölçüm | Cihaz | Sonuç |
|---|---|---|
| Co-op boss dövüşü | Redmi (host) / OnePlus | 59.8 / ~31 FPS |
| Solo boss dövüşü + GC yakalama (301 kare) | OnePlus | 30.6 FPS; oyun kodu **0 B/kare** (yalnızca tek seferlik level-up paneli/TMP) |
| Tahliye (solo, rotor gölgesi açık) | Redmi | 60 FPS |
| Bellek (HUD) | Redmi / OnePlus | ~340 MB / ~217–270 MB |

## DECISIONS / DEVIATIONS
- **Boss = kalabalık gövdesi (D-021):** TDD_02 §21'deki SkinnedMeshRenderer + Animator yerine kemik texture hattı; tek instance, gölge atmaz. Ayrı NetworkObject yok (D-017 ile tutarlı): gövde snapshot'la, durum ve saldırılar ayrı mesajlarla gelir.
- **Prop Throw:** arenadaki gerçek araç/konteyner yerine yerden sökülen beton parçası (görsel yay); fırlatılabilir prop sistemi M11'de değerlendirilir.
- **Oyuncu itmesi (knockback) yok:** boss saldırıları hasar veriyor ama oyuncuyu itmiyor (oyuncu hareketi client'ta; host itmesi ayrı bir mesaj gerektirir).
- **Kalıcı para birimi M9'da:** dönüşüm hesaplanıp sonuç ekranında gösteriliyor; kasaya yazma (Scrap, save migrasyonu) M9 kapsamı.
- **VirtualHorde cihazda tetiklenmedi:** kısa dev koşularında tehdit I'de kaldık (sanal sürü tehdit II'den itibaren), geride zombi kalmadı; davranış EditMode testiyle doğrulandı.

## OPEN ISSUES
- **Boss dengesi:** kaçmayan botlar bir denemede boss'u öldürdü, diğerinde 61 s'de düştü. Hasar değerleri (35/15/30/25) ve bekleme süreleri insan playtest'iyle ayarlanmalı; zayıf noktaya botlar hiç isabet ettirmedi (önden ateş ediyorlar).
- Dev telemetrisi 5 s'de bir ~25 KB ayırıyor (yalnızca dev build; boss satırı ekleyince büyüdü, M5'ten bilinen konu) → M13.
- Host'ta kcp2k sunucu alımı ~0.9 KB/kare (M7'den, üçüncü taraf) → M13.
- Helikopter modeli, boss gerçek gölgesi, oyuncu modeli (kapsül) → M11.
