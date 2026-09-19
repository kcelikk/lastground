# Vertical Slice Playtest — Yönerge (VS1)

Tarih: 2026-09-19 · Build: `main` @ M8 (`abc1e1c`) · Karar: D-022

Amaç: 20–30 dakikalık "hayatta kal → boss → tahliye" döngüsünün **eğlenceli, anlaşılır ve iki–dört telefonda sorunsuz** olup olmadığını gerçek oyuncularla görmek (TDD_03 §35). Özellikle cevap aradığımız sorular:

1. İlk 3 dakika anlaşılıyor mu (hareket, nişan, otomatik ateş, level-up seçimi)?
2. Director'ın gerilim–tepe–nefes dalgası hissediliyor mu, sıkıcı veya bunaltıcı an var mı?
3. Harita olayları (erzak düşüşü, jeneratör, silah deposu…) takımı gerçekten haritada gezdiriyor mu?
4. **Boss (Mutant Brute, 12:00 civarı)** adil mi: saldırılar okunuyor mu, kaçılabiliyor mu, kaç denemede yenildi?
5. Tahliye kararı ("şimdi çık mı, devam mı?") ilgi çekici mi, iniş alanı bulunuyor mu?
6. Co-op: bağlanma, düşen arkadaşı kaldırma, takım panosu anlaşılır mı; takılma/kopma oldu mu?

## Hazırlık (oturumu yöneten kişi)

- **APK:** `Builds/Android/LastGround-dev.apk` (89 MB). Telefonlara kopyala (USB, Drive, Bluetooth). Android 8.0+ ve 64-bit gerekir.
- Kurulum: dosyaya dokun → "Bilinmeyen kaynaklara izin ver" → Yükle. Eski sürüm varsa üstüne kurulur.
- Bu bir **geliştirme** build'idir: alttaki yeşil yazılar (FPS, ağ) ve sağ alttaki "Development Build" normaldir; oyuncular görmezden gelsin.
- **Aynı Wi-Fi ağı şart** (yerel co-op). Misafir/okul ağları cihazları birbirinden yalıtabilir; o zaman bir telefon **hotspot** açsın, diğerleri ona bağlansın.
- Telefonlar şarjda olsun; ekran parlaklığı yüksek, ses açık (boss ve helikopter sesi ipucu veriyor).

## Oturum akışı (yaklaşık 60 dk)

| Adım | Süre | Ne yapılır |
|---|---|---|
| 1. Tanışma | 5 dk | Oyunu anlatma; yalnızca "aynı Wi-Fi'dayız, biri OYUN KUR'a bassın" de. Neyin nasıl çalıştığını **anlatma** — anlaşılmayanı görmek istiyoruz |
| 2. Tek oyuncu ısınma | 5 dk | Herkes TEK OYUNCU ile 3–5 dk oynasın |
| 3. Co-op run 1 | 20–25 dk | Host: YEREL CO-OP → OYUN KUR → herkes katılınca BAŞLAT. Diğerleri: YEREL CO-OP → listeden oyunu seç (görünmezse IP İLE KATIL, IP lobi ekranında yazar). Hedef: 12:00'deki boss'a ve sonrasındaki tahliyeye ulaşmak |
| 4. Kısa mola + form | 5 dk | Geri bildirim formunun ilk bölümü |
| 5. Co-op run 2 | 15–20 dk | Farklı host; bu kez "tahliyeyi kaçırıp devam etmeyi" de deneyin |
| 6. Form + sohbet | 10 dk | Formun kalanı, serbest yorum |

**Gözlemcinin not alacakları** (oyunculara soru sormadan): nerede duraksadılar, neyi aradılar, nerede güldüler/söylendiler, boss'ta kaç kişi öldü ve neden, tahliye alanını buldular mı, hangi dakikada sıkıldılar.

## Bilinen eksikler (oyunculara söyleyebilirsin)

- Oyuncu karakteri henüz kapsül; müzik yok (yalnızca efektler); menüde AYARLAR sınırlı.
- Kalıcı ilerleme (kilit açma, karakter, perk) sonraki aşamada; sonuç ekranındaki "Kasaya giren" şimdilik yalnızca gösterim.
- Grafikler son hâlinde değil.

## Sorun olursa

- **Oyun listede görünmüyor:** IP İLE KATIL → host'un lobi ekranındaki IP. Olmazsa hotspot.
- **"FARKLI SÜRÜM":** bütün telefonlarda aynı APK olmalı.
- **Donma/çökme:** saati ve ne yapıldığını not et; mümkünse telefonu bana getir (log alırım).

Sonuçlar: `docs/playtest/FEEDBACK_FORM.md` doldurulup bana iletilir; ben `docs/playtest/VS1_RESULTS.md` olarak özetler, bulguları milestone'lara dağıtırım.
