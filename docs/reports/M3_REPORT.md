# M3 Raporu — Horde Simulation

- **Tarih:** 2026-09-18 · **Branch:** `m3-horde` · **Unity:** 6000.3.24f1 · **Durum:** ⏳ Onay bekliyor
- **Cihazlar:** Redmi Pad Pro = MID (host, kablosuz ADB) · OnePlus 5T = LOW (client, USB). Aynı Wi-Fi.

## Çıkış kriterleri (TDD_03 §36 M3)
| Kriter | Sonuç |
|---|---|
| Host'ta 300 sim zombi, iki oyuncuyu çevreliyor, takılma yok | ✅ 21 dk boyunca 300 zombi, iki gezen oyuncu (`-lg-wander`); katmanlı halkalar, duvar/boşluklardan akış. Anti-stuck dürtmesi 0.4/s (300 zombide; yalnızca 14 m surround menzili dışında) |
| Host MID ≥ 45 FPS | ✅ Redmi host **ort. 60.0 / min 59.7 FPS**; sim ort. **0.41 ms**, tick max 1.81 ms |
| Client downstream ≤ 20 KB/s | ✅ 600 B/tick bütçesiyle **ort. 17.7 / maks. 18.8 KB/s** (5 dk doğrulama). 700 B ile 20 dk testte ort. 18.0 / maks. 21.2 → bütçe düşürüldü |
| 20 dk stabil | ✅ 1286 s (21.4 dk), iki cihazda **0 hata/exception**, bağlantı kopması yok, RTT ort. 32.6 ms |

