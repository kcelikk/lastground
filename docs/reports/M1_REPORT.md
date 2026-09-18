# M1 Raporu — Networking PoC

- **Tarih:** 2026-09-18 · **Branch:** `m1-networking` · **Unity:** 6000.3.24f1 · **Mirror:** 96.11.2 · **Durum:** ✅ Onaylandı (2026-09-18), `main`e merge edildi. Hotspot ve iki telefon testi açık (OPEN_QUESTIONS B4, B5)
- **Test düzeni:** OnePlus 5T (Android 10, LOW) + dizüstü Linux dev oyuncusu (T14), aynı ev Wi-Fi'ı (192.168.1.0/24). İkinci telefon (Redmi Pad Pro) henüz bağlanmadı.

## Çıkış kriterleri (TDD_03 §36 M1)
| Kriter | Sonuç |
|---|---|
| Router'da 2 cihaz birbirini buluyor | ✅ Telefon, dizüstü host'unu listede buldu ("Player-9441 · 1/4 · 66 ms"), satıra dokununca katıldı |
| Hotspot'ta 2 cihaz birbirini buluyor | ⏳ **Denenmedi** (OPEN_QUESTIONS B5) |
| Hareket senkron | ✅ İki yön; host teleport'u kırpıyor (test) |
| 300 dummy'de client downstream ≤ 20 KB/s | ✅ Ort. **5.97**, en fazla **6.97 KB/s** payload (12.5 dk). Taşıma başlıkları (UDP/IP + KCP + Mirror batch) dahil tahmin ≤ ~9 KB/s |
| 10 dk kopmasız | ✅ **12.5 dk** (755 s), 0 kopma |
| Gecikme/kayıp simülasyonunda interpolasyon düzgün | ✅ EditMode: 50 ms gecikme + %5 kayıp altında 15 m içindeki varlıklar ≤ 1.2 m hata, bant bütçe içinde. Cihazda yapay gecikme denenmedi |
| "2 telefon" | ⚠️ Telefon + dizüstü ile yapıldı; iki telefonla tekrar Redmi bağlanınca |

## Ölçümler (dev build, `[NetStats]` 5 s örnekleri)
| Senaryo | Cihaz | Downstream | Upstream | RTT | FPS | En uzun frame |
|---|---|---|---|---|---|---|
| Soak 12.5 dk, dizüstü host | OnePlus 5T client | ort. 5.97 / en fazla 6.97 KB/s | 0.36 KB/s | ort. 33 / en fazla 59 ms | en düşük 30.5, ort. 30.6 (LOW, 30 kilit) | 49 ms |
| aynı | Dizüstü host | – | ort. 5.98 KB/s | – | 59.9 | 40 ms |
| Telefon host (2 dk) | OnePlus 5T host (300 sim + render + gönderim) | 0.36 KB/s | ~6.1 KB/s | – | **30.6** | 34 ms |
| aynı | Dizüstü client | ~6.4 KB/s | 0.36 KB/s | 28–31 ms | 60 | 17 ms |

