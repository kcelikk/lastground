# M4 Raporu — Shooting & Combat (MVP KAPISI)

- **Tarih:** 2026-09-18 · **Branch:** `m4-combat` · **Unity:** 6000.3.24f1 · **Durum:** 🚧 Taslak — iki telefonlu 20 dk kapı testi Redmi şarj olunca yapılacak
- **Cihazlar:** OnePlus 5T = LOW (USB) · Redmi Pad Pro = MID (kablosuz ADB) · Linux PC (yalnızca ağ karşı tarafı, performans referansı değil)

## Kapı kriterleri (TDD_03 §36 M4)
| Kriter | Sonuç |
|---|---|
| 2 telefon, 250+ zombi, 20 dk savaş | ⏳ **Bekliyor (Redmi şarjda).** Ön testler: Redmi host + OnePlus client 3 dk temiz; PC host + OnePlus client **21.5 dk, 0 hata** |
| Crash / desync yok | ✅ şimdiye kadar: iki cihazda 0 exception; client 300 zombiyi 16–19 KB/s ile izliyor; claim'lerin 0'ı "hedeften uzak" (pozisyon uyumsuzluğu yok) |
| LOW ≥ 30, MID 45–60 FPS | ✅ OnePlus (LOW) 30.6 ort.; Redmi (MID host) 60.0 |
| 0 GC/frame | ✅ **oyun kodu 0 B**: EditMode testi (host sim + savaş + replikasyon + client silahı, 90 tick) ve cihazda 300 kare call-stack kaydı. Kalan: Mirror `KcpTransport.OnGUI` ~365 B/f (yalnızca dev build) ve Mirror/kcp2k client döngüsünde ~27 B/f (üçüncü taraf, aşağıda) |
| Yeni oyuncu kontrolleri 1 dk'da kullanabiliyor | ⏳ Kullanıcı testi bekleniyor. Dokunmatik nişan/ateş cihazda `adb input swipe` ile doğrulandı |

## Uygulanan
- **Kontroller:** `TouchTwinStickInput` — sol yarı yüzen hareket stick'i, sağ yarı yüzen nişan stick'i (0.25 nişan, 0.55 ateş, 0.15 s flick tutma; ateşte düğme kırmızı), üst %25 butonlara ayrılmış. Editör/masaüstü: WASD + IJKL / fare.
- **AimResolver:** manuel modda yumuşak aim assist (silah başına ±10° koni, ayar: kapalı/düşük/yüksek); **otomatik nişan + ateş** modu (8 Hz hedef seçimi, görüş hattı kontrolü, sağ stick ile geçersiz kılınabilir). HUD'daki "OTOMATİK ATEŞ" düğmesi ayarı kaydeder.
- **Silah (`WeaponDefinition`, AR):** 16 hasar, 8 atış/s, 30 şarjör, 1.8 s reload (boşalınca / 1 s ateşsiz), 30 m, ±1.5° spread, %8 ×2 crit, 1 delme, knockback. `ShotRng(runSeed, oyuncu, atışNo)` → spread ve crit client ile host'ta birebir aynı.
- **İsabet:** `HitQuery` — ışın vs zombi dairesi (0.5 m), NavGrid DDA duvarları; delme sırası en yakından. Silah, öldürdüğünü öngördüğü zombiyi 0.6 s "ölü varsayar": mermiler arkadakine geçer, otomatik nişan onu atlar.
- **Host doğrulaması (`HitClaimValidator` + `CombatAuthority`):** atıcı canlı mı, zombi aynı nesil ve canlı mı, menzil (+2 m), gördüğü konum host'takine 2.5 m içinde mi, görüş hattı, atış hızı (token bucket, ×1.25), atış başına claim sayısı. Host'un kendi silahı da aynı yoldan geçer.
- **Zombi savaşı:** can (walker 45), **windup'lı saldırı** (0.45 s telegraph; uzaklaşan oyuncu kaçar), hasar 5 / 1.6 s, knockback + kısa sendeleme (stagger) — sendeleyen zombi dümen kullanmaz, sürtünmeyle yavaşlar.
- **Oyuncu canı (`PlayerHealthSystem`):** 100 HP; 0'da 5 s yerde, sonra **olduğu yerde** kalkar + 3 s dokunulmazlık + 5 m içindeki zombileri iten dalga. Zombiler ölü oyuncuyu hedeflemez. Zombi teması hızı düşürür (−%7/zombi, en fazla −%35).
- **Ağ:** `PlayerInput`/`PlayerStates`'e ateş bayrağı (diğer cihazlar tracer çizer); `HitClaimBatch` (40, güvenilir, 11 B/claim); `PlayerVitals` (41, değişince ≤ 10 Hz, client'ta `Hurt` event'i). Başkalarının isabeti için ayrı mesaj yok: snapshot'taki **hit bayrağı** (0 ek bant) → flash + kan. `NetProtocol.Version = 3`.
- **Sunum:** `TopDownCameraRig` (nişan/hareket look-ahead, trauma shake), tracer + namlu alevi (instanced additive, tek draw), zombi hit flash (shader, ek veri yok), isabette kan, hasar sayıları (TMP havuzu, crit altın), HP barı, mermi + reload halkası, hasar vinyeti, "yere düştün — N" sayacı, ölü oyuncu yatar / dokunulmaz yanıp söner.
- **Ses (temel):** `SfxPlayer` (sabit AudioSource havuzu, ses başına aralık + eşzamanlı ses sınırı, yerel oyuncuya göre mesafe/pan), `CombatAudio`; klipler run başında **sentezlenen placeholder'lar** (atış, isabet, ölüm, hasar).
- **Dev:** `-lg-autofire`, `-lg-gc-capture [SN]`, telemetriye savaş sayaçları (atış, claim, kabul/red nedenleri, öldürme, oyuncu ölümü, zombi saldırısı), `GcAllocReport` çağrı yığını denemesi.

