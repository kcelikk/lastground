# Last Ground — Özgün zombi karakter paftaları

2026-09-18 tarihinde `imagegen` becerisi ve yerleşik `image_gen` aracıyla üretilen altı PNG konsept paftası. Her görsel 1536 × 1024 piksel; ön, yan, arka ve üç çeyrek görünüş içerir. Bunlar 3D modelleme referansıdır; FBX/GLB, mesh, rig, animasyon veya oyun içi kaplama atlası değildir.

| Görsel | Tür | Ayırt edici tasarım |
|---|---|---|
| [01-walker.png](01-walker.png) | Walker | Soluk ten, mavi bakım gömleği, çökmüş duruş |
| [02-runner.png](02-runner.png) | Runner | İnce atletik gövde, kısa saç, bordo ceket |
| [03-tank.png](03-tank.png) | Tank | Geniş omuzlar, ağır işçi tulumu, kalın kollar |
| [04-spitter.png](04-spitter.png) | Spitter | Uzun boyun, belirgin boğaz, koyu zeytin iş önlüğü |
| [05-exploder.png](05-exploder.png) | Exploder | Yuvarlak karın silüeti, pas turuncusu koruyucu tulum |
| [06-brute-boss.png](06-brute-boss.png) | Brute boss | Asimetrik iri kollar, organik omuz plakaları, geniş gövde |

## Kaynak ve kapsam

`docs/DECISIONS.md` D-009/D-018, TDD_01 §0.13/§8.5 ve mevcut zombi içerik tanımları esas alındı. `docs/reference` altındaki 13 PNG incelendi. Özellikle `lastground-img-1.png`, `lastground-img.png`, `lastground-board-2.png` ve `lastground-board-3.png` görsel dil referansıdır. Referanslardaki küçük zombi figürlerinin yüz ve kıyafet ayrıntıları özgün olarak yorumlandı; görseller kesilip çıkartılmadı. Karakterler yeni AI üretimleridir; benzersizlik araştırması yapılmadı.

D-009'daki asset tedarik kararını değiştiren bir runtime uygulaması yapılmadı. Projenin mevcut modelleri ve oyun kodu değiştirilmedi. Paftalar görsel tasarım önerileridir; milestone onayı veya tamamlanmış oyun asset'i anlamına gelmez.

## Modellemeye aktarım

- Paftalar teknik CAD/ortografik ölçüm değildir. Kamera ve poz farkları ile küçük kıyafet ayrıntıları modellemede uzlaştırılmalıdır. Walker kol kıvrımları, Tank askıları ve Brute asimetrisinde ön görünüşü temel alın.
- Sayfalar karaktere göre ölçeklenmiştir; paftalar arasında aynı piksel boyu aynı dünya boyu demek değildir. TDD_01 §0.2'deki insan, Tank ve boss oranlarını ayrıca uygulayın.
- Spitter ve Exploder'ın özel saldırı efektleri gösterilmez. Exploder görseli sakin durum referansıdır; parlama telegraph'ı ayrı materyal/efekt çalışması gerektirir.
- Walker ve Exploder üzerinde araç tarafından eklenen küçük kıyafet yazıları üretim kaplamasına taşınmamalıdır; son kaplamada kaldırılmalıdır.
- Sürü için retopoloji, UV/atlas, rig ve animasyon üretiminden sonra D-018'deki kemik matrisi texture bake + GPU instancing hattı kullanılır. TDD_02 §21.3 hedefleri LOD0 ≤ 2.000 vertex, LOD1 yaklaşık 800, LOD2 yaklaşık 300; gerçek performans telefonda doğrulanır. Boss bütçesi ayrıca ele alınır.

## Doğrulama ve bağımlılıklar

Altı çıktı görsel olarak incelendi; dört görünüş ve tam boy kadraj kontrol edildi. PNG başlıkları/dosya boyutları kontrol edildi. Kod veya runtime asset değişmediği için Unity derleme, APK ve cihaz performans testi çalıştırılmadı; performans ölçülmedi.

Sorumluluk: karakter silüeti, kıyafet ve malzeme yönünü anlatmak. Bağımlılıklar: yukarıdaki kararlar ve referanslar. Değiştirirsen etkilenenler: bu paftalara göre hazırlanacak modelleme, kaplama, rig ve animasyon işleri; mevcut oyun doğrudan bu dosyaları tüketmez.

Nihai istemlerin tamamı: [PROMPTS.md](PROMPTS.md).

## Bağımsız özgün seri — 2026-09-19

Kullanıcının referans görsellerinden bağımsız, gerçekçi ve abartısız karakter isteği üzerine dört yeni pafta eklendi. Özgünlük; yüz, yaş, beden yapısı, meslek geçmişi ve kıyafet ayrıntıları üzerinden tasarlandı. Evrensel benzersizlik iddiası veya karşılaştırmalı karakter araştırması yapılmadı.

| Görsel | Tasarım | Görsel kimlik |
|---|---|---|
| [07-gece-vardiyasi.png](07-gece-vardiyasi.png) | Gece Vardiyası | Kısa gri saçlı kadın teknisyen; çapraz kapanan mavi ceket, sırtta tek reflektör şerit, tek eldiven |
| [08-kirec-ustasi.png](08-kirec-ustasi.png) | Kireç Ustası | Yaşlı, tıknaz sıvacı; mineral tozlu kahverengi yelek, dikilmiş diz yamaları |
| [09-soguk-depo.png](09-soguk-depo.png) | Soğuk Depo | Uzun ve ince depo çalışanı; yüksek yakalı soluk mürdüm mont, tek onarılmış manşet |
| [10-son-sefer.png](10-son-sefer.png) | Son Sefer | Topuzlu kadın otobüs görevlisi; petrol yeşili hırka, sökülmüş yaka kartının soluk izi |

Her yeni PNG 1536 × 1024 piksel ve dört tam boy görünüş içerir. Çıktılar görsel olarak incelendi. Gece Vardiyası'nda istemdeki eldiven yönü ters üretilmiştir; modellemede ön görünüşteki anatomik sağ eldiven esas alınmalıdır. Bu seri mevcut düşman davranışları için görsel varyant önerisidir; yeni gameplay sınıfı veya runtime 3D model eklemez. Önceki altı pafta korunmuştur.

Üretim: yerleşik `image_gen`, görsel girdi olmadan. Tam istemler: [PROMPTS_ORIGINALS.md](PROMPTS_ORIGINALS.md).
