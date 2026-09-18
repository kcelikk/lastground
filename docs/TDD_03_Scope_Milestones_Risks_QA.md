# LAST GROUND — Technical Design & Development Plan
## Bölüm 3/3 — Kapsam, Milestone'lar, Riskler, Soruların Cevapları (§34–§40, A–J)

> Durum: v0.2 — kararlar işlendi (bkz. `DECISIONS.md`). Milestone sırası D-011 geliştirme önceliğine göre yeniden düzenlendi.

---

## §34. MVP Scope

MVP artık "önce oyun hissi" değil, **"önce en büyük teknik riskler"** sırasıyla kurulur (D-011). MVP = aşağıdaki dört teknik kanıtın tek bir oynanabilir sahnede birleşmesi (M1–M4).

| Kanıt | İçerik | Kabul kriteri |
|---|---|---|
| **1. Networking PoC** | Mirror host/client, LAN discovery (router + hotspot), Join by IP, minimal lobby, 2 oyuncu hareketi, 300 dummy varlıkla snapshot akışı | 2 telefon birbirini buluyor; 300 dummy'de client downstream ≤ 20 KB/s; 10 dk kopmasız |
| **2. Zombie rendering benchmark** | Asset Store zombie + Mixamo animasyon → VAT → instanced render, LOD, kalite preset'leri, benchmark runner | Eldeki tüm cihazlarda 20→300 CSV; MID'de 250 görünür zombide ≥ 45 FPS; LOW'da ≥ 30 FPS'lik cap belirlendi |
| **3. Horde simulation** | ZombieWorld SoA, flow field, surround, AI LOD, host sim + client replikasyonu, greybox harita | Host'ta 300 sim zombi, 2 telefonda iki oyuncuyu kovalıyor, FPS ve bant bütçesi içinde |
| **4. Shooting / combat** | Twin-stick dokunmatik kontroller, kamera, AR hitscan, HitClaim doğrulama, zombi saldırısı, oyuncu HP/ölüm, pooled VFX | **KAPI:** 2 telefon, 250+ zombi, 20 dk savaş: crash yok, desync yok, FPS hedefleri tutuyor |

**MVP'de özellikle yok:** director, XP, upgrade, loot, event, boss, meta, final art. Bunlar kapıdan sonra.

**Tamamlanma tanımı (tümü):** savaşta 0 B/frame GC · hedef FPS'ler (LOW ≥ 30, MID 45–60) · gameplay kodu Mirror'ı referans etmiyor · tüm UI metinleri EN/TR key'lerinden geliyor.

---

## §35. Vertical Slice Scope

Hedef: "sonsuz survival + extraction döngüsünün 20–30 dakikalık cilalı sürümü" — eğlence, performans ve network'ün 2–4 gerçek telefonda birlikte doğrulanması.

| Alan | İçerik |
|---|---|
| Harita | 3 bölge (Industrial Yard, Abandoned Street, Gas Station), Asset Store environment; en az 1 bölge referans atmosferine yakın |
| Mod | Endless survival, Threat I–V, 2 extraction penceresi, HUD: `SURVIVAL · HORDE · THREAT` |
| Silahlar | Pistol, Assault Rifle, Shotgun + Grenade (maç içi geçici geliştirmeler) |
| Zombiler | Walker, Runner, Spitter, Exploder + elite modifier; 250+ görünür hedef |
| Director | Tüm durumlar, desenler, oyuncu sayısı ölçeklemesi, performance governor |
| Build | 25 upgrade, 2 yetenek (Combat Drone, Grenade Pack), kompakt level-up UI |
| Loot | Takıma eşit XP + coin; instanced özel loot, weapon crate, consumable'lar |
| Event | Supply Drop, Rescue Signal + explosive barrel, medical station |
| Boss | Mutant Brute (Slam, Charge, Summon) |
| Co-op | 2–4 oyuncu, downed/revive, emote |
| Meta | Scrap, 2 açılabilir silah, 2 karakter, 3 perk, 2 loadout, birkaç skin/emote/title |
| Ayarlar | Kalite preset'leri, kontroller (auto fire), ses, EN/TR |
| Ses | Çekirdek döngü sesleri + adaptif müzik (Calm/Horde/Boss) |

---

## §36. Milestone Plan (D-011 önceliğine göre yeniden sıralandı)

Her milestone: **DESIGN → IMPLEMENT → COMPILE → TEST → PROFILE → APPROVE**. Onay olmadan sonrakine geçilmez. Süreler kaba tahmindir (1 geliştirici + AI, gerçek cihaz testleri dahil).

