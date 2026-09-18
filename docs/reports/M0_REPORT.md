# M0 Raporu — Architecture & Project Foundation

- **Tarih:** 2026-09-18 · **Branch:** `m0-foundation` · **Unity:** 6000.3.24f1 · **Durum:** APPROVE bekliyor
- Çıkış kriterleri (TDD_03 §36): APK telefonda açılıyor ⏳ · profiler bağlanıyor ⏳ · EN↔TR dil değişimi ✅ (Editor/test) · repo başka ortamda clone edilip açılıyor ✅ (temiz clone, aynı makine)

## CREATED / CHANGED FILES
| Assembly | Dosyalar |
|---|---|
| `LastGround.Core` | `Tick/` TickLoop, TickScheduler, TickPhase, TickPhaseInfo, ITickable · `Run/` RunContext, RunRole · `Net/` ISession, ICommandSink, INetCommand, IGameEventStream, INetClock, ILanDiscovery, DiscoveredHost, SessionState · `Events/` EventChannel, EventReader · `Random/` DeterministicRandom (PCG32), Hash32 · `Ids/` PlayerId, ZombieHandle, PickupId, PropId · `Pooling/` PoolService, ComponentPool, RingBuffer, IPoolable · `Services/` AppServices · `Logging/` Log, LogCategory |
| `LastGround.Localization` | JsonLocalizationService, ILocalizationService, ILocalizationSource, ResourcesLocalizationSource, LanguageCatalog, LanguageInfo, LocalizedText, JsonSettings |
| `LastGround.Save` | JsonFileSaveStore, ISaveStore, SaveService, ISaveService, SettingsData, Crc32 |
| `LastGround.UI` | MainMenuScreen, SafeAreaFitter, PerfHud |
| `LastGround.App` | AppRoot, BootLoader, QualityDefaults, SceneNames |
| `LastGround.Editor` | ProjectSetup, QualityLevels, SceneBuilder, BuildScripts, LocalizationValidator |
| `LastGround.Tests.EditMode` | 8 test sınıfı, 33 test |
| Boş (sınır için) | Data, Gameplay, Input, Rendering, Audio, Meta, Platform, Networking — asmdef + README + AssemblyInfo |
| Veri / ayar | `Resources/Localization/{languages.json, en/ui.json, tr/ui.json}` · `Settings/Rendering/URP_{Low,Medium,High}` + renderer'lar · `Scenes/Boot`, `Scenes/Menu` · TMP Essential Resources |
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
- `Builds/Android/LastGround-dev.apk` — **44.5 MB** (development, IL2CPP ARM64)
- Cihaza kurulum: **yapılmadı** — OnePlus 5T USB'de algılanmıyor (OPEN_QUESTIONS B4, ertelendi)

## TEST RESULTS
EditMode **33/33 geçti** (ana çalışma alanı ve temiz clone): DeterministicRandom (PCG32 referans değerleri sabitlendi), EventChannel, RingBuffer, TickLoop (sabit adım, hitch sınırı, pause, faz sırası), Localization (fallback, tr-TR kültüründe lookup/format, validator), Save (roundtrip, bozuk dosyada .bak, debounce), QualityDefaults, CodeRules (ToUpper/ToLower, culture'sız Parse, string.Compare, UnityEngine.Random taraması).

## PROFILE
**Ölçülmedi** — cihaz bağlanamadı. PerfHud (FPS, frame ms, GC B/frame, bellek, kalite) dev build'de hazır; ilk ölçüm cihaz bağlanınca.

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
1. **Cihaz testi (B4):** APK kurulumu, açılış, profiler bağlantısı ve cihazda EN↔TR — telefon bağlanınca `adb install -r Builds/Android/LastGround-dev.apk`.
2. İkinci OS'te clone testi yapılmadı (Windows makine yok); temiz clone aynı makinede doğrulandı.
3. Font: TMP LiberationSans + dinamik fallback (Türkçe glifler dinamik atlas'tan). Condensed OFL font M11'de.
4. Unity log'unda zararsız `dotnet build-server … No .NET SDKs were found` satırı (Unity'nin kendi dotnet'i); derlemeyi etkilemiyor.
5. Sonraki: **M1 — Networking PoC** (Mirror, LoopbackSession, LAN discovery, lobby, 300 dummy snapshot). M1 için 2 cihazın aynı Wi-Fi'da bağlı olması gerekiyor.