## CREATED / CHANGED FILES
| Assembly | Dosyalar |
|---|---|
| Core | `Input/ControlMode`, `NetMsgId` (+40, 41), `NetProtocol` (v3) |
| Data | `Weapons/` WeaponDefinition, WeaponFireMode · `Zombies/ZombieDefinition` · `Players/PlayerDefinition` · `Presentation/CameraProfile` · QualityPresetDefinition (+TracerCap, DamageNumberCap, AudioVoices) |
| Gameplay | `Combat/` ShotRng, HitClaim, IHitClaimSink, HitQuery, DamageResolver, WeaponController, IWeaponStatus, AimResolver, HitClaimVerdict, HitClaimValidator, CombatAuthority, ShotFired, RemoteShotEmitter · `Players/` PlayerHealthSystem, PlayerHurt, PlayerRespawn, IPlayerDamageSink, PlayerMotor, PlayerStateTable · `Zombies/` ZombieWorld (+`.Combat`, `.Targeting` partial'ları), ZombieSteeringJob (stagger, hedefsiz zombi), ZombieTuning · `Crowd/` CrowdHit, CrowdFlags, CrowdState/CrowdReplica (hit kanalları) · `Navigation/NavGrid` (Raycast, HasLineOfSight) |
| Networking | `Replication/` HitClaimSync, PlayerVitalsSync, PlayerSync (ateş bayrağı), CrowdReplicationReceiver (bayraklar) |
| Rendering | `Combat/TracerSystem`, `TopDownCameraRig` (FollowCamera kaldırıldı), ZombieRenderSystem (hit flash), BloodSystem (isabet), PlayerViews |
| Audio | SfxId, ProceduralSfx, SfxPlayer, CombatAudio |
| Input | `TouchTwinStickInput` (TouchMoveInput'un yerine) |
| UI | `Run/CombatHud`, `Run/DamageNumbers`, PerfHud (alt ortaya taşındı) |
| App | RunInstaller (+`.Presentation` partial), DevAutomation, RunTelemetry, GcProfileCapture |
| Editor | `Setup/CombatContentBuilder`, RunSceneBuilder (+`.Hud`), QualityPresetBuilder, `Tools/GcAllocReport` |
| İçerik | `Assets/ScriptableObjects/{Weapons,Zombies,Players,Presentation}`, `LG_FX_Additive` shader, `M_Tracer`, `Art/UI/{White,HurtVignette}.png`, Run sahnesi, EN/TR `hud.*` + `weapon.assault_rifle` key'leri |
| Testler | `CombatTests` (17), `CombatNetworkTests` (4) |

## UNITY EDITOR ACTIONS
Yok. Sahne ve asset'ler `RebuildScenesBatch` ile üretildi.

## INSPECTOR CONFIGURATION
Yok. Balance değerleri `Assets/ScriptableObjects/*` asset'lerinde; Inspector'dan ayarlanabilir (`CombatContentBuilder` mevcut değerlere dokunmaz).

## ANDROID BUILD STEPS
`BuildScripts.BuildAndroidDevelopment`, artımlı 1–3 dk, APK 66.1 MB. Unity build'i adb sunucusunu yeniden başlatır → Redmi için `adb connect 192.168.1.7:36807`.

## TEST RESULTS
EditMode **100 / 100** (M3'e göre +21). Öne çıkanlar: loopback'te client ateş eder → host doğrular → 5 zombi ölür → client 5 ceset görür (15/15 claim kabul, 0 red); savaş döngüsü 90 tick'te **0 B** ayırır; hile benzeri atış seli (40 atış bir anda) ≤ 6 kabul; duvar arkası, eski nesil, uzak konum reddedilir; windup'tan kaçılır; kalkma zombileri iter. PlayMode testi yok.

## PROFILE
| Test | Cihaz | FPS | Frame max | Sim | Ağ | Not |
|---|---|---|---|---|---|---|
| Solo, 300 zombi, otomatik ateş, 90 s | OnePlus (LOW) | 30.6 | 33 ms | 0.3–0.5 ms | — | 161 öldürme, GC tepe 368 B/f (Mirror OnGUI) |
| Redmi host + OnePlus client, 3 dk | Redmi (MID) | 60.0 | 16.7 ms | 0.34–0.37 ms | çıkış 17.7 KB/s | 878 öldürme |
| 〃 | OnePlus (LOW) | 30.6 | 32.7 ms | — | giriş 18.4 KB/s, RTT 32 ms | |
| PC host + OnePlus client, **21.5 dk** | OnePlus (LOW) | 30.6 ort. (min 29.5) | 196 ms (tek), ort. 35 | — | giriş 16.6 ort. / 18.6 maks KB/s | 0 hata, 6383 öldürme, 120 oyuncu ölümü |
| GC call-stack kaydı, 300 kare savaş | OnePlus client | | | | | oyun kodu 0 B; Mirror IMGUI 365.6 + NetworkLoop 27.0 + telemetri |
| **İki telefon 20 dk kapı testi** | Redmi + OnePlus | ⏳ | | | | |

Bellek: OnePlus 145–148 MB, Redmi 264 MB.

## DECISIONS / DEVIATIONS
| Konu | TDD | Uygulanan | Gerekçe |
|---|---|---|---|
| Ölüm | "Alive/Dead + restart" | 5 s sonra **olduğu yerde** kalkma + dokunulmazlık + itme | Run'ı bozmadan uzun soak; M5'te downed/revive ile değişir |
| Görüş hattı | Physics raycast (Environment layer) | NavGrid DDA | Host'ta fizik sahnesi gerekmez, deterministik, client ile aynı sonuç; alçak engeller de mermiyi keser (M7'de gözden geçirilecek) |
| HitQuery | ZombieSpatialGrid | Tüm slotlarda kaba kuvvet | 512 slot × ≤ 40 atış/s = µs'ler; ölçülebilir fark yok |
| Başkalarının isabeti | `ZombieHitFxBatch` 10 Hz | Snapshot'taki hit bayrağı | 0 ek bant; efekt snapshot hızına bağlı (yakında 15 Hz) |
| Kontrol modları | 3 mod | Manuel + otomatik nişan/ateş | "Otomatik nişan, manuel ateş" M9 ayarlar ekranıyla |
| Hasar sayıları | Instanced rakam atlası | Dünya uzayında TMP havuzu (12/24/32) | Basit ve 0 GC (SetText); gerekirse M13'te atlas |
| Ses | Kayıtlı SFX | Sentezlenmiş placeholder'lar | Asset bütçesi/kaynağı henüz yok (D1); `SfxId` tablosu korunur |
| Yedek mermi | Silah başına | Sınırsız (yalnızca şarjör) | Mermi ekonomisi M6 |
| Haptik | Hasar/ölümde titreşim | Yok | Android `Vibrate` çok uzun; M11/M13'te kısa haptik API |
| Walker hasarı | — | 8 → **5**, cooldown 1.6 s | Director olmadan 300 zombi tek oyuncuya kilitleniyor; yoğunluk M5'te |

## OPEN ISSUES
1. **İki telefonlu 20 dk kapı testi** (Redmi şarjda) + kullanıcı kontrol testi ("1 dk'da öğreniliyor mu").
2. **Reddedilen claim'ler:** PC testinde %33, bunların **%95'i `TargetGone`** (zaten ölmüş zombiye atış, zararsız). Düzeltme sonrası build'de silah öldürdüğünü öngördüğü zombiyi atlıyor; iki telefon testinde oran ölçülecek. Diğerleri: görüş hattı 298, hız 75, ölü atıcı 75, uzak konum **0**.
3. **Mirror/kcp2k client ~27 B/frame** (üçüncü taraf; oyun kodu değil). IL2CPP yığınları cihaz dışında çözülmüyor; M13'te kcp2k client soket okuma yolunda incelenecek.
4. Tek seferlik GC tepeleri (client HUD'da bir kez 4906 B/f) ve 196 ms'lik tek frame tepesi — sürekli değil; ilk kullanım (TMP glif, ses) şüphesi, M13 PSO/ısınma ile.
5. Director olmadan 300 zombi sürekli oyunculara akıyor → otomatik ateşle ~10–20 s'de bir ölüm. Denge M5 (HordeDirector, dalgasız yoğunluk).
6. Anti-stuck sayacı savaşta arttı (sendeleme "takılma" sayılıyordu) — düzeltildi, iki telefon testinde doğrulanacak.
7. Hotspot testi (B5) açık; zombiler placeholder (D1).
