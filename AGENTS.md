# Last Ground — AI Geliştirme Kuralları

Bu dosya Claude Code, Codex ve diğer AI ajanları için bağlayıcı çalışma kurallarıdır. İnsan geliştiriciler için de geçerlidir.

## 1. Kaynaklar ve öncelik
1. `docs/DECISIONS.md` — onaylı kararlar. **Çelişkide her zaman bu geçerlidir.**
2. `docs/TDD_01…03` — mimari ve tasarım (v0.2). Sayısal balance değerleri hipotezdir.
3. `docs/OPEN_QUESTIONS.md` — cevap bekleyen sorular. Cevaplanan madde DECISIONS'a taşınır.
4. Kararla çelişen bir istek gelirse uygulamadan önce çelişkiyi belirt ve onay iste.

## 2. Çalışma akışı
- Milestone döngüsü: **DESIGN → IMPLEMENT → COMPILE → TEST → PROFILE → APPROVE**. Onay olmadan sonraki milestone'a geçilmez (TDD_03 §36).
- Branch: `main` her zaman derlenir. Her milestone kendi branch'inde (`m0-foundation`, `m1-networking`…) yapılır, onaydan sonra `main`'e merge edilir (D-016).
- Commit: küçük ve tek konulu. İngilizce, emir kipinde başlık (`Add TickLoop fixed-step scheduler`).
- Her milestone sonunda §7'deki formatta rapor verilir.

## 3. Mimari kurallar (derleyici zorlar — asmdef)
```
Core ← Data ← Gameplay ← Rendering / UI (read-only) ; Networking ← Core, Data, Gameplay, Platform, Mirror
Localization ← Core   Save / Meta ← Core, Data   Platform ← Core   App ← hepsi (composition root)
```
- **Gameplay → Mirror ✘, Gameplay → UI ✘, Gameplay → Save ✘.** Mirror tipleri yalnızca `LastGround.Networking` içinde (D-001).
- Ağ mesajları `Core/Net` altında düz struct'lar; `SyncVar`, `[Command]`, `[Rpc]` yalnızca adapter sınıflarında (TDD_02 §15.11).
- Rol kontrolü (`Host/Client/Offline`) yalnızca installer ve network adapter'larında; gameplay koduna `if (isServer)` serpiştirilmez.
- `AppServices` (servis kaydı) yalnızca App, installer'lar ve UI/presentation bağlamalarında kullanılır. Gameplay sistemleri bağımlılıklarını constructor ile alır.
- Presentation (VFX/ses/UI) event dinler; event'in sim'den mi ağdan mı geldiğini bilmez.
- Zombiler GameObject/NavMeshAgent/NetworkObject **değildir** — SoA veri + instanced render (TDD_01 §8).

## 4. Kod kuralları
- Dosya başına bir tip, hedef ≤ 250–300 satır. Namespace = assembly root namespace + klasör.
- Public API'de kısa `/// <summary>`.
- Sihirli sayı yok: balance ScriptableObject'te; teknik sabitler `NetworkTuningProfile` / `QualityPresetDefinition` vb. içinde.
- SO'lar runtime'da **salt okunur**; runtime durum saf C# sınıflarında.
- Her sistem klasöründe kısa `README.md`: sorumluluk, bağımlılıklar, "değiştirirsen etkilenenler".
- Kod yorumları ve identifier'lar İngilizce; dokümanlar Türkçe.

### Hot path (savaş sırasında çalışan her kod)
- Hedef **0 B/frame GC**. LINQ, closure/lambda yakalama, boxing, `string` birleştirme, `foreach` over interface yok.
- Nesne başına `Update()` yok — sistemler `ITickable` olup `TickScheduler`'a kaydolur. Gameplay'de coroutine yok.
- Gameplay sırasında `Instantiate/Destroy` yok — pool kullan (TDD_02 §20).
- UI sayıları TMP `SetText(format, number)` ile.

### Determinizm
- Rastgelelik yalnızca `DeterministicRandom` (run seed + alt akış). `UnityEngine.Random` / `System.Random` yasak.
- Sim zamanı host tick'inden türetilir; sim kodunda `Time.time` yok.
- Kimlikler: `PlayerId`, `ZombieHandle`, `PickupId`, `PropId`, SO `id` string'leri. Ağda GameObject referansı yok.

### Türkçe kültür tuzakları (TDD_02 §24A.4)
- `ToUpper()`/`ToLower()` yasak → `ToUpperInvariant()` veya büyük harfli metni tabloya yaz.
- Sayı parse/format: `CultureInfo.InvariantCulture`. Karşılaştırma: `StringComparison.Ordinal`.
- `CodeRulesTests` bu kuralları tarar; testi geçmeyen kod merge edilmez.

### Localization (D-010)
- Kodda kullanıcıya görünen metin yok. Key biçimi `alan.alt_alan.öğe` (küçük harf, nokta).
- Tablolar: `Assets/Resources/Localization/{dil}/{tablo}.json`. İngilizce referans dildir.
- Yeni key → hem `en` hem `tr` tablosuna. `LastGround/Localization/Validate` (ve test) eksik/fazla key ve `{0}` uyuşmazlığını yakalar.
- İstisna: yalnızca dev build'lerde görünen teşhis metinleri (PerfHud) lokalize edilmez.

## 5. Komutlar
Unity: `~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity` (bkz. D-012; 6.6 serisi kullanılmaz).
```bash
U=~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity
# Derleme kontrolü
$U -batchmode -nographics -quit -projectPath . -logFile - | grep -E "error CS|warning CS"
# EditMode testleri
$U -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults /tmp/editmode.xml
# Proje ayarlarını yeniden uygula (idempotent)
$U -batchmode -nographics -projectPath . -buildTarget Android -executeMethod LastGround.EditorTools.Setup.ProjectSetup.ApplyAllBatch
# Android development APK → Builds/Android/LastGround-dev.apk
$U -batchmode -nographics -projectPath . -buildTarget Android -executeMethod LastGround.EditorTools.Build.BuildScripts.BuildAndroidDevelopment
# Cihaza kur (Unity'nin adb'si PATH'te önde)
adb install -r Builds/Android/LastGround-dev.apk
```
- Unity Editor açıkken batchmode aynı projeyi açamaz; önce Editor'ü kapat.
- **Performans verisi yalnızca gerçek telefondan** (OnePlus 5T = LOW, Redmi Pad Pro = MID). Editor ölçümü kabul edilmez.

## 6. Unity ayarları (değiştirme)
Force Text serialization, Visible Meta Files, Input System only, Linear, IL2CPP ARM64, min API 26, Vulkan + GLES3, ASTC, URP Forward, kalite seviyeleri LOW/MEDIUM/HIGH (`Assets/Settings/Rendering/URP_*`). Hepsi `ProjectSetup.cs` içinde; elle değiştirmek yerine script'i güncelle.

## 7. Milestone rapor formatı
```
## Mx Raporu
CREATED / CHANGED FILES     — assembly bazında liste
UNITY EDITOR ACTIONS        — geliştiricinin Editor'de yapması gerekenler (yoksa "yok")
INSPECTOR CONFIGURATION     — elle ayarlanan alanlar
ANDROID BUILD STEPS         — komut, süre, APK boyutu
TEST RESULTS                — EditMode/PlayMode sayıları, başarısızlar
PROFILE                     — cihaz, FPS, frame ms, GC/frame, bellek (ölçülmediyse "ölçülmedi")
DECISIONS / DEVIATIONS      — TDD'den sapmalar ve gerekçesi
OPEN ISSUES                 — bilinen sorunlar, sonraki adımlar
```
