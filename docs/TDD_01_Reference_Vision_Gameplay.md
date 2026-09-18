# LAST GROUND — Technical Design & Development Plan
## Bölüm 1/3 — Referans Analizi, Vizyon, Gameplay Mimarileri (§0–§14)

> Durum: v0.2 — kararlar işlendi (bkz. `DECISIONS.md`, çelişkide o geçerlidir). Kod yazılmadı.
> Terimler: "Host" = oyunu açan telefon (server + client). "Sim" = host'taki otoriter simülasyon. "Replica" = client'taki görsel kopya.
> Bu dokümandaki tüm sayısal balance değerleri **başlangıç hipotezidir**; M2/M4/M10 ölçümleri ve playtest ile değişecektir.

---

## §0. Referans Görsel Analizi

İncelenen dosyalar:
- `docs/reference/lastground-img-1.png` — tek kare, "massive horde" tepe anı (1536×1024).
- `docs/reference/lastground-img.png` — 10 panelli akış board'u: Ana Menü, Oyun İçi, Lobby, Level Up, Envanter, Harita, Boss, Revive, Extraction, Ayarlar.
- `docs/reference/lastground-board-2.png` — 10 panel (EN): ana menü, oyun içi, 4 kişilik co-op, level up, envanter/karakter (loadout), harita (Industrial Zone), silah seçimi, boss (The Brute), revive (downed), extraction. *(2026-09-18, §0.12)*
- `docs/reference/lastground-board-3.png` — 10 panel (TR): ana menü, karakter/silah seçimi, local co-op lobi, erken oyun, yoğun horde, level up, boss (Mutant Behemoth), harita/görevler, envanter, extraction. *(2026-09-18, §0.12)*

### 0.1 Kamera

| Ölçüt | img-1 (peak horde) | Panel 2 (oyun içi) | Last Ground hedefi |
|---|---|---|---|
| Pitch (yere bakış açısı) | ~55–60° | ~48–52° | **55°** varsayılan (profil ile 50–60°) |
| Yaw | Dünya eksenine hizalı (~0°): duvarlar ekran yatayına paralel | Hafif çapraz (~10–15°) | **0° sabit, kamera dönmez** (twin-stick yön eşlemesi için şart) |
| Projeksiyon | Perspektif, dar FOV (dikey çizgiler az yakınsıyor → tele his) | Perspektif, biraz daha geniş FOV | Perspektif, dikey FOV **~35°** |
| Kamera mesafesi (tahmini) | ~24–28 m | ~16–18 m | **20–26 m**, duruma göre dinamik |
| Görünür zemin alanı | ~38 × 26 m | ~20 × 13 m | 20:9 telefonda **~34 × 15 m** varsayılan, peak'te ~40 × 18 m |
| Karakterin ekran yüksekliği | ~%8 | ~%13–15 | **%9–11** (1080p yükseklikte ~100–120 px) |
| Oyuncu ekran konumu | Tam merkez | Merkez, hafif alt | Merkez + nişan yönüne 2–3 m look-ahead |

