# M0 Raporu — Architecture & Project Foundation

- **Tarih:** 2026-09-18 · **Branch:** `m0-foundation` · **Unity:** 6000.3.24f1 · **Durum:** ✅ Onaylandı (2026-09-18), `main`e merge edildi
- Çıkış kriterleri (TDD_03 §36): APK telefonda açılıyor ✅ (OnePlus 5T) · profiler bağlantı noktası ✅ (adb forward ile el sıkışma; Editor Profiler penceresinden bağlanma geliştiricide) · EN↔TR dil değişimi ✅ (cihazda) · repo başka ortamda clone edilip açılıyor ✅ (temiz clone, aynı makine)

## CREATED / CHANGED FILES
| Assembly | Dosyalar |
|---|---|
| `LastGround.Core` | `Tick/` TickLoop, TickScheduler, TickPhase, TickPhaseInfo, ITickable · `Run/` RunContext, RunRole · `Net/` ISession, ICommandSink, INetCommand, IGameEventStream, INetClock, ILanDiscovery, DiscoveredHost, SessionState · `Events/` EventChannel, EventReader · `Random/` DeterministicRandom (PCG32), Hash32 · `Ids/` PlayerId, ZombieHandle, PickupId, PropId · `Pooling/` PoolService, ComponentPool, RingBuffer, IPoolable · `Services/` AppServices · `Logging/` Log, LogCategory |
| `LastGround.Localization` | JsonLocalizationService, ILocalizationService, ILocalizationSource, ResourcesLocalizationSource, LanguageCatalog, LanguageInfo, LocalizedText, JsonSettings |
| `LastGround.Save` | JsonFileSaveStore, ISaveStore, SaveService, ISaveService, SettingsData, Crc32 |
| `LastGround.UI` | MainMenuScreen, SafeAreaFitter, PerfHud |
| `LastGround.App` | AppRoot, BootLoader, QualityDefaults, SceneNames |
| `LastGround.Editor` | ProjectSetup, QualityLevels, SceneBuilder, BuildScripts, LocalizationValidator |
| `LastGround.Tests.EditMode` | 9 test sınıfı, 35 test |
| Boş (sınır için) | Data, Gameplay, Input, Rendering, Audio, Meta, Platform, Networking — asmdef + README + AssemblyInfo |
| Veri / ayar | `Resources/Localization/{languages.json, en/ui.json, tr/ui.json}` · `Settings/Rendering/URP_{Low,Medium,High}` + renderer'lar · `Scenes/Boot`, `Scenes/Menu` · TMP Essential Resources |
| Stripping | `Assets/link.xml` — JSON veri sınıfları (LanguageCatalog, LanguageInfo, SettingsData) |
| Kök | `.gitignore`, `.gitattributes` (LF, LFS, UnityYAMLMerge), `AGENTS.md`, `CLAUDE.md`, `Packages/manifest.json` |

## UNITY EDITOR ACTIONS
Yok. Tüm ayarlar `LastGround/Setup/Apply Project Settings` (idempotent; 2. çalıştırmada diff yok) ile üretildi. Menu sahnesi görsel olarak düzenlenebilir; SceneBuilder mevcut sahnenin üzerine yazmaz.

## INSPECTOR CONFIGURATION
Elle ayar yok. Player: IL2CPP · ARM64 · min API 26 · target Auto · Vulkan + GLES3 · ASTC · GameActivity · Optimized Frame Pacing · Render outside safe area · Landscape L/R · Linear · Incremental GC · stripping Medium · Input System only · Force Text · Visible Meta Files.
URP (TDD_02 §21.9): LOW 0.7 scale / MSAA yok / HDR yok / gölge yok / ek ışık 0 · MEDIUM 0.85 / 2× / HDR / gölge 512 / 2 ışık · HIGH 1.0 / 4× / HDR / gölge 1024 / 4 ışık · hepsinde depth/opaque texture kapalı, SRP Batcher açık.

## ANDROID BUILD STEPS
```
Unity -batchmode -nographics -projectPath . -buildTarget Android \
  -executeMethod LastGround.EditorTools.Build.BuildScripts.BuildAndroidDevelopment
```
- Sonuç: **başarılı**, 0 hata / 0 uyarı · ilk IL2CPP build **9:00 dk** (tahmin 12–20 dk) · tepe RAM 2.9 GB
- `Builds/Android/LastGround-dev.apk` — **45.0 MB** (development, IL2CPP ARM64) · artımlı build **0.9 dk**
- Kurulum: `adb install -r` (streamed, ~20 s) → OnePlus 5T (Android 10, API 29), Vulkan 1.1 / Adreno 540 ile açıldı

