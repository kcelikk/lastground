# M9 Raporu — Meta İlerleme

- **Tarih:** 2026-09-19 · **Branch:** `m9-meta` · **Unity:** 6000.3.24f1 · **Durum:** ✅ Onaylandı (2026-09-19), `main`e merge edildi
- **Cihazlar:** Redmi Pad Pro = MID (USB `368a7a72`) · OnePlus 5T = LOW (USB `3e415066`)
- **Kararlar:** D-005 (kalıcı güç yok), D-022 (VS playtest'i paralel, Mixamo oyuncu karakterleri)

## Çıkış kriterleri (TDD_03 §36 M9)
| Kriter | Sonuç |
|---|---|
| Run sonu kalıcı kazanç | ✅ Tahliye/wipe sonrası Hurda, istatistikler ve rozetler profile yazılıyor. Cihazda solo tahliye: `profile.json` → Scrap 39, rozet `first_extraction`, Runs 3 / Extractions 1 / Wipes 2 / Kills 285 |
| Kalıcı güç yok (D-005) | ✅ Karakterlerde stat alanı yok; perk'ler tek artı + tek eksi, sınırlar içinde; teçhizatlar aynı seviye (test) |
| Unlock + kuşanma | ✅ Hazırlık ekranında 7 sekme; fiyat, ön koşul (teçhizat → silah), unvanlar yalnızca kazanılır (test + Redmi'de elle) |
| Save migrasyonu | ✅ Sürümlü `ProfileMigrator` (adım adım); daha yeni sürümün profili okunur ama üzerine yazılmaz (test) |
| Co-op'ta meta | ✅ Seçim lobby roster'ında 64 bitlik değerle taşınıyor; host ranger, client survivor: iki cihazda doğru gövde/kıyafet, perk'ler build tabanında, emote balonları senkron, 0 exception |
| Cihazda bütçe | ✅ Co-op Redmi 59.8 / OnePlus 30.2 FPS; oyuncu gövdeleri + emote'larla oyun kodu GC **0 B/kare** (OnePlus, 301 kare) |

## Uygulanan
1. **İçerik (`Data/Meta`, `MetaContentBuilder`):** 2 karakter (Ranger varsayılan, Survivor 3000), 7 kıyafet (600–800), 5 perk, 5 silah açılımı (Sniper 1500, Machine Gun 2500), 5 teçhizat (Assault, Breacher, Suppressor, Marksman, Heavy), 3 emote (Wave, Salute 400, Cheer 600), 7 unvan/rozet. Katalog `Resources/Meta/META_Catalog`.
   | Perk | Artı | Eksi | Fiyat |
   |---|---|---|---|
   | Field Medic | +%30 kaldırma hızı | −5 max can | varsayılan |
   | Scavenger | +%40 toplama yarıçapı | −%8 şarjör değiştirme | varsayılan |
   | Grenadier | +1 granat | −%20 yedek mermi | 1000 |
   | Sprinter | +%8 hareket | −%8 hasar azaltma | 1000 |
   | Sharpshooter | +%5 kritik | −%6 atış hızı | 1200 |
2. **Profil (Save):** `profile.json` (`ProfileData`): Hurda, sahip olunanlar, rozetler, kuşanılanlar, karakter başına kıyafet, ömür boyu istatistikler. `ProfileMigrator` JSON ağacı üzerinde sürüm zinciri; yükseltilemeyen/yeni profil salt okunur.
3. **Meta servisleri:** `MetaProfile` (sahiplik, geri düşme), `UnlockService`, `MetaProgressionService` (run coin'i × `ScrapPerCoin` → Hurda; toplam/maksimum istatistikler; rozet bir kez). App'te `MetaService` hepsini bağlar, kaydeder ve lobby seçimini günceller.
4. **Run'a uygulama:** `PlayerMeta` (karakter, kıyafet, perk, teçhizat, unvan, sahip olunan silahlar) roster'da; run başında perk çifti build tabanına (level-up sıfırlamasında kalır), teçhizat başlangıç silahı + granat, düşme havuzu takımın sahip olduğu silahlar. Yeni stat'lar: kaldırma hızı, bonus granat, yedek mermi.
5. **Oyuncu karakterleri (D-022):** Mixamo Swat Guy (Ranger) ve Erika Archer (Survivor) + 9 hareket (nişanlı bekleme, ileri/geri koşu, ateş, darbe, ölüm, 3 emote); kalabalık GPU skinning hattıyla (`PlayerBodyRenderer`): nişana dönük, harekete göre ileri/geri koşu, ateş, düşmüşken sürünme, ölüm; kıyafet = palet tonu (8–11), ayak altında takım rengi halkası. Kapsül görünüm yedek olarak duruyor.
6. **Emote:** sağ üstte emote düğmesi (kuşanılan 3), oyuncunun üstünde balon + animasyon; host indeks ve 2 s bekleme kontrolü (`EmoteSync`).
7. **UI:** Ana menüde "Hazırlık" + Hurda; Hazırlık ekranı (karakter, kıyafet, perk, teçhizat, cephanelik, emote, unvan; perk'lerde artı/eksi; sağda Hurda + istatistikler); lobby'de oyuncu başına karakter/perk/unvan satırı; sonuç ekranında kazanılan Hurda ve yeni rozetler. ~88 yeni key (EN/TR).
8. **Ağ:** `PlayerMetaUpdate` (7), `EmoteRequest` (22), `EmotePlayed` (23), JoinRequest/roster'a meta, RunEnd'e boss öldürme. `NetProtocol.Version` 8.
9. **Dev:** `-lg-character ID`, `-lg-emote-every SEC`.

## CREATED / CHANGED FILES
| Assembly | Dosyalar |
|---|---|
| Core | `NetMsgId` (+7, 22, 23), `NetProtocol` v8, `SessionMessages` (JoinRequest/LobbyMember meta), `LobbyPlayer.Meta`, `SessionConfig.LocalMeta`, `ISession.SetLocalMeta`, NetSession (host meta güncellemesi) |
| Data | `Meta/` MetaItem, CharacterDefinition, OutfitDefinition, PerkDefinition, LoadoutDefinition, WeaponUnlock, EmoteDefinition, TitleDefinition, ProfileStat, StatModifier, MetaCatalog · StatId (+3) · CrowdClipId (+3 emote) |
| Save | ProfileData, ProfileStats, OutfitChoice, ProfileMigrator, SaveService/ISaveService (profil) |
| Meta | MetaProfile, UnlockService, UnlockResult, MetaProgressionService, RunSummary, BankReport, IMetaStore |
| Gameplay | `Meta/` PlayerMeta, MetaApplier · PlayerBuild (perk tabanı), LoadoutAuthority.SetStart, WeaponController (yedek mermi sınırı), PlayerHealthSystem (kaldırma hızı), PlayerEmote + PlayerStateTable.Emotes, RunResult/RunReferee (BossKills) |
| Networking | EmoteSync, RunEndSync |
| Rendering | `Players/PlayerBodyRenderer`, `Crowd/CrowdTints`, LG_CrowdInstanced (16 renkli palet) |
| UI | `Menu/MetaScreen`, MainMenuScreen, LobbyScreen, `Run/EmoteBar`, `Run/EmoteBubbles`, ResultsScreen |
| App | MetaService, AppRoot, RunInstaller(.Meta), DevAutomation |
| Editor | MetaContentBuilder, CrowdBodySource (oyuncu gövdeleri, `MaterialTiles`), BodyTextureBaker, MenuSceneBuilder(.Meta), RunSceneBuilder(.Meta) |
| Tools | `Tools/Mixamo/mixamo_fetch.py` (+2 karakter, +9 hareket) |
| Tests | MetaTests (8), LinkXmlTests, DirectorTests/CombatContentTests güncellemesi |
| İçerik | `Resources/Meta/META_Catalog`, `ScriptableObjects/Meta`, `Art/Crowd/Baked/player_*`, sahneler, EN/TR key'leri, `link.xml` |

## UNITY EDITOR ACTIONS
Yok.

## INSPECTOR CONFIGURATION
Yok (sahne kurucuları atar).

## ANDROID BUILD STEPS
`BuildScripts.BuildAndroidDevelopment`, ~4 dk, APK **90.5 MB** (M8: 89.4 MB; +1 MB iki oyuncu gövdesi).

## TEST RESULTS
- EditMode: **181 / 181** (M8: 173). Yeni: her perk tek artı + tek eksi ve sınırlar içinde; hiçbir unlock perk dışında güç eklemez; `PlayerMeta` paketleme (eski client = meta yok); fiyat/ön koşul/kuşanma; bankalama + rozet bir kez; profil kaydet/yükle; migrator zinciri + yeni profil üzerine yazılmaz; meta roster'da ve lobby'de güncellenir.
- Cihaz: Redmi'de Hazırlık ekranı (sekme, satın alma, kuşanma); co-op ranger (host) + survivor (client) doğru gövde ve kıyafet, emote senkron, 0 exception; solo tahliye sonrası profil bankalandı.

## PROFILE
| Ölçüm | Cihaz | Sonuç |
|---|---|---|
| Co-op (iki oyuncu gövdesi + emote) | Redmi (host) / OnePlus | 59.8 / 30.2 FPS |
| Solo survivor + 3 s'de bir emote, GC yakalama (301 kare) | OnePlus | oyun kodu **0 B/kare** (yalnızca Mirror `OnGUI` ~366 B ve dev telemetrisi, ikisi de release'te yok) |

## DECISIONS / DEVIATIONS
- **Oyuncu gövdeleri kalabalık hattında:** TDD'deki SkinnedMeshRenderer + Animator yerine kemik texture + GPU skinning (D-018/D-021 ile aynı); oyuncu başına tek çizim, 0 GC. Üst/alt gövde ayrımı yok: geri koşarken "backwards" klibi, yan hareket ileri koşu.
- **Kıyafet = ton:** ayrı mesh/kaplama yerine albedo tonu; gerçek kıyafet varyantları ve silah kaplamaları M11.
- **Unvan = rozet:** unvanlar satılmaz, rozet eşiğiyle kazanılır; lobby'de gösterilir.
- **Upgrade zaman aşımı ayarı** (M5'ten M9'a ertelenmişti) yapılmadı → playtest verisiyle M10.
- **Oyuncu itmesi yok** (M8'den): değişmedi.

## OPEN ISSUES
- Fiyatlar ve `ScrapPerCoin` hipotez: ilk tahliyede ~40 Hurda, ilk satın alma ~10 run. Playtest verisiyle ayarlanmalı.
- Oyuncu elinde silah modeli yok (Mixamo karakterleri silahsız; nişan pozu var) → M11.
- M8'den: boss dengesi, kcp2k host ~0.9 KB/kare, dev telemetrisi → M13.
- Vertical Slice playtest formları bekleniyor.
