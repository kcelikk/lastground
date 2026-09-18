# LAST GROUND — Technical Design & Development Plan
## Bölüm 2/3 — Network, Rendering, Optimizasyon, Veri, Proje Yapısı, Bütçeler (§15–§33)

> Durum: v0.2 — kararlar işlendi (bkz. `DECISIONS.md`). Tüm bütçe sayıları hipotezdir; gerçek Android cihazlarda ölçülerek güncellenecek.

---

## §15. Network Architecture

### 15.1 Topoloji
- **Listen-server, yıldız topoloji:** host telefon = server + local client. Client'lar sadece host ile konuşur (client↔client trafik yok → hotspot'ların client izolasyonu bizi etkilemez).
- **Transport:** UDP üzerinde KCP (reliable + unreliable kanallar).
- **Framework:** **Mirror — onaylandı (D-001)** (gerekçe §E, Bölüm 3). Mirror tipleri yalnızca `LastGround.Networking` assembly'sinde görünür; değiştirilebilirlik kuralları §15.11.

### 15.2 Katmanlar
```
┌───────────────────────────── Gameplay (LastGround.Gameplay) ──────────────────────────────┐
│ ZombieWorld, DamageResolver, HordeDirector, LootService …   (Mirror'ı bilmez)             │
│        ▲ komutlar (ICommandSink)                    │ GameEvent akışları (ring buffer)    │
└────────┼────────────────────────────────────────────┼─────────────────────────────────────┘
┌────────┴──────────── Session API (LastGround.Core) ─┴─────────────────────────────────────┐
│ RunContext{Role: Offline|Host|Client}, ISession, ICommandSink, IGameEventStream           │
└────────▲────────────────────────────────────────────┬─────────────────────────────────────┘
┌────────┴──────────── Networking (LastGround.Networking, Mirror) ──────────────────────────┐
│ Adapters: PlayerNetworkAdapter, ZombieSnapshotSender/Receiver, GameEventReplicator,       │
│ HitClaimValidator, LobbyService, LanDiscovery, Serializers (quantization)                 │
└───────────────────────────────────────────┬───────────────────────────────────────────────┘
                                         KCP / UDP
Presentation (Rendering/Audio/UI) ◄── aynı GameEvent akışı (host'ta sim'den, client'ta ağdan)
```
**Kilit fikir:** Presentation katmanı event'lerin sim'den mi yoksa ağdan mı geldiğini bilmez. Host ve client'ta aynı VFX/ses/UI kodu çalışır.

### 15.3 Roller
- **Offline (Solo):** Mirror host modu, dinleme kapalı (Mirror'daki `NetworkServer.listen = false` benzeri ayar; M1'de doğrulanacak). Solo ile co-op **tek kod yolu**.
- **Host:** otoriter sistemler + local oyuncu.
- **Client:** replica sistemleri + local oyuncu.
- `RunInstaller`, role göre hangi sistemlerin oluşturulacağına karar verir. Gameplay koduna `if (isServer)` serpiştirilmez; rol kontrolü yalnızca installer ve adapter'larda.

