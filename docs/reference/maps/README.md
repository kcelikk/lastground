# Last Ground — Harita görsel konseptleri

2026-09-19. Yerleşik `image_gen` ile TDD_01 §0.13 ve §11 temel alınarak oluşturulan dört örnek çevre görseli.

| Dosya | İçerik |
|---|---|
| [01-industrial-district.png](01-industrial-district.png) | Depo, kontrol noktası, sanayi avlusu, terk edilmiş sokak, hastane, benzin istasyonu ve tahliye alanını bağlayan genel bölge görünümü |
| [02-foundry-yard.png](02-foundry-yard.png) | Açık savaş merkezi, konteyner siperleri, çatısı gizlenen üretim holü ve geniş kapılarıyla dökümhane avlusu |
| [03-gas-station.png](03-gas-station.png) | Pompa adalarının çevresinde dolaşım, kenarlarda araçlar, market ve servis bölümü |
| [04-hospital-extraction.png](04-hospital-extraction.png) | Hastane servis avlusu, alçak siperler, metro giriş yapısı ve açık tahliye pisti |

## Kullanım ve sınırlar

Sorumluluk: çevre atmosferi, malzeme, aydınlatma ve alan düzeni için sanat referansı. Bağımlılıklar: DECISIONS ve TDD_01 §11. Değiştirirsen etkilenenler: ileride bu referanslardan hazırlanacak greybox ve çevre sanat çalışmaları. Oyun bu PNG dosyalarını doğrudan tüketmez.

Görseller model, Unity sahnesi, ölçülü yerleşim planı veya navgrid değildir. Genel harita ile yakın bölge görselleri bağımsız konseptlerdir; birebir aynı geometrinin farklı kameraları değildir. İstemdeki metreler tasarım hedefidir, görsellerden ölçü doğrulanamaz. Kamera açısı ve perspektif yaklaşık yorumlanmıştır.

Görsel kontrolde merkezlerin açık olması, çevrede yoğun prop kullanımı ve turuncu/soğuk ışık ayrımı görüldü. Genel haritadaki yol döngüleri kavramsaldır. Kapı açıklıkları, özellikle hastane çevresindeki kapalı görünen kapılar ve metro girişinin kapanması, greybox aşamasında düzeltilip her savaş alanında en az iki gerçek geçilebilir çıkış doğrulanmalıdır. Benzin istasyonunun servis çukuru oynanabilir düz zeminde kapatılmalı veya dekoratif engel olarak ele alınmalıdır. Bunlar onaylı level tasarımı değildir.

Üretim görselleri görsel olarak incelendi ve PNG dosyaları kontrol edildi. Runtime değişikliği yapılmadı; Unity testleri/cihaz profili çalıştırılmadı. Genel harita kareye yakın, yakın çevre görselleri yatay kadrajdadır.

Görsel girdi kullanılmadı. Tam üretim istemleri: [PROMPTS.md](PROMPTS.md).