Çıkarım: img-1 "çok zombi görünsün" kadrajı, Panel 2 "karakter okunaklı olsun" kadrajı. Telefon ekranı (6–6.7") için ikisinin arası doğru: sürüyü görecek kadar geniş, silueti okuyacak kadar yakın. Sabit açı + sabit yaw bize büyük performans avantajı verir: frustum küçük, gökyüzü/ufuk hiç görünmez, kameradan görünmeyen yüzeyler modellenmeyebilir, host her oyuncunun ekran ayak izini (camera footprint) matematiksel olarak hesaplayabilir (ekran dışı spawn testi için).

### 0.2 Karakter / Çevre Ölçeği
- Zombi boyu ≈ oyuncu boyu (~1.8 m). Tank/Brute ~1.4–1.8×. Boss (Panel 7 "Mutant Brute") ~3.5× oyuncu.
- Sandık ~1 m, varil ~1 m, askeri araç ~5 m, ışık kuleleri 8–10 m (üst kısımları kadrajdan taşıyor → kule tepeleri düşük detay olabilir).
- img-1 merkezdeki açık alan çapı ~20 m, zemindeki boyalı daire ~14 m.
- **Level design metriği:** combat alanları ≥ 25–40 m açık alan, geçitler 6–12 m, kenarlarda prop yoğunluğu.

### 0.3 Zombi Yoğunluğu
- img-1: kadrajda **~170–220 zombi**. Oyuncu çevresinde **6–9 m yarıçaplı "nefes alanı"**, dışında yoğun bir halka. Sürü çevreliyor ama oyuncunun manevra alanı var.
- Panel 2: **~50–60 zombi**, oyuncu kalabalığın içinde, 2–4 m temas mesafesi.
- Uzaktaki zombiler karanlıkta neredeyse siluet → uzak LOD'larda detay gereksiz.
- **Tasarım çıkarımı:** "Halka + boşluk" kompozisyonu hem görsel hem gameplay olarak doğru. AI'da `SurroundRing` parametresi olarak modellenecek (§8). img-1 yoğunluğu run'ın son 5 dakikasının tepe anıdır; ortalama an değildir.
- **Hedef (D-007):** 250 görünür zombi stress-test hedefi, 300+ simüle zombi mimari hedefi. Tier'lar arası fark zombi *sayısında* değil, zombi başına görsel maliyette (LOD mesafesi, gölge, shading) olacak. Düşük segmentte gerekirse görünür cap düşürülür; karar benchmark ölçümüyle verilir.

### 0.4 Işık Stili
- Gece. Ana ton koyu, soğuk-nötr asfalt; düşük ambient.
- Sıcak sodyum projektörler (~2700–3200 K). img-1'de köşelerde 6–7 ışık kaynağı; aydınlık havuzları oynanış alanını çerçeveliyor.
- Yangın/ateş turuncu nokta ışıklar (Panel 2 ve 7).
- **Muzzle flash sahnenin en parlak noktası** ve oyuncuyu "key light" gibi aydınlatıyor → oyuncu her zaman görsel odak.
- Soğuk mavi vurgu: silah sandığı (img-1), extraction pisti (Panel 9).
- Yüksek kontrast, belirgin vinyet, hafif bloom, havada kıvılcım/kül, duman/sis hissi (Panel 1, 7).
- Projektörlerden uzun dinamik gölgeler (Panel 9) → mobilde pahalı, bake edilmeli.

### 0.5 Çevre Kompozisyonu
- Beton duvar + grafiti ("LAST GROUND / SURVIVE UPGRADE REPEAT"), tel örgü, ışık kuleleri, askeri araç, konteyner/sandık, varil, rögar kapağı, zemin boyaları, moloz.
- **Merkez açık, kenarlar yoğun prop.** Mobil için ideal: detay kadraj kenarında ve statik (batch'lenebilir), dinamik kalabalık merkezde düz zeminde.
- Zemin tek seviyeli, düz. Çok katlı yapı yok → 2D (XZ) navigasyon yeterli.

### 0.6 Combat Okunabilirliği
Güçlü yanlar:
- Oyuncu = en parlak ve merkezdeki öğe.
- Loot doygun renklerle ayrışıyor: altın coin, yeşil "XP", kırmızı-beyaz medkit, mavi parlayan silah sandığı, sarı zemin çerçeveleri.

Zayıf yanlar (mobilde düzeltilmeli):
- Zombiler ile zemin aynı değer aralığında (koyu kırmızı üstüne koyu kırmızı). Telefonda, güneş altında, 150+ zombide gürültüye dönüşür.
- Düşman saldırı telegraph'ı (uyarı göstergesi) yok.
- Takım arkadaşları birbirine çok benziyor (Panel 7'de 4 asker ayırt edilemiyor).
- Kan, zemin rengini neredeyse tamamen boğuyor → pickup ve telegraph okunurluğunu düşürür.

Çözümler: zombilerde hafif rim/fresnel ışığı; elite'lerde emissive göz/renk; oyuncu altında takım rengi halka + isim; tutarlı tehlike renk dili (turuncu-kırmızı zemin telegraph'ı); kan değeri zeminden koyu ama doygunluğu düşük; loot unlit/emissive.

### 0.7 Kan / Ceset Sunumu
- img-1: ~25–35 ceset; oyuncu çevresindeki 15 m'lik alanın ~%35–45'i kanla kaplı. Sıçrama + havuz + sürüklenme izleri.
- Panel 8 (revive): yakın plan kan havuzu.
- **Çıkarım:** "Savaş alanı birikiyor" hissi şart. Ama yüzlerce ayrı decal ve kalıcı ceset ile değil; **bölge bazlı kan birikim RenderTexture'ı + kısa ömürlü ceset** ile yapılacak (§21.5–21.6). Ceset yok olsa bile kan izi kalır → kalıcı carnage illüzyonu.

### 0.8 VFX Yoğunluğu
Muzzle flash, ince sıcak tracer, kıvılcım, zemin ateşi (Panel 2), patlama (Panel 7), duman, havada kül. Referans hepsini aynı anda gösteriyor. Mobil bütçe: eşzamanlı ≤ 30 aktif emitter, `Emit()` tabanlı paylaşımlı particle sistemleri, overdraw kontrolü.

### 0.9 UI Stili (panel panel)
Genel: askeri/endüstriyel, koyu yarı saydam paneller, ince kenarlıklar, dar (condensed) büyük harf sans-serif. Renk dili: kırmızı = HP/tehlike, turuncu = XP/seviye, altın = para, mavi = seçili/rare, yeşil = iyi ping.

| Panel | İçerik | Mobil notu |
|---|---|---|
| 1 Ana Menü | Sol dikey buton listesi, büyük logo, key-art arka plan | Butonlar ≥ 56dp yükseklik |
| 2 Oyun İçi | Sol üst: portre + HP + XP + LV; altında silah+mermi, granat, medkit. Üst orta: WAVE + kalan zombi. Sağ üst: radar mini-map. Sol alt: joystick. Sağ alt: ateş/nişan, granat, yetenek, yakın dövüş | Sağ joystick yok → bizde sağ alt = aim stick |
| 3 Lobby | FIND GAMES / JOIN BY IP sekmeleri, yenile, satır: isim – x/4 – ping – JOIN | Brief ile birebir uyumlu |
| 4 Level Up | 3 kart, ikon + değer + rarity, seçili kart mavi çerçeve | Tam ekran versiyon multiplayer'da kullanılmayacak |
| 5 Envanter | Sekmeler (Silahlar/Ekipman/Yükseltmeler/Karakter/Kostümler), silah stat tablosu, "YÜKSELT 600" | Kalıcı stat yükseltme → §37 ile çelişiyor |
| 6 Harita | Bölge noktaları, "Önerilen seviye 5+", BÖLGEYE GİT | Run içi tehlike seviyesi olarak yorumlanmalı |
| 7 Boss | Üst orta isim + HP bar | Uygun |
| 8 Revive | İsim etiketi, kırmızı artı, "CANLANDIRILIYOR…" + progress + saniye | Uygun |
| 9 Extraction | Üst orta geri sayım, helikopter | Uygun |
| 10 Ayarlar | Grafik Düşük/Orta/Yüksek, Gölge, Efektler, Ceset Sayısı, Kan Decal, FPS 30/60; Kontroller, Ses, Oyun, Dil | Brief §30 preset'leriyle birebir uyumlu |

Board'daki UI **Türkçe** → lokalizasyon (TR/EN) ilk günden string key tabanlı olmalı. Font Türkçe glifleri (ş, ğ, ı, İ) desteklemeli (örn. OFL lisanslı condensed bir font).

### 0.10 Referans ↔ Brief Çelişkileri (karar önerileri)
> 2026-09-18: board-2 ve board-3'te de "WAVE / ZOMBIES REMAINING" ve envanterde "YÜKSELT" görünüyor. Karar değişmedi: **D-003 ve D-005 geçerli.** Görev sayaçları eklendi (**D-019**, §0.12, §12.4).

| Referansta | Brief | Öneri |
|---|---|---|
| "WAVE 7 / 54 ZOMBIES REMAINING" | Dalga değil, Horde Director | **KARAR D-003:** `SURVIVAL 08:42 · HORDE: HIGH · THREAT III` (§9.11). Kalan zombi sayacı hiçbir yerde yok |
| Radar mini-map'te her zombi kırmızı nokta | Her zombi gösterilmez | 12–16 sektörlü **yön/şiddet halkası** |
| img-1 sağ alt silah mağazası paneli, sol alt logo | Minimal mobil HUD | HUD'da yok; menüye ait |
| Panel 5 kalıcı silah seviyesi "YÜKSELT" | Co-op balance'ı bozan kalıcı güç yok | **KARAR D-005:** Envanter ekranı "Arsenal" olur: silah **açma** + weapon skin. Stat yükseltme butonu yok (§14.7) |
| Panel 6 "Önerilen seviye" | Hesap seviyesi yok | Run içi bölge tehlike seviyesi |
| Panel 2 sağ altta tek ateş butonu | Twin-stick sağ stick | Sağ alt bölge = aim stick; aksiyon butonları stick etrafında yay şeklinde |
| Panel 4 tam ekran Level Up | Ekranı kapatmamalı | img-1'deki **kompakt alt-orta** versiyon kullanılacak |

### 0.11 Mobil Uygulanabilirlik Tablosu

| Öğe | Doğrudan gerçek zamanlı | Optimize edilerek | İllüzyonla taklit |
|---|---|---|---|
| Sabit izometrik kamera, dar FOV | ✔ (performans avantajı) | | |
| 250 görünür / 300+ simüle zombi | | ✔ VAT + GPU instancing + AI LOD | Uzak kalabalık siluet/LOD2 |
| Animasyon çeşitliliği | | ✔ 4 gövde × renk paleti × VAT zaman offseti × ölçek | |
| Çok sayıda sıcak projektör | | | ✔ Baked lightmap + zeminde additive "ışık havuzu" + hacimsel koni kartları |
| Projektör gölgeleri | | | ✔ Baked; dinamikler için blob gölge |
| Muzzle flash çevreyi aydınlatıyor | | Tek gerçek point light (yalnız local oyuncu, HIGH) | ✔ Zemine additive flash decal + karakter shader "flash" uniform'u |
| Zombilerin lamba altında aydınlanması | | | ✔ Top-down "lighting grid" texture'ı (zemin ışık haritası) VAT shader'da örneklenir |
| Yoğun kan | | Instanced splat quad'lar (cap'li) | ✔ Bölge bazlı kan birikim RenderTexture'ı (tek sample) |
| Cesetler | | ✔ Donmuş VAT kare, cap + yere batarak kaybolma | Kan izi kalır |
| Yangın / duman | | ✔ Flipbook atlas, düşük parçacık | Zeminde ısı parıltısı quad'ı |
| Havada kül/kıvılcım | ✔ Kameraya bağlı tek ucuz sistem | | |
| Bloom | | ✔ MED/HIGH düşük iterasyon | LOW: emissive + glow sprite |
| Sis / hacimsel duman | | | ✔ Shader yükseklik sisi + birkaç büyük yumuşak kart |
| Vinyet + color grading | LUT ✔ | | Vinyet = UI overlay image (post-process değil) |
| SSAO / DOF / motion blur | ✘ kullanılmayacak | | AO lightmap'e bake |
| Boss detayı | ✔ tek skinned mesh, poly bütçesi ona ayrılır | | |
| Helikopter extraction | ✔ scripted animasyon | | Rotor blur kartı |
| Zemin çatlak/çizgileri | | ✔ Trim sheet + detail texture + vertex color | Decal yerine mesh'e bake |

### 0.12 Ek board'lardan (board-2, board-3) öğrenilenler
| Konu | Referans | Uygulama |
|---|---|---|
| Takım renkleri | P1 mavi, P2 yeşil, P3 sarı, P4 mor halka + isim + isim etiketi | `PlayerViews` slot renkleri aynı sırada (M1'den beri) |
| Takım listesi | Sol üstte P1–P4 satırları: isim + HP çubuğu | §14.3 takım listesi, M5 |
| HUD düzeni | Sol üst portre + HP + LV, sağ üst radar, sol alt joystick, sağ alt nişan/ateş + bomba + yetenek, alt sağ mermi `22/120` | §3, M4 |
| **Görev paneli** | "Bölgeyi temizle — Zombileri öldür: 12/50", "Aktif görev: Jeneratörü çalıştır 0/1" | **D-019**, §12.4 (M5/M7) |
| Harita işaretleri | Main objective, side mission, supply drop, safe house, boss area, extraction; bölge adı ("Region 1 — Industrial Zone") | §11, §12, M7 |
| Loadout | Primary / Secondary / Grenade / **Special (Combat Drone)** | §7.6 yetenekler, §14.7 loadout (M9) |
| Karakter ekranı | Health, Armor, Move Speed, Crit Chance/Damage, Pickup Radius | Yalnızca **gösterim**: tüm karakterler aynı taban stat (D-005); perk takasları (±%10) gösterilir |
| Revive | "DOWNED — Waiting for revive 18.4s" + halka | §14.2 (bleedout 25 s) |
| Extraction | "EXTRACTION 02:17 — Get to the helicopter!" | §2.2, §12.2 |
| Co-op lobi | Oyun Oluştur / Oyun Ara / IP ile Katıl + bulunan oyunlar (isim, x/4, ping, KATIL) | M1 `LocalCoopScreen` ile aynı yapı (görsel cila M11) |
| Çelişkiler | WAVE/ZOMBIES REMAINING, "YÜKSELT" | Uygulanmaz (D-003, D-005) |

### 0.13 Nihai sanat yönü (tüm referanslar)
- **Zombiler:** insan oranlarında, gerçekçi, soluk/çürümüş ten, yırtık kirli kıyafet, kanlı; siluetler birbirine yakın — sürü tek bir koyu kütle olarak okunur. Renkli/karikatür stil **hedef değildir**; M2'deki Quaternius modelleri yalnızca pipeline/benchmark placeholder'ıdır (D-009, D-018).
- **Palet:** koyu kahve-kırmızı-siyah zemin, sodyum turuncusu lamba havuzları ve ateş, soğuk mavi vurgular (loot, extraction ışığı). Kan zemini kaplar.
- **Kahramanlar:** taktik asker siluetleri (kask, yelek, maske); takım halkası rengi ile ayrışır.

---

## §1. Game Vision

**Tek cümle:** Last Ground; karanlık, endüstriyel bir kıyamet sonrası haritada 1–4 arkadaşın aynı Wi-Fi üzerinden, aktif nişan alarak ve her run'da farklı build kurarak, durmadan akan ve giderek büyüyen zombi sürülerine karşı ne kadar dayanabileceklerini ve ne zaman extraction'a gideceklerini seçtikleri mobil twin-stick endless horde survival shooter'dır.

**Ürün özeti (onaylı):**
```
Platform:    Android first (iOS'a uygun mimari)
Players:     1–4 player LAN co-op
Backend:     None
Networking:  Host-authoritative Mirror LAN networking (gameplay'den izole)
Gameplay:    Endless survival / extraction-style horde combat
             No traditional wave system · Shared XP and currency
             No pay-to-win or permanent weapon-stat advantage
Performance: Low-end ≥ 30 FPS · Mid-range 45–60 FPS · High 60 FPS
             250 visible zombies stress target · 300+ simulated zombies architecture target
```

**Temel his:** *"Her yönden üzerime geliyorlar ama kurduğum build sayesinde giderek daha güçlü hale geliyorum."*

**Tasarım sütunları (her karar bunlara göre test edilir):**
1. **Aktif beceri** — nişan, pozisyon, geri çekilerek ateş. Auto-aim erişilebilirlik seçeneği, meta değil.
2. **Sürü bir kütledir** — zombiler tek tek değil, akan, çevreleyen, iten bir kalabalık.
3. **Build çeşitliliği ve co-op sinerji** — her run farklı; dört oyuncu dört farklı rol üretir.
4. **Hareket zorunluluğu** — geniş harita, event'ler, kampı cezalandıran director.
5. **Backend'siz anında co-op** — aynı odadaki arkadaşlar 20 saniyede aynı oyunda.
6. **Mobil öncelik** — 30/60 FPS stabil, pil ve ısı bilinci, parmakla rahat kontrol.

**Farklılaşma (tür içi):**

| Konu | Tipik "survivor-like" | Last Ground |
|---|---|---|
| Saldırı | Otomatik | Twin-stick aktif nişan (auto opsiyonel) |
| Alan | Sonsuz boş arena | Bölgeli geniş harita, çevre etkileşimi |
| Düşman | 2D sprite sürü | 3D kalabalık, çevreleme, akış |
| Çok oyunculu | Yok / online | 1–4 yerel Wi-Fi co-op, revive |
| Tempo | Sabit artış / dalga | Dalgasız, sürekli akış; director: gerilim–tepe–nefes döngüsü |
| Bitiş | Süre dolar | Sonsuz tırmanış; periyodik extraction pencereleri ile risk/ödül |

**Hedef kitle:** mid-core mobil oyuncu, 16+, arkadaş grupları; tipik seans 15–30 dk (endless, oyuncu extraction ile bitirir). Yoğun gore nedeniyle ayarlarda "kan efektlerini azalt" seçeneği.

---

## §2. Core Gameplay Loop

### 2.1 Üç katmanlı döngü
- **An (2–10 sn):** hareket → nişan → ateş → öldür → pickup → pozisyon değiştir.
- **Karşılaşma (1–3 dk):** baskı artar → tepe → kısa nefes → level-up seçimi → loot toplama/revive.
- **Run (sonsuz; tipik seans 15–30 dk):** keşif → event'ler → elite → boss → daha büyük sürüler → extraction penceresi: **çık ya da devam et**.
- **Meta (run'lar arası):** kalıcı currency → yeni silah/karakter/perk/loadout/kozmetik → yeni run. Güç artışı yok (§14.7).

### 2.2 Endless survival + extraction yapısı (D-003, D-004)
Dalga yok, "kalan zombi" yok, run süresi sabit değil. Dünya **Threat Level** ile sürekli tırmanır; director bu tırmanışın üzerine gerilim–tepe–nefes dalgalanması ekler.

| Survival süresi | Threat | Beklenen ekrandaki zombi | Yeni öğeler |
|---|---|---|---|
| 00:00–03:00 | I | 5–40 | Walker, silahı tanıma, ilk Supply Drop |
| 03:00–07:00 | II | 30–90 | Runner, Spitter, ilk elite, Power Generator |
| 07:00–12:00 | III | 60–150 | Exploder, Tank, Weapon Cache, Elite Hunt |
| 12:00–15:00 | IV | 100–200 | **İlk Boss** → yenilince **1. extraction penceresi** |
| 15:00–22:00 | V | 150–250 | Massive horde'lar, elite modifier kombinasyonları, Rescue Signal |
| 22:00+ | VI, VII… | 250+ (cihaz cap'i) | Boss tekrar döner (+modifier), her ~7 dk yeni extraction penceresi; tırmanış sonsuz |

**Extraction penceresi:**
- Boss yenildiğinde (ilk ~12–15 dk) ve sonra periyodik olarak (~7 dk) haritanın bir bölgesinde iniş alanı açılır; 90 s boyunca aktif kalır.
- Takım alana gidip 45–60 s savunursa **extract** olur: run biter, toplanan ödüllerin tamamı + extraction bonusu kasaya girer.
- Pencereyi kaçıran veya bilerek görmezden gelen takım devam eder: Threat artar, ödül çarpanı büyür (risk/ödül).
- Co-op'ta extraction takım kararıdır: alanda en az bir oyuncu varken sayaç ilerler; alandaki oyuncular çıkarken dışarıdaki Downed/Dead oyuncular da ödüllerin tabanını alır.

**Bitiş ve ödül:**
- **Extract:** run coin'in %100'ü + extraction bonusu (Threat seviyesine göre artar).
- **Tüm takım düşer:** run coin'in %60'ı kalıcı currency'e dönüşür, extraction bonusu yok. (Oyuncu cezalandırılmaz ama çıkmak her zaman kazançlı.)
- **Skor:** survival süresi, en yüksek Threat, kill, revive sayısı → yerel istatistikler ve badge/title koşulları.
- Tüm eşikler `DirectorProfile` / `ThreatCurveDefinition` / `ExtractionRulesDefinition` SO'larında.

---

## §3. Mobile Control Architecture

### 3.1 Katmanlar
```
Touch (Input System / EnhancedTouch)
   ↓
TouchInputRouter        — parmak → bölge sahipliği (sol stick / sağ stick / buton)
   ↓
VirtualJoystick (x2)    — dead zone, normalize, eşikler (saf C#, UI'dan bağımsız)
   ↓
PlayerInputFrame (struct: move, aim, aimActive, fireHeld, buttons bitmask, seq)
   ↓
IPlayerInputSource      — LocalTouchInput | GamepadInput | BotInput (test) | NetworkReplayInput
   ↓
PlayerMotor / WeaponController / AbilityController
```
Aynı `PlayerInputFrame` yapısı; local oyuncu, gamepad, otomatik test botu ve network client komutları için ortaktır. Gameplay kodu dokunmatiği hiç bilmez.

### 3.2 Sol stick — hareket
- Sol yarım ekranın alt ~%75'i. **Floating joystick:** parmağın bastığı yerde doğar.
- Yarıçap 60–70 dp, dead zone 0.12, analog hız (eğri `ControlProfile` SO'da).
- Parmak bölgeden çıksa da sahiplik korunur (drag sırasında kaybolmaz).

### 3.3 Sağ stick — nişan
- Sağ yarım ekranın alt bölgesi, floating.
- `|v| > aimThreshold (0.25)` → karakter üst gövdesi nişan yönüne döner, ateş yok.
- `|v| > fireThreshold (0.55, ayarlanabilir)` → ateş.
- Ayar: "Nişan = Ateş" (tek eşik 0.2).
- Parmak bırakılınca son nişan yönü 0.15 sn korunur (flick-shot).
- **Soft aim assist (manuel modda):** stick yönünden ±8–12° koni ve silah menzili içinde hedef puanlama (açı farkı, mesafe, tehdit) → yumuşak mıknatıslama. Ayar: Kapalı / Düşük / Yüksek.

### 3.4 Auto Fire modu
- Sağ stick gizlenir. `AutoTargetSelector` 8 Hz'de hedef seçer. Öncelik: yakındaki Exploder > oyuncuya temas eden > elite > en yakın görünür.
- Karakter hareket yönünden bağımsız hedefe döner (geri çekilirken ileri ateş yine mümkün).
- Hedefin altında küçük bir reticle.
- Settings → Kontroller: `Manuel (twin-stick)` / `Otomatik nişan + ateş` / `Otomatik nişan, manuel ateş`.

### 3.5 Butonlar (sağ stick çevresinde yay)
| Buton | Davranış |
|---|---|
| Granat | Tap: nişan yönüne 8 m. Basılı tut + sürükle: hedefle, bırak: at |
| Yetenek | Upgrade'den gelen aktif yetenek (drone boost, turret vb.), cooldown halkası |
| Silah değiştir | 2 slot |
| Medkit | Stoktaki medkit'i kullan |
| Etkileşim (bağlamsal) | Sadece yakında jeneratör/sandık varken görünür, basılı tut |
- Reload otomatiktir (şarjör bitince / 1 sn ateş edilmeyince); manuel reload butonu ayardan açılır.
- Revive **butonsuzdur**: yanında durmak yeter (mobilde kalabalık altında buton aramak kötü UX).

### 3.6 Ergonomi kuralları
- Aksiyon butonları ≥ 64 dp (~10 mm), butonlar arası ≥ 8 dp; stick aktif alanı ≥ 140 dp çap.
- Safe area (çentik/punch-hole) desteği; sol el modu (ayna); HUD ölçeği %80–120; buton opaklığı.
- Haptics: hasar alma, level-up, downed (ayar ile kapatılabilir).
- Joystick alanları GraphicRaycaster'a değil, `TouchInputRouter` içindeki özel hit-test'e bağlıdır (UI raycast maliyeti yok).
- Gamepad: Input System action map aynı `PlayerInputFrame`'e yazar → Bluetooth gamepad desteği bedava gelir.

### 3.7 Network ilişkisi
Client local input'u kendi karakterine anında uygular. Host'a 30 Hz `PlayerStateInput` (pozisyon, hız, nişan yaw'ı, ateş durumu, silah, seq) gönderir. Host'ta local oyuncu da aynı komut API'sinden geçer (loopback), özel durum yoktur.

---

## §4. Camera Architecture

- **`TopDownCameraRig`** (tek MonoBehaviour, Update'i yok; `TickScheduler` presentation fazında çağrılır).
- **`CameraProfile` SO:** pitch 55°, yaw 0°, dikey FOV 35°, mesafe 22 m, takip smoothTime 0.12 s, look-ahead, zoom aralıkları, shake limitleri.
- **Takip:** critically-damped spring; hedef = local oyuncu + look-ahead (manuel nişanda nişan yönü × 2.5 m, auto modda hareket yönü × 1.5 m).
- **Dinamik zoom:** taban + horde intensity (+%10) + silah menzili (sniper +%8) + boss arenası override. Zoom değişimleri yavaş (0.5–1 s), oyuncu fark etmeden.
- **Screen shake:** trauma modeli (Perlin), maks 0.3 m / 1.5°; ayarda azaltılabilir.
- **Occlusion:** kamera ile oyuncu arasına giren duvar/çatı `OccluderFader` ile dither-fade (transparan sıralama yok, opaque kalır). Büyük iç mekanlarda "roof hide volume".
- **Neden sabit yaw 0°:** joystick yukarı = ekran yukarı = dünya +Z. Zihinsel döndürme yok; level design kamera yönüne göre "cutaway" yapılabilir; host oyuncunun ekran ayak izini (footprint) hesaplayabilir.
- **Multiplayer:** her cihaz yalnızca kendi local oyuncusunu takip eder. Ekran dışı takım arkadaşları → kenar okları (isim, mesafe, HP). Ölünce spectator: takım arkadaşları arasında geçiş.
- **Cinemachine kullanılmayacak** (MVP): ihtiyaç basit; özel rig daha az overhead, deterministik ve AI ile düzenlemesi kolay.
- Far clip ~70 m, skybox yok (solid koyu clear), ikinci kamera yok (mini-map prerender texture, §UI).

---

## §5. Combat Architecture

### 5.1 İlkeler
- Zombiler GameObject değildir → isabet tespiti **Physics collider'ları ile değil**, `ZombieSpatialGrid` üzerinde ray-vs-circle sorgusu ile yapılır.
- Physics yalnızca statik çevre görüş hattı (LOS) için: `Environment` layer'ına raycast. Rigidbody simülasyonu yok.
- Tüm hasar tek bir pipeline'dan geçer. Host otoriterdir, client öngörür.

### 5.2 Hasar pipeline'ı
```
CLIENT                                           HOST
WeaponController.TryFire (input)
  ├─ ShotRNG(seed=hash(runSeed, playerId, shotSeq)) → spread yönü, crit
  ├─ Local VFX/SFX (anında)
  ├─ HitQuery (client replica zombileri + env LOS)
  ├─ Öngörülen hit flash + damage number
  └─ HitClaim{shotSeq, weaponId, zombieIdx, hitPos, pierceIdx} ──►  HitClaimValidator
                                                                     (fire rate, ammo, menzil, hedef canlı,
                                                                      pozisyon toleransı, LOS)
                                                                   DamageResolver
                                                                     (aynı ShotRNG → aynı crit; stat, armor,
                                                                      direnç, modifier'lar)
                                                                   ZombieWorld.ApplyDamage
                                                                     └─ HP ≤ 0 → ZombieKilledEvent
                                                                          ├─ XP / Loot / Director / Stats
                                                                          └─ Replicator → DeathBatch ──► tüm client'lar
```
- Host'un kendi oyuncusu aynı yolu network atlaması olmadan kullanır.
- **Deterministik atış RNG'si:** spread ve crit, `(runSeed, playerId, shotSeq)` tohumundan üretilir. Client damage number'ı anında ve host ile birebir aynı gösterir; ekstra bant genişliği gerekmez.

### 5.3 Veri yapıları
- `DamageInfo` (struct): sourcePlayer, weaponId, amount, isCrit, damageType flags (Ballistic, Fire, Explosive, Toxic, Melee), hitPoint, direction, knockback, pierceIndex.
- `DamageTarget` (struct): kind (Zombie, Player, Boss, Prop) + id. Zombiler için `ZombieHandle`, diğerleri için `IDamageable` (az sayıda nesne).

### 5.4 Formüller (başlangıç)
- Giden: `final = base × (1 + ΣdamagePct) × (isCrit ? critMult : 1) × typeMult × (1 − targetResist)`
- Gelen (oyuncu): dodge rulosu (host) → `raw × (1 − damageReduction)` → önce Armor havuzu, sonra Health.
- Friendly fire: kapalı. Oyuncunun kendi patlamaları kendine %25 (tasarım kararı, SO'da).

### 5.5 Zombi → oyuncu hasarı
Host: temas mesafesi + **windup** (0.35–0.5 s telegraph animasyonu) → hâlâ menzildeyse hasar. Kaçınılabilir saldırı = beceri. Client'a `PlayerVitals` (değişince reliable) + hit event.

### 5.6 Durum efektleri ve itme
- Burn, slow, stun: zombi SoA verisinde timer + flag olarak; Burst job'da tick edilir. GameObject/coroutine yok.
- Knockback: sim'de hız impulsu, tip başına kütle (Tank etkilenmez). Mermiler Walker'ları hafif geri iter → güç fantezisi.
- AoE: `ExplosionSystem` → grid'de daire sorgusu (zombiler + oyuncular + prop'lar).

### 5.7 Geri bildirim
Per-instance hit flash (shader parametresi), paylaşımlı blood burst `Emit`, hit marker, damage number (ayar), throttled ses. Kill confirm host'tan gelir (LAN'da 10–40 ms).

---

## §6. Weapon Architecture

### 6.1 Veri
**`WeaponDefinition` SO:** id, displayNameKey, icon, slotType, fireMode (Hitscan / Pellet / Projectile / Cone*), damage, fireRate, magazineSize, reloadTime, range, accuracy, spreadDeg, pelletCount, critChance, critMultiplier, penetration, knockback, projectileSpeed, maxReserveAmmo, ammoType, moveSpeedMultiplierWhileFiring, aimAssistStrength, recoilKick (görsel), tags (Ballistic, Automatic, Shotgun, Precision, Heavy, Explosive), `WeaponVisualProfile`, `SoundDefinition`'lar. *Cone (flamethrower) sonraki sürüm.

**`WeaponVisualProfile` SO:** mesh, muzzle socket offset, muzzle flash flipbook, tracer stili, shell casing açık/kapalı, ground flash boyutu.

### 6.2 Runtime
- `WeaponInstance` (saf C#): definition, ammoInMag, reserve, cooldown, reloadTimer, `ShotModifiers` struct'ı (upgrade'lerden: +pierce, burnDps, explodeOnKillChance, bounce…).
- `WeaponStats` (struct, cache'li): definition + oyuncu statları + modifier'lar. **Her frame değil**, sadece build/ekipman değişince yeniden hesaplanır.
- `WeaponController` (oyuncu başına): slotlar, fire döngüsü, reload, input tüketimi.
- Fire mode'lar arayüz değil, allocation'sız statik fonksiyonlar (`HitscanFire.Execute(ref ShotContext)`), switch ile seçilir.

### 6.3 Hitscan vs Projectile
- **Hitscan:** Pistol, SMG, AR, Sniper, Machine Gun, Shotgun (pellet = çoklu hitscan).
- **Projectile:** Grenade, Rocket, Spitter tükürüğü, özel yetenekler. `ProjectileSystem` host'ta struct listesi olarak simüle eder (GameObject yok). Client'a yalnızca `ProjectileSpawn {id, type, origin, velocity, startTick}` gider; client aynı deterministik uçuşu kendisi çizer. Çarpma/patlama host'tan event olarak gelir.

### 6.4 MVP başlangıç değerleri (placeholder)

| Silah | Hasar | Atış/sn | Şarjör | Reload | Menzil | Spread | Crit | Pen. | Not |
|---|---|---|---|---|---|---|---|---|---|
| Pistol | 18 | 3 | 12 | 1.2 s | 22 m | 2° | %5 ×2 | 0 | Sonsuz yedek mermi |
| SMG | 9 | 12 | 30 | 1.6 s | 18 m | 6° | %5 ×2 | 0 | Hareket cezası yok |
| Assault Rifle | 16 | 8 | 30 | 1.8 s | 30 m | 3° | %8 ×2 | 1 | Başlangıç silahı |
| Shotgun | 8 × 8 pellet | 1.2 | 6 | 2.4 s | 12 m | 18° | %5 ×2 | 0 | Yüksek knockback |
| Sniper | 120 | 0.9 | 5 | 2.6 s | 45 m | 0° | %25 ×2.5 | 4 | Kamera +%8 zoom |
| Machine Gun | 14 | 10 | 100 | 4.0 s | 28 m | 5° | %5 ×2 | 1 | Ateşte hız −%20 |

### 6.5 Görseller
- Tracer: GameObject değil; `TracerSystem` instanced uzatılmış quad'lar, ömür 0.06 s, cap 64.
- Muzzle flash: 2–3 karelik flipbook quad + zemine additive flash decal. Gerçek ışık yalnızca local oyuncu + HIGH.
- Kovanlar: paylaşımlı particle system `Emit()`; LOW'da kapalı.
- Seri silahlar (SMG/MG) sesi loop + tail tekniğiyle (§23).

### 6.6 Mermi ekonomisi
Pistol sonsuz. Diğer silahların yedek mermisi var; ammo crate/drop'lar hareket sebebi yaratır. Mermi baskısı "ılımlı": oyuncuyu cezalandırmak değil, haritada dolaştırmak için.

---

## §7. Upgrade / Build Architecture

### 7.1 Stat sistemi
- `StatId` enum: MaxHealth, Armor, MoveSpeed, DamagePct, FireRatePct, ReloadSpeedPct, CritChance, CritDamage, PickupRadius, DodgeChance, DamageReduction, XpGainPct, Luck, ReviveSpeedPct, AbilityCooldownPct …
- `StatBlock`: `base[]`, `flatAdd[]`, `pctAdd[]`, `mult[]` → `final[]` (dizi, allocation yok). Dirty flag ile yalnızca değişince hesaplanır.
- `StatModifier` (struct): statId, op (Flat / PercentAdd / Multiply), value, sourceId.

### 7.2 Upgrade verisi
**`UpgradeDefinition` SO:**
- `id` (kalıcı string), nameKey, descKey, icon, rarity, tags, maxStacks, baseWeight
- Koşullar: requiresTags, requiresUpgradeIds, excludesUpgradeIds, minPlayerLevel, requiredWeaponTags
- `effects`: `[SerializeReference] List<UpgradeEffect>` —
  - `StatEffect` (StatModifier listesi, rarity'ye göre değer)
  - `WeaponModifierEffect` (pierce, fire ammo, explode-on-kill…)
  - `AbilityGrantEffect` (Combat Drone, Turret, Grenade Pack → `AbilityDefinition`)
  - `OnKillEffect` (lifesteal, kill başına kalkan)
  - `OnHitEffect` (burn, slow şansı)
  - `EvolutionEffect` (ileride: iki upgrade maksimumda → evrimleşmiş versiyon)

### 7.3 Rarity
Common %60 / Rare %28 / Epic %10 / Legendary %2 (Luck stat'ı kaydırır). Aynı upgrade'in rarity'ye göre değer kademesi (örn. hasar +%10 / +%15 / +%22 / +%30). Değerler `RarityTable` SO'da.

### 7.4 Teklif üretimi (host)
1. Uygun upgrade'leri filtrele (stack, koşul, çakışma).
2. Rarity rulosu → o rarity içinde ağırlıklı seçim (tekrarsız) × 3.
3. Kural: 3 tekliften en az biri %50 olasılıkla mevcut silah/build tag'leriyle sinerjili.
4. Co-op çeşitliliği: takım arkadaşının **maksimuma ulaştırdığı** upgrade'lerin ağırlığı −%20 (opsiyonel, SO'da).
5. Her oyuncunun kendi RNG akışı (`hash(runSeed, playerId, "upgrade")`).

### 7.5 Level-up akışı (multiplayer'da oyun durmaz)
```
Host: XP → LevelUp → PendingOffers kuyruğu (oyuncu başına) → OfferMessage ─► Client
Client: Kompakt panel (alt-orta, ekranın ~%45 genişlik / ~%28 yükseklik), oyun devam eder
        Oyuncu küçültüp sonra seçebilir ("+1" rozeti)
        Zaman aşımı (Ayar: Kapalı / 5 s / 10 s) → heuristic auto-select (sinerji ağırlığı, rastgele değil)
Client: SelectUpgrade{offerId, index} ─► Host: doğrula → uygula → BuildChanged{playerId, upgradeId} ─► herkes
Herkes: Stat'ları SO'lardan kendi hesaplar (ağa stat değil, yalnızca upgrade id gider)
```
- **Solo:** Ayar "Seçim sırasında duraklat" (varsayılan açık) — sim tick'i durdurulur. Co-op'ta bu seçenek devre dışı.
- Level eğrisi: `LevelCurveDefinition` SO (örn. `xp(n) = a·n^1.5 + b`), oyuncu sayısına göre çarpan.

### 7.6 Yetenekler ve yoldaşlar
Combat Drone gibi yoldaşlar ayrı NetworkObject değildir. Drone pozisyonu sahibinin pozisyonundan **deterministik** türetilir (orbit açısı = f(tick)). Her client kendisi hesaplar; ağa sadece drone'un atış event'leri gider.

### 7.7 Geçicilik kuralı (D-005)
Maç içindeki tüm upgrade'ler, silah geliştirmeleri, ekipman ve level **run bitince sıfırlanır**. `PlayerBuild` run kapsamlı bir nesnedir ve save'e yazılmaz. Kalıcı profile yalnızca meta unlock'lar ve istatistikler gider.

### 7.8 İçerik hedefleri
MVP (M5): 12 upgrade · Vertical Slice: 25–30 · Release: 60+.

---

## §8. Zombie AI Architecture

### 8.1 İlke
Zombi başına MonoBehaviour, `Update()`, `NavMeshAgent`, `Rigidbody`, `Collider` **yok**. Tüm zombiler host'taki `ZombieWorld` içinde **Structure-of-Arrays** (NativeArray) verisi olarak yaşar ve Burst job'larıyla güncellenir.

`ZombieSimData` (SoA, kapasite örn. 512): position, velocity, heading, hp, typeIndex, state, stateTimer, targetPlayer, slotAngle, lodTier, flags (elite, burning, stunned, attacking), statusTimers, generation.

### 8.2 Sim tick pipeline (host, sabit 30 Hz)
```
1. AiLodJob            — tier: T0 <18 m, T1 18–35 m, T2 35–60 m, >60 m → dormant/virtual (en yakın oyuncuya)
2. SpatialGridBuildJob — 2 m hücre, counting sort → cellStart/cellCount (tek geçiş)
3. TargetSelectJob     — 0.5 s'de bir (bucket'lı): hedef oyuncu (yakınlık, aggro, downed ağırlığı)
4. FlowSampleJob       — hedef oyuncunun flow field'ından yön (bilinear)
5. SteeringJob (paralel)
     uzak: flow yönü
     yakın (<10 m): surround slot hedefi + doğrudan yaklaşma
     + separation (3×3 hücre, maks 8 komşu)
     + statik engel kaydırma (walkable grid)
     + tip parametreleri (hız, hızlanma)
6. IntegrateJob        — pos += vel·dt, bloklu hücrelerde kayma, oyuncu yumuşak çarpışması
7. AttackStateJob      — menzil + windup timer → NativeQueue<AttackEvent>
8. Main thread         — AttackEvent → DamageResolver; ölüm/spawn event'leri
```
**LOD frekansları:** T0 her tick (30 Hz), T1 her 2. tick (15 Hz, dt×2), T2 her 6. tick (5 Hz). `index % N` bucket'ları ile maliyet tick'lere yayılır.

### 8.3 Navigasyon: Flow Field
- **Bake:** Editör aracı `NavGridBaker`, Unity'nin NavMesh bake'ini kullanıp `NavMesh.SamplePosition` ile 1 m'lik walkable grid üretir → `NavGridAsset` (byte dizisi). NavMesh'in bake aracı kullanılır, runtime'da NavMeshAgent kullanılmaz.
- **Runtime:** oyuncu başına (maks 4) 96×96 m'lik pencerede **integration field** (Burst BFS/Dijkstra) + 8-yön gradient. Oyuncu hücre değiştirdiğinde yeniden hesaplanır, time-sliced. 9.216 hücre × 4 → mid cihazda ~1 ms civarı bekleniyor (M4'te ölçülecek).
- Pencere dışındaki zombiler: bölge portal grafı + önceden hesaplanmış next-hop tablosu → portala git, pencereye girince flow field'a geç.
- Barricade kırılınca/kurulunca yalnızca ilgili pencere dirty olur.

### 8.4 Sürü hissi
- **Çevreleme (SurroundSlotSolver):** Her 0.5 s, her hedef oyuncu için çevresindeki saldırganlar 12 açısal sektöre dağıtılır. Kalabalık sektördeki fazla zombilerin `slotAngle`'ı boş sektörlere atanır → doğal kuşatma ve flank. Halka yarıçapı yaklaştıkça 6 m → 1 m daralır (img-1'deki "halka + boşluk" görüntüsü).
- **Darboğaz akışı:** flow field + separation → dar geçitlerden "akan" kalabalık kendiliğinden oluşur.
- **Oyuncu asla tamamen hapsolmaz:** zombiler oyuncuyu bloklamaz; oyuncu Walker'ları iter. Temas eden zombi başına hız −%7, toplam maks −%35 (yakalanma hissi ama kaçış mümkün). Tank/Brute istisna (itilemez, ama sayıları azdır).
- **Anti-stuck:** 1 s ilerleyemeyen zombiye jitter; tüm oyunculardan uzak ve 10 s görünmeyen zombi director havuzuna geri alınıp yakın spawn noktasına "geri dönüştürülür".
- **Gürültü:** atış sesi yarıçapındaki dormant zombileri uyandırır (hafif gizlilik katmanı).

### 8.5 Tip davranışları (job içinde `switch(type)`, veri SO'dan)
| Tip | Davranış |
|---|---|
| Walker | Standart akış + çevreleme, windup'lı pençe |
| Runner | Yüksek hız, 4–6 m'de lunge (sıçrama), düşük HP |
| Tank | Yavaş, yüksek kütle; diğer zombileri iter, barricade kırar, knockback almaz |
| Spitter | 8–12 m mesafe korur, yanlara kayar, toxic projectile (ProjectileSystem) |
| Exploder | <6 m'de sprint, 0.8 s parlama telegraph'ı → AoE patlama; zincirleme varil tetikler |

### 8.6 Animasyon
Animator yok. Sim durumu + hız → VAT klip indeksi (walk/run/attack/hit/death varyantları) + instance başına zaman offseti. Client'ta aynı mantık replica verisiyle çalışır.

### 8.7 Client tarafı
Client'ta AI çalışmaz. `ZombieReplicaWorld` snapshot'ları interpolate eder (§17), animasyon/VFX/ses yerel üretilir.

---

## §9. Horde Director Architecture

### 9.1 Bileşenler
`IntensityTracker` · `DirectorStateMachine` · `SpawnBudget` · `SpawnCardDeck` · `SpawnLocator` · `HordePatternSelector` · `EliteInjector` · `PerformanceGovernor` — hepsi saf C#, host'ta, `DirectorProfile` ve `PlayerCountScalingProfile` SO'larıyla beslenir.

### 9.2 Girdiler (2 Hz örnekleme)
elapsed time · canlı oyuncu sayısı · takım HP oranı · son 10 s alınan hasar · takım DPS'i (hareketli ortalama) · yaşayan zombi ve oyuncu başına yakın zombi sayısı · son downed/ölüm sayısı · takım güç skoru (ortalama level, silah tier'ı, upgrade sayısı) · bölge tehlike seviyesi · aktif event · **host frame-time payı**.

### 9.3 Yoğunluk modeli
- Oyuncu stresi `S ∈ [0,1]`: alınan hasar/maxHP × k1 + yakın zombi yoğunluğu × k2 + düşük HP terimi; zamanla söner.
- Takım yoğunluğu = ağırlıklı ortalama (en stresli oyuncuya ekstra ağırlık).

### 9.4 Durum makinesi
```
Calm ─► BuildUp ─► Peak ─► PeakHold ─► Relax ─► BuildUp …
                     ▲           │
      (Elite / Event / Boss / MassiveHorde enjeksiyonları zaman eğrisinden)
```
- BuildUp → Peak: yoğunluk > 0.75 veya süre doldu.
- PeakHold: 15–30 s (rastgele, profilde).
- Relax: yoğunluk < 0.3 olana kadar, en az 10–20 s. Relax'ta sadece trickle spawn.
- Süreler ve eşikler rastgelelikle varyasyonlu → her run farklı.

### 9.5 Bütçe
```
spawnPointsPerSec = baseCurve(t) × stateMult × playerScale.spawnRate
                  × powerAdjust(clamp 0.8–1.3; gerçek DPS / beklenen DPS)
                  × perfGovernor(0.5–1.0)
maxAlive          = maxAliveCurve(t) × playerScale.count × hostDeviceCap
```

### 9.6 Spawn kartları ve desenler
- `SpawnCardDefinition`: zombieType, cost (Walker 1, Runner 2, Exploder 3, Spitter 4, Tank 10), minTime, weightCurve, maxConcurrent, groupSize, eliteChance.
- Desenler (`HordePatternDefinition`): **Trickle** (kenarlardan 1–3), **Pack** (tek yönden 6–12), **Pincer** (zıt iki sektör), **Surround** (4–6 sektör aynı anda — peak), **Flood** (darboğaz/portal üzerinden), **Ambush** (dormant uyandırma).

### 9.7 Spawn konumu
Harita `SpawnZone` noktaları → filtre: en yakın oyuncuya 22–45 m, **tüm oyuncuların kamera ayak izinin dışında** (sabit kamera sayesinde host tam hesaplar), walkable, flow mesafesi ulaşılabilir, desenin istediği sektör.

### 9.8 Elite ve anti-kamp
- Elite = tip + modifier (Armored, Fast, Toxic, Volatile), 3–5× HP, glow, duyuru.
- **Anti-kamp:** takım aynı bölgede > N s hareketsizse o bölgede bütçe artar, Spitter ağırlığı artar (uzaktan baskı), event'ler başka bölgede açılır.

### 9.9 Event bağlantısı ve debug
- `MapEventDirector`, director'dan geçici "baskı artışı" isteyebilir (Rescue Signal = sürekli Surround deseni).
- Dev build'de overlay: yoğunluk grafiği, durum, bütçe, yaşayan sayısı, governor çarpanı.

### 9.10 Oyuncu sayısı ölçeklemesi (`PlayerCountScalingProfile` — başlangıç)
| | 1P | 2P | 3P | 4P |
|---|---|---|---|---|
| Toplam horde çarpanı | 1.00 | 1.60 | 2.05 | 2.50 |
| Spawn hızı | 1.00 | 1.35 | 1.65 | 1.90 |
| Elite şansı | 1.0 | 1.2 | 1.4 | 1.6 |
| Special zombi payı | 1.0 | 1.25 | 1.5 | 1.75 |
| Boss HP | 1.0 | 1.7 | 2.3 | 2.9 |
| Boss modifier | — | +1 summon | +1 summon, −%10 CD | +2 summon, −%15 CD, ek saldırı |
| Event yoğunluğu | 1.0 | 1.3 | 1.55 | 1.8 |
Zombi HP'si oyuncu sayısıyla **artmaz** (güç fantezisini korur). Final değil — playtest ile ayarlanacak.

### 9.11 Oyuncuya gösterim — HUD (D-003)
Üst orta, tek satır, lokalize:
```
SURVIVAL 08:42  ·  HORDE: HIGH  ·  THREAT III
```
| Gösterge | Kaynak | Değerler | Güncelleme |
|---|---|---|---|
| **SURVIVAL** | Host tick'inden türetilen run süresi | `mm:ss`, 60 dk sonrası `h:mm:ss` | Client'ta yerel, host start tick'ine bağlı |
| **HORDE** | Director'ın *anlık* baskısı: takım yoğunluğu + durum makinesi | CALM · LOW · MEDIUM · HIGH · EXTREME (histerezisli eşikler, 2 s altında titremez) | 2 Hz `DirectorInfo` |
| **THREAT** | *Uzun vadeli* tırmanış seviyesi (süre + oyuncu gücü) | I, II, III … (roma rakamı) | Seviye atlayınca event |
- HORDE rengi: CALM gri → LOW beyaz → MEDIUM sarı → HIGH turuncu → EXTREME kırmızı nabız.
- THREAT seviyesi atladığında 2 s'lik kısa banner: "THREAT IV — Tanks detected" (yeni zombi tipini haber verir).
- Mini-map'in etrafındaki 12 sektörlük halka, sürünün **yönünü** gösterir; HORDE metni **şiddetini**.
- Extraction penceresi açıkken satırın altında ikinci satır: `EXTRACTION 01:12 → GAS STATION`.
- Kasıtlı olarak **global sayı yok**: ne "kalan zombi" ne "dalga no". Oyuncu tehlikeyi sayıyla değil, hisle ve seviye adıyla okur.
- **Görev paneli (D-019):** sol üstte, portrenin altında aktif görevler: `Bölgeyi temizle — Zombileri öldür: 12/50`, `Jeneratörü çalıştır: 0/1`. Sayaçlar yalnızca **görev kapsamlıdır** (bölge/event hedefi); sürü büyüklüğünü veya "kalan zombi"yi göstermez. Detay §12.4.

---

## §10. Boss Architecture

- Boss tek bir NetworkObject'tir (az sayıda nesne için sorun değil). Host'ta `BossController` durum makinesi: `Intro → Phase1 → Phase2 (≤%60) → Enraged (≤%25) → Death`.
- **`BossDefinition` SO:** baseHp, oyuncu sayısı ölçekleme, faz listesi (her faz: saldırı havuzu + ağırlık + cooldown, hareket hızı, summon ayarları), zayıf nokta çarpanı, arena gereksinimi.
- **`BossAttackDefinition` SO:** telegraph şekli (daire/koni/çizgi/halka), telegraph süresi (**mobilde ≥ 0.8 s**), aktif süre, hasar, knockback, cooldown, menzil koşulları, çevre tag'leri.

**Mutant Brute saldırıları:**
| Saldırı | Mekanik | Oyuncuyu zorladığı şey |
|---|---|---|
| Ground Slam | 6 m daire + genişleyen şok halkası | Dışarı koş, halkanın üstünden zamanla |
| Charge | Çizgi telegraph; duvara çarparsa 2 s sersemler; barricade kırar; varillere çarparsa patlar | Yana kaç, boss'u varile yem ol |
| Prop Throw | Arenadaki "fırlatılabilir" araç/konteyneri alır, oyuncu konumuna daire telegraph | Sürekli hareket |
| Summon Scream | Director'dan Runner paketi ister | Hedef önceliği, takım koordinasyonu |
| Frenzy (Enraged) | Slam + Charge kombosu | Tüm mekanikleri birleştir |

- **Zayıf nokta:** sırttaki tümör ×2 hasar → co-op'ta flank ödüllenir.
- **Hedefleme:** aggro tablosu (verilen hasar, yakınlık) + 8–12 s'de bir zorunlu hedef değişimi → tüm oyuncular hareket eder.
- **Network:** pozisyon 20 Hz unreliable; HP değişince 10 Hz throttle; `BossAttackStarted{attackId, startTick, targetPos, dir}` reliable → client telegraph'ı tick-senkron oynatır. Hasar host'ta.
- **Arena:** Boss event'i oyuncuları arenaya yönlendirir; boss sırasında normal spawn bütçesi %30–50.
- **Görsel:** tek skinned mesh, 1–2 materyal, LOD; Animator kullanan az sayıdaki nesneden biri.

---

## §11. Map Architecture

### 11.1 Genel
- Tek sahne, bölgeli büyük harita (tam open-world değil). Bounds ~350 × 350 m; oynanabilir alan ~60–80 bin m².
- Combat bölgeleri ~60–80 m; bağlantı yolları 10–15 m genişlik, 40–80 m uzunluk.
- 1 unit = 1 m. Zemin düz; çok katlı yapı yok; birkaç büyük girilebilir hol (Warehouse) roof-hide ile.
- Oyuncu hızı 5 m/s → bölgeyi ~14 s'de geçer (metrik tablosu level design dokümanında tutulacak).

### 11.2 Bölge grafı (döngülü, dead-end yok)
```
      [Warehouse] ────────── [Military Checkpoint]
           │                          │
 [Industrial Yard] ── [Abandoned Street] ── [Hospital]
           │                          │           │
      [Gas Station] ─────────────────┘   [Underground Entrance]
                                            (sabit extraction LZ)
```
Brief'teki sıra korunur ama döngüler eklenir: geri dönmek zorunda kalmadan event'ler arası dolaşım.

### 11.3 Veri ve sahne işaretçileri
- `MapDefinition` SO: bölgeler, portal grafı, spawn noktası, extraction LZ adayları (her bölgede ≥1), mini-map texture, `NavGridAsset`.
- `RegionDefinition` SO: id, nameKey, dangerLevel, spawn tag'leri, izin verilen event'ler, ambience, ışık profili, kan haritası çözünürlüğü.
- Sahnede: `RegionVolume` (bounds), `SpawnZone`, `EventAnchor`, `PlayerSpawnPoint`, `InteractableAnchor`, `PortalMarker`.

### 11.4 Level design kuralları (referanstan)
- Merkez açık, kenar yoğun prop.
- Her combat alanında ≥ 2 çıkış.
- Darboğazlar flow field "akışı" için bilinçli tasarlanır.
- Işık = oynanış: aydınlık alan güvenli his, karanlık = tehlike ve spawn.
- Kamera footprint'ine göre görüş hattı uzunluğu.

### 11.5 Render / bellek
- MVP'de streaming/Addressables yok. Bölge renderer'ları `CullingGroup` ile local kameraya uzaklığa göre açılıp kapanır (host sim'i render'dan bağımsız çalışır).
- Top-down açı nedeniyle occlusion culling gereksiz.
- Mini-map: editörde ortografik prerender (1024²) + UI marker'ları. İkinci kamera yok.
- Greybox: ProBuilder.

---

## §12. Event System

### 12.1 Mimari
- `MapEventDirector` (host): zaman pencereleri + ağırlıklı rastgele seçim, cooldown, oyuncu sayısı, bölge, director durumu. Aynı anda maks 1 major + 1 minor.
- `MapEventDefinition` SO: id, type, minTime, cooldown, weight, allowedRegionTags, duration, reward `LootTableDefinition`, director baskı profili, marker ikonu, duyuru key'i, ses.
- `MapEventInstance` (abstract, saf C#): `OnAnnounce / OnStart / OnTick / OnInteract / OnEnd`. Her tip küçük bir alt sınıf.
- Yaşam döngüsü: `Scheduled → Announced (10–20 s uyarı, mini-map + kenar oku) → Active → Success/Fail/Expired → Reward → Cleanup`.
- Replikasyon: `EventState{instanceId, defId, anchorId, state, progress(byte), timer}` değişince reliable + progress 2 Hz.

### 12.2 Event tipleri
| Event | Mekanik | Ödül |
|---|---|---|
| Supply Drop | Uçak geçiş sesi, 20 s sonra başka bölgeye sandık düşer; 2 s basılı tutarak açılır | Oyuncu başına **instanced** epic loot |
| Weapon Cache | Kilitli depo, yakın bölgede anahtar/elite | Özel silah seçimi (oyuncu başına) |
| Power Generator | Yakıt kutusu getir veya 8 s etkileşim (baskı altında) | Işıklar yanar (ışık grubu açılır), 60 s otomatik turret'ler |
| Rescue Signal | 8 m yarıçap 60–90 s savunma; içeride oyuncu yoksa ilerleme durur | Legendary teklif + coin, ölü oyuncuların respawn'ı |
| Elite Hunt | İşaretli elite haritada dolaşır | Legendary upgrade teklifi |
| Boss Event | Arenaya çağrı (Threat IV'te ilk, sonra modifier'larla tekrar) | Boss loot + extraction penceresi açılır |
| Extraction Window | Periyodik: rastgele bölgede LZ 90 s açık; 45–60 s savun → extract. Kaçırılırsa run devam eder (§2.2) | Ödüller kasaya + Threat bonusu |

### 12.4 Görevler ve görev sayaçları (D-019)
Görevler event sisteminin oyuncuya görünen yüzüdür; hareket ve risk/ödül kararı üretir, dalga hissi üretmez.
- **Tipler (başlangıç):**
  | Görev | Sayaç | Tamamlanınca |
  |---|---|---|
  | Bölgeyi temizle | Bölge içindeki takım kill'i `12/50` (hedef, oyuncu sayısı ve Threat ile ölçeklenir) | Bölge güvenli: kısa nefes (director Relax), loot sandığı |
  | Jeneratörü çalıştır | Etkileşim/yakıt `0/1` veya `0/3` | Işıklar + turret'ler (§12.2 Power Generator) |
  | Hayatta kal / savun | Süre `01:12` | Rescue Signal / Extraction |
  | Elite avı | `0/1` | Legendary teklif |
  | Supply topla | Sandık `0/2` | Instanced loot |
- **Kural:** sayaç hedefi sabit ve görev başında bellidir; director spawn'ı sayaçtan bağımsız sürer (görev bitince zombiler durmaz). Aynı anda en fazla 1 ana + 1 yan görev.
- **Veri:** `ObjectiveDefinition` SO (id, titleKey, formatKey, tip, hedef eğrisi, bölge tag'leri, ödül tablosu); `MapEventDefinition` 0..n objective içerir.
- **Replikasyon:** `ObjectiveState{instanceId, defId, current(ushort), target(ushort), state}` değişince reliable (sayaç değişimi ≤ 4 Hz birleştirilir). Kill sayacı host'ta `ZombieKilledEvent` + bölge testiyle artar (kill sahipliği önemsiz, D-002).
- **UI:** `HudObjectivePanel` — en fazla 2 satır, ikon + lokalize başlık + `SetText("{0}/{1}")`; tamamlanınca 2 s vurgu. Harita ekranında görev işaretleri (§11).
- **Milestone:** altyapı + "Bölgeyi temizle" M5; diğer görevler event'lerle M7.

### 12.3 Çevresel etkileşimler (brief §23)
Sahneye yerleştirilen etkileşimliler NetworkObject **değildir**; `InteractableRegistry` editörde hiyerarşi yolundan kararlı `ushort` id'ler üretir. Durum değişimleri küçük reliable mesajlarla gider. Sadece runtime'da doğan deployable'lar (turret) NetworkObject olabilir.

| Nesne | Davranış |
|---|---|
| Explosive Barrel | HP; patlayınca AoE, zincirleme; oyunculara %50 |
| Fuel Tank | Büyük patlama + 8 s ateş zemini |
| Temporary Turret | Kurulabilir/tamir edilebilir, host-sim hitscan, sınırlı mermi |
| Barricade | Nav hücrelerini bloklar; kırılınca flow field yerel güncellenir |
| Electric Trap | Butonla aktif, 10 s stun + hasar alanı, cooldown |
| Ammo Crate | Oyuncu başına cooldown'lu dolum |
| Medical Station | Şarjlı, zamanla iyileştirme |
| Abandoned Vehicle | Siper; araba alarmı zombileri çeker (yem); ağır hasarda patlar |

---

## §13. Loot System

### 13.1 Model (nesne üretimi minimum)
**Temel kural (D-002):** Kill'i kimin yaptığı ödülü **hiç** etkilemez. XP ve temel coin takım havuzuna gider ve **tüm takım üyelerine eşit miktarda** yansır. Oyuncuya özel olan tek şey, haritada fiziksel olarak bulunan özel loot ve silah pickup'larıdır; bunlar da **instanced**'tır (her oyuncu kendi kopyasını görür ve alır), yani kapışma imkânsızdır.

| Tür | Dünyada nesne? | Paylaşım | Not |
|---|---|---|---|
| XP (tüm kill'ler) | **Hayır** | **Takım: herkese eşit** — her oyuncu aynı XP'yi alır, herkes kendi level'ını atlar | Kill noktasından kıvılcım VFX (cap'li, sadece görsel). Solo/4P ilerleme hızı `LevelCurveDefinition.playerCountXpScale` ile dengelenir |
| XP Cache (elite/event) | Evet | Herhangi biri dokununca **herkese eşit** | Hareket teşviki |
| Temel coin | Evet, **toplanmış** | **Takım: herkese eşit** — biri dokununca herkesin run coin'i artar | 2×2 m hücrede 0.5 s biriktirilir → tek pickup (1/5/25 görsel kademe) |
| Ammo / Medkit / Grenade | Evet | **Oyuncu başına instanced** | Her oyuncu kendi kopyasını görür; stoku doluysa kendi kopyası yerde kalır |
| Temporary Buff | Evet | Takım aurası (yakındaki herkes) | Kapışma yok |
| Equipment (özel loot) | Evet | **Oyuncuya özel, instanced** | |
| Weapon Crate / Weapon pickup | Evet | **Oyuncuya özel, instanced** | Açınca 2 silah seçeneği veya coin'e çevir |
| Supply Drop sandığı | Evet | Tek sandık, açılınca **herkese ayrı** ödül rulosu | |

**Instanced loot'un ağ modeli:** Host drop'u `PickupSpawn{id, type, pos, ownerMask}` ile yayınlar. `ownerMask` her oyuncu için bir bit; her client yalnızca kendi bitini render eder. Oyuncu kendi kopyasını aldığında sadece kendi biti temizlenir. Tek pickup id'si, tek mesaj, 4 kopya.

Neden: sıfır toplamlı değil → "loot çalma" gerilimi yok, kill sahipliği tartışması yok, ekstra dünya nesnesi yok, bant genişliği yok. Bireysel fark build seçimlerinden gelir.

### 13.2 Runtime
- `LootService` (host): drop çözümü, coin hücre birleştirme, bütçe kontrolü.
- `PickupRegistry`: maks 256 aktif, `ushort` id, durum (spawned/claimed), despawn timer (coin 60 s). Cap aşılırsa aynı tip en yakınlar birleşir.
- `PickupRenderSystem` (her cihaz): instanced, shader'da bob/glow, zemin halkası; tip başına 1 draw.
- **Toplama:** client, pickup radius içine girince talep eder ve yerel "oyuncuya uçma" animasyonunu anında başlatır → host mesafeyi toleransla doğrular → `ClaimResult` yayınlar. Nadir red durumunda görsel geri alınır.
- Mesajlar: `PickupSpawnBatch{id, type, pos, value}` reliable, `PickupClaimed{id, playerId}` reliable.

### 13.3 Loot tabloları ve ekonomi
- `LootTableDefinition` SO: ağırlıklı girdiler, garanti girdiler, rulo sayısı, koşullar (minTime, oyuncu sayısı, Luck).
- **Ekonomi governor'ı:** hedef "dakika başı coin" eğrisi; sürü büyüdükçe ekonomi doğrusal şişmez (beklenen değer kontrolcüsü + kötü şans koruması).

---

## §14. Co-op Design

### 14.1 Paylaşılan / bireysel
- **Paylaşılan (eşit):** XP (herkes aynı miktarı alır, level'lar bireysel ilerler), temel run coin, event ödüllerinin tabanı, extraction kararı, harita durumu.
- **Bireysel:** build/upgrade seçimleri, silahlar, özel loot/ekipman, instanced consumable'lar, HP/armor.
- MVP'de class yok; roller build'lerden doğar. `PlayerDefinition` ileride class'lara genişler.

### 14.2 Downed / Revive
```
ALIVE ──HP=0──► DOWNED (bleedout 25 s) ──süre biter──► DEAD (spectate)
                   │                                      │
                   └─takım arkadaşı 2 m içinde 4 s─► ALIVE (%30 HP, 2 s dokunulmazlık)
                                                          │
                              Rescue Signal başarısı veya 60 s + takım hayatta ─► respawn (build korunur, %50 HP)
```
- Downed: %30 hızla sürünme, ateş yok (ileride: pistol upgrade'i). Zombiler downed oyuncuyu %50 daha az hedefler.
- Revive: yakınında durmak yeterli, buton yok. Hasar almak ilerlemeyi %50 yavaşlatır. Alan terk edilirse ilerleme hemen sıfırlanmaz, yavaşça söner.
- Aynı hayatta tekrar tekrar downed olmak bleedout'u hızlandırır (3. seferde %50 daha hızlı).
- **Solo:** run başına 1 "Adrenaline" kendi kendini kaldırma hakkı (upgrade ile artabilir); yoksa Downed = game over.
- Tüm oyuncular Downed/Dead → run biter.
- Tüm durumlar host otoritesinde; `PlayerVitals{hp, armor, state, bleedoutTimer, reviveProgress}`.

### 14.3 Takım geri bildirimi
Ekran kenarı okları (isim, mesafe, HP), downed arkadaşın nabız efekti + mini-map ping, üst sol takım listesi (isim, HP, durum). Hızlı ping çarkı ("Yardım/Buraya/Loot/Git") M13 sonrası.

### 14.4 Birlikte kalmak
Sert tether yok. Director, gruptan kopmuş oyuncuya hafif ek baskı uygular (yalnız kurt baskısı) → birlikte kalmak teşvik edilir ama görev için ayrılmak mümkün.

### 14.5 Bağlantı olayları
- MVP: katılım yalnızca lobby'de.
- Client kopması: host devam eder, avatar 10 s sonra kaldırılır; client'a kısmi ödüllü sonuç ekranı.
- Host çıkarsa / arka plana alırsa: oturum herkes için biter, client'lar kısmi ödülle kaydeder. **Host migration planlanmıyor.**
- İleride: aynı `playerGuid` ile 60 s içinde yeniden bağlanma.

### 14.6 Meta güç dengesi
Kalıcı ilerleme yataydır (§14.7). Yeni oyuncu ile 30 saatlik oyuncu aynı run'a **aynı güç bütçesiyle** başlar; fark yalnızca seçenek çeşitliliğindedir.

### 14.7 Meta Progression (D-005)
**Altın kural:** Hiçbir kalıcı unlock sayısal güç avantajı vermez. Maç içi tüm güç (level, upgrade, silah geliştirme, ekipman) run sonunda sıfırlanır.

| Meta kategori | İçerik | Güç etkisi | Co-op denge kuralı |
|---|---|---|---|
| **Silah açma** | Başlangıç havuzuna ve crate havuzuna yeni silah ekler | Yok — yeni silahlar **sidegrade** (aynı güç bütçesi, farklı oyun tarzı) | Kilitli silah, sahibi olmayan oyuncuya da run içinde crate'ten düşebilir |
| **Karakter** | Farklı görünüş + ses + 1 imza perk slotu | Yok (aynı base statlar) | Tüm karakterlerin güç bütçesi eşit |
| **Karakter perk'leri** | Oyun tarzını değiştiren takaslar: "Revive %30 hızlı / max HP −%5", "Pickup radius +%40 / reload −%8", "Granat +1 / başlangıç mermisi −%20" | **Net sıfır** (her perk bir artı + bir eksi) | Perk başına tek tek değer cap'i ±%10; sadece 1 aktif perk (MVP) |
| **Başlangıç loadout** | Başlangıç silahı + 1 consumable seçimi | Yok (tüm loadout'lar eşit tier) | |
| **Skin / Outfit** | Karakter görünümü | Yok | Siluet ve takım rengi halkası her skin'de okunaklı kalır |
| **Weapon skin** | Silah materyali/renk varyantı | Yok | Muzzle flash/tracer renkleri değişmez (okunabilirlik) |
| **Emote** | Kısa animasyon + ses (lobby ve run içi) | Yok | Ağda tek `EmoteMessage{playerId, emoteId}` |
| **Badge / Title** | Başarım ve istatistiklerden kazanılır, lobby ve isim etiketinde görünür | Yok | "Threat VII'de extract", "500 revive" gibi |

**Para birimleri:** tek kalıcı currency ("Scrap") — run sonunda kazanılır; silah/karakter/perk/loadout/kozmetik açar. İkinci bir premium para birimi MVP'de yok. İleride kozmetik satın alma eklenirse yalnızca kozmetik kategorilerine bağlanır (pay-to-win yok).

**Co-op'ta meta görünürlüğü:** lobby'de her oyuncunun karakteri, skin'i, title'ı görünür; farklı meta ilerlemedeki oyuncular aynı lobby'de tamamen eşit güçtedir.