### 15.4 Zaman ve tick
- Host sim tick: **30 Hz** sabit adım (`TickScheduler` accumulator). Render frame hızından bağımsız.
- Network gönderim: 30 Hz, stream başına oran (§17).
- Client saati: host tick tahmini (Mirror'un `NetworkTime` RTT/offset bilgisi) → `renderTime = estimatedHostTime − interpDelay (100 ms)`.

### 15.5 Mesaj kataloğu
| Mesaj | Yön | Kanal | Oran | Boyut (yaklaşık) |
|---|---|---|---|---|
| PlayerStateInput (pos, vel, aimYaw, fire, weapon, seq, shotCount) | C→H | Unreliable | 30 Hz | ~20 B |
| HitClaimBatch | C→H | Reliable | Tick başına toplu | ~9 B/claim |
| Command (SelectUpgrade, UseItem, Interact, ThrowGrenade, Pickup) | C→H | Reliable | Olay | 4–16 B |
| PlayerStates (diğer oyuncular) | H→C | Unreliable | 20 Hz | ~16 B/oyuncu |
| PlayerVitals | H→C | Reliable | Değişince (≤10 Hz) | ~10 B |
| ZombieEnterBatch (relevance'a giriş / spawn) | H→C | Reliable | Tick başına toplu | ~10 B/zombi |
| ZombieSnapshot (tier'lı) | H→C | Unreliable | §17 | ~7 B/zombi |
| ZombieDeathBatch | H→C | Reliable | Tick başına toplu | ~6 B/zombi |
| ZombieHitFxBatch (başkalarının isabetleri) | H→C | Unreliable | 10 Hz | ~5 B/hit |
| ProjectileSpawn / Impact | H→C | Reliable | Olay | ~20 B |
| TeamXp (takım XP + herkesin level'ı) / UpgradeOffer / BuildChanged | H→C | Reliable | Olay | 6–20 B |
| TeamWallet (run coin) | H→C | Reliable | Değişince (≤4 Hz) | 6 B |
| Emote | C→H→C | Reliable | Olay | 4 B |
| PickupSpawnBatch (ownerMask dahil) / PickupClaimed | H→C | Reliable | Olay | ~10 B / 4 B |
| DirectorInfo (intensity, run time) | H→C | Unreliable | 2 Hz | ~6 B |
| EventState / InteractableState | H→C | Reliable | Değişince | ~12 B |
| BossState / BossAttackStarted | H→C | Unreliable / Reliable | 20 Hz / olay | ~16 B |
| HordeGroupSummary (uzak sürüler, mini-map) | H→C | Unreliable | 1 Hz | ~8 B/grup |
| RunStart / RunResults / LoadRun | H→C | Reliable | Olay | değişken |

### 15.6 Oyuncu nesneleri
- 4 oyuncu = 4 NetworkIdentity (sorun değil). `PlayerNetworkAdapter : NetworkBehaviour` gameplay bileşenlerinin (`PlayerMotor`, `PlayerHealth`, `WeaponController`) durumunu okur/yazar; gameplay bileşenleri Mirror'ı bilmez.
- M1'de hız için Mirror'un unreliable NetworkTransform'u (client→server yönü) kullanılabilir; M4'te hız/teleport doğrulaması olan özel `PlayerStateInput`'a geçilir.

### 15.7 Sahne akışı
Mirror offline scene = `Menu`, online scene = `Run`. Harita, `MapLoader` ile additive yüklenir. Tüm oyuncular `Ready` gönderene kadar başlamaz → host 3 s geri sayım → `RunStart{startTick, seed}`.

### 15.8 Serileştirme
Mirror `NetworkWriter`/`NetworkReader` + özel extension'lar (quantized pozisyon, yaw byte, bit-pack). Pooled writer'lar → gameplay'de GC yok.

### 15.9 Sürüm ve içerik doğrulama
Handshake'te `ProtocolVersion` (int) + `ContentHash` (tüm SO katalog id'lerinin hash'i). Uyuşmazlıkta anlaşılır hata: "Farklı oyun sürümü".

### 15.10 Debug araçları
- `NetStatsOverlay`: mesaj tipi başına in/out kbps, paket/sn, RTT, kayıp.
- Mirror latency simulation transport ile editörde 50 ms / %2 kayıp / 20 ms jitter testleri.
- Unity 6 **Multiplayer Play Mode** ile editörde host + client'lar.
- Kategorili log (`Net`, `Sim`, `Director`), release'te derlenmez.
- Güvenlik: LAN co-op, anti-cheat hedefi yok; doğrulamalar **desync ve bug** önlemek için.

### 15.11 Değiştirilebilirlik kuralları (Mirror → FishNet vb.)
1. **Mesaj sözleşmeleri bizim:** ağ mesajları `LastGround.Core/Net/Contracts` içinde düz `struct`'lar ve kendi `INetWriter/INetReader` arayüzümüzle serileştirilir. Mirror'a özgü `NetworkMessage`, `SyncVar`, `[Command]`, `[Rpc]` yalnızca adapter sınıflarında kullanılır.
2. **Tek giriş noktası:** gameplay yalnızca `ISession`, `ICommandSink`, `IGameEventStream<T>`, `ILanDiscovery`, `INetClock` görür.
3. **Adapter sayısı sınırlı:** `MirrorSession`, `MirrorTransportBridge`, `PlayerNetworkAdapter`, `BossNetworkAdapter`, `MirrorLobbyAdapter`, `MirrorDiscoveryAdapter`. Framework değişimi = bu dosyaların yeniden yazımı.
4. **SyncVar yerine açık mesaj:** durum senkronu bizim mesajlarımızla → FishNet'in SyncVar/Observer modeline bağımlılık oluşmaz.
5. **Loopback test transport'u:** `LoopbackSession` (framework'süz, aynı process'te host+client) → gameplay replikasyon testleri Mirror olmadan EditMode/PlayMode testlerinde çalışır; ikinci implementasyon olarak soyutlamanın gerçekten sızdırmaz olduğunu kanıtlar.
6. **Kural denetimi:** `LastGround.Gameplay`, `UI`, `Rendering`, `Audio` asmdef'leri Mirror'ı referans edemez (derleyici zorlar).

---

## §16. Network Authority Model

**Prensip: "Client önerir, host karar verir."** Host'un kendi oyuncusu da aynı komut API'sinden geçer.

| Sistem | Otorite | Client ne yapar | Replikasyon |
|---|---|---|---|
| Oyuncu hareketi | Client (sahibi) + host doğrulaması (hız/teleport) | Anında yerel hareket | 30 Hz → diğerlerine 20 Hz |
| Nişan / ateş input'u | Client | Yerel VFX, isabet tespiti, HitClaim | Ateş sayacı + HitFx batch |
| Zombilere hasar | Host (claim doğrulama) | Öngörülen flash + damage number | Death batch |
| Oyuncu HP / armor / dodge | Host | UI | Reliable, değişince |
| Zombi AI / spawn | Host | Interpolasyon | Enter / Snapshot / Death |
| Horde Director | Host | — | Intensity 2 Hz |
| Boss AI | Host | Telegraph'ı tick-senkron oynatma | State + AttackStarted |
| Loot üretimi / sahiplik | Host (XP+temel coin takıma eşit; özel loot/silah/consumable `ownerMask` ile instanced) | Toplama talebi, öngörülen mıknatıs; yalnız kendi bitini render | Spawn / Claim |
| Takım XP / level / teklif | Host (kill sahipliği ödülü etkilemez) | Seçim | TeamXp / Offer / BuildChanged |
| Survival süresi / Horde / Threat | Host | HUD gösterimi (süre host tick'inden yerel türetilir) | Start tick + DirectorInfo 2 Hz + ThreatLevelChanged |
| Extraction penceresi | Host | LZ gösterimi, alan içi ilerleme | EventState |
| Map event'leri / etkileşimliler | Host | Etkileşim talebi | State |
| Revive / downed | Host | İlerleme gösterimi | Vitals |
| Game over / extraction | Host | Sonuç ekranı | Reliable |
| Kamera, ses, VFX, ayarlar | Local | — | Yok |
| Meta save | Her cihaz kendi | Kendi kaydı | Host sonuçta `RunResults` gönderir |

---

## §17. Zombie Network Optimization

### 17.1 Kimlik
- Zombi başına NetworkIdentity **yok**. `ZombieHandle = (ushort slotIndex, byte generation)`.
- Enter mesajı generation taşır; snapshot'lar sadece index taşır. Client, generation'ı eşleşmeyen veya Enter'ı henüz gelmemiş slot'a ait snapshot girdisini yok sayar.

### 17.2 Snapshot girdisi (bit-pack, 56 bit = 7 B)
| Alan | Bit | Not |
|---|---|---|
| slotIndex | 10 | 1024 kapasite |
| posX | 16 | 512 m / 65536 → ~7.8 mm hassasiyet |
| posZ | 16 | |
| yaw | 6 | 5.6° adım |
| animState | 3 | idle/walk/run/attack/hit/special/dying |
| flags | 5 | attacking, stunned, burning, elite, lowHp |
Başlık: hostTick (varint) + count (varint). İlk sürüm **mutlak** değerler gönderir (paket kaybına dayanıklı, baseline takibi yok). Delta sıkıştırma yalnızca ölçüm gerektirirse eklenir.

### 17.3 İlgi yönetimi (client başına)
| Tier | Koşul (client'ın oyuncusuna göre) | Gönderim | Interpolasyon |
|---|---|---|---|
| A | Kamera footprint + 6 m pay (~<20 m) | 15 Hz | 100 ms buffer |
| B | 20–35 m | 6 Hz | Örnek aralığında lerp |
| C | 35–60 m | 2 Hz | Lerp + 250 ms'ye kadar extrapolasyon |
| — | > 60 m | Gönderilmez | Client replica'yı siler (Exit) |
- Client'ın replica kümesi = ilgili küme. `Enter/Exit` reliable batch'lerle yönetilir; Death event'i yalnızca ilgili client'lara gider.
- Tier hesabı: 3 client × 300 zombi = 900 mesafe testi/tick → Burst job'da ihmal edilebilir.

### 17.4 Zamanlama ve bütçe
- **Bucket rotasyonu:** Tier A 15 Hz = her 2 tick'te bir; zombiler iki bucket'a bölünür → her tick yaklaşık eşit boyutlu paket (Wi-Fi'da burst yok, jitter az).
- **Priority accumulator:** her zombinin önceliği `(son gönderimden beri geçen süre) × tierAğırlığı`. Client başına tick bütçesi (örn. 700 B/tick ≈ 21 KB/s) dolana kadar en yüksek öncelikliler gönderilir. Yük arttığında sistem çökmez; uzak zombiler zarifçe seyrekleşir.
- Paket payload'u ≤ 1100 B (IP fragmentasyonu yok); gerekirse bölünür.

### 17.5 Client interpolasyonu
- Zombi başına 2–3 örneklik halka buffer (hostTick damgalı).
- `renderTime` arasında lerp; veri gecikirse son hızla ≤ 250 ms extrapolasyon, sonra bekle.
- Hata > 3 m → snap.
- Animasyon, hit reaksiyonu, kan, ses yerel üretilir (ağa görsel veri gitmez).

### 17.6 Event batching
Enter/Death/HitFx/Pickup event'leri tick boyunca biriktirilir → client başına tip başına tek mesaj.

### 17.7 Grup simülasyonu (uzak sürüler)
60 m ötesindeki ve henüz materyalize edilmemiş sürüler `VirtualHorde` olarak host'ta nokta+sayı+yön olarak yaşar (bölge portal grafı üzerinde ilerler). Client'a sadece `HordeGroupSummary` (1 Hz) → mini-map yön/şiddet halkası ve uzak sürü ambiyans sesi. İlgi yarıçapına yaklaşınca bireysel zombilere dönüşür.

### 17.8 Reddedilen yaklaşım: deterministik lockstep
Client'larda aynı AI'ı deterministik çalıştırıp yalnız input göndermek bant genişliğini düşürür ama: farklı ARM çiplerde float determinizmi garanti değil, bir desync tüm oturumu bozar, debug çok zordur, geç katılım imkânsızlaşır. Snapshot yaklaşımı daha sağlam. **Reddedildi.**

### 17.9 Doğrulama
`NetStatsOverlay` + CSV log; hedefler §32. Testler: 2.4 GHz kalabalık ağ, 50 ms gecikme, %2–5 kayıp.

---

## §18. Android LAN Discovery

### 18.1 Protokol (UDP broadcast istek/yanıt)
- **Host `DiscoveryResponder`:** UDP 47777'yi dinler (yalnızca lobby açıkken; run başlayınca kapanır → pil).
- **Client `DiscoveryBrowser`:** Find Games ekranı açıkken saniyede bir istek gönderir:
  `{magic "LGRD", protocolVersion, nonce, sendTimeMs}`
  Hedefler: `255.255.255.255` **ve** her aktif IPv4 arayüzünün **directed broadcast** adresi (örn. `192.168.1.255`). Hotspot ve çoklu arayüzlü cihazlar için şart.
- **Host yanıtı (unicast):** `{magic, protocolVersion, contentHash, sessionName "KADIR'S GAME", players, maxPlayers, gamePort, state (Lobby/InRun), nonce}`.
- **Client:** ping = şimdi − gönderim zamanı (nonce eşleşmesi). Host IP:port'a göre tekilleştirme; 3 s yanıt yoksa listeden düş.

### 18.2 Android özel gereksinimleri
- İzinler: `INTERNET`, `ACCESS_NETWORK_STATE`, `ACCESS_WIFI_STATE`, `CHANGE_WIFI_MULTICAST_STATE`.
- **`WifiManager.MulticastLock`:** discovery aktifken (host ve client) acquire, ekrandan çıkınca release. Birçok cihaz güç tasarrufu için broadcast paketlerini lock olmadan filtreler. `AndroidJavaObject` ile çağrılır.
- **Arayüz listesi Java tarafından alınmalı:** Android 11+ (API 30+ hedefli uygulamalar) netlink kısıtlamaları nedeniyle .NET `NetworkInterface.GetAllNetworkInterfaces()` IL2CPP'de boş dönebilir/hata verebilir. Güvenli yol: `java.net.NetworkInterface.getNetworkInterfaces()` → `InterfaceAddress.getBroadcast()` / `getNetworkPrefixLength()` (küçük bir `AndroidJavaClass` köprüsü). **M1'de doğrulanacak.**
- Socket'ler `0.0.0.0`'a bind edilir; broadcast her arayüz için ayrı gönderilir.
- Host lobby ekranı kendi IP'sini gösterir (wlan0 / ap0 / swlan0 tercihli) → Join by IP.

### 18.3 Hotspot senaryoları
| Senaryo | Beklenen | Not |
|---|---|---|
| Router, herkes aynı Wi-Fi | ✔ | AP/client isolation kapalı olmalı |
| Host telefon hotspot açar, diğerleri bağlanır | ✔ **Önerilen yedek** | Host IP hotspot arayüzünde; directed broadcast şart |
| Üçüncü bir telefon hotspot, 4 oyuncu client olarak bağlı | Çoğunlukla ✔ | Bazı OEM hotspot'ları client↔client'ı engelleyebilir → test matrisi |
| Misafir ağı / kafe Wi-Fi (isolation açık) | ✘ | Anlaşılır hata + "Hotspot deneyin" önerisi |
| Wi-Fi'da internet yok | ✔ (beklenen) | Aynı subnet rotası Wi-Fi'dan gider; sorun çıkarsa `ConnectivityManager.bindProcessToNetwork` yedeği |

### 18.4 UX
- Boş liste metni: "Aynı Wi-Fi ağında olduğunuzdan emin olun. Bulunamazsa telefonlardan biri hotspot açsın veya IP ile katılın."
- İleride: host ekranında QR kod (ip:port).

### 18.5 iOS için hazırlık
iOS 14+ yerel ağ izni (`NSLocalNetworkUsageDescription`) ister; broadcast/multicast için Apple'dan `com.apple.developer.networking.multicast` entitlement'ı gerekir. Alternatif: Bonjour. Bu nedenle discovery `ILanDiscovery` arayüzü arkasında, platform implementasyonu değiştirilebilir.

---

## §19. Host / Join Flow

### 19.1 Ekran akışı
```
MAIN MENU
├── SOLO ─────────────► Loadout ─► Run (Offline)
├── LOCAL CO-OP
│   ├── CREATE GAME ──► Oturum ayarı (isim, 2–4 oyuncu, zorluk)
│   │                    └► StartHost(UDP 7777) + DiscoveryResponder ─► HOST LOBBY
│   ├── FIND GAMES ───► Liste (isim, x/4, ping, [JOIN]) ─► JOINING ─► CLIENT LOBBY
│   └── JOIN BY IP ───► IP (+port) gir, son kullanılan hatırlanır ─► JOINING
├── ARSENAL (meta)
├── SETTINGS
└── QUIT
```

### 19.2 Bağlantı el sıkışması
```
Client ── JoinRequest{protocolVersion, contentHash, playerName, playerGuid, loadoutId, cosmeticId} ──► Host
Host   ── JoinAccepted{playerId, lobbySnapshot}  |  JoinRejected{reason} ──► Client
reason: VersionMismatch | Full | InProgress | Refused
Client tarafı 5 s zaman aşımı → Timeout
```

### 19.3 Lobby
- Slotlar: isim, cihaz tier ikonu (host seçimine yardımcı), hazır durumu, ping.
- Herkes loadout (başlangıç silahı) seçer; host harita/zorluk seçer.
- Host lobby ekranında IP adresi görünür.
- [START] yalnızca herkes hazırken aktif.

### 19.4 Başlatma
`LoadRun{runSeed, mapId, difficulty, slots}` → herkes sahneyi yükler → `Ready` → host 3 s geri sayım → `RunStart{startTick}`.

### 19.5 Run sırasında
- Client kopması: host devam eder, 10 s sonra avatar kaldırılır; client "Bağlantı koptu" → kısmi ödüllü sonuç.
- Host uygulamayı arka plana alırsa (telefon araması vb.): client'lar 10 s zaman aşımı → "Host bağlantısı koptu" → kısmi ödül kaydı.
- Co-op'ta menü oyunu durdurmaz.
- `Screen.sleepTimeout = NeverSleep` (run süresince).

### 19.6 Bitiş
`RunResults{oyuncu başına istatistik, ödül}` → herkes kendi cihazına kaydeder → "Lobby'ye dön" (oturum korunur) veya "Ana menü".

---

## §20. Object Pooling Architecture

### 20.1 Yapı
- `PoolService`: run kapsamlı, `RunInstaller` tarafından oluşturulur (statik singleton değil).
- İki tür pool:
  1. **GameObject/Component pool** (`ComponentPool<T> where T : Component, IPoolable`): Transform veya özel bileşen gereken nesneler — patlama, telegraph, supply crate, audio voice, UI öğeleri.
  2. **Veri slot pool'u** (struct dizisi + free list / ring buffer): instanced çizilen her şey — zombiler, cesetler, pickup'lar, kan splat'leri, tracer'lar, damage number'lar, projectile'lar.
- **`Emit()` tabanlı VFX:** blood burst, kıvılcım, kovan, muzzle dumanı için efekt tipi başına tek kalıcı ParticleSystem; olay başına `ParticleSystem.Emit(EmitParams)` → olay başına GameObject yok.
- `IPoolable`: `OnSpawned()`, `OnDespawned()`. Zamanlı iade tek bir tick listesiyle (coroutine yok).
- Prewarm: `PoolDefinition` SO (prefab, LOW/MED/HIGH adetleri) yükleme ekranında.
- Taşma politikası: dekoratif şeyler (ceset, decal, damage number) → en eskiyi geri dönüştür; gameplay nesneleri → dev build'de uyarı + büyüme.

### 20.2 Pool tablosu (başlangıç cap'leri LOW/MED/HIGH)
| Nesne | Strateji | Cap |
|---|---|---|
| Zombiler (sim, host) | SoA slot + free list | 512 kapasite (tüm tier'lar; hedef 300+ aktif, D-007) |
| Zombi görselleri | Instanced batch listesi | görünür hedef 250 (tüm tier'lar); LOW'da benchmark sonucuna göre 150–250 |
| Projectile (sim) | Struct listesi | 64 |
| Muzzle flash | Instanced quad slotları | 8 / 12 / 16 |
| Tracer | Instanced ring buffer | 32 / 48 / 64 |
| Kovanlar | Emit | kapalı / 100 / 200 parçacık |
| Blood burst | Emit | 150 / 300 / 500 parçacık |
| Blood splat (zemin) | Instanced ring buffer | 32 / 96 / 192 |
| Patlama | ComponentPool | 4 / 6 / 8 |
| Damage number | Instanced digit atlas ring buffer | 12 / 24 / 32 |
| Loot/pickup | Slot + instanced | 256 |
| Ceset | Instanced ring buffer | 20 / 50 / 100 |
| Audio voice | ComponentPool | 16 / 24 / 32 |
| Telegraph | ComponentPool | 8 |

### 20.3 Kurallar
Gameplay sırasında `Instantiate/Destroy` yok (dev build sayaç + assert). Hot path'te LINQ, closure, boxing, string birleştirme yok. UI sayıları TMP `SetText(format, int)` ile.

---

## §21. Rendering Architecture

### 21.1 URP kurulumu
- **Forward** rendering path (Deferred yok; Forward+ yalnızca HIGH tier Vulkan'da ölçülerek değerlendirilir).
- Kalite başına ayrı URP Asset (LOW/MED/HIGH).
- Depth texture ve opaque texture kapalı (soft particle, refraction yok).
- HDR: LOW kapalı, MED/HIGH R11G11B10.
- Post-processing: Bloom (MED/HIGH, düşük kalite), tonemapping + LUT color grading (tüm tier'lar). Vinyet UI overlay.
- AA: başlangıç LOW kapalı, MED MSAA 2×, HIGH MSAA 4× (tile-based GPU'larda MSAA görece ucuz) — M2/M13'te ölçülerek kesinleşir.
- Graphics API: Vulkan öncelikli, OpenGL ES 3.x yedek.

### 21.2 Aydınlatma
- **Statik çevre:** baked lightmap (GPU Progressive Lightmapper), AO bake dahil.
- **Dinamik nesneler:** light probe'lar (oyuncular, boss).
- **Zombiler — "Lighting Grid" hilesi:** editörde bake edilmiş top-down düşük çözünürlüklü zemin ışık texture'ı (örn. 512² / bölge). VAT shader dünya XZ konumundan örnekler → lamba altındaki zombi turuncu aydınlanır, karanlıktaki zombi kararır. Instance başına ek maliyet: tek texture lookup. Pickup ve cesetler de aynı texture'ı kullanır.
- **Realtime ışık:** ana directional ("ay ışığı") + ek dinamik ışık limiti LOW 0 / MED 2 / HIGH 4 — yalnızca local muzzle flash (HIGH), en yakın 2 patlama, boss saldırı parıltısı.
- **Sahte ışık:** lambaların altındaki additive "ışık havuzu" mesh'leri, hacimsel koni kartları, emissive lamba kafaları + bloom.
- **Gölge:** HIGH: yalnızca oyuncular + boss directional gölge (1024, 25 m, 1 cascade); MED: 512 veya blob; LOW: instanced blob gölge. Zombiler gölge atmaz (instanced blob, zombi batch'iyle aynı çağrıda). Çevre gölgesi bake.

### 21.3 Zombi render'ı (VAT + GPU instancing)
- **VAT (Vertex Animation Texture):** Editör aracı `VatBaker`, skinned mesh'in klip karelerini `SkinnedMeshRenderer.BakeMesh` ile örnekler → pozisyon (+ normal) texture'ı (RGBAHalf). Klipler: walk×2, run, attack×2, hit, death×3, idle; 30 fps.
- Bütçe: LOD0 ≤ 2.000 vertex, LOD1 ~800, LOD2 ~300 (her LOD için ayrı küçük VAT). Alternatif: kemik matris texture'ı (tek texture, LOD'lar arası paylaşım) — bellek sıkışırsa değerlendirilir.
- Çizim: `Graphics.RenderMeshInstanced` — (mesh varyantı × LOD) başına batch, çağrı başına ≤ 1023 instance. Instance verisi: matris + (klip indeksi, zaman offseti, tint indeksi, hit flash, sink) per-instance property dizileri. Gerekirse BatchRendererGroup'a geçiş (M19).
- Culling: sabit kamera → XZ düzleminde basit 2D footprint testi (Burst job).
- **Çeşitlilik:** 4 gövde mesh × 8 renk paleti (shader lookup) × kir/kan maskesi × ölçek 0.92–1.08 → 100+ görünüm, tek materyal.
- **Okunabilirlik:** global rim-light parametresi; elite'lerde emissive göz + tint.
- Oyuncular (maks 4) ve boss: normal SkinnedMeshRenderer + Animator.

### 21.4 Çevre
- Modüler kit + trim sheet; 2–4 adet 2048² atlas (ASTC 6×6; albedo + smoothness paketli). Normal map yalnızca kahraman prop'larda.
- Paylaşımlı `LG/Environment` lightmapped shader, static batching + SRP Batcher, büyük prop'larda LODGroup.
- Vertex color ile kir blending; zemin çatlakları mesh'e bake (decal değil).
- Skybox yok; far clip ~70 m.

### 21.5 Kan sistemi (3 katman)
| Katman | Yöntem | LOW / MED / HIGH |
|---|---|---|
| L1 Burst | Paylaşımlı ParticleSystem `Emit()`, kare başına throttle | ✔ / ✔ / ✔ |
| L2 Splat | Instanced zemin quad'ları, 16 şekillik atlas, ring buffer, en eskiyi soldur | kapalı / 96 / 192 |
| L3 Birikim | **Bölge başına kan RenderTexture'ı** (R8). Ölümlerde `CommandBuffer` ile stamp (kare başına maks N). Zemin shader'ı örnekler → koyu, ıslak (smoothness artışı) kırmızı | 512² / 1024² / 1024² |
- 7 bölge × 1024² R8 ≈ 7 MB; sadece aktif bölgelerin RT'si bellekte tutulabilir.
- **İllüzyon:** ceset kaybolur, kan izi kalır → img-1'deki savaş alanı birikimi tek sample ile elde edilir.
- Ayarlar: "Kan Decal" = L2+L3 aç/kapa; "Azaltılmış gore" = renk ve yoğunluk düşürme.

### 21.6 Cesetler
- Ölümde zombi instance'ı VAT death klibini oynatır → klip sonunda `CorpseSystem` ring buffer'ına taşınır (son kare sabit, aynı VAT texture).
- Ömür 8–20 s; kaybolma = 1.5 s'de 0.5 m yere batma (opaque kalır, overdraw yok, batching bozulmaz).
- Cap: LOW 20 / MED 50 / HIGH 100 (Ayarlar → Ceset Sayısı slider'ı bu değeri değiştirir).
- Cesetler **client-local**'dir; host yalnızca death event'i (varyant + yön) gönderir.

### 21.7 VFX
- 1–2 adet 2048² FX atlası; tek additive + tek alpha-blend materyal; flipbook.
- Tracer: instanced uzatılmış quad. Muzzle flash: quad + zemin flash decal.
- Patlama: flipbook + debris `Emit` + shake + opsiyonel realtime ışık (HIGH, maks 2).
- Ateş zemini: flipbook + ısı parıltısı quad'ı. Ambiyans: kameraya bağlı kül/kıvılcım sistemi.
- Overdraw: kameraya yakın büyük parçacık yok; LOW'da emisyon ×0.5.

### 21.8 Shader listesi (az ve paylaşımlı)
`LG/Environment` · `LG/Character` (oyuncu/boss) · `LG/ZombieVAT` · `LG/Pickup` · `LG/FX_Additive` · `LG/FX_AlphaBlend` · `LG/GroundDecal` (splat, ışık havuzu, telegraph) · UI default.
Shader variant stripping (`IPreprocessShaders` + URP feature stripping) → build süresi, bellek ve yükleme için kritik.

### 21.9 Kalite preset'leri (başlangıç hipotezi)
Tier eşlemesi (D-006): **LOW** = düşük/orta 4–6 GB · **MEDIUM** = orta 6–8 GB · **HIGH** = üst 8–12+ GB.

| Ayar | LOW | MEDIUM | HIGH |
|---|---|---|---|
| FPS hedefi | **min 30** (30 kilit) | **stabil 45–60** (60 hedef, termalde 45 tabanı; 30 seçenekli) | 60 |
| Render scale | 0.65–0.70 | 0.80–0.85 | 1.0 (1080p yükseklikte cap) |
| AA | Kapalı | MSAA 2× | MSAA 4× |
| HDR / Bloom | Kapalı / kapalı | Açık / düşük | Açık / orta |
| Gölge | Blob | Blob + oyuncu 512 | Oyuncu+boss 1024, 25 m |
| Ek dinamik ışık | 0 | 2 | 4 |
| Görünür zombi hedefi (stress) | 250 (LOD2 ağırlıklı; ölçüme göre 150–250 cap) | 250 | 250–300 |
| Zombi LOD0 / LOD1 mesafesi | 6 m / 14 m | 12 m / 24 m | 18 m / 35 m |
| Zombi shading | Rim yok, tint + lighting grid | Rim + lighting grid | Rim + lighting grid + hit flash detay |
| Ceset cap | 20 | 50 | 100 |
| Kan splat / birikim | kapalı / 512² | 96 / 1024² | 192 / 1024² |
| Parçacık çarpanı | 0.5 | 0.8 | 1.0 |
| Kovanlar | Kapalı | Açık | Açık |
| Audio voice | 16 | 24 | 32 |
**Önemli:** Gameplay zombi sayısı tüm cihazlarda aynıdır (host belirler). Görsel yoğunluk her cihazda yerel olarak düşürülür (§I, Bölüm 3).

---

## §22. Mobile Optimization Strategy

### 22.1 CPU
- Zombi, flow field, snapshot build işleri Burst + Jobs (worker thread'ler).
- Tek `TickScheduler.Update()` → sıralı fazlar: `Input → LocalPlayer → NetReceive → Director* → ZombieSim* → Combat → Loot/Events → NetSend → Presentation (Camera, Render prep, UI, Audio)` (*host).
- Nesne başına `Update()` yok, gameplay'de coroutine yok.
- Physics: `Physics.simulationMode = Script`, simülasyon adımı çalıştırılmaz; sadece statik çevre raycast'leri. `autoSyncTransforms` kapalı.
- Animator yalnızca oyuncular + boss (culling açık).
- UI: canvas bölme (statik / dinamik / popup), layout group'suz HUD, TMP `SetText` non-alloc.
- IL2CPP, ARM64, managed stripping Medium, incremental GC açık. Hedef **0 B/frame** allocation.

### 22.2 GPU
Instancing, atlas, düşük overdraw (yarı saydam UI panelleri dahil), render scale, ASTC + mipmap, realtime reflection probe yok, sadece Bloom + LUT, zombilerde normal map yok, vertex bütçeleri (§31).

### 22.3 Termal ve pil
- **Adaptive Performance** (Android provider) termal uyarıları → kademeli düşüş: render scale → parçacık → görünür zombi cap → 30 FPS.
- "Pil tasarrufu" ayarı (30 FPS kilit).
- Optimized Frame Pacing açık.
- Discovery yalnızca ilgili ekranda; run'da kapalı.

### 22.4 Yükleme ve takılmalar
Shader/PSO ön ısıtma: yükleme ekranında `ShaderVariantCollection` ve Unity 6 `GraphicsStateCollection` (Vulkan PSO) ile ısınma → ilk patlama/ilk silah takılmaları önlenir.

### 22.5 Cihaz tier tespiti
İlk açılışta `DeviceTierDetector`: `SystemInfo` (GPU, RAM, çekirdek) + menüde 5 s sessiz benchmark → varsayılan preset. Kullanıcı değiştirebilir.

### 22.6 Profiling iş akışı
- Development Build + Autoconnect Profiler (USB/ADB), Profile Analyzer, Memory Profiler, Frame Debugger.
- GPU: Android GPU Inspector, Snapdragon Profiler (Adreno), Arm Performance Studio (Mali).
- Sistem: `adb shell dumpsys gfxinfo`, `dumpsys thermalservice`, `dumpsys batterystats`.
- Oyun içi `PerfHud`: FPS, CPU main ms, GC alloc/frame, zombi sayıları (sim/görünür), net kbps, sıcaklık durumu.
- `PerfBenchmarkRunner`: stress sahnesinde 20 → 50 → 100 → 150 → 200 → 250+ zombi, her adım 60 s; ortalama / %1 low frame time, sıcaklık başı/sonu, pil düşüşü → `persistentDataPath` altına CSV.
- **Editor sonuçları performans verisi olarak kabul edilmez.**

---

## §23. Audio Architecture

- **`AudioService`:** sabit voice pool (LOW 16 / MED 24 / HIGH 32 AudioSource). 3D ses; **listener oyuncu konumunda** (kamerada değil — top-down'da doğru mesafe hissi için).
- **`SoundDefinition` SO:** clip varyasyonları, volume/pitch aralığı, kategori, öncelik (0–255), ses başına maks voice, min tekrar aralığı, spatial blend, falloff.
- **Kategori cap'leri:** Local silah 6 · takım silahları 4 · patlama 3 · zombi 6 · çevre 4 · UI 3 · müzik 2 (crossfade).
- **Voice stealing:** doluysa en düşük öncelikli + en uzak/en sessiz voice çalınır.
- **Sürü illüzyonu:** `HordeAmbienceController` 3 katmanlı loop (uzak/orta/yakın inilti yatağı), 20 m içindeki zombi sayısı + director yoğunluğu ile karışır. Bireysel one-shot yalnızca en yakın ≤ 6 zombi için, saniyede maks 4 zombi one-shot'ı. 100 zombi = ~4–6 voice.
- **Seri silahlar:** SMG/MG için loop + tail (atış başına voice yerine tek voice).
- **Müzik:** director durumuna bağlı adaptif stem'ler: Calm / Tension / Horde / Boss / Extraction.
- **AudioMixer:** Master → Music, SFX (Weapons, Explosions, Zombies, Environment), UI; patlama/boss kükremesinde ducking snapshot; ayar slider'ları.
- **Import:** kısa SFX → Decompress On Load (ADPCM/Vorbis), mono 3D; ambiyans/müzik → Streaming Vorbis; zombi sesleri 22–32 kHz.
- Ağa ses mesajı gitmez; sesler event'lerden türetilir.

---

## §24. Save Architecture

- **Arayüzler:**
  - `ISaveStore` — `bool TryRead(string key, out string data)`, `void Write(string key, string data)`
  - `ISaveService` — `ProfileData Profile`, `SettingsData Settings`, `RequestSave()` (debounced)
- **`JsonFileSaveStore`:** `persistentDataPath/save/{key}.json`; atomik yazma (`.tmp` → flush → replace), son sağlam kopya `.bak`, CRC32 bozulma kontrolü (anti-cheat değil).
- **Serileştirme:** Newtonsoft JSON (Unity'nin resmi paketi) — sözlük ve sürüm esnekliği; kayıt yalnızca menü/run sonu olduğundan allocation önemsiz.
- **Dosyalar:**
  - `settings.json` — cihaza özel (grafik, kontrol, ses, dil).
  - `profile.json` — kalıcı currency (Scrap), açılmış silahlar / karakterler / perk'ler / loadout'lar, sahip olunan ve kuşanılmış skin / outfit / weapon skin / emote / badge / title, istatistikler (en uzun survival, en yüksek Threat, extract sayısı…), başarımlar, oyuncu adı + `playerGuid`.
  - **Run durumu (level, upgrade, run silah geliştirmeleri) asla profile yazılmaz** (D-005).
- **Sürümleme:** `int version` + `SaveMigrator` zinciri (v1→v2→…).
- **Ne zaman:** run sonu, menüde satın alma/açma, ayar değişikliği (1 s debounce), `OnApplicationPause(true)`. Savaş sırasında yazma yok.
- **Referanslar:** her zaman SO katalog `id` string'leri (asset adı/indeks değil).
- **Gelecek:** `CloudSaveStore : ISaveStore` + çakışma çözümü. Profil verisi birleştirilebilir tasarlanır (unlock kümeleri = birleşim, istatistikler = max/toplam).
- **Monetization'a hazırlık (MVP'de implementasyon yok):** kozmetikler katalog id'leri ile; ileride `IStoreService` / `IAdsService` eklenebilir. Hiçbir satın alma gameplay gücü vermez.

---

## §24A. Localization Architecture (D-010)

### 24A.1 Karar: hafif, JSON tabanlı özel sistem
| Seçenek | Artı | Eksi |
|---|---|---|
| Unity Localization paketi | Resmi, String Table editörü, smart strings | Addressables bağımlılığı (MVP'de kaçınıyoruz), ağır, AI ile düzenlemesi zor asset formatı |
| **Özel JSON string tabloları** ✔ | Bağımlılık yok, diff'lenebilir, yeni dil = yeni dosya, çeviri araçlarına/AI'a doğrudan verilebilir | Editör araçlarını biz yazarız (küçük) |
İleride gerekirse JSON → Unity Localization String Table import aracı yazılabilir; key'ler aynı kalır.

### 24A.2 Dosyalar
```
Assets/Resources/Localization/     (M0: Resources.Load ile yüklenir; Addressables yok)
├── languages.json          [{ "code":"en", "name":"English", "fallback":null },
│                            { "code":"tr", "name":"Türkçe",  "fallback":"en" }]
├── en/  ui.json  gameplay.json  items.json  meta.json   (varsayılan / referans dil)
└── tr/  ui.json  gameplay.json  items.json  meta.json
```
- Kategori başına ayrı dosya → dosyalar küçük, merge çakışması az.
- Format: düz `key → string` sözlüğü. Örnek: `"hud.survival": "SURVIVAL {0}"`, `"hud.horde.high": "HIGH"`, `"weapon.ar.name": "Assault Rifle"`.
- Key isimlendirme: `alan.alt_alan.öğe` (küçük harf, nokta ayraç). SO'lar metin değil **key** tutar (`nameKey`, `descKey`).

### 24A.3 Runtime
- `ILocalizationService`: `string Get(string key)`, `string Format(string key, int/float arg)` (allocation'sız overload'lar, önbellekli), `event LanguageChanged`, `CurrentLanguage`.
- Yükleme: seçili dil + fallback zinciri (tr → en). Eksik key → İngilizce; o da yoksa dev build'de `#key#` görünür + log.
- `LocalizedText` bileşeni (TMP): key alır, dil değişince yenilenir. HUD'un sık değişen sayıları için format string bir kez çözülür, sayı `SetText` ile basılır (GC yok).
- **Varsayılan dil:** İngilizce. İlk açılışta cihaz dili Türkçe ise Türkçe önerilir (ayar ekranından değiştirilebilir) — istenirse bu otomatik seçim kapatılabilir.
- Dil değişimi menüde anında; run içinde de çalışır.

### 24A.4 Türkçe'ye özel teknik tuzaklar
- **Türkçe I/İ problemi:** `ToUpper()` / `ToLower()` / `string.Compare` kültüre bağlıdır; cihaz dili Türkçe iken `"id".ToUpper()` → `"İD"` olur. Kural: tüm id, key, dosya adı, parse ve karşılaştırma işlemleri `CultureInfo.InvariantCulture` / `StringComparison.Ordinal` ile. Büyük harf UI metinleri **çeviri dosyasında zaten büyük harf yazılır** (runtime `ToUpper` yok).
- Sayı/zaman biçimi: `float.Parse` ve JSON okumada invariant kültür (Türkçe'de ondalık ayraç virgül).
- Font: TMP font asset'i Latin Extended-A glifleri (ş ğ ı İ ç ö ü) içermeli; gelecekteki diller için fallback font listesi.
- Metin uzunluğu: TR/DE metinleri EN'den ~%30 uzun olabilir → UI'da auto-size sınırları + **pseudo-localization** modu (dev build'de tüm metinleri %40 uzatıp aksanlı karakterle göster).

### 24A.5 Araçlar
- `LocalizationValidator` (Editor): eksik/fazla key raporu (en referans), kullanılmayan key, SO'larda geçersiz key, format argümanı sayısı uyuşmazlığı.
- CSV export/import (çevirmen veya AI çevirisi için).
- Yeni dil ekleme = `languages.json`'a satır + klasör kopyala + çevir. Kod değişikliği yok.

---

## §25. ScriptableObject Data Model

**Kurallar:** SO'lar runtime'da **salt okunur**. Runtime durum saf C# sınıflarında. Hot path, yüklemede SO'lardan üretilen blittable struct'ları kullanır (örn. `NativeArray<ZombieTypeParams>`). Her tanımın kalıcı string `id`'si var; editör aracı tekrar eden id'leri yakalar ve `ContentHash` üretir.

| SO | Önemli alanlar |
|---|---|
| `GameDatabase` | Tüm katalogların kökü (silah, upgrade, zombi, loot, event, ses…) |
| `PlayerDefinition` | Base statlar, hareket, pickup radius, görsel prefab (ileride class) |
| `WeaponDefinition` | §6.1 |
| `WeaponVisualProfile` | Mesh, socket, flash, tracer, kovan |
| `ProjectileDefinition` | Hız, yerçekimi, yarıçap, ömür, patlama, görsel |
| `UpgradeDefinition` + `UpgradeEffect` (SerializeReference) | §7.2 |
| `RarityTable` | Ağırlıklar, değer kademeleri, Luck etkisi |
| `LevelCurveDefinition` | XP eğrisi, oyuncu sayısı çarpanı |
| `AbilityDefinition` | Cooldown, tip (drone/turret/grenade), parametreler |
| `ZombieDefinition` | Tip, HP, hız, hızlanma, yarıçap, kütle, saldırı (menzil/windup/cooldown/hasar), XP, loot tablosu, spawn maliyeti, özel parametreler, görsel |
| `ZombieVisualDefinition` | Mesh LOD'ları, VAT texture'ları, klip tablosu, tint paleti |
| `EliteModifierDefinition` | HP çarpanı, ek davranış, görsel |
| `DirectorProfile` | Zaman eğrileri, durum süreleri, eşikler, desen ağırlıkları |
| `PlayerCountScalingProfile` | §9.10 tablosu |
| `SpawnCardDefinition` | §9.6 |
| `HordePatternDefinition` | Sektör sayısı, grup boyutu, zamanlama |
| `BossDefinition` / `BossAttackDefinition` | §10 |
| `MapDefinition` / `RegionDefinition` | §11.3 |
| `NavGridAsset` | Bake edilmiş walkable grid + portal grafı |
| `MapEventDefinition` | §12.1 |
| `LootTableDefinition` / `PickupDefinition` | §13 |
| `InteractableDefinition` | HP, patlama, cooldown, şarj |
| `CameraProfile` / `ControlProfile` | §4 / §3 |
| `QualityPresetDefinition` | §21.9 tablosu |
| `PoolDefinition` | Prefab + tier başına prewarm |
| `SoundDefinition` / `MusicStateDefinition` | §23 |
| `NetworkTuningProfile` | Tick oranları, tier mesafeleri, byte bütçeleri, interp gecikmesi |
| `ThreatCurveDefinition` | Threat seviyesi eşikleri, seviye başına açılan zombi tipleri/modifier'lar |
| `ExtractionRulesDefinition` | Pencere zamanlaması, LZ süresi, savunma süresi, ödül çarpanları, takım düşerse dönüşüm oranı |
| `CharacterDefinition` | Görsel, ses seti, imza perk slotu (base statlar `PlayerDefinition` ile aynı) |
| `PerkDefinition` | Artı/eksi `StatModifier` çifti (net sıfır, ±%10 cap), unlock fiyatı |
| `LoadoutDefinition` | Başlangıç silahı + consumable |
| `CosmeticDefinition` (+ alt tip: Skin, Outfit, WeaponSkin) | Hedef (karakter/silah), materyal/mesh varyantı, unlock koşulu |
| `EmoteDefinition` | Animasyon klibi, ses, süre |
| `BadgeDefinition` / `TitleDefinition` | Kazanım koşulu (istatistik eşiği), ikon, `nameKey` |
| `UnlockDefinition` | Fiyat (Scrap), ön koşul, hedef içerik id'si |
| `LanguageCatalog` | (JSON `languages.json` ile eşlenik; SO değil, bkz. §24A) |

---

## §26. Unity Project Structure

```
lastground/                       (git + Git LFS)
├── docs/                         TDD, ADR'ler, milestone raporları
├── AGENTS.md / CLAUDE.md         AI geliştirme kuralları (M0'da)
├── Packages/  ProjectSettings/
└── Assets/
    ├── Art/            Characters/ Zombies/ Environment/ Props/ VFX/ UI/ Fonts/
    ├── Audio/          SFX/ Music/ Ambience/ Mixers/
    ├── Materials/
    ├── Prefabs/        Player/ Boss/ Weapons/ VFX/ Interactables/ UI/ Systems/
    ├── Scenes/         Boot/ Menu/ Run/ Maps/ Tests/
    ├── ScriptableObjects/  Weapons/ Upgrades/ Zombies/ Director/ Boss/ Maps/ Events/
    │                       Loot/ Audio/ Quality/ Pools/ Network/ Meta/
    ├── Resources/Localization/   languages.json, en/*.json, tr/*.json (runtime yüklemesi için Resources altında)
    ├── Settings/       URP asset'leri (LOW/MED/HIGH), Volume profilleri, Input Actions
    ├── ThirdParty/     Mirror (versiyonu sabitlenmiş)
    ├── Scripts/
    │   ├── Core/       Bootstrap, TickScheduler, RunContext, Session API, Events, RNG, Ids
    │   ├── Player/  Input/  Camera/  Weapons/  Combat/
    │   ├── Zombies/  AI/  Horde/  Boss/
    │   ├── Map/        (ek) Map/Region/NavGrid runtime
    │   ├── Environment/(ek) Etkileşimli prop'lar
    │   ├── Loot/  Upgrades/  Events/
    │   ├── Networking/ Adapters/ Messages/ Serialization/ Discovery/ Lobby/
    │   ├── Rendering/  (ek) VAT render, BloodMap, Corpse, Tracer, Quality
    │   ├── UI/  Save/  Audio/  Pooling/  Utilities/
    │   ├── Meta/       (ek) Kalıcı ilerleme (unlock, kozmetik, perk, badge)
    │   ├── Localization/ (ek) ILocalizationService, LocalizedText
    │   └── Platform/   (ek) Android köprüleri, termal, cihaz tier
    ├── Editor/         VatBaker, NavGridBaker, LightingGridBaker, MinimapBaker,
    │                   ContentIdValidator, BuildScripts
    └── Tests/          EditMode/ PlayMode/ Performance/
```

### 26.1 Assembly Definition'lar ve bağımlılık kuralları
```
LastGround.Core          ← (Unity.Mathematics, Collections, Burst)
LastGround.Data          ← Core                          (SO tanımları)
LastGround.Gameplay      ← Core, Data                    (Player, Weapons, Combat, Zombies, AI, Horde,
                                                          Boss, Map, Environment, Loot, Upgrades, Events)
LastGround.Input         ← Core
LastGround.Rendering     ← Core, Data, Gameplay(read-only)
LastGround.Audio         ← Core, Data
LastGround.UI            ← Core, Data, Gameplay(read-only)
LastGround.Localization ← Core
LastGround.Save / Meta   ← Core, Data
LastGround.Platform      ← Core
LastGround.Networking    ← Core, Data, Gameplay, Platform, Mirror
LastGround.App           ← hepsi (Bootstrap/Installer'lar — composition root)
LastGround.Editor / Tests
```
**Yasaklar:** Gameplay → Mirror ✘, Gameplay → UI ✘, Gameplay → Save ✘. Bu kurallar asmdef referanslarıyla derleyici tarafından zorlanır; AI ajanlarının (Claude/Codex) mimariyi yanlışlıkla bozmasını da engeller.

### 26.2 AI geliştirme kuralları (AGENTS.md'ye taşınacak)
- Dosya başına bir sınıf, hedef ≤ 250–300 satır; namespace = klasör.
- Sihirli sayı yok; balance SO'da, teknik sabitler `NetworkTuningProfile`/`QualityPresetDefinition`'da.
- Public API'de kısa XML özet.
- Her sistem klasöründe kısa `README.md`: sorumluluk, bağımlılıklar, "değiştirirsen etkilenenler".
- Hot path kuralları (§20.3) code review checklist'inde.

### 26.3 Geliştirme ortamı ve OS bağımsızlığı (D-012)
- **Makine:** Lenovo T14 — i5-1135G7 (4 çekirdek / 8 thread), 16 GB RAM, 512 GB SSD, Intel Iris Xe. Editor bu makinede çalışır; sunucu/VPS'te Editor yok. Kurulum rehberi: `SETUP_NEW_MACHINE.md`.
- **OS:** Ubuntu 26.04 LTS (onaylı, D-012). Unity resmî listesinde 22.04/24.04 var; 26.04 için şart **Unity 6.3 LTS ≥ 6000.3.13f1** (libxml2 düzeltmesi). 6.6 serisi kullanılmaz. Editor XWayland üzerinden çalışır.
- **Beklenen build süreleri (hedef, ölçülecek):** ilk IL2CPP Android build 12–20 dk · sonraki build'ler 3–7 dk · script derlemesi (asmdef'ler sayesinde) < 10 sn. Bu yüzden iterasyonun çoğu Editor Play Mode'da, cihaz build'i günde birkaç kez.
- Unity sürümü `ProjectSettings/ProjectVersion.txt` ile sabit; Android SDK/NDK/JDK **Unity Hub'ın kurduğu** sürümler (harici yol ayarı yok). `adb` olarak Unity'nin platform-tools'u kullanılır.
- **Git kuralları:**
  - `.gitignore`: `Library/ Temp/ Obj/ Logs/ UserSettings/ Build*/ *.csproj *.sln .vs/ .idea/`
  - `.gitattributes`: metin dosyalarında `* text=auto eol=lf`; Unity YAML (`*.unity *.prefab *.asset *.mat *.meta`) metin + `merge=unityyamlmerge`; binary'ler (`*.fbx *.png *.psd *.tga *.wav *.ogg *.exr *.ttf`) **Git LFS**.
  - Editor ayarı: Asset Serialization = **Force Text**, Version Control = **Visible Meta Files**.
  - Her asset'in `.meta` dosyası commit edilir; yalnızca büyük/küçük harfle ayrışan dosya adları yasak (Windows/Linux çakışması).
  - Depo: `git@github.com:kcelikk/lastground.git` (**private**; Asset Store içeriği barındırdığı için public olmamalı). Repo kökü = Unity proje kökü.
  - Branch: `main` her zaman derlenen sürüm; milestone başına branch (`m0-foundation`), onay sonrası merge.
- **Kodda OS bağımlılığı yok:** yol birleştirme `Path.Combine`, mutlak yol yok, editör araçları shell script değil C# (`BuildScripts.BuildAndroid()` → `-batchmode -executeMethod`).
- **Otomasyon ayrımı:** Unity arayüzü işleri (sahne, prefab, Inspector, lightmap bake, Hub/lisans) geliştiricide; kod, batchmode derleme/test, APK build, `adb install`, logcat, benchmark CSV toplama Claude Code tarafında.

### 26.4 Hibrit art pipeline ve asset kabul kuralları (D-009)
| Kaynak | Kullanım | Kural |
|---|---|---|
| Asset Store — environment | Modüler endüstriyel kit | `Assets/ThirdParty/<Vendor>/` altına; materyaller bizim `LG/Environment` shader'ımıza dönüştürülür, texture'lar atlas'a toplanır |
| Asset Store — zombie base | 3–4 gövde | **Zombi kabul kontrol listesi** (aşağıda) |
| Asset Store — weapons | 6+ silah | ≤ 3k tris, tek materyal, ortak atlas |
| Mixamo / Asset Store — animasyon | Humanoid klipler → retarget → VAT bake | Root motion kapalı; klip listesi §21.3 |
| Özel + AI destekli — UI | İkon, panel, HUD | Vektör/9-slice; sprite atlas |
| AI destekli — texture/decal/blood/graffiti | Kan splat atlası, grafiti, zemin detayları | Tileable, 1024–2048, atlas'a paketlenir |
| Özel (ileride) — hero karakterler | Oyuncu karakterleri | M-sonrası; placeholder Asset Store/Mixamo |

**Zombi modeli kabul kontrol listesi** (250–300 zombi hedefi için):
- Humanoid rig (Mixamo retarget'e uygun), tek skinned mesh, **tek materyal** (veya birleştirilebilir)
- LOD0 ≤ 2.000 vertex hedefi; daha yüksekse decimation ile LOD0/1/2 üretilebilmeli
- Texture ≤ 1024² (atlas'a girebilir), tint maskesi üretilebilir
- Lisans: ticari mobil oyunda kullanım + ekip içi paylaşım uygun
- VAT bake testi geçmeli (M2 benchmark sahnesinde)

**Lisans ve kaynak kaydı:** `docs/ASSET_SOURCES.md` — her üçüncü taraf/AI asset'i için kaynak, lisans türü, tarih, kullanıldığı yer. Asset Store "editor extension" türündeki araçlar kullanıcı başına lisans gerektirebilir; içerik asset'lerinin ekip içi kullanım koşulları asset bazında kontrol edilir. AI üretim araçlarının ticari kullanım şartları kayda geçirilir. Mixamo içerikleri oyunun içinde kullanılır, ham dosya olarak yeniden dağıtılmaz.

---

## §27. Main C# Classes

(M = ilk görüldüğü milestone, v0.2 planına göre — TDD_03 §36)

**Core**
| Sınıf | Sorumluluk | M |
|---|---|---|
| `AppBootstrap` | Boot sahnesi composition root: save/ayar yükle, kalite uygula, kalıcı servisler | M0 |
| `RunInstaller` | Run sahnesi composition root: role göre sistemleri oluşturup bağlar | M0 |
| `TickScheduler` | Tek `Update`, sabit sim adımı + sıralı fazlar | M0 |
| `ITickable` / `TickPhase` | Faz sözleşmesi | M0 |
| `RunContext` | Rol, seed, oyuncu sayısı, harita, tick | M0 |
| `ISession`, `ICommandSink`, `IGameEventStream<T>` | Network sınırı | M0 |
| `EventChannel<T>` | Allocation'sız struct ring buffer olay kanalı | M0 |
| `DeterministicRandom` | PCG/xorshift struct RNG, alt akışlar | M0 |
| `ZombieHandle`, `PlayerId`, `PickupId`, `PropId` | Kimlik struct'ları | M0 |

**Input / Camera**
| `TouchInputRouter` | Parmak → bölge sahipliği | M1 (basit) / M4 |
| `VirtualJoystick` / `VirtualJoystickView` | Stick mantığı / görseli | M1 (basit) / M4 |
| `PlayerInputFrame`, `IPlayerInputSource`, `LocalTouchInputSource` | Input sözleşmesi | M1 |
| `AimAssist`, `AutoTargetSelector` | Soft aim / auto fire | M4 |
| `TopDownCameraRig`, `CameraShake`, `OccluderFader` | Kamera | M4 |

**Player**
| `PlayerRoot` | Bileşen referansları, kimlik | M1 |
| `PlayerMotor` | Kinematik hareket, grid/collider kayması, zombi yavaşlatması | M1 / M4 |
| `PlayerStats` (`StatBlock`) | Stat hesaplama | M4 / M5 |
| `PlayerHealth` | HP/armor, Alive/Downed/Dead durum makinesi | M4 / M5 |
| `PlayerAnimatorDriver` | Alt gövde hareket / üst gövde nişan blend | M4 |
| `PlayerRegistry` | Oyuncu listesi (AI, director, UI buradan okur) | M1 |
| `ReviveSystem` | Revive ilerlemesi (host) | M5 |

**Weapons / Combat**
| `WeaponController`, `WeaponInstance`, `WeaponStats` | Silah runtime | M4 |
| `HitscanFire`, `PelletFire`, `ShotContext` | Atış modları | M4 / M6 |
| `ShotRng` | Deterministik atış rastgeleliği | M4 |
| `ProjectileSystem` | Struct projectile sim | M6 |
| `HitQuery` | Ray-vs-zombi grid + env LOS | M4 |
| `DamageResolver`, `DamageInfo` | Hasar hesabı (host) | M4 |
| `HitClaimValidator` | Client claim doğrulama (Networking) | M4 |
| `ExplosionSystem`, `StatusEffectSystem` | AoE, burn/slow/stun | M6 |
| `TeamDpsTracker` | Director girdisi | M5 |

**Zombies / AI**
| `ZombieWorld` (+ `ZombieSimData`) | SoA sim sahibi, spawn/despawn, handle | M3 |
| `ZombieSpatialGrid` | Hücre sıralı indeks + sorgular | M3 |
| `ZombieSteeringJob`, `ZombieIntegrateJob`, `ZombieAttackJob`, `AiLodJob` | Burst job'lar | M3 |
| `SurroundSlotSolver` | Çevreleme | M3 |
| `FlowFieldService`, `IntegrationFieldJob` | Oyuncu başına flow field | M3 |
| `NavGrid` (runtime) + `NavGridAsset` | Walkability | M3 |
| `ZombieSpawner` | Director isteklerini materyalize eder | M3 |
| `ZombieReplicaWorld` | Client interpolasyonu | M3 |
| `IZombieRenderSource` | Sim veya replica'dan render verisi | M2 |

**Horde / Boss**
| `HordeDirector`, `DirectorStateMachine`, `IntensityTracker`, `SpawnBudget`, `SpawnLocator`, `HordePatternSelector`, `EliteInjector`, `PerformanceGovernor`, `VirtualHordeSystem` | §9 | M5 |
| `BossController`, `BossPhaseController`, `BossAttackExecutor` (+ GroundSlam, Charge, PropThrow, Summon) | §10 | M8 |

**Map / Environment / Events / Loot / Upgrades**
| `MapLoader`, `RegionTracker`, `SpawnZone`, `EventAnchor` | Harita | M3 / M7 |
| `InteractableRegistry`, `ExplosiveBarrel`, `Barricade`, `TurretDeployable`, `MedicalStation` … | Çevre | M7 |
| `MapEventDirector`, `MapEventInstance` (+ tipler) | Event'ler | M7 |
| `LootService`, `PickupRegistry`, `PickupCollector`, `XpService`, `RunWallet` | Loot | M5 |
| `UpgradeOfferGenerator`, `PlayerBuild`, `LevelUpController`, `CompanionDrone` | Build | M5 |

**Networking**
| `LgNetworkManager` | Mirror NetworkManager alt sınıfı; yalnızca bağlantı yaşam döngüsü | M1 |
| `MirrorSession : ISession` | Session implementasyonu | M1 |
| `PlayerNetworkAdapter` | Oyuncu senkronu | M1 |
| `ZombieSnapshotSender` / `ZombieSnapshotReceiver`, `NetworkRelevance` | §17 | M1 (dummy) / M3 |
| `GameEventReplicator` | Event ↔ mesaj | M1 / M3 |
| `LobbyService`, `JoinHandshake` | Lobby | M1 |
| `ILanDiscovery`, `UdpLanDiscovery`, `AndroidMulticastLock`, `AndroidNetworkInterfaces` | Discovery | M1 |
| `QuantizationSerializers`, `NetStatsMonitor` | Serileştirme, ölçüm | M1 |

**Rendering / UI / Audio / Save / Pooling / Platform / Utilities**
| `ZombieRenderSystem`, `VatClipTable` | Instanced VAT çizim | M2 |
| `CorpseSystem`, `BloodSplatSystem`, `BloodMapPainter`, `TracerSystem`, `DamageNumberSystem`, `PickupRenderSystem` | Presentation | M2 / M11 |
| `QualityService` | Preset uygulama | M2 |
| `HudController` + paneller, `LevelUpPanel`, `MiniMapView`, `TeammateIndicators`, `ReviveProgressView`, `BossHealthBar` | HUD | M4 / M5 |
| `ScreenRouter`, `MainMenuScreen`, `LocalCoopScreen`, `LobbyScreen`, `JoinByIpScreen`, `SettingsScreen`, `ResultsScreen`, `SafeAreaFitter`, `LocalizedText` | Menüler | M0 / M1 / M9 |
| `AudioService`, `HordeAmbienceController`, `MusicController` | Ses | M4 (temel) / M12 |
| `ISaveStore`, `JsonFileSaveStore`, `SaveService`, `SaveMigrator`, `ProfileData`, `SettingsData` | Save | M0 / M9 |
| `ILocalizationService`, `JsonLocalizationService`, `LocalizedText`, `LocalizationValidator` (Editor) | Localization | M0 |
| `MetaProgressionService`, `UnlockService`, `CosmeticLoadout`, `PerkApplier`, `BadgeEvaluator` | Meta | M9 |
| `ThreatTracker`, `ExtractionController`, `HudThreatPanel` | Endless/extraction + HUD | M5 |
| `LoopbackSession` | Framework'süz test session'ı | M1 |
| `PoolService`, `ComponentPool<T>`, `IPoolable`, `RingBuffer<T>` | Pooling | M0 / M2 |
| `DeviceTierDetector`, `ThermalMonitor` | Platform | M2 / M13 |
| `PerfHud`, `PerfBenchmarkRunner`, `DebugCommands`, `Quantization` | Araçlar | M0 / M2 |

---

## §28. Scene Structure

```
Scenes/Boot/Boot.unity          (ilk sahne, çok hafif)
 └─ [App]  AppBootstrap, LgNetworkManager (DontDestroyOnLoad), AudioService(kalıcı), PerfHud(dev)

Scenes/Menu/Menu.unity          (Mirror offline scene)
 ├─ [UI] Canvas_Menu (ScreenRouter: Main, LocalCoop, Lobby, JoinByIp, Settings, Arsenal), EventSystem
 ├─ [Background] Key-art sahnesi veya statik görsel
 └─ [Menu] DiscoveryController, LobbyController

Scenes/Run/Run.unity            (Mirror online scene — sistemler, harita yok)
 ├─ [Systems]   RunInstaller, TickScheduler, PoolService root
 ├─ [Rendering] ZombieRenderSystem, CorpseSystem, BloodSystems, TracerSystem, FX emitters
 ├─ [Audio]     MusicController, HordeAmbienceController
 ├─ [Camera]    CameraRig → Main Camera
 ├─ [Lighting]  Directional Light (ay), Global Volume
 ├─ [UI]        Canvas_HUD_Static, Canvas_HUD_Dynamic, Canvas_Controls, Canvas_Popups, EventSystem
 └─ [Debug]     DebugCommands (dev)

Scenes/Maps/Map_Greybox.unity   (additive)
 ├─ Environment (static)      ├─ Lighting (baked lamba, probe, ışık havuzları)
 ├─ Regions (RegionVolume×N)  ├─ Navigation (NavBakeVolume, NavGridAsset referansı)
 ├─ SpawnZones / EventAnchors / PlayerSpawnPoints
 ├─ Interactables             └─ MapDefinitionBinding

Scenes/Tests/
 ├─ T_Movement.unity        (M4 kontrol hissi)
 ├─ T_ZombieStress.unity    (M2 benchmark: PerfBenchmarkRunner)
 ├─ T_NetLoopback.unity     (M1/M3 editör testleri)
 └─ T_VfxGallery.unity      (M11 efekt bütçeleri)
```

---

## §29. Prefab Structure

```
Player.prefab
 ├─ (root) NetworkIdentity, PlayerNetworkAdapter, PlayerRoot, PlayerMotor, PlayerHealth,
 │         WeaponController, PlayerAnimatorDriver
 ├─ Model            SkinnedMeshRenderer + Animator
 │   └─ WeaponSocket
 ├─ AimIndicator     (zemin nişan çizgisi, local oyuncuda açık)
 ├─ TeamRing         (takım renkli zemin halkası)
 └─ NameTagAnchor

Boss_MutantBrute.prefab   NetworkIdentity, BossNetworkAdapter, BossController | Model (Animator) | WeakPoint
CameraRig.prefab          TopDownCameraRig, CameraShake, OccluderFader | Main Camera
NetworkManager.prefab     LgNetworkManager, KcpTransport, discovery bileşenleri
RunSystems.prefab         (Run sahnesindeki [Systems] grubu — tekrar kullanım için)

Weapons/   W_Pistol, W_SMG, W_AR, W_Shotgun, W_Sniper, W_MG   (yalnızca görsel mesh + socket'ler)
VFX/       FX_BloodBurst_Emitter, FX_Sparks_Emitter, FX_ShellCasing_Emitter,
           FX_Explosion_Small/Large (pool), FX_FireZone, FX_Telegraph_Circle/Line/Cone, FX_AmbientEmbers
Interactables/  ExplosiveBarrel → (variant) FuelTank, Barricade, ElectricTrap, AmmoCrate,
                MedicalStation, AbandonedVehicle, Generator, SupplyCrate (NetworkIdentity), Turret (NetworkIdentity)
UI/        HUD_Canvas, Joystick, ActionButton, UpgradeCard, LobbyEntry, TeammateIndicator, DamageNumberAtlas
```
- **Zombi prefab'ı yoktur** (veri + instanced render). **Pickup prefab'ı yoktur** (instanced).
- Prefab variant'ları: `Player_Base` → ileride `Player_Medic` vb.

---

## §30. Android Permissions / Network Requirements

### 30.1 Manifest izinleri
```
android.permission.INTERNET                   (socket'ler için zorunlu)
android.permission.ACCESS_NETWORK_STATE
android.permission.ACCESS_WIFI_STATE
android.permission.CHANGE_WIFI_MULTICAST_STATE (MulticastLock)
android.permission.VIBRATE                     (haptics)
```
Gerekmeyenler: konum izinleri (SSID okunmayacak), `NEARBY_WIFI_DEVICES` (Wi-Fi Direct yok), Bluetooth, arka plan servisi.

### 30.2 Player Settings (öneri)
| Ayar | Değer |
|---|---|
| Scripting backend | IL2CPP |
| Target architecture | ARM64 |
| Minimum API | 26 (Android 8.0) — M0'da Unity 6.3 LTS gereksinimiyle teyit |
| Target API | Google Play'in yayın anında zorunlu kıldığı en güncel seviye |
| Graphics API | Vulkan, OpenGLES3 |
| Texture compression | ASTC |
| Orientation | Landscape Left + Right (otomatik) |
| Internet Access | Require |
| Optimized Frame Pacing | Açık |
| Render outside safe area | Açık (UI SafeAreaFitter ile) |
| Application entry | GameActivity (Unity 6 varsayılanı) |
| Build | AAB (Play), geliştirme için APK |

### 30.3 Ağ gereksinimleri
- Portlar (yapılandırılabilir): **UDP 7777** oyun (KCP), **UDP 47777** discovery.
- Aynı subnet / broadcast domain; AP/client isolation kapalı.
- Önerilen: 5 GHz Wi-Fi veya host hotspot. Beklenen RTT < 30 ms, kayıp < %2. Oynanabilir sınır ~100 ms / %5.
- Editör/PC ile test ederken PC firewall'unda portlar açılmalı.

### 30.4 Test matrisi (M1, M10, M14)
Router 2.4 GHz · Router 5 GHz · Host hotspot · Üçüncü telefon hotspot · Misafir ağı (beklenen hata mesajı). Eldeki kombinasyon: **OnePlus 5T (Android 10, OxygenOS) ↔ Redmi Pad Pro (Android 14, HyperOS)** — hem eski/yeni Android hem farklı üretici katmanı kapsanıyor; her iki cihaz da sırayla host rolünde denenir. Üçüncü/dördüncü cihaz gerektiren testler (M10) test grubundan ödünç cihazlarla yapılır.

---

## §31. Performance Budget

### 31.1 Referans cihaz sınıfları (D-006)
| Tier | Tanım | Örnek sınıf | FPS hedefi |
|---|---|---|---|
| LOW | Düşük/orta: 4–6 GB RAM, eski Snapdragon (6xx/7xx eski nesil) / MediaTek Helio G serisi; Mali-G52/G57, Adreno 6xx | Galaxy A1x/A2x, Redmi Note 11–13 4G sınıfı | **Minimum 30 FPS** (1% low ≥ 25) |
| MID | Orta: 6–8 GB RAM, Snapdragon 7-serisi / Dimensity 7xxx / Exynos 13xx | Galaxy A5x, Redmi Note 13 Pro sınıfı | **Stabil 45–60 FPS** (hedef 60, termal altında ≥ 45) |
| HIGH | Üst: 8–12+ GB RAM, Snapdragon 8-serisi / Dimensity 9xxx / Tensor | Galaxy S2x, Pixel 8+ sınıfı | 60 FPS |
- Geliştirme tek bir üst seviye telefona göre optimize edilmez; **birincil optimizasyon hedefi MID**, kabul kriteri LOW'da 30 FPS.
- **Eldeki gerçek cihazlar (D-013):**

| Cihaz | Çip / GPU | Ekran | Android | Rol |
|---|---|---|---|---|
| OnePlus 5T | Snapdragon 835 / Adreno 540 | 2160×1080, 18:9 | 10 (API 29) | **LOW referansı** — "≥ 30 FPS" kabul cihazı. Eski GPU sürücüsü nedeniyle Vulkan sorun çıkarırsa bu cihazda GLES3'e düşülür |
| Xiaomi Redmi Pad Pro | Snapdragon 7s Gen 2 / Adreno 710 | 2560×1600, 120 Hz tablet | 14 | **MID referansı** — "45–60 FPS" hedef cihazı. Yüksek çözünürlük nedeniyle render scale kritik; 16:10 tablet en-boy oranını da doğrular |
| HIGH sınıf | — | — | — | **Yok.** M10'a kadar gerekmiyor; gerekirse test grubundan ödünç alınır |
| Emülatör (Windows/Linux) | x86 + ARM çeviri | — | — | Yalnızca UI/akış/localization testi. **Performans ve LAN discovery için geçersiz** (NAT arkasında) |
- Her benchmark raporunda cihaz adı, Android sürümü, ekran çözünürlüğü, render scale ve sıcaklık durumu birlikte yazılır. Eksik sınıf raporda "ölçülmedi" olarak işaretlenir.

### 31.2 Frame bütçesi — MID, 60 FPS (16.67 ms), 4P host, 300 sim / 250 görünür zombi
45 FPS tabanı (22.2 ms) termal durum için pay; LOW'da aynı dağılım 33.3 ms bütçeye ölçeklenir.
| Kalem | Main thread (ms) |
|---|---|
| Input + local oyuncu | 0.3 |
| Zombi sim, 300 zombi (schedule/complete + main kısmı) | 1.2 |
| Director / events / loot | 0.3 |
| Combat çözümleme | 0.4 |
| Network (serialize + deserialize) | 0.6 |
| Zombi render hazırlığı, 250 görünür (culling job + batch doldurma) | 1.0 |
| Animator (4 oyuncu + boss) | 0.5 |
| UI | 0.8 |
| Audio | 0.3 |
| Diğer / engine overhead | 1.0 |
| **Toplam** | **~6.4** (+ ~3 ms pay) |
| Worker thread'ler (Burst job'lar) | ≤ 3 ms toplam |
| Render thread | ≤ 4 ms |
| GPU | ≤ 12 ms |
Client'lar sim/director maliyetini taşımaz (~2 ms daha hafif).

### 31.3 Render sayıları (MID)
| Metrik | Bütçe |
|---|---|
| SetPass calls | ≤ 60 |
| Batches / draw calls | ≤ 200 |
| Görünür üçgen | ≤ 400 k (250 zombi ~175 k LOD karışımı, çevre ~120 k, oyuncu/boss ~30 k, FX/UI kalan). LOW: ≤ 220 k (zombi ~90 k, LOD2 ağırlıklı) |
| Yoğun anda ortalama overdraw | ≤ 2.5× |

### 31.4 Diğer
- GC allocation: savaşta **0 B/frame**; menü/yükleme dışında spike yok.
- Menü → run yükleme: ≤ 10 s (MID).
- Termal: 20 dk run sonunda FPS düşüşü ≤ %15 (Adaptive Performance ile).
- Pil: 20 dk run'da ≤ %12–15 (MID, %50 parlaklık) — hedef, ölçülecek.

---

## §32. Network Bandwidth Budget

### 32.1 Client başına downstream (4P, host 300 zombi, client'ın ilgili kümesi)
| Akış | Hesap | KB/s |
|---|---|---|
| Zombi Tier A | 60 zombi × 15 Hz × 7 B | 6.3 |
| Zombi Tier B | 80 × 6 Hz × 7 B | 3.4 |
| Zombi Tier C | 80 × 2 Hz × 7 B | 1.1 |
| Enter + Death batch | ~15/s × 10 B + 15/s × 6 B | 0.24 |
| Diğer oyuncular | 3 × 20 Hz × 16 B | 0.96 |
| HitFx (başkalarının isabetleri) | ~30/s × 5 B | 0.15 |
| Loot / XP / event / director / boss | | ~0.8 |
| Paket başlıkları (UDP/IP + KCP + batching) | ~35 paket/s × ~40 B | ~1.4 |
| **Toplam (steady)** | | **~14 KB/s (~115 kbps)** |

### 32.2 Limitler
| Metrik | Hedef | Tavan |
|---|---|---|
| Client downstream | ≤ 20 KB/s | 40 KB/s (priority accumulator ile zorlanır) |
| Client upstream | ≤ 3 KB/s | 5 KB/s |
| Host toplam upstream (3 client) | ≤ 60 KB/s | 120 KB/s (~1 Mbps) |
| Paket payload | ≤ 1100 B | fragmentasyon yok |
| Client upstream detay | PlayerStateInput 30 Hz × 20 B = 0.6 KB/s + HitClaim ~10/s × 9 B + başlık ~1.2 KB/s | |
Wi-Fi'da asıl risk bant genişliği değil **airtime ve jitter** → küçük, düzenli, unreliable paketler; burst yok.

---

## §33. Memory Budget

| Kalem | LOW (4–6 GB cihaz) | MID (6–8 GB) |
|---|---|---|
| Texture'lar (çevre atlas, karakter, FX, UI) | 250 MB | 400 MB |
| Mesh'ler | 40 MB | 70 MB |
| VAT / animasyon texture'ları | 20 MB | 40 MB |
| Render target'lar (render scale, bloom, kan haritaları, gölge) | 40 MB | 90 MB |
| Audio | 30 MB | 50 MB |
| Shader / PSO | 30 MB | 50 MB |
| Managed heap | 60 MB | 80 MB |
| Native sim verisi (zombi SoA, grid, 4 flow field) | < 10 MB | < 10 MB |
| Pool'lar (VFX GO, audio, UI) | 30 MB | 50 MB |
| Engine + IL2CPP kod baseline | ~200 MB | ~220 MB |
| **Toplam PSS hedefi** | **≤ 900 MB** | **≤ 1.3 GB** |
- HIGH: ≤ 1.8 GB.
- Android Low Memory Killer riski nedeniyle Memory Profiler snapshot'ı her milestone raporunda.
- İndirme boyutu: base AAB ≤ 150 MB (Play base modül sınırı 200 MB); aşılırsa Play Asset Delivery.
