# Last Ground — Durum ve Yapılacaklar

_Son güncelleme: 2026-09-19 · Sonraki oturum buradan devam eder._

## Şu an neredeyiz
- **Branch:** `m7-map-events` (M6 main'e merge edildi ve push edildi; M7 commit'leri yalnızca yerelde).
- **M7 — Harita, Olaylar ve Çevre: onay bekliyor** (branch `m7-map-events`, merge/push yok). Rapor: `docs/reports/M7_REPORT.md`.
- EditMode testleri: **163 / 163** geçiyor. OnePlus 30 FPS, Redmi 60 FPS, oyun kodu GC 0 B/kare.
- Harita yeniden üretimi: `MinimapBaker.BakeBatch` (grafikli) → `RebuildScenesBatch`. `ProjectSetup` QualitySettings MSAA'yı 0'a çekiyor; commit'lemeden `git checkout ProjectSettings/QualitySettings.asset`.
- Zombiler artık gerçekçi Mixamo gövdeleri (8 karakter). Ham FBX'ler git dışında: yeniden bake için önce `MIXAMO_TOKEN=… python3 Tools/Mixamo/mixamo_fetch.py`, sonra `CrowdBaker.BakeAllBatch`. Görünüm kontrolü: `CrowdPreview.RenderBatch` (batchmode, `-nographics` olmadan).
- **Paralel oturum:** Codex aynı repoda `docs/reference`, `docs/ASSET_SOURCES.md`, `docs/MIXAMO_*` ve `Assets/ThirdParty/Mixamo` README/meta üzerinde çalışıyor; kullanıcı bunları ayrı commit'liyor. `git add -A` kullanma.
- `ProjectSettings` (MSAA 2x, ışık ayarları) kullanıcı onayıyla tutuldu.

## Sonraki oturumda ilk adımlar
1. Kullanıcıdan M7 onayını al (rapor: `docs/reports/M7_REPORT.md`; OPEN ISSUES'taki MSAA kararı dahil).
2. Onay gelince: `M7_REPORT.md` durumunu "Onaylandı" yap → commit → `git checkout main && git merge --no-ff m7-map-events` → `git push origin main m7-map-events` → M8 branch'i (TDD_03 §36; VirtualHorde M8'e ertelendi).

## Tamamlanan milestone'lar
| M | Konu | Durum |
|---|---|---|
| M0 | Foundation (proje, asmdef, tick loop, save, localization, CI komutları) | ✅ onaylı, main |
| M1 | Networking PoC (Mirror transport-only, LAN discovery, lobby, replikasyon) | ✅ onaylı, main |
| M2 | Zombie rendering benchmark (kemik texture GPU skinning, LOD, kalite preset'leri) | ✅ onaylı, main |
| M3 | Horde simulation (Burst ZombieWorld, flow field, katmanlı surround, AI LOD) | ✅ onaylı, main |
| M4 | Shooting & Combat — MVP kapısı (twin-stick, host doğrulamalı hit claim, windup'lı zombi saldırısı, VFX, ses) | ✅ onaylı, main |
| M5 | Endless loop (director, threat, downed/revive, XP + 12 upgrade, coin + instanced pickup, "Bölgeyi temizle", sonuç ekranı) | ✅ onaylı, main |
| M7 | Harita, olaylar, çevre (3 bölge, CC0 çevre, mini-harita, 6 olay, etkileşimliler, anti-kamp, bölge kartı) | ⏳ onay bekliyor |
| M6 | Combat Content I (6 silah, 2 slot, granat, Runner/Tank/Spitter/Exploder, elite, durum efektleri, spawn deck, gerçekçi Mixamo zombileri) | ✅ onaylı, main |

## M6'dan açık konular (rapordaki OPEN ISSUES)
- Denge (mermi, granat, elite, spawn deck) hipotez; insan playtest'i gerekli.
- Görsel: tablette üst satır can barıyla çakışıyor, coin'ler büyük, oyuncu kapsül, zombilerde normal map yok → M11.
- Ek zombi animasyon varyantları ve Runner sıçrama klibi; silah sesleri prosedürel (M12).

## M5'ten açık konular (rapordaki OPEN ISSUES)
- Denge: botlarla HORDE etiketi çoğunlukla LOW; insanla playtest gerek (`Assets/ScriptableObjects/Director/DIR_Default.asset`).
- Ertelenenler: VirtualHorde + HordeGroupSummary → M7; elite/anti-kamp → M6/M7; upgrade zaman aşımı ayarı → M9; spectate → M10; pickup uçma animasyonu, UI art → M11.
- Dev telemetrisi 5 s'de bir ~10–14 KB ayırıyor (yalnızca dev build; HUD "GC max"ı kirletiyor) → M13.
- Önceki milestone'lardan: Mirror/kcp2k client ~27 B/frame (üçüncü taraf), hotspot testi (OPEN_QUESTIONS B5), nihai zombi asset'i / Asset Store bütçesi (D1), anti-stuck savaşta ~2/s.

## Cihazlar ve bağlantı
- **OnePlus 5T** = LOW, USB, seri `3e415066`.
- **Redmi Pad Pro** = MID, kablosuz ADB. Son adres `192.168.1.14:33733`; IP ve port değişebilir → Redmi'de Geliştirici seçenekleri → Kablosuz hata ayıklama ekranından oku. Ekran kilitlenince/uykuda kablosuz hata ayıklama kapanıyor: testlerde "Uyanık kal" + şarj.
- Unity batchmode build/test adb sunucusunu yeniden başlatır → sonra `adb connect <ip>:<port>`.
- Port bulunamazsa: `adb connect` dene; olmazsa kullanıcıdan Kablosuz hata ayıklama ekranındaki IP:port'u iste.

## Sık kullanılan komutlar (ayrıntı: AGENTS.md §5)
```bash
U=~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity
$U -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults /tmp/editmode.xml
$U -batchmode -nographics -projectPath . -buildTarget Android -executeMethod LastGround.EditorTools.Setup.ProjectSetup.RebuildScenesBatch
$U -batchmode -nographics -projectPath . -buildTarget Android -executeMethod LastGround.EditorTools.Build.BuildScripts.BuildAndroidDevelopment
$U -batchmode -nographics -projectPath . -buildTarget Linux64 -executeMethod LastGround.EditorTools.Build.BuildScripts.BuildLinuxDevelopment
# İki cihaz testi (Redmi host, OnePlus client):
adb -s <redmi> shell "am start -S -n com.asgardgame.lastground/com.unity3d.player.UnityPlayerGameActivity -e lgargs '-lg-host -lg-start-at 2 -lg-wander -lg-autofire -lg-quit-after 960'"
adb -s 3e415066 shell "am start -S -n com.asgardgame.lastground/com.unity3d.player.UnityPlayerGameActivity -e lgargs '-lg-join <redmi-ip> -lg-wander -lg-autofire -lg-autopick -lg-quit-after 950'"
# Log: adb -s <cihaz> logcat -s Unity | grep NetStats  (logcat -G 16M ile tamponu büyüt)
```
Dev argümanları: `-lg-host`, `-lg-join IP`, `-lg-solo`, `-lg-start-at N`, `-lg-wander`, `-lg-autofire`, `-lg-autopick`, `-lg-quit-after S`, `-lg-bench [S]`, `-lg-quality N`, `-lg-gc-capture [S]`.
Not: Android'de `Application.Quit` sonrası süreç açık kalabiliyor; bitişi `[NetStats]`/`quit-after reached` log satırından anla, gerekirse `am force-stop`.