İlgili küme (client'a replike edilen): 147–209 / 300 varlık. Mesaj oranı: client'a ~55–65 mesaj/s.

## CREATED / CHANGED FILES
| Assembly | İçerik |
|---|---|
| `LastGround.Core` | `Net/Wire` (NetWriter/NetReader, bit-pack, Quantize) · `Net/Protocol` (NetMsgId, NetProtocol, mesajlar) · `Net/Link` (IServerLink, IClientLink, INetLinkFactory, LoopbackNetwork) · `Net/Session` (ISession, NetSession ×3 partial, NetClock, NetStats, SessionConfig, LobbyPlayer, ISessionService) · `Input/` (PlayerInputFrame) · `Tick/TickAction` · `Run/RunLaunch` |
| `LastGround.Gameplay` | `Crowd/` CrowdState, CrowdReplica, DummyCrowdSim, ICrowdRenderSource · `Players/` PlayerStateTable, PlayerMotor |
| `LastGround.Networking` | `Mirror/` MirrorLinkFactory, MirrorServerLink, MirrorClientLink, LgPacket · `Replication/` CrowdReplicationSender/Receiver, PlayerSync, ReplicationTuning, NetTime · `Discovery/` UdpLanDiscovery, DiscoveryPacket |
| `LastGround.Platform` | `Net/` AndroidNetworkInterfaces (Java), DotNetNetworkInterfaces, AndroidMulticastLock, PlatformNet |
| `LastGround.Input` | FloatingJoystick, TouchMoveInput |
| `LastGround.Rendering` | CrowdRenderer (RenderMeshInstanced), PlayerViews, FollowCamera |
| `LastGround.UI` | LocalCoopScreen, LobbyScreen, RunHud, ScreenRouter, NetMessageKeys; MainMenuScreen güncellendi |
| `LastGround.App` | SessionService, RunInstaller, `Dev/` DevAutomation, RunTelemetry; AppRoot ağ kurulumu |
| `LastGround.Editor` | UiFactory, MenuSceneBuilder, RunSceneBuilder, AndroidPermissions (manifest), BuildScripts.BuildLinuxDevelopment |
| Veri | `Scenes/Run/Run.unity`, Menu yeniden üretildi · `Art/Materials/M1_*` · 32 yeni EN/TR string · `Assets/ThirdParty/Mirror` |
| Testler | +31 test (toplam 66): NetWire, NetSession (handshake, red nedenleri, kopma, saat, negatif id), Replication (bütçe, doğruluk, gecikme/kayıp, teleport), DiscoveryPacket |

## UNITY EDITOR ACTIONS
Yok. Sahneler `ProjectSetup.RebuildScenesBatch` ile üretildi (Menu ve Run'ın üzerine yazar; elle düzenleme yapılacaksa bundan sonra bu komut kullanılmamalı).

## INSPECTOR CONFIGURATION
Yok. KcpTransport runtime'da `[App]` kökünde oluşturuluyor (port 7777, varsayılan KCP ayarları). Desktop player: 1280×720 pencere, arka planda çalışır (yalnızca test eşi).

## ANDROID BUILD STEPS
- APK **62.9 MB** (M0: 45 MB; fark Mirror + yeni sistemler). Tam build 5.3 dk, artımlı ~1 dk. Linux dev oyuncusu 0.2–2 dk.
- Manifest'e eklenen izinler: ACCESS_NETWORK_STATE, ACCESS_WIFI_STATE, CHANGE_WIFI_MULTICAST_STATE, VIBRATE (+ INTERNET). Konum izni yok.
- Otomasyon: `adb shell "am start -n com.asgardgame.lastground/com.unity3d.player.UnityPlayerGameActivity -e lgargs '-lg-join 192.168.1.10 -lg-wander'"` (tırnaklar önemli).

## TEST RESULTS
EditMode **66/66**. Cihazda: solo run, discovery → katılma → lobby → run, iki yönde host/client, 12.5 dk soak, host kapanınca client'ın menüye dönmesi.

## Cihazda bulunan ve düzeltilen hatalar
1. **Negatif bağlantı id'si:** kcp2k bazı uç noktalara negatif `connectionId` veriyor; `NetSession` "id < 0 = bağlantı yok" varsaydığı için host telefona `JoinAccepted` göndermiyordu (telefon 5 s'de zaman aşımına düşüyordu). Aynı makinedeki iki Linux oyuncusunda id pozitif çıktığı için görünmedi. Düzeltme: açık `HasConnection` bayrağı; `LoopbackNetwork.NegativeConnectionIds` ile regresyon testi.
2. **RTT 0 ms:** LAN ping'i bir frame'den kısa olduğundan frame zamanıyla ölçülen RTT/saat ofseti 0'a yuvarlanıyordu. Düzeltme: `SessionConfig.TimeSource` (gerçek zaman). Sonrası RTT 28–59 ms.

## DECISIONS / DEVIATIONS
| Konu | TDD | Uygulanan | Gerekçe |
|---|---|---|---|
| Mirror kullanımı (D-017) | Oyuncular NetworkIdentity, Mirror online/offline sahne | Mirror yalnızca taşıyıcı; tüm durum kendi mesajlarımızla; sahne geçişi SessionService'te | Tam değiştirilebilirlik, Loopback ile test, Mirror varsayımları yok |
| Mirror NetworkDiscovery | başlangıç noktası | kullanılmadı; kendi `UdpLanDiscovery` | Java arayüz listesi + directed broadcast + MulticastLock gereksinimi zaten özel kod istiyordu (§18.2) |
| Lobby "hazır" | herkes hazır olunca START | M1'de START her zaman açık; run başında RunReady ile senkron başlangıç (10 s zaman aşımı) | Loadout seçimi yok |
| Oyuncu adı | – | İlk açılışta `Player-NNNN`; düzenleme ekranı yok | Settings ekranı sonraki milestone |
| Kamera / render | M4 / M2 | Basit takip kamerası, kapsül instancing | M1 kapsamı ağ |
| Test eşi | 2 telefon | Telefon + Linux dev oyuncusu | Redmi henüz bağlı değil |

## OPEN ISSUES
1. **Hotspot testi** yapılmadı (B5). Redmi Pad Pro ile iki telefonlu tekrar (B4).
2. **GC:** PerfHud menüde ve run'da ara sıra 368–419 B/frame tepe gösteriyor (sürekli değil). Kaynağı henüz belirlenmedi; M4 kapısı "0 B/frame" olduğu için M2/M4 başında profiler ile incelenecek. `RunTelemetry` 5 s'de bir string üretir (yalnızca dev build).
3. Açılış süresi: kurulumdan sonraki ilk açılışta ~11–13 s (IL2CPP veri çıkarımı); sonraki açılışlarda kısa. M13'te ölçülecek.
4. Discovery ping'i istek aralığına bağlı kaba bir değer (listeleme amaçlı); lobby'deki ping ise gerçek RTT.
5. Ekran kilitliyken başlatılan uygulama kilit ekranının arkasında kalıyor (test sırasında görüldü; oyunla ilgili değil).
6. Sonraki: **M2 — Zombie Rendering Benchmark** (VAT, LOD, kalite preset'leri, 20→300 benchmark CSV). Asset Store zombie paketi seçimi (OPEN_QUESTIONS D1) M2 başında gerekiyor; seçilmezse Mixamo placeholder ile başlanır.
