# M6 Raporu — Combat Content I

- **Tarih:** 2026-09-19 · **Branch:** `m6-combat-content` · **Unity:** 6000.3.24f1 · **Durum:** ✅ Onaylandı (2026-09-19), `main`e merge edildi
- **Cihazlar:** Redmi Pad Pro = MID (host, kablosuz ADB) · OnePlus 5T = LOW (USB) · Linux PC (yalnızca işlev testi)

## Çıkış kriterleri (TDD_03 §36 M6)
| Kriter | Sonuç |
|---|---|
| Her tip gerçek cihazda bütçe içinde | ✅ Tüm tipler açıkken (run saati 7. dakikadan) OnePlus 5T **sabit 30.6 FPS**, 154 zombiye kadar, kare ~33 ms; Redmi 60 FPS. Gerçekçi Mixamo gövdeleriyle (LOD0 ~2000 vertex) de aynı. Oyun kodu GC **0 B/kare** (301 kare, yoğun savaş) |
| Network'te doğru | ✅ Loopback testleri: loadout, client granatı (host doğrulama → iki cihazda aynı uçuş), projectile bitişi, patlama, elite id + durum bayrakları, duyurular. İki telefon co-op (iki kez, son test gerçekçi gövdelerle): 0 hata, tüm tip/elite/granat/patlama olayları iki cihazda, level ve süre iki cihazda aynı (PROFILE) |

