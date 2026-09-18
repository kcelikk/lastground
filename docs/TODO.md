# Last Ground — Durum ve Yapılacaklar

_Son güncelleme: 2026-09-18 · Sonraki oturum buradan devam eder._

## Şu an neredeyiz
- **Branch:** `m5-loop` (main'e merge edilmedi, GitHub'a push edilmedi).
- **M5 — Endless Loop Core: bitti, kullanıcı onayı bekliyor.** Rapor: `docs/reports/M5_REPORT.md`, director grafikleri: `docs/benchmarks/M5/`.
- EditMode testleri: **129 / 129** geçiyor.
- Kullanıcıya sorulan açık soru: son iki telefon testinde Redmi host otomatik seçim olmadan her level'da upgrade seçti — tablete dokunuldu mu? (Dokunarak/otomatik seçim sayaçları eklendi.)

## Sonraki oturumda ilk adımlar
1. Kullanıcıdan M5 onayını al (ve yukarıdaki soruyu netleştir).
2. Onay gelince: `M5_REPORT.md` durumunu "Onaylandı" yap → commit → `git checkout main && git merge --no-ff m5-loop` → `git push origin main m5-loop` → `git checkout -b m6-combat-content`.
3. **M6 — Combat Content I**'e başla (TDD_03 §36): 6 silah (Pistol, SMG, AR, Shotgun, Sniper, Machine Gun — değerler TDD_01 §6.4), pellet + `ProjectileSystem` (grenade), 2 silah slotu, mermi ekonomisi; Runner, Tank, Spitter, Exploder; elite modifier'lar; durum efektleri (burn/slow/stun). Çıkış: her tip gerçek cihazda bütçe içinde, ağda doğru.

## Tamamlanan milestone'lar
| M | Konu | Durum |
|---|---|---|
| M0 | Foundation (proje, asmdef, tick loop, save, localization, CI komutları) | ✅ onaylı, main |
| M1 | Networking PoC (Mirror transport-only, LAN discovery, lobby, replikasyon) | ✅ onaylı, main |
| M2 | Zombie rendering benchmark (kemik texture GPU skinning, LOD, kalite preset'leri) | ✅ onaylı, main |
| M3 | Horde simulation (Burst ZombieWorld, flow field, katmanlı surround, AI LOD) | ✅ onaylı, main |
| M4 | Shooting & Combat — MVP kapısı (twin-stick, host doğrulamalı hit claim, windup'lı zombi saldırısı, VFX, ses) | ✅ onaylı, main |
| M5 | Endless loop (director, threat, downed/revive, XP + 12 upgrade, coin + instanced pickup, "Bölgeyi temizle", sonuç ekranı) | ✅ onaylı, main |

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