| # | Öncelik | Hedef | Ana çıktılar | Çıkış kriteri (ölçülebilir) | Tahmini |
|---|---|---|---|---|---|
| **M0** | Temel | Architecture & Project Foundation | Unity 6.3 LTS projesi, Git + LFS + `.gitattributes`, paketler, Android/URP ayarları (3 kalite asset'i), klasörler + asmdef'ler, `TickScheduler`, `RunContext`, Session API sözleşmeleri, `EventChannel`, `DeterministicRandom`, `PoolService` iskeleti, **Localization (EN/TR JSON)**, Save iskeleti (settings), PerfHud, Boot/Menu sahneleri, `BuildScripts`, AGENTS.md | APK telefonda açılıyor, profiler bağlanıyor, EN↔TR dil değişimi çalışıyor, repo başka bir OS'te clone edilip açılabiliyor | 4–5 gün |
| **M1** | **1 · Networking** | Networking PoC | Mirror entegrasyonu (adapter'lar), `MirrorSession`, `LoopbackSession`, `ILanDiscovery` (MulticastLock + Java arayüz köprüsü + directed broadcast), Join by IP, handshake (sürüm/content hash), minimal lokalize lobby (Create/Find/Join by IP), basit joystick + kapsül oyuncu hareket senkronu, **300 dummy varlık snapshot akışı** (tier'lar, priority bütçesi, interpolasyon), `NetStatsOverlay` | Router **ve** hotspot'ta 2 telefon birbirini buluyor · hareket senkron · 300 dummy'de client downstream ≤ 20 KB/s · 10 dk kopmasız · gecikme/kayıp simülasyonunda interpolasyon düzgün | 6–7 gün |
| **M2** | **2 · Rendering** | Zombie Rendering Benchmark | `VatBaker` (Humanoid klip → VAT), Asset Store zombie (kabul listesiyle) + Mixamo animasyonlar, `ZombieRenderSystem` (RenderMeshInstanced, LOD0/1/2, tint), lighting grid prototipi, ceset + kan L1/L2 basit, `QualityService` + 3 preset, `PerfBenchmarkRunner` (20→50→100→150→200→250→300, 60 s adımlar, CSV), `DeviceTierDetector` taslağı | Eldeki tüm cihazlarda CSV · MID: 250 görünürde ≥ 45 FPS · LOW: ≥ 30 FPS'yi tutan görünür cap belirlendi · bellek bütçe içinde · karar raporu | 5–7 gün |
| **M3** | **3 · Horde** | Horde Simulation (networked) | `ZombieWorld` SoA + Burst job'lar, spatial grid, `NavGridBaker` + flow field, steering + separation, `SurroundSlotSolver`, AI LOD, anti-stuck, VirtualHorde temel, greybox harita (80×80 m + darboğazlar), dummy'lerin yerine gerçek zombi replikasyonu (Enter/Snapshot/Death), `ZombieReplicaWorld`, test spawner | Host'ta 300 sim zombi, iki oyuncuyu çevreliyor, takılma yok · host MID'de ≥ 45 FPS · client downstream ≤ 20 KB/s · 20 dk stabil | 7–9 gün |
| **M4** | **4 · Combat** | Shooting & Combat — **MVP KAPISI** | `TouchInputRouter`, twin-stick joystick'ler, aim assist, Auto Fire ayarı, `TopDownCameraRig`, `WeaponDefinition` (AR), hitscan + `ShotRng`, `HitQuery`, `HitClaimValidator`, `DamageResolver`, zombi windup saldırısı, `PlayerHealth` (Alive/Dead + restart), pooled muzzle/tracer/blood, damage number, temel ses | **KAPI:** 2 telefon (varsa 3–4), 250+ zombi, 20 dk savaş · crash/desync yok · LOW ≥ 30, MID 45–60 FPS · 0 GC/frame · yeni oyuncu 1 dk'da kontrolleri kullanabiliyor | 6–8 gün |
| **M5** | **5 · Loop** | Endless Loop Core | `HordeDirector` (dalgasız), `ThreatTracker`, HUD `SURVIVAL · HORDE · THREAT`, takıma eşit XP + level-up + 12 upgrade (geçici), takım coin, instanced pickup (`ownerMask`), downed/revive, sonuç ekranı (basit) | 3 run'ın yoğunluk grafikleri farklı · level-up co-op'ta oyunu durdurmuyor · XP/coin tüm client'larda eşit | 7–9 gün |
| **M6** | 5 · Loop | Combat Content I | 6 silah + pellet + `ProjectileSystem` (grenade), 2 slot, ammo; Runner, Tank, Spitter, Exploder, elite modifier'lar; status efektleri | Her tip gerçek cihazda bütçe içinde, network'te doğru | 7–9 gün |
| **M7** | 5 · Loop | Map, Events & Environment | Asset Store environment ile 3 bölge (sonra 7), portal grafı, bölge culling, `MinimapBaker`, `LightingGridBaker`, 6 event, çevre etkileşimlileri | Event'ler kampı kırıyor · harita boyunca akış · bellek bütçe içinde | 10–14 gün |
| **M8** | 5 · Loop | Boss & Extraction | Mutant Brute (fazlar, telegraph'lar), `ExtractionController` (periyodik pencereler), Threat ile boss tekrarı, ödül dönüşümü | Boss 2–4P'de adil ve senkron · extract / devam kararı çalışıyor | 7–9 gün |
| — | — | **Vertical Slice kontrol noktası** | §35 | Dış playtest, 2–4 telefon | — |
| **M9** | **6 · Progression** | Meta Progression | Profil save + migrasyon, Scrap, unlock servisi, Arsenal ekranı, karakterler, perk'ler (net sıfır), loadout'lar, skin/outfit/weapon skin, emote (ağ), badge/title | Kalıcı güç artışı olmadığı test ile doğrulanır · save migrasyon testi | 7–9 gün |
| **M10** | 1 · Networking | 4-Player & Host Load | 4 telefon testleri, `PerformanceGovernor` ayarı, relevance/bütçe ayarı, takım UI, bağlantı kopması akışları, test matrisi (§30.4) | 4P + 300 sim zombi, host MID'de bütçe içinde, 30 dk soak | 5–7 gün |
| **M11** | **7 · Polish** | Visual Polish | Referans atmosferi: lightmap bake, ışık havuzları, kan birikim haritası, VFX, UI art (özel + AI) | Referansa yakın görünüm, bütçeler korunmuş | 12–18 gün |
| **M12** | 7 · Polish | Audio | §23 tamamı, adaptif müzik, mixer | Voice cap'leri aşılmıyor | 5–7 gün |
| **M13** | 7 · Polish | Mobile Optimization | Termal kademeler, PSO warmup, BRG değerlendirmesi, bellek, pil | 30 dk soak tüm tier'larda hedefte | 7–10 gün |
| **M14** | Yayın | Release Preparation | AAB, Play Console, Data Safety (veri toplanmıyor), içerik derecelendirmesi, store listing EN/TR, kapalı test | Kapalı test sürümü yayında | 7–10 gün |

**Brief'teki M0–M20 listesiyle eşleşme:** M1 Networking = eski M5 (+ eski M6'nın replikasyon kısmı) · M2 = eski M4 render kısmı · M3 = eski M3 + M4 AI kısmı · M4 = eski M1 + M2 + M6 kapısı · M5 = eski M7 + M10 + M14 · M6 = eski M8 + M9 · M7 = eski M11 + M12 · M8 = eski M15 · M9 = eski M16 · M10 = eski M13 · M11–M14 = eski M17–M20.

**Not:** M1'de kontroller bilinçli olarak basit (tek joystick + kapsül); gerçek twin-stick hissi M4'te. Asset Store zombie paketinin M2 başlangıcına kadar seçilmiş olması gerekir; seçilmezse M2 placeholder humanoid + Mixamo ile yapılır ve paket sonra aynı pipeline'dan geçirilir.

---

## §37. Biggest Technical Risks  &  §38. Solutions

| # | Risk | Etki | Olasılık | Çözüm | Erken tespit |
|---|---|---|---|---|---|
| 1 | Android LAN discovery güvenilmez (OEM broadcast filtresi, hotspot arayüzleri, AP isolation) | Yüksek | Yüksek | Java arayüz listesi + arayüz başına directed broadcast, MulticastLock, Join by IP, hotspot önerisi, ileride QR; test matrisi | **M1** |
| 2 | LOW segmentte 250 görünür zombi 30 FPS'yi tutmuyor | Yüksek | **Yüksek** | LOD2 ağırlıklı render, düşük render scale, rim/gölge kapalı, görünür cap ölçümle belirlenir (gameplay sayısı değişmez, uzak zombiler çizilmez) | **M2** |
| 3 | Host telefonun aşırı yüklenmesi (300 sim + render + 3 client'a gönderim) | Yüksek | Orta | §G stratejileri; PerformanceGovernor; sim/render ayrımı; lobby'de en güçlü cihazı host önerisi | M3, M10 |
| 4 | Asset Store zombie/environment asset'leri mobil bütçeye uymuyor (yüksek poly, çoklu materyal) | Yüksek | Yüksek | §26.4 kabul kontrol listesi, decimation + atlas + shader dönüşümü; satın almadan önce demo/specs kontrolü | M2, M7 |
| 5 | 20–30 dk run'da termal throttling | Orta | Yüksek | MID'de 45 FPS tabanı, 30 FPS modu, Adaptive Performance kademeleri, 30 dk soak | M2, M13 |
| 6 | Kalabalık 2.4 GHz Wi-Fi'da jitter/kayıp | Orta | Orta | Küçük düzenli unreliable paketler, priority accumulator, interp buffer, kayıp simülasyonu | M1 |
| 7 | Client hit claim ↔ host zombi pozisyonu uyuşmazlığı | Orta | Orta | Geniş tolerans; gerekirse host'ta 300 ms pozisyon geçmişi ile hafif rewind | M4 |
| 8 | Network soyutlaması sızıyor → framework değişimi pahalı | Orta | Orta | §15.11 kuralları, asmdef zorlaması, `LoopbackSession` ikinci implementasyonu | M1 |
| 9 | VAT pipeline / Mixamo retarget sorunları | Yüksek | Orta | M2'de önce tek zombi + 3 klip ile uçtan uca; sonra genişlet | M2 |
| 10 | Sürü AI'ı takılıyor / kümeleniyor / eğlenceli değil | Yüksek | Orta | Flow field + debug görselleştirme, SO ile ayar, erken playtest | M3, M5 |
| 11 | Endless modda anlamlı tırmanış ve extraction kararının sıkıcı olması | Orta | Orta | Threat seviyesi başına yeni tehdit tipi, ödül çarpanı görünür, playtest | M5, M8 |
| 12 | Kapsam şişmesi | Yüksek | Yüksek | M4 kapısı, Vertical Slice, §39 yasak listesi | Her milestone |
| 13 | 4–6 GB cihazlarda bellek nedeniyle kapanma | Yüksek | Orta | §33 bütçesi, her milestone Memory Profiler snapshot'ı | M2, M7 |
| 14 | Türkçe kültür hataları (I/İ, ondalık virgül) → id/parse bug'ları yalnızca TR cihazlarda | Orta | **Yüksek** | Invariant/Ordinal kuralı, analyzer/grep kontrolü, TR dilli cihazda test | M0 |
| 15 | Shader derleme / PSO takılmaları | Orta | Yüksek | Variant stripping, ShaderVariantCollection, GraphicsStateCollection warmup | M6, M13 |
| 16 | GC spike'ları | Orta | Orta | Allocation kuralları, PlayMode performans testlerinde GC.Alloc ölçümü | M1+ |
| 17 | Kaotik ekranda UI okunabilirliği ve dokunmatik konfor | Yüksek | Orta | Gerçek telefon UX testleri, boyut/kontrast kuralları, telegraph renk dili | M4, M5 |
| 18 | Oyuncu sayısı ve perk'lerle balance | Orta | Yüksek | SO-tabanlı ayar, net-sıfır perk kuralı, yerel CSV istatistik export'u | M5, M9 |
| 19 | AI destekli geliştirmede (Claude + Codex) mimari erozyonu | Orta | Orta | asmdef sınırları, küçük dosyalar, sistem README'leri, AGENTS.md, `DECISIONS.md`, testler | M0+ |
| 20 | Asset/AI içerik lisans belirsizliği | Orta | Orta | `ASSET_SOURCES.md` kaydı, private repo, ticari kullanım şartlarının arşivlenmesi | M2+ |
| 21 | Windows/Linux arası proje farkları (satır sonu, büyük/küçük harf, yol) | Düşük | Orta | `.gitattributes`, Force Text, OS'siz C# build script'leri, iki OS'te clone testi | M0 |
| 22 | Host uygulamayı arka plana alıyor (arama, bildirim) | Orta | Orta | Zarif bitiş, kısmi ödüller kaydedilir; host migration kapsam dışı | M4 |
| 23 | Burst/IL2CPP Android build sorunları | Orta | Düşük | Her milestone'da tam Android build, build script'i | M0+ |

---

## §39. Features to Avoid in MVP

- Wave sistemi, "kalan zombi" sayacı, dalga arası mola ekranları (kalıcı olarak kapsam dışı — D-003)
- Kalıcı stat/silah seviyesi artışı (kalıcı olarak kapsam dışı — D-005)
- Kill sahipliğine bağlı ödül, paylaşılan pickup'lar için kapışma mekaniği (kalıcı olarak kapsam dışı — D-002)
- Host migration, yeniden bağlanma, oyun ortasında katılma
- İnternet üzerinden oyun, relay, matchmaking, dedicated server, herhangi bir backend
- Bluetooth / Wi-Fi Direct multiplayer
- Zombi başına NetworkObject, zombi başına NavMeshAgent, skinned-mesh zombiler
- Client-side prediction/reconciliation, deterministik lockstep
- Ragdoll, fizik tabanlı ceset, yok edilebilir çevre
- Realtime GI, SSAO, SSR, DOF, motion blur, volumetrik ışık
- Dinamik gün-gece döngüsü, hava durumu
- Addressables / asset streaming, Unity Localization paketi (JSON sistemi yeterli)
- Class sistemi, yetenek ağaçları, crafting, üs inşası, silah eklentileri, envanter grid'i
- Araç sürme, çok katlı binalar
- Sesli/yazılı sohbet
- Reklam SDK'sı, IAP, analytics SDK'sı, cloud save
- EN/TR dışındaki diller
- Özel modellenmiş hero karakterler (placeholder Asset Store/Mixamo)

---

## §40. Recommended First Implementation Step

### 40.1 M0 öncesi hazırlık durumu
| # | Madde | Durum |
|---|---|---|
| 1 | Geliştirme makinesi: **Lenovo T14** (i5-1135G7, 16 GB, 512 GB SSD, Iris Xe) | ✅ Temin edildi, kurulum bekliyor |
| 2 | İşletim sistemi: **Ubuntu 26.04 LTS** — kurulu, onaylandı (D-012; `SETUP_NEW_MACHINE.md` §2) | ✅ Tamam |
| 3 | Unity Hub + Unity 6.3 LTS (≥ 6000.3.13f1) + Android modülleri; Unity hesabıyla tek seferlik giriş | ⏳ Kurulum sırasında |
| 4 | Git + LFS + **private** repo `git@github.com:kcelikk/lastground.git`; SSH key GitHub'a eklenmiş | ⏳ Key üretildi, eklenmeyi bekliyor |
| 5 | Test cihazları: OnePlus 5T (LOW), Redmi Pad Pro (MID) — USB hata ayıklama açık, RSA onayı verilmiş | ⏳ Yeni makinede tekrar onay gerekecek |
| 6 | Aynı Wi-Fi + hotspot açabilen bir cihaz (M1 için) | ✅ Mevcut |
| 7 | M2 öncesi: Mixamo için Adobe hesabı, Asset Store zombie paketi bütçesi/seçimi | ⏳ M1 sonunda ele alınacak |
Kalan tüm sorular: `OPEN_QUESTIONS.md`.

### 40.2 M0 adımları (onay sonrası)
1. Git deposu: `.gitignore`, `.gitattributes` (LF + LFS + UnityYAMLMerge), `docs/` taşınır.
2. Unity 6.3 LTS (≥ 6000.3.13f1) **Universal 3D** şablonu → Android platformu → Player Settings (§30.2) → Force Text + Visible Meta Files.
3. Paketler: URP, Input System, Burst, Collections, Mathematics, TextMeshPro (UGUI), Newtonsoft JSON, Adaptive Performance + Android provider, Memory Profiler, Profile Analyzer, Multiplayer Play Mode; Mirror (`Assets/ThirdParty/Mirror`, sürüm sabit — M1'de bağlanır).
4. Klasör yapısı + asmdef'ler (§26.1) + AGENTS.md (kod kuralları, invariant culture kuralı, dosya boyutu, rapor formatı).
5. Core: `TickScheduler`, `RunContext`, Session API sözleşmeleri (`ISession`, `ICommandSink`, `IGameEventStream<T>`, `INetClock`, `ILanDiscovery`), `EventChannel<T>`, `DeterministicRandom`, `PoolService` iskeleti.
6. Localization: `languages.json`, `en/ui.json`, `tr/ui.json`, `JsonLocalizationService`, `LocalizedText`, `LocalizationValidator`.
7. Save iskeleti: `ISaveStore`, `JsonFileSaveStore`, `SettingsData` (dil + kalite).
8. Boot → Menu sahne akışı; menüde dil değiştirme ve PerfHud.
9. `BuildScripts.BuildAndroidDevelopment()`; telefona yükleme, profiler bağlantısı.
10. M0 raporu (§44 formatı: CREATED FILES, UNITY EDITOR ACTIONS, INSPECTOR CONFIGURATION, ANDROID BUILD STEPS…).

Gerekçe: M1'in (en büyük risk: Android LAN) ihtiyaç duyduğu her şey — derlenen Android pipeline'ı, Session sözleşmeleri, lokalize UI altyapısı — M0'da hazır olur; M1 doğrudan telefon-telefon testine başlar.

---

# Soruların Cevapları

## A) 4 Android cihaz ve 100+ zombi için en mantıklı network mimarisi nedir?

**Host-authoritative listen-server + "zombileri nesne değil veri akışı olarak replike etmek".**

1. **Yıldız topoloji, KCP/UDP.** Host telefon server + client; client'lar yalnızca host ile konuşur.
2. **Oyuncular = 4 NetworkObject** (az sayıda, sorun değil). Hareket client'ta anında uygulanır, host hız/teleport doğrular.
3. **Zombiler = NetworkObject değil.** Host'ta Burst SoA simülasyonu. Client'a üç akış gider:
   - `Enter/Death` (reliable, tick başına batch),
   - `Snapshot` (unreliable, 7 byte/zombi, bit-pack, quantize),
   - `HitFx` (unreliable, görsel).
4. **Client başına ilgi yönetimi + network LOD:** kamera footprint'i 15 Hz, orta mesafe 6 Hz, uzak 2 Hz, 60 m ötesi hiç. Priority accumulator + tick başına byte bütçesi → yük artınca uzak zombiler zarifçe seyrekleşir, sistem çökmez.
5. **Client interpolasyonu** (100 ms buffer, kısa extrapolasyon). Animasyon, kan, ses, ceset client'ta yerel.
6. **Atışlar:** client isabeti kendi gördüğü replica'da tespit eder ve claim gönderir; host doğrular ve hasarı hesaplar. Deterministik atış RNG'si sayesinde crit/spread iki tarafta aynı → anlık, tutarlı damage number.
7. **Uzak sürüler grup olarak** (VirtualHorde) simüle edilir; client'a 1 Hz özet.
8. **Beklenen sonuç:** 300 zombili bir host'tan client başına ~14 KB/s (~115 kbps); host toplam ~0.4–0.5 Mbps.

## B) Hangi sistemleri ilk günden multiplayer-aware tasarlamazsak ileride büyük refactor gerekir?

| Sistem | Tek oyunculu tasarlanırsa ne bozulur | İlk günden kural |
|---|---|---|
| **Kimlikler** | Referanslar (GameObject, index) ağda anlamsız | `PlayerId`, `ZombieHandle`, `PickupId`, `PropId`, SO `id` string'leri |
| **Otorite / durum sahipliği** | "Kim HP'yi değiştirir?" her yere dağılır | Tüm durum değişimleri komut → otoriter sistem yolundan |
| **Input** | Input doğrudan transform'u hareket ettirir | `PlayerInputFrame` → `IPlayerInputSource`; local oyuncu da komut yolundan |
| **Hasar pipeline'ı** | `enemy.hp -= dmg` her yerde | Tek `DamageResolver`, `DamageInfo` struct'ı |
| **Rastgelelik** | `UnityEngine.Random` her yerde → host/client farklı sonuç | `DeterministicRandom` alt akışları, run seed'i |
| **Zaman** | `Time.time` her yerde | `SimTime` / host tick |
| **"Tek oyuncu" varsayımı** | AI, director, kamera, UI `Player.Instance` kullanır | `PlayerRegistry`, N oyuncu; UI "local player view"a bağlanır |
| **Zombi mimarisi** | GameObject + NavMeshAgent → replike edilemez, ölçeklenmez | SoA veri + render ayrımı + sim/replica ayrımı |
| **Event → presentation** | VFX/ses sim kodunun içinde tetiklenir → client'ta çalışmaz | Sim event üretir, presentation event dinler (kaynak sim ya da ağ) |
| **Loot sahipliği** | Tetikleyici collider'da "aldım" | Talep → host karar → claim event |
| **Level-up** | `Time.timeScale = 0` | Pause'suz kuyruk + komut |
| **Revive / downed** | HP=0 → Destroy | Durum makinesi (Alive/Downed/Dead) |
| **Singleton/GameManager** | Rol ayrımı imkânsız | Installer'lı composition root, role göre sistem kümesi |
| **Save** | Run durumu ile kalıcı profil karışır | Run durumu host'ta geçici; profil her cihazda yerel |

## C) Referans görseldeki görüntü kalitesine mobilde yaklaşmak için en iyi optimizasyon hileleri nelerdir?

1. **Karanlık bütçemizdir.** Gece + dar kamera = uzak ve kenar detay görünmez; poligonu merkeze ve ışık havuzlarına harca.
2. **Sabit kamera açısı:** gökyüzü yok, kameraya dönük olmayan yüzler basit, 2D footprint culling, küçük frustum.
3. **VAT + GPU instancing** ile 250–300 zombi; tek materyal, renk paleti + ölçek + zaman offseti ile çeşitlilik.
4. **Lighting Grid texture:** zombiler lamba altında aydınlanır, gerçek ışık yok.
5. **Baked lightmap + sahte ışık havuzları + hacimsel koni kartları** ile sodyum projektör atmosferi.
6. **Muzzle flash = zemin flash decal'i + shader uniform'u** (gerçek ışık yalnız local, HIGH).
7. **Bölge kan birikim RenderTexture'ı:** img-1'deki kanlı savaş alanı tek texture sample ile; cesetler kaybolur, kan kalır.
8. **Cesetler yere batarak kaybolur** (opaque, batching bozulmaz, overdraw yok).
9. **`Emit()` tabanlı paylaşımlı particle sistemleri** (olay başına GameObject yok).
10. **Bloom + LUT tek post-process;** vinyet UI overlay; SSAO yerine baked AO.
11. **Blob gölgeler** zombi batch'inde; gerçek gölge yalnızca oyuncu/boss.
12. **Rim light + emissive loot** → az poligonla güçlü okunabilirlik.
13. **Render scale + MSAA** dengesi (tile-based GPU dostu).
14. **Trim sheet + atlas + vertex color** ile az materyalle zengin endüstriyel çevre.
15. **Yükseklik sisi shader'da**, birkaç büyük duman kartı → atmosfer derinliği.

## D) Zombie AI için NavMeshAgent yerine hangi yaklaşımı öneriyorsun?

**Burst + Jobs ile veri yönelimli "flow field + steering" kalabalık simülasyonu:**
- **Navigasyon:** oyuncu başına (maks 4) pencereli flow field (integration field + gradient). Uzak zombiler için bölge portal grafı. Walkable grid editörde **Unity NavMesh bake'inden örneklenir** (NavMesh bake aracı kullanılır, runtime agent kullanılmaz).
- **Hareket:** flow yönü + separation (spatial hash, 3×3 hücre, maks 8 komşu) + statik grid kayması. Collider, Rigidbody, Physics yok.
- **Sürü hissi:** SurroundSlotSolver (12 sektör kuşatma), daralan halka, darboğaz akışı, oyuncuyu bloklamayan yumuşak itme.
- **AI LOD:** 30 / 15 / 5 Hz tick'ler, bucket'larla yayılmış; 60 m ötesi VirtualHorde grup simülasyonu.
- **Tip davranışları:** job içinde blittable tip parametreleriyle switch.

**Neden NavMeshAgent değil:** ajan başına path ve avoidance maliyeti (kalabalıkta pahalı ve main-thread ağırlıklı), ajan başına GameObject/Transform, ağda veri olarak replike edilemez, yüzlerce ajanın birbirine sıkışması ve "tek sıra kuyruk" davranışı, dinamik LOD zorluğu. Flow field'in maliyeti ise zombi sayısından değil **hedef sayısından** (≤ 4) bağımsızdır — tam bizim durumumuz.

## E) FishNet, Mirror veya Unity Netcode — hangisi ve neden?

**Karar: Mirror — onaylandı (D-001).** M1 Networking PoC'de gerçek cihazlarda doğrulanır.

| Kriter | Mirror | FishNet | Netcode for GameObjects |
|---|---|---|---|
| Android LAN | ✔ KCP, olgun | ✔ Tugboat (LiteNetLib) | ✔ Unity Transport |
| Yerel ağ discovery | ✔ **Hazır NetworkDiscovery** bileşeni (başlangıç noktası) | Çekirdekte yok, topluluk eklentisi | Çekirdekte yok (topluluk örneği) |
| Host-authoritative | ✔ | ✔ | ✔ |
| Yüzlerce AI varlık | Custom message ile ✔ (zaten NetworkObject kullanmıyoruz) | Custom broadcast ile ✔ | Custom messaging ile ✔ |
| Düşük bant genişliği | Batching + bizim quantization | Güçlü paketleme + bizim quantization | NetworkVariable yükü; custom ile ✔ |
| Kolay debug | ✔ Basit mental model, geniş topluluk | Orta: daha fazla soyutlama (tick, prediction, observers) | Orta |
| Unity 6 uyumu | ✔ | ✔ | ✔ (first-party) |
| Mobil performans | İyi | Çok iyi | İyi |
| Bakım kolaylığı | ✔ Kararlı API, uzun geçmiş | Major sürümler arası kırıcı değişiklikler geçmişi | First-party ama ekosistem Unity servislerine (Relay/Lobby/Distributed Authority) yönelik |
| AI (Claude/Codex) ile geliştirme | ✔ En fazla doküman/örnek → daha az hatalı API üretimi | Daha az | Orta |

**Karar gerekçesi:**
1. **En büyük performans kararımız framework'ten bağımsız:** zombiler NetworkObject değil; kendi snapshot/relevance/bütçe sistemimizi custom message'larla yazıyoruz. Bu yüzden FishNet'in nesne başına verimlilik ve gelişmiş observer avantajları bizim için büyük ölçüde geçersiz. Framework'ten istediğimiz: sağlam UDP transport (reliable + unreliable), allocation'sız custom mesaj API'si, oyuncu senkronu, sahne akışı, discovery.
2. **Discovery hazır geliyor** — en riskli Android problemine en hızlı başlangıç (gerekirse `ILanDiscovery` arkasında kendi 150 satırlık UDP implementasyonumuzla değiştirilir).
3. **Debug ve bakım basitliği** + **en geniş bilgi tabanı** → Claude + Codex iş akışında daha doğru kod.
4. **NGO** first-party avantajına rağmen LAN discovery ve interest management'ta ek iş gerektiriyor ve yol haritası online servislerle entegrasyona odaklı; backend'siz LAN oyunu için ekstra değer sağlamıyor.
5. **FishNet güçlü ikinci tercih:** M1'de Mirror'da çözülemeyen bir sorun (ör. transport performansı) çıkarsa geçiş maliyeti, Mirror'un yalnızca `LastGround.Networking` assembly'sinde yaşaması sayesinde ~1–2 hafta ile sınırlı.

## F) İki telefonun aynı Wi-Fi/hotspot'ta birbirini otomatik bulması Android'de nasıl uygulanmalı?

1. Host lobby açıkken UDP 47777'de `DiscoveryResponder` dinler.
2. Client "Find Games" ekranında saniyede bir `{magic, protocolVersion, nonce, time}` isteğini **hem `255.255.255.255`'e hem de her aktif IPv4 arayüzünün directed broadcast adresine** gönderir.
3. Arayüz listesi ve broadcast adresleri **Java API'si** (`java.net.NetworkInterface` → `InterfaceAddress.getBroadcast()`) üzerinden `AndroidJavaClass` köprüsüyle alınır (Android 11+'da .NET arayüz listesi güvenilmez).
4. Host ve client discovery süresince **`WifiManager.MulticastLock`** tutar (`CHANGE_WIFI_MULTICAST_STATE` izni); ekrandan çıkınca bırakır.
5. Host unicast yanıt verir: oturum adı, oyuncu sayısı, port, sürüm, content hash, nonce.
6. Client ping'i nonce ile ölçer, listeyi tekilleştirir, 3 s sessiz olanı düşürür, sürüm uyuşmazsa satırı "Farklı sürüm" olarak gösterir.
7. Yedekler: host ekranında IP göster → **Join by IP**; router isolation durumunda **host telefonun hotspot'u** (yıldız topolojide client↔client trafik olmadığı için hotspot izolasyonu sorun değil); ileride QR.
8. İzinler: `INTERNET`, `ACCESS_NETWORK_STATE`, `ACCESS_WIFI_STATE`, `CHANGE_WIFI_MULTICAST_STATE`. Konum izni gerekmez (SSID okumuyoruz).
9. Tüm bunlar `ILanDiscovery` arkasında → iOS'ta Bonjour/entitlement'lı implementasyon.

## G) 1 host + 3 client yapısında host telefonun aşırı yükünü nasıl engelleriz?

1. **İş dağılımı:** client'lar kendi hareketini, isabet tespitini (raycast'ler), VFX'ini, sesini, cesetlerini, animasyonlarını üstlenir. Host yalnızca doğrular.
2. **Sim, Burst worker thread'lerinde;** main thread'de yalnızca schedule/complete ve event işleme.
3. **Sim ≠ render:** host'un sim zombi sayısı ile host'un **çizdiği** zombi sayısı ayrıdır. Host kendi ekranı için görsel cap'ini ve kalitesini diğer cihazlar gibi yerel olarak düşürür.
4. **Sabit 30 Hz sim tick'i** render FPS'inden bağımsız; LOD bucket'ları maliyeti tick'lere eşit yayar.
5. **Oyuncular dağıldığında** T0 zombi sayısı artar → toplam `maxAlive` oyuncu ölçeklemesiyle ve host cihaz cap'iyle sınırlı; dağınık oyunculara ayrı bütçe değil, paylaşılan global bütçe.
6. **PerformanceGovernor:** host frame time'ı bütçeyi aşarsa director spawn hızını ve maxAlive'ı dinamik olarak %50'ye kadar düşürür (oyuncu fark etmez; sürü biraz seyrekleşir).
7. **Network maliyeti sınırlı:** client başına byte bütçesi, pooled writer'lar, snapshot build Burst job'da.
8. **Lobby'de cihaz tier ikonu + "en güçlü cihaz host olsun" önerisi.**
9. **Termal:** host için Adaptive Performance kademeleri önce görsel kaliteden keser, sim'e en son dokunur.
10. **Ölçüm:** M13'te host rolünde ayrı benchmark (4 cihaz) ve 30 dk soak.

## H) Yüzlerce zombi varken network traffic nasıl kontrol altında tutulmalı?

1. Zombi başına NetworkObject yok; **7 byte'lık bit-pack snapshot**, quantize pozisyon ve yaw.
2. **Client başına ilgi:** yalnızca yakındakiler; 60 m ötesi gönderilmez; mini-map için grup özetleri.
3. **Mesafe tier'ları:** 15 / 6 / 2 Hz.
4. **Priority accumulator + client başına tick byte bütçesi:** tavan aşılmaz, yük artınca uzaklar seyrekleşir.
5. **Bucket rotasyonu:** her tick eşit paket boyu → Wi-Fi jitter'ı azalır.
6. **Event batching:** Enter/Death/HitFx/Pickup tick başına tek mesaj.
7. **Sadece durum, görsel değil:** animasyon, kan, ses, ceset client'ta üretilir.
8. **Deterministik türetme:** atış RNG'si, drone pozisyonu, projectile uçuşu, upgrade → stat hesaplama ağa gitmez.
9. **Mutlak değerler + unreliable kanal:** kayıp paket yeniden gönderilmez, bir sonraki snapshot düzeltir.
10. **Ölçüm:** NetStatsOverlay mesaj tipi başına kbps; §32 bütçeleri milestone çıkış kriteri.

## I) Mobil cihazlarda görsel kalite ile zombi sayısı arasındaki dengeyi nasıl kurmalıyız?

1. **Gameplay sayısı ≠ görsel maliyet.** Gameplay zombi sayısını host belirler ve herkes için aynıdır (hedef 300+ sim). Her cihaz zombi başına görsel maliyeti kendi tier'ına göre düşürür.
2. **Sayı > detay (D-007, D-009).** Hedef 250 görünür zombi. Sürü hissi siluet yoğunluğundan gelir; zombi başına poligon ve shader karmaşıklığı düşük tutulur, kazanılan bütçe sayıya gider. Görsel mükemmellik sonraki aşamada.
3. **Tier'lar arasında önce ne düşer:** kovanlar → kan splat'leri → ceset cap'i → parçacık çarpanı → bloom → gölge → rim light → render scale → LOD0/LOD1 mesafeleri → (en son) görünür zombi cap'i → FPS kilidi. Adaptive Performance ve Ayarlar aynı sırayı izler.
4. **Görünür cap devreye girerse:** kameraya yakın ve tehdit oluşturan zombiler her zaman çizilir; yalnızca footprint kenarındaki uzak zombiler çizilmez. Gameplay değişmez.
5. **Okunabilirlik asla düşmez:** oyuncu, telegraph, pickup, elite vurgusu tüm tier'larda aynı netlikte.
6. **Dinamik LOD bias:** ekrandaki zombi sayısı eşiği geçince LOD0 mesafesi otomatik kısalır.
7. **Hedefler (D-006):** LOW ≥ 30 FPS, MID stabil 45–60, HIGH 60. LOW'da 250 görünürün tutup tutmadığı M2'de ölçülür; tutmazsa cap o ölçümle belirlenir.
8. **Director cihaz bilincine sahip:** host cihazın sim cap'i maxAlive'ı sınırlar; en zayıf client için sim sayısı düşürülmez (onun görsel cap'i devreye girer).
9. **Karar ölçümle verilir:** her preset ve her fiziksel cihaz için 20→300 benchmark CSV'si ve 30 dk termal test.

## J) İlk 30 günlük development plan (D-011 önceliğiyle)

**30. gün hedefi:** M3 çıkışı — iki telefonda host'un simüle ettiği **300 zombi** iki oyuncuyu çevreliyor, render ve network bütçeleri ölçülmüş. Combat kapısı (M4) ~38–40. gün. Onay beklemeleri ve cihaz testleri süreyi uzatabilir; çıkış kriterleri esnetilmez.

**1. Hafta — Temel (M0) + network başlangıcı**
| Gün | İş |
|---|---|
| 1 | Git deposu (.gitignore/.gitattributes/LFS), Unity projesi, Android/URP ayarları, paketler, telefonda boş APK, profiler |
| 2 | Klasörler + asmdef'ler + AGENTS.md; `TickScheduler`, `RunContext`, Session API sözleşmeleri, `EventChannel`, RNG, Pool iskeleti |
| 3 | Localization (EN/TR JSON, servis, `LocalizedText`, validator), Save iskeleti (settings) |
| 4 | Boot/Menu akışı, dil değiştirme, PerfHud, `BuildScripts`, ikinci OS'te clone testi → **M0 raporu / onay** |
| 5–7 | **M1:** Mirror kurulumu, `MirrorSession` + adapter'lar, `LoopbackSession`, handshake; `ILanDiscovery` + MulticastLock + Java arayüz köprüsü |

**2. Hafta — Networking PoC (M1) + render hazırlığı**
| Gün | İş |
|---|---|
| 8 | Lokalize minimal lobby (Create / Find / Join by IP), basit joystick + kapsül hareket senkronu |
| 9 | Router + hotspot testleri (2 telefon), hata düzeltme |
| 10–11 | 300 dummy varlık: Enter/Snapshot/Exit, tier'lar, priority bütçesi, client interpolasyonu, `NetStatsOverlay`; gecikme/kayıp simülasyonu |
| 12 | 10 dk stabilite + bant ölçümü → **M1 raporu / onay** |
| 13–14 | **M2:** `VatBaker` (tek zombie + 3 klip uçtan uca), Mixamo retarget |

**3. Hafta — Zombie Rendering Benchmark (M2)**
| Gün | İş |
|---|---|
| 15–16 | `ZombieRenderSystem` (instanced, LOD0/1/2, tint), lighting grid prototipi |
| 17 | Ceset + kan L1/L2 basit, `QualityService` + 3 preset |
| 18 | `PerfBenchmarkRunner` + stress sahnesi (20→300) |
| 19–20 | Eldeki tüm cihazlarda benchmark, darboğaz düzeltmeleri, görünür cap kararları → **M2 raporu / onay** |
| 21 | **M3:** `ZombieWorld` SoA, spatial grid |

**4. Hafta — Horde Simulation (M3)**
| Gün | İş |
|---|---|
| 22–23 | Steering + separation Burst job'ları, greybox harita, `NavGridBaker` |
| 24–25 | Flow field (oyuncu başına), `SurroundSlotSolver`, AI LOD, anti-stuck |
| 26–27 | Dummy'lerin yerine gerçek zombi replikasyonu, `ZombieReplicaWorld`, test spawner |
| 28–29 | 2 telefonda 300 zombi: FPS, bant, stabilite; düzeltmeler |
| 30 | 20 dk stabilite testi → **M3 raporu / onay** |

---

# Karar Durumu

**Onaylanan kararlar:** `DECISIONS.md` D-001 … D-011 (Mirror, takıma eşit XP/coin, dalgasız HUD, endless + extraction, güç vermeyen meta, cihaz sınıfları, 250/300 zombi hedefleri, masaüstü + Git, hibrit art, EN/TR localization, geliştirme önceliği).

**Benim aldığım ve itiraz edilmezse geçerli olacak küçük tasarım kararları:**
1. Ammo / medkit / grenade de **oyuncu başına instanced** (D-002'nin "kapışma olmasın" amacı için).
2. Takım tamamen düşerse run coin'in %60'ı kalıcı currency'e dönüşür; extract etmek %100 + bonus.
3. İlk açılışta cihaz dili Türkçe ise Türkçe önerilir; varsayılan ve yedek dil İngilizce.
4. Perk'ler "net sıfır" takaslardır (artı + eksi, ±%10 cap), MVP'de tek aktif perk.
5. Localization için Unity Localization paketi yerine hafif JSON sistemi (Addressables bağımlılığı olmadan).

**M0'a başlamadan önce gereken bilgiler ve onaylar:** `OPEN_QUESTIONS.md` bölüm A (OS seçimi, kurulum listesi, git kimliği, repo düzeni, M0 onayı).