## Uygulanan
- **ZombieWorld (host):** native SoA dizileri, Burst `SpatialGridBuildJob` + paralel `ZombieSteeringJob` (flow field takibi, ayrılma, oyuncuya min. 0.7 m, surround slotuna "arrive"). Sonuçlar `CrowdState`'e yazılır → mevcut replikasyon/render yolu değişmeden çalışır.
- **Navigasyon:** `NavGridBaker` (Editor, `Physics.CheckBox` örnekleme) → `Greybox_NavGrid.asset` (82×82, 1 m hücre, 1176 engelli). `FlowFieldSet`: oyuncu başına Burst BFS, yalnızca oyuncu hücre değiştirince yeniden kurulur.
- **SurroundSlotSolver — katmanlı halkalar:** 14 m içindeki zombiler en yakından başlayarak içten dışa halkalara yerleşir (iç halka 1.6 m'de 12 saldırgan, halka aralığı 0.85 m, sektör dolunca komşu sektöre, sonra dış halkaya). İlk cihaz testindeki "oyuncunun üstüne yığılma" giderildi; editör testinde ortalama komşu mesafesi 0.67 m.
- **AI LOD:** mesafeye göre adım atlama (1 / 2 / 6 tick), zombiler arasında fazı kaydırılmış.
- **Anti-stuck:** yolda ilerlemeyen zombiye yana dürtme; halkada bekleyenler muaf.
- **Greybox harita:** 80×80 m, iç duvarlar (z = ±20, boşluklu darboğazlar), konteynerler, kolonlar (`GreyboxMapBuilder`).
- **Ölüm replikasyonu:** `ZombieDeath = 33`, `NetProtocol.Version = 2`, girdi başına 7 B güvenilir batch → client'ta ceset + kan.
- **TestHordeSpawner:** 300 nüfusu korur, oyuncudan 22–45 m uzakta yürünebilir hücrede doğurur, 70 m ötesini geri dönüştürür, saniyede 1 test ölümü (M5 Horde Director'ın geçici yerine).
- **Telemetri:** `[NetStats]` satırına `simMs`, `simMaxMs`, `unstuck` eklendi.
- **Oyuncu hareketi:** `PlayerMotor` NavGrid duvarlarında kayar.

## CREATED / CHANGED FILES
| Assembly | Dosyalar |
|---|---|
| Core | `NetMsgId` (+ZombieDeath), `NetProtocol` (v2) |
| Data | `Map/NavGridAsset` |
| Gameplay | `Navigation/` NavGrid, FlowFieldJobs, FlowFieldSet · `Zombies/` ZombieWorld, ZombieSteeringJob, SurroundSlotSolver, ZombieTuning, TestHordeSpawner · CrowdReplica (`Die`), PlayerMotor · README |
| Networking | CrowdReplicationSender (ölüm batch'i), CrowdReplicationReceiver, ReplicationTuning (600 B/tick) |
| App | RunInstaller (host ZombieWorld + spawner, NavGrid), `Dev/RunTelemetry`, asmdef (Burst/Collections) |
| Editor | `Map/` GreyboxMapBuilder, NavGridBaker · `Setup/RunSceneBuilder` |
| İçerik | `Art/Maps/Greybox_NavGrid.asset`, `M1_Wall`/`M1_Prop` materyalleri, yeniden üretilmiş `Run`/`Menu` sahneleri |
| Testler | `HordeSimulationTests` (yeni), `ReplicationTests.Deaths_ReachTheClient_AsCorpses` |

## UNITY EDITOR ACTIONS
Yok. Harita ve nav grid `RebuildScenesBatch` ile üretildi.

## INSPECTOR CONFIGURATION
Yok (`RunInstaller._navGrid` sahne üreticisi tarafından atanıyor).

## ANDROID BUILD STEPS
`BuildScripts.BuildAndroidDevelopment`, artımlı ~67 s, APK 66.1 MB. Not: Unity build'i adb sunucusunu yeniden başlatıyor → kablosuz cihaz için `adb connect 192.168.1.7:<port>` tekrar gerekiyor (mDNS bu makinede cihazı bulamadı; port taramayla bulundu).

## TEST RESULTS
EditMode **79 / 79** geçti (M2'ye göre +6). `ThreeHundred_SurroundThePlayer`: 300 zombi, 750 tick, editörde 0.065 ms/tick; 238 zombi oyuncu yakınında, ≥ 6 saldırgan, komşu mesafesi > 0.55 m. PlayMode testi yok.

## PROFILE
| Ölçüm | Redmi (host, MID) | OnePlus (client, LOW) |
|---|---|---|
| FPS | ort. 60.0, min 59.7 | 30.6 (LOW 30 kilidi) |
| Frame max | ort. 18.0 ms, tepe 58.3 ms (tek örnek) | ort. 33.3, tepe 35.1 ms |
| Sim | ort. 0.41 ms, tick max 1.81 ms | — |
| Ağ | çıkış ort. 17.8 KB/s (600 B) | giriş ort. 17.7, maks. 18.8 KB/s; RTT 31–46 ms |
| Bellek | 264 MB | — |
| GC/frame | HUD tepe 1626 B/f (dev build; ~360 B/f Mirror `OnGUI`) | ölçülmedi |

Tek cihaz (OnePlus solo, 300 zombi): sim 0.45–0.53 ms, 30.6 FPS.

## DECISIONS / DEVIATIONS
| Konu | TDD | Uygulanan | Gerekçe |
|---|---|---|---|
| VirtualHorde temel | M3 kapsamında | **M5'e ertelendi** | 60 m ötesi sürüleri Director üretir (TDD_02 §G tablosunda `VirtualHordeSystem` = M5); M3'te spawner 70 m ötesini geri dönüştürüyor |
| `ZombieReplicaWorld` | Ayrı sınıf | Mevcut `CrowdReplica` genişletildi | Aynı sorumluluk; M1'den beri test edilmiş |
| AI LOD | 30/15/5 Hz tick | Adım atlama 1/2/6 (30/15/5 Hz) | Aynı oranlar, tek job içinde |
| Snapshot bütçesi | "örn. 700 B/tick" | 600 B/tick | 700 B'de downstream tepe 21.2 KB/s; hedef ≤ 20 |
| Surround | Sektör slotları | Katmanlı halkalar (iç 1.6 m, aralık 0.85 m), saldırı menzili 1.85 m | Cihazda "blob" gözlendi; halkalar kuyruk oluşturuyor |

## OPEN ISSUES
1. **GC tepe 1626 B/f** (dev build HUD maks.). Sabit değer ~360 B/f (Mirror `OnGUI`); tepenin kaynağı `-lg-gc-capture` + `GcAllocReport` ile M4 başında bulunacak (M4 kapısı 0 GC/frame).
2. **Frame tepe 58.3 ms** (Redmi, 20 dk'da tek örnek) — sürekli değil; M4 profilinde izlenecek.
3. Anti-stuck 0.4/s: darboğazlarda sıkışan yolcuların dürtülmesi. Oyun hissini M4/M5 playtest'i belirleyecek (Risk #10).
4. `ReplicationTuning` / `ZombieTuning` henüz düz sınıf; `NetworkTuningProfile` / zombi SO'larına taşınması M4–M5'te (balance SO kuralı).
5. `ZombieWorld.cs` 310 satır (hedef ≤ 300) — M4'te saldırı eklenirken `partial` ile bölünecek.
6. Hotspot testi (B5) hâlâ açık. Görsel: zombiler placeholder (D1 Asset Store bütçesi açık).
7. Sonraki: **M4 — Shooting & Combat (MVP kapısı)**.
