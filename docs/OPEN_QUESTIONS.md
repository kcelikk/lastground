# Last Ground — Açık Sorular ve Bekleyen Onaylar

> Bu dosya cevaplanmayı bekleyen soruların tek listesidir. Cevaplanan madde `DECISIONS.md`'ye taşınır ve buradan silinir.
> Son güncelleme: 2026-09-18

## A. Yeni makine / kurulum (M0 öncesi — bloklayıcı)

> A1 (işletim sistemi) cevaplandı → **Ubuntu 26.04 LTS**, Unity 6.3 LTS ≥ 6000.3.13f1 şartıyla. Bkz. `DECISIONS.md` D-012.
> A2 (kurulum listesi), A3 (git kimliği), A4 (SSH key) cevaplandı → `DECISIONS.md` D-015.
> A5 (repo düzeni), A6 (branch düzeni) cevaplandı → `DECISIONS.md` D-016.
> A7 (M0 başlama onayı) verildi (2026-09-18). M0 raporu: `docs/reports/M0_REPORT.md`. **A bölümünde açık soru kalmadı.**

## B. Donanım / test

| # | Soru | Neden gerekli | Durum |
|---|---|---|---|
| B1 | T14'te boş SO-DIMM yuvası var mı (16 → 24/32 GB)? Cihaz geldiğinde `sudo dmidecode -t memory` ile birlikte bakabiliriz. Şart değil | Build ve editör konforu | ⏳ Bilgi |
| B2 | HIGH sınıf (Snapdragon 8 serisi vb.) bir cihaza erişim var mı? Test grubundan ödünç alınabilir mi? | Üst segment FPS doğrulaması (M10'a kadar gerekmez) | ⏳ Bilgi |
| B3 | 21 kişilik test grubundaki kaç kişi **aynı ortamda ikişerli** test yapabilir? LAN co-op yalnızca aynı Wi-Fi'da çalışır | Co-op test planı | ⏳ Bilgi |
| B4 | OnePlus 5T bağlantısı — **çözüldü** (2026-09-18: telefon recovery/sideload modundaydı, sonra USB debugging kapalıydı; TalkBack geliştirici menüsü Developer options ile karıştırılmıştı). Redmi Pad Pro henüz bağlanmadı | M1 iki cihaz testi | ⏳ Redmi bekliyor |
| B5 | **Hotspot testi (M1 çıkış kriteri):** telefonlardan biri hotspot açıp diğer cihaz ona bağlanınca discovery + oyun. Dizüstünü hotspot'a geçirmek Claude'un internet bağlantısını da taşır; senin onayınla ve uygun anda yapılır. Alternatif: iki telefonla (Redmi bağlanınca) | M1 onayı | ⏳ Bekliyor |

## C. Yayın / altyapı (M14'e kadar gerekmiyor)

| # | Soru | Neden gerekli | Durum |
|---|---|---|---|
| C1 | Google Play geliştirici hesabın var mı? (Yeni kişisel hesaplarda: production'a çıkmadan önce ≥12 testçi, 14 gün kapalı test) | Yayın takvimi | ⏳ Bilgi |
| C2 | Test dağıtımı **Play Internal Testing** üzerinden olsun mu (önerim), yoksa VPS'ten APK indirme sayfası mı? | Dağıtım akışı | ⏳ Bekliyor |
| C3 | VPS'in donanımı (CPU/RAM) nedir? Yalnızca gizlilik politikası + tanıtım sayfası mı olacak, ileride CI de mi? | VPS rolü | ⏳ Bilgi |
| C4 | Oyun adı "Last Ground" ile Play'de yayınlanacak mı? (İsim çakışması kontrolü M14'te yapılır). **Paket adı** şu an geçici: `com.asgardgame.lastground`, şirket adı `AsgardGame` — ilk Play yüklemesinden sonra paket adı değiştirilemez | Mağaza | ⏳ Bilgi |

## D. İçerik / tasarım (ilgili milestone'dan önce)

| # | Soru | Ne zaman gerekli | Durum |
|---|---|---|---|
| D1 | Asset Store bütçesi var mı, varsa yaklaşık ne kadar? (zombie + environment + weapon paketleri) | **M2 öncesi** | ⏳ Bekliyor |
| D2 | AI destekli texture/UI üretimi için hangi aracı kullanıyorsun? (ticari kullanım şartlarını `ASSET_SOURCES.md`'ye kaydedeceğim) | M2 / M11 | ⏳ Bilgi |
| D3 | Oyuncu karakteri: tek asker mi, baştan 2 karakter mi? (meta ilerleme için) | M9 | ⏳ Bilgi |
| D4 | Gore seviyesi: referans görseldeki yoğunluk mu, biraz daha ölçülü mü? (Play yaş derecelendirmesini etkiler) | M11 / M14 | ⏳ Bilgi |

## E. Benim aldığım, itiraz gelmezse geçerli sayılacak kararlar

1. Ammo / medkit / grenade pickup'ları oyuncu başına ayrı kopya (kapışma olmasın diye).
2. Takım tamamen düşerse run coin'in %60'ı kalıcı currency'e dönüşür; extract edilirse %100 + bonus.
3. Varsayılan dil İngilizce; cihaz dili Türkçe ise ilk açılışta Türkçe önerilir.
4. Perk'ler "net sıfır" takas (bir artı + bir eksi, ±%10 sınırı), MVP'de tek aktif perk.
5. Localization için Unity Localization paketi yerine hafif JSON string tablosu.
6. Zombi görselleri VAT (vertex animation texture) + GPU instancing ile çizilir; skinned mesh yalnızca oyuncu ve boss için.