## Uygulanan
1. **6 silah (§6.4):** Pistol (sonsuz yedek), SMG, AR (başlangıç), Shotgun (8 saçma), Sniper (delme 4, kamera +%8), Machine Gun (ateşte −%20 hız). `CombatCatalog` hepsini wire id ile indeksler.
2. **2 slot + mermi ekonomisi (§3.5, §6.6):** birincil + tabanca; şarjör + yedek, otomatik reload yedekten, birincil kuruyunca tabancaya geçiş, elle değiştirmede 0.25 s çekme. Mermi kutusu (yedeğin payı), yeni silah dolu gelir.
3. **Granat + `ProjectileSystem` (§6.3):** kapalı form uçuş (başlangıç → bitiş + süre, yay yüksekliği); duvarın önünde durur, iner, 0.3 s fitil → `ExplosionSystem` (düşüşlü alan hasarı, itme, 1.2 s sersemletme). Friendly fire kapalı: oyuncunun granatı yalnızca kendisine %25. Buton: dokun = nişan yönüne 8 m, basılı tut + sürükle = zeminde iniş halkası, bırak = at. Host stok/cooldown/menzil doğrular.
4. **Zombi tipleri (§8.5):** Runner 4–6 m'de sıçrar; Tank itilemez ve kütle ağırlıklı ayrılmayla hafifleri iter; Spitter 8–12 m bandında yana kayar, görüş hattı varsa durup tükürür (hedefi yarım hızla önler, isabette hasar + yavaşlatma); Exploder yakında koşar, fitil yakar (yanıp sönme + bip), patlar — vurularak öldürülürse de patlar, zincirleme patlama. Tip davranışı Burst job'da `ZombieTypeParams` tablosuyla.
5. **Elite modifier'lar (§9.8):** Zırhlı (hasar ×0.6, itilmez), Hızlı (hız ×1.45), Zehirli (vuruşu yavaşlatır, hasar ×1.3), Patlayıcı (ölünce patlar); 3–4× can, renkli parıltı, "ELİT …" duyurusu, her zaman silah düşürür. Tempo: 3. dakikadan, en az 40 s arayla, canlı elite sınırı.
6. **Durum efektleri (§5.6):** yanma (0.5 s darbelerle, öldürme kredisi yakana), yavaşlatma (en güçlü kazanır, %40 taban), sersemletme (windup/sıçrama/tükürük iptali). Kaynaklar: granat, Spitter, Zehirli elite, 3 yeni upgrade (Yakıcı Mermi, Sakatlayıcı Atış, Sersemletici Mermi — toplam 15).
7. **Director spawn deck (§9.6, §2.2):** desen boyutu puan; her spawn tipini kart ağırlığı/maliyet (Walker 1, Runner 2, Exploder 3, Spitter 4, Tank 10), açılma zamanı (Runner/Spitter 3:00, Exploder/Tank 7:00) ve eşzamanlı sınırla seçer; yeni tip ilk görüldüğünde duyuru ("Koşucu görüldü").
8. **Loot:** tip başına XP/coin; mermi, granat, silah drop'ları oyuncu başına instanced (D-002); silah yürüyerek değil "AL" butonuyla alınır.
9. **Ağ:** `LoadoutSync` (host → herkes silah/granat; client → host atış), `CombatFxSync` (projectile spawn/bitiş, patlama), duyurular, oyuncu durumunda elde tutulan slot, vitals'ta yavaşlatma, enter mesajında tip + elite tek baytta, 5 bitlik durum bayrakları. `NetProtocol.Version` 5.
10. **Gerçekçi zombi gövdeleri (D-009, kullanıcı isteği):** Quaternius placeholder'ları yerine `docs/reference/models` paftalarına göre seçilmiş 8 Mixamo karakteri (4 Walker, Runner, Tank = Mutant, Spitter = Parasite, Exploder = Survivor) + 18 hareket. Humanoid hareketler her gövdenin kendi oranına retarget edilerek kemik texture'ına bake edilir; yoğun taramalar LOD öncesi kaynaştırılır (LOD ~2000/820/310); gövde başına albedo (UDIM parçaları birleşik, Mutant'ın bilim-kurgu camgöbeği söndürülmüş). Ham FBX'ler git'te değil (lisans + 235 MB); `Tools/Mixamo/mixamo_fetch.py` yeniden indirir.
11. **Sunum:** tip gövdesi/ölçeği, elite/yanma/fitil/sersemleme parıltısı (instanced `_Glow`), projectile ve patlama efektleri (zemin flaşı, parçacık, kamera sarsıntısı), ağır silah sesi, patlama/tükürük/bip/değiştirme sesleri, HUD: silah adı, şarjör / yedek (∞), değiştirme ve granat butonları, "AL" butonu, duyuru bannerı.
12. **Dev:** `-lg-grenades` (bot kalabalığa granat atar), `-lg-run-time SEC` (geç tipleri hemen açar), telemetride M6 sayaçları, `CrowdPreview` (bake edilmiş gövdelerin CPU-skin önizleme sayfası).

## CREATED / CHANGED FILES
| Assembly | Dosyalar |
|---|---|
| Core | `PlayerInputFrame` (değiştir, granat + hedef), `NetMsgId` (+42–46, 51), `NetProtocol` v5 |
| Data | `Combat/` CombatCatalog, ProjectileDefinition, ProjectileMotion, ExplosionSpec, ExplosionKind, EliteModifierDefinition · `Weapons/` WeaponDefinition (slot, yedek mermi, zoom), WeaponSlot, WeaponTags · `Zombies/` ZombieDefinition (tip bölümleri), ZombieBehaviour · `Director/` SpawnDeckDefinition, SpawnCard · `Crowd/` CrowdTypeLook, CrowdVisualCatalog (TypeLooks), CrowdAnimationSet (Albedo) · StatId (+3), LootDefinition |
| Gameplay | `Zombies/` ZombieWorld (+`.Types`, `.Status`), ZombieSteeringJob, ZombieTypeParams, IExplosionSink, IProjectileLauncher · `Projectiles/` ProjectileTable, ProjectileSystem, ExplosionSystem, ExplosionFx · `Combat/` WeaponController (+`.Slots`), LoadoutTable, LoadoutAuthority, IGrenadeSink, HitClaimValidator, CombatAuthority, RemoteShotEmitter, AimResolver, ShotRng · `Director/` SpawnDeck, DirectorAnnouncement, HordeDirector · `Loot/` PickupRegistry, PickupCollector, PickupType · `Players/` IKillCreditSink, IPlayerDamageSink, PlayerHealthSystem, PlayerStateTable, PlayerMotor · `Crowd/` CrowdState, CrowdReplica, CrowdFlags, CrowdDeath · `Upgrades/` WeaponStats, TeamProgress |
| Networking | CombatFxSync, LoadoutSync, DirectorInfoSync, PlayerSync, PlayerVitalsSync, CrowdReplication{Sender,Receiver}; asmdef + Unity.Mathematics |
| Rendering | `Combat/` ProjectileRenderSystem, ExplosionEffects, GrenadeAimMarker, FxMeshes · `Crowd/` ZombieRenderSystem (+`.Looks`), CorpseBuffer · `Loot/` PickupRenderSystem · TopDownCameraRig · `LG_CrowdInstanced.shader` (_Glow) |
| UI / Input / Audio | WeaponHud, CombatHud, RunStatusHud · TouchTwinStickInput (+`.Buttons`) · CombatAudio, ProceduralSfx, SfxId |
| App | RunInstaller (+Presentation), `Dev/` DevGrenadier, DevAutomation, RunTelemetry |
| Editor | `Setup/` WeaponContentBuilder, ZombieContentBuilder, UpgradeContentBuilder, RunSceneBuilder (+`.Weapons`) · `Crowd/` CrowdBaker, CrowdBodySource, CrowdCatalogBuilder, MixamoImport, PoseSampler, BodyTextureBaker, CrowdPreview |
| İçerik | `ScriptableObjects/{Weapons,Zombies,Combat,Director,Upgrades}` (5 silah, 4 zombi, 2 projectile, 4 elite, katalog, spawn deck, 3 upgrade), `Art/Crowd/Baked` (8 gövde + albedo, 36 MB), 5 materyal, EN/TR key'leri |
| Araç | `Tools/Mixamo/mixamo_fetch.py`, `.gitignore` (ham Mixamo) |
| Testler | CombatContentTests (4), ZombieTypeTests (8), ProjectileTests (4), WeaponLoadoutTests (5), CombatContentNetworkTests (2), DirectorTests +1, ReplicationTests +1 |

## UNITY EDITOR ACTIONS
Yok. Asset'ler, sahne ve gövdeler batch komutlarıyla üretildi. Gövdeleri yeniden bake etmek için önce `MIXAMO_TOKEN=… python3 Tools/Mixamo/mixamo_fetch.py`, sonra `CrowdBaker.BakeAllBatch`.

## INSPECTOR CONFIGURATION
Yok. Denge değerleri `Assets/ScriptableObjects/*` altında (builder mevcut değerlere dokunmaz). Tip görünümü `Art/Crowd/CrowdCatalog.asset → TypeLooks` (bake sırasında yeniden yazılır).

## ANDROID BUILD STEPS
`BuildScripts.BuildAndroidDevelopment`, artımlı ~1–1.5 dk, APK **80.7 MB** (M5: 66 MB; +14 MB gövde albedo'ları ve kemik texture'ları).

## TEST RESULTS
EditMode **154 / 154** (M5'e göre +25). Öne çıkanlar: Runner sıçraması, Tank'ın itilmemesi ve Walker'ı itmesi, Spitter'ın bandı koruyup tükürmesi, Exploder fitili ve vurulunca patlaması (öldürme kredisiyle), yanma/sersemletme/yavaşlatma, elite çarpanları ve Patlayıcı elite, granatın hedefe inip yalnızca atanı yaralaması ve duvarda durması, tükürüğün oyuncuyu yavaşlatması ve duvarda durması, zincirleme patlama, 10 dakikalık başsız director ile tip açılma zamanları ve elite temposu, iki slot + yedek + tabancaya geçiş, taşınmayan silahla claim reddi (değişim toleransıyla), on-hit upgrade'ler, loadout/granat/patlama/duyuru loopback replikasyonu, tip + elite + bayrak replikasyonu.

## PROFILE
| Test | Cihaz | FPS | En kötü kare | Not |
|---|---|---|---|---|
| Solo 130 s, tüm tipler, Mixamo gövdeleri | OnePlus (LOW) | **30.6** sabit | 33 ms (açılışta tek 263 ms) | 0 hata, 154 zombiye kadar, 81 öldürme |
| Solo, Mixamo gövdeleri | Redmi (MID) | **60** | 16.7 ms | 0 hata; ekran görüntüsüyle görünüm kontrolü |
| Solo 130 s, tüm tipler (placeholder gövde) | OnePlus (LOW) | 30.6 | 33 ms | 18 sıçrama, 9 tükürük, 2 patlayan Exploder, 4 granat, 8 patlama |
| **İki telefon 5 dk, Mixamo gövdeleri (Redmi host, OnePlus client)** | Redmi / OnePlus | **60.0** (min 57.8) / **30.6** (min 29.4) | 183 / 229 ms (tek) | 0 hata, client 5.7 KB/s, RTT 32 ms, 179 öldürme, 20 sıçrama, 40 tükürük, 3 elite, 5 granat, 5 patlama; iki cihazda aynı level (6) |
| İki telefon 5 dk, placeholder gövde (Redmi host, OnePlus client) | Redmi / OnePlus | 60.0 (min 58.4) / 30.6 (min 28.9) | 117 / 180 ms (tek) | 0 hata, client 10 KB/s, RTT 34 ms, 189 öldürme, 20 sıçrama, 18 tükürük, 4 Exploder, 2 elite, 4 granat, 15 patlama |
| GC yakalama (301 kare, yoğun savaş) | OnePlus | | | **oyun kodu 0 B/kare**; yalnızca dev telemetrisi (release'te yok) ve tek seferlik level-up paneli açılışı (TMP) |

## DECISIONS / DEVIATIONS
| Konu | TDD | Uygulanan | Gerekçe |
|---|---|---|---|
| Zombi asset'i (D-009, D1) | Asset Store zombie | **Mixamo** (ücretsiz, oyuna gömülü ticari kullanım) — 8 gerçekçi karakter | Kullanıcının "referans görsellere uygun, gerçekçi" isteği; bütçe sorusu (D1) açık |
| Ham Mixamo FBX | — | Git dışında, indirme scripti repoda | Kullanıcı kararı: lisans (tek başına dağıtım yasak) + 235 MB |
| Zombi animasyonu | Tip başına klip seti | Ortak zombi seti + Mutant seti, Humanoid retarget | 18 hareketle 8 gövde; Spitter'ın saldırı klibi = çığlık (tükürük telegraph'ı) |
| Silah edinme | Sandık / 2 seçenek (§13.1) | Başlangıç AR + Pistol; drop (elite her zaman) ile birincil değişir | Kullanıcı seçimi; harita sandıkları M7 |
| Silah slotları | Genel 2 slot | Birincil + tabanca (pickup birincili değiştirir) | Sonsuz tabanca her zaman yedek; basit |
| Granat tahmini | — | Client atışı host'a gider, uçuş host'tan gelir (~RTT/2 gecikme) | LAN'da 10–40 ms; yanlış tahmin riski yok |
| Mermi doğrulaması | Validator mermiyi sayar | Host atış hızını ve taşınan silahı doğrular, mermiyi değil | LAN co-op; atıcı cihaz mermiyi tutar |
| Anti-kamp | Director (§9.8) | Yok | Bölge verisi gerekir (M7) |
| Durum efekti kaynakları | Silah modifier'ları | 3 on-hit upgrade + granat + Spitter + Zehirli elite | Mevcut 6 silahta doğal yanma/yavaşlatma yok |
| Tip görünümü | Tip başına model | Gövde listesi + ölçek + hafif sabit parıltı (Spitter yeşil, Exploder turuncu) | Okunabilirlik (§0.6); nihai görsel cila M11 |

## OPEN ISSUES
1. **Denge:** mermi, granat, elite ve spawn deck değerleri hipotez; botlar geç oyunda (7. dakikadan başlatılınca) ~75–90 s'de düşüyor. İnsan playtest'i gerekli.
2. **Görsel:** tablette (16:10) üstteki "HAYATTA KALMA" satırı can barının altında kalıyor; coin'ler büyük; oyuncu hâlâ kapsül (operatör modeli M9/M11); zombiler için normal map yok (shader yalnızca albedo).
3. Walker animasyon çeşitliliği 2 yürüyüş; ek varyant (topallama, sürünerek yürüme) ve Runner sıçrama klibi (`jump attack`) sonraki içerikte.
4. Tabanca dahil tüm silahlar aynı prosedürel ses ailesini kullanıyor (M12).
5. Önceki milestone'lardan: Mirror/kcp2k client ~27 B/f, hotspot testi (B5), Asset Store bütçesi (D1), dev telemetrisinin 5 s'lik ayırmaları.
6. `ProjectSettings` (MSAA 2x, ışık ayarları): grafikli editör önizlemesi sırasında Unity yazdı — kullanıcı onayıyla tutuldu.
7. Sonraki: **M7 — Map, Events & Environment** (`docs/reference/maps` konseptleri: sanayi bölgesi, dökümhane, benzin istasyonu, hastane + tahliye).
