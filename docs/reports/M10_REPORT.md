# M10 Raporu — 4 Oyuncu ve Host Yükü

- **Tarih:** 2026-09-19 · **Branch:** `m10-four-player` · **Unity:** 6000.3.24f1 · **Durum:** ⏳ Onay bekliyor
- **Cihazlar:** Redmi Pad Pro = MID (USB `368a7a72`, Wi-Fi 192.168.1.14) · OnePlus 5T = LOW (USB `3e415066`) · 3. ve 4. oyuncu: aynı LAN'daki PC'de iki Linux dev build'i (D-023)
- **Kararlar:** D-023 (2 telefon + 2 PC botu)

## Çıkış kriterleri (TDD_03 §36 M10)
| Kriter | Sonuç |
|---|---|
| 4P + 300 sim zombi, host MID'de bütçe içinde | ✅ Redmi host, 4 oyuncu, 300 zombi: ortalama 60.0 FPS (5 s'lik en kötü pencere 54.8), zombi sim 1.1 ms (en fazla 1.6), governor 0.98–1.00 |
| 30 dk soak | ✅ 32 dk 4 oyunculu run (boss dahil): dört oyuncu sonuna kadar bağlı, iki telefonda **0 exception**, OnePlus client ortalama 30.6 FPS |
| Host ağ bütçesi | ✅ Host gönderimi (3 client) Redmi soak'unda ort. 14.7 / en fazla 37.7 KB/s; LOW host + 300 zombide en fazla 59.3 KB/s (hedef ≤ 60). Client alımı en fazla 16.3 KB/s (≤ 20) |
| Bağlantı kopması akışları | ✅ Client kopunca host devam ediyor, avatar 10 s donuk kalıp siliniyor; host kopunca client'ta "BAĞLANTI KOPTU" + kısmi ödül (cihazda: 24 coin → 14 Hurda bankalandı) |
| Takım UI | ✅ 3 takım arkadaşı satırı (ayrılan oyuncuda "BAĞLANTI KOPTU"), ölüyken takım arkadaşını izleme |
| Test matrisi (§30.4) | 🟡 Ev router'ı (2.4/5 GHz ayrımı yapılmadı) ile 4 oyuncu; hotspot ve misafir ağı bu milestone'da denenmedi (OPEN ISSUES) |