## TEST RESULTS
EditMode **35/35 geçti** (ana çalışma alanı ve temiz clone): DeterministicRandom (PCG32 referans değerleri sabitlendi), EventChannel, RingBuffer, TickLoop (sabit adım, hitch sınırı, pause, faz sırası), Localization (fallback, tr-TR kültüründe lookup/format, validator), Save (roundtrip, bozuk dosyada .bak, debounce), QualityDefaults, CodeRules (ToUpper/ToLower, culture'sız Parse, string.Compare, UnityEngine.Random taraması), LinkXml (JSON tipleri korunuyor mu).

**Cihaz testi (OnePlus 5T):**
- İlk kurulumda **hata bulundu:** IL2CPP stripping `LanguageCatalog` constructor'ını sildi → `JsonSerializationException`, localization başlamadı, menüde key'ler göründü. Editor testleri bunu yakalayamaz. Düzeltme: `link.xml` + `LinkXmlTests` + AGENTS.md kuralı. Aynı risk `SettingsData` için de vardı (ikinci açılışta patlayacaktı).
- Düzeltme sonrası: menü EN açıldı; dil düğmesi → TR (İ, Ç, Ş, Ü glifleri doğru); uygulama kapatılıp açılınca `tr` korundu (`settings.json` CRC başlıklı yazılmış); logcat'te exception yok.
- Otomatik kalite: 5.6 GB RAM → **LOW (Q0), 30 FPS kilidi** (D-013 ile uyumlu).

## PROFILE
OnePlus 5T · Android 10 · 2160×1080 · LOW (render scale 0.7) · menü sahnesi, PerfHud:

| Durum | FPS | Frame | GC | Bellek |
|---|---|---|---|---|
| Açılışın ilk 0.5 s'si | 18 | 55.9 ms (max 241.8) | 2.1 MB/f (yükleme) | 116 MB |
| Kararlı | **31** (30 kilit) | 32.7 ms (max 32.7) | **0 B/f** | 118 MB |

Menü sahnesi performans kanıtı değildir; gerçek bütçe ölçümü M2 benchmark'ında. MID cihaz (Redmi Pad Pro) bu milestone'da denenmedi.

## DECISIONS / DEVIATIONS
| Konu | TDD | Uygulanan | Gerekçe |
|---|---|---|---|
| Localization konumu | `Assets/Localization/` | `Assets/Resources/Localization/` | Addressables olmadan runtime yükleme; TDD_02 §24A.2 güncellendi |
| Unity Localization / Addressables | kullanılmaz | kullanılmadı | — |
| TMP Essentials | Editor importu | `.unitypackage` doğrudan açıldı (GUID'ler korunarak) | Batchmode'da `ImportPackage` Exit'ten önce bitmiyor |
| Adaptive Performance | paket M0'da | yalnızca built-in modül; Android provider M13'te | Ayarlanmadan eklemek fayda sağlamıyor |
| Mirror | M0'da klasöre, M1'de bağlanır | eklenmedi | M1'in ilk işi |
| `RunInstaller` | M0 | M1'e kaldı | Run sahnesi M1'de doğuyor |
| Settings ekranı | — | M0'da yalnızca ana menüde dil düğmesi | Dil değişimi kriteri için yeterli |
| .NET SDK | 8 | 10 (LTS) | 26.04 deposunda 8 yok (D-015) |
| Paket adı | — | `com.asgardgame.lastground` (geçici) | OPEN_QUESTIONS C4 |
| Servis erişimi | composition root | `AppServices` kaydı; Gameplay'de yasak (AGENTS.md §3) | UI bağlamaları için pratik |

## OPEN ISSUES
1. Editor Profiler penceresinden canlı bağlantı geliştirici tarafında denenmeli (player bağlantı noktası doğrulandı). Redmi Pad Pro henüz bağlanmadı.
2. İkinci OS'te clone testi yapılmadı (Windows makine yok); temiz clone aynı makinede doğrulandı.
3. Font: TMP LiberationSans + dinamik fallback (Türkçe glifler dinamik atlas'tan). Condensed OFL font M11'de.
4. Unity log'unda zararsız `dotnet build-server … No .NET SDKs were found` satırı (Unity'nin kendi dotnet'i); derlemeyi etkilemiyor.
5. Sonraki: **M1 — Networking PoC** (Mirror, LoopbackSession, LAN discovery, lobby, 300 dummy snapshot). M1 için 2 cihazın aynı Wi-Fi'da bağlı olması gerekiyor.