## Uygulanan
1. **Kopan oyuncu (TDD_02 §19.5):** oturumdan ayrılan oyuncu her cihazda 10 s boyunca donuk avatar olarak kalır (ateş etmez, takım listesinde "BAĞLANTI KOPTU", halkası soluk), sonra silinir. Host'un son durum mesajları silinen oyuncuyu geri getirmez.
2. **Host kopması:** run sırasında host kaybolursa client menüye atılmaz; kendi gördüğü süre, en yüksek tehdit, zombi ölümleri ve takım coin'iyle "BAĞLANTI KOPTU" sonuç ekranı açılır. Ödül wipe gibi hesaplanır (%60) ve profile bankalanır. Host uygulamayı arka plana alırsa client'lar 10 s sonra (kcp zaman aşımı) aynı ekranı görmeli; cihazda yalnızca zorla kapatma denendi.
3. **İzleme (spectate):** ölü oyuncunun kamerası yaşayan bir takım arkadaşını izler; ölüm şeridinin altında "İZLENİYOR: isim" + SONRAKİ düğmesi. Dönünce kamera kendi bedenine geçer.
4. **Ekran açık:** run boyunca `Screen.sleepTimeout = NeverSleep` (TDD_02 §19.5), çıkışta sistem ayarına döner.
5. **Katılma sağlamlığı:** ilk UDP bağlantısı düşerse (Wi-Fi güç tasarrufu/ARP) katılma penceresi içinde 0.5 s arayla 3 deneme. PC botlarında ilk denemenin ara sıra "ConnectFailed" vermesiyle bulundu.
6. **Kimlik:** varsayılan oyuncu adı artık GUID'den türetiliyor (aynı anda açılan iki cihaz aynı "Player-XXXX" adını alıyordu).
7. **Dev:** `-lg-damage-scale X` (host; soak botlarının hayatta kalması için), `-lg-save-slot NAME` (aynı PC'de her bota ayrı kayıt ve kimlik).

## CREATED / CHANGED FILES
| Assembly | Dosyalar |
|---|---|
| Core | NetSession(.Client) (transport yeniden deneme), SessionConfig (ConnectAttempts, ConnectRetryDelay) |
| Gameplay | `Players/` SpectatorTarget, PlayerStateTable (Disconnected, MarkDisconnected), PlayerHealthSystem (DamageScale) · `Run/` ConnectionLossTracker, RunResult (ConnectionLost) |
| Networking | PlayerSync (10 s kopma süresi, ayrılanı geri getirmeme) |
| Rendering | TopDownCameraRig (izleme), PlayerBodyRenderer (ayrılan oyuncunun halkası soluk) |
| UI | `Run/` SpectatorBar, TeamPanel ("BAĞLANTI KOPTU"), ResultsScreen (başlık) |
| App | SessionService (RunConnectionLost), RunInstaller(.Session), AppRoot (kayıt yuvası, isim), DevAutomation |
| Editor | RunSceneBuilder(.Session) |
| Tests | FourPlayerTests (4), NetSessionTests (+1, yeniden deneme), ReplicationTests (kopma süresi) |
| İçerik | Run/Menu sahneleri, EN/TR key'leri (+5) |

## UNITY EDITOR ACTIONS
Yok.

## INSPECTOR CONFIGURATION
Yok (sahne kurucuları atar).

## ANDROID BUILD STEPS
`BuildScripts.BuildAndroidDevelopment`, ~3 dk, APK **92.2 MB**. Linux dev build (`BuildLinuxDevelopment`, ~5.5 dk) PC botları için.
PC botu: `Builds/Linux/LastGround.x86_64 -screen-width 320 -screen-height 180 -screen-fullscreen 0 -lg-quality 0 -lg-save-slot bot1 -lg-join <host-ip> -lg-wander -lg-autofire -lg-autopick`

## TEST RESULTS
- EditMode: **186 / 186** (M9: 181). Yeni: 3 client + 300 zombide host gönderimi ≤ 60 KB/s ve her client ≤ 20 KB/s (loopback: 19.3 KB/s); ayrılan oyuncu her cihazda 10 s donuk sonra silinir; ölüyken izleme sırası, ölü/ayrılan atlanır, dönünce kendi bedeni; host kaybında kısmi sonuç (wipe oranı); ilk bağlantı hatası yeniden denenir ve host açılınca katılır.
- Cihaz: 4 oyuncunun lobby'si ve run başlangıcı; run ortasında bot kapatıldı → host 3 oyuncuyla sürdü; host zorla kapatıldı → OnePlus'ta "BAĞLANTI KOPTU" + Hurda +14; OnePlus ölünce kamera takım arkadaşlarına geçti.

## PROFILE
| Ölçüm | Cihaz | Sonuç |
|---|---|---|
| 32 dk soak, 4P, host | Redmi (MID) | ort. 60.0 FPS, 5 s'lik en kötü 54.8; ilk saniyedeki sahne yüklemesi dışında en uzun kare 42 ms; sim ort. 1.1 / en fazla 1.6 ms; 300 zombi; governor ≥ 0.98 |
| 32 dk soak, 4P, client | OnePlus (LOW) | ort. 30.6 FPS, en kötü 26.9; ilk saniye dışında en uzun kare 49 ms; alım ort. 4.6 / en fazla 16.3 KB/s |
| 4P host, 300 zombi (run 18. dk'dan) | OnePlus (LOW) | sabit 30.6 FPS, en uzun kare 34 ms, sim ≤ 2.3 ms, governor 1.00, gönderim en fazla 59.3 KB/s |
| GC, 4P host, 301 kare | Redmi | oyun kodu **0 B/kare**; kcp2k host alımı ~1.5 KB/kare (üçüncü taraf, client sayısıyla artıyor), Mirror `OnGUI` ~367 B (yalnızca dev) |

## DECISIONS / DEVIATIONS
- **PerformanceGovernor ayarı değişmedi:** iki host sınıfında da 4P + 300 zombide bütçe aşılmadı (governor en düşük 0.98). Eşikler (%15 üst, 0.5 taban) yerinde bırakıldı; gerçek ısıl yük altında (uzun oturum, sıcak cihaz) izlenecek.
- **Relevance/bütçe ayarı değişmedi:** client başına 600 B/tick priority accumulator, host toplamını 3 client'ta ~60 KB/s'de tutuyor; LOW host'ta bu sınıra dayanıldı ama aşılmadı.
- **PC botları pencereli:** `-batchmode` (headless) Linux build'inde kcp2k istemcisi ilk alımda soket hatası verip kopuyor; pencereli modda sorun yok. Botlar 320×180 pencerede çalıştırıldı. Performans ölçümü yalnızca telefonlardan (D-023).
- **Yeniden bağlanma ve host migration yok** (TDD_03 §39, MVP dışı): ayrılan oyuncu aynı run'a dönemez.
- **Ölüyken izleme yalnızca ölü (Dead) durumda:** düşmüş (Downed) oyuncu kendi bedenini görür (kaldırılmayı bekler).

## OPEN ISSUES
- **kcp2k host GC:** ~1.5 KB/kare (3 client), M7'de ~0.9 KB. Üçüncü taraf (kaynağı muhtemelen `ReceiveFrom` endpoint ayırması, doğrulanmadı) → M13 (kcp2k güncellemesi ya da yama).
- **Test matrisi eksikleri (§30.4):** host hotspot, üçüncü telefon hotspot, misafir ağı (beklenen hata mesajı) ve 2.4/5 GHz ayrımı denenmedi; ödünç telefonla 4 telefon doğrulaması isteğe bağlı (D-023).
- Headless Linux botunda kcp2k kopması (yalnızca test aracı).
- M8/M9'dan: boss dengesi ve fiyatlar playtest bekliyor; dev telemetrisi → M13.
