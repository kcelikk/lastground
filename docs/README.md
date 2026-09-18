# LAST GROUND — Dokümantasyon

| Dosya | İçerik |
|---|---|
| [DECISIONS.md](DECISIONS.md) | **Onaylı kararlar (D-001…) — çelişkide geçerli olan kaynak** |
| [OPEN_QUESTIONS.md](OPEN_QUESTIONS.md) | Cevap bekleyen sorular ve onaylar |
| [reports/](reports/) | Milestone raporları (M0_REPORT.md …) |
| [benchmarks/](benchmarks/) | Cihaz benchmark CSV'leri (M2…) |
| [ASSET_SOURCES.md](ASSET_SOURCES.md) | Üçüncü taraf / AI içerik lisans kaydı |
| [SETUP_NEW_MACHINE.md](SETUP_NEW_MACHINE.md) | Yeni geliştirme makinesi (Lenovo T14): OS seçimi, kurulum listesi, taşıma ve doğrulama adımları |
| [TDD_01_Reference_Vision_Gameplay.md](TDD_01_Reference_Vision_Gameplay.md) | §0 Referans görsel analizi, §1–§14 vizyon, loop, kontroller, kamera, combat, silah, build, zombi AI, director, boss, harita, event, loot, co-op |
| [TDD_02_Network_Tech_Budgets.md](TDD_02_Network_Tech_Budgets.md) | §15–§33 network mimarisi, otorite, zombi replikasyonu, LAN discovery, host/join, pooling, rendering, optimizasyon, ses, save, SO modeli, proje yapısı, sınıflar, sahneler, prefab'lar, Android izinleri, performans/bant genişliği/bellek bütçeleri |
| [TDD_03_Scope_Milestones_Risks_QA.md](TDD_03_Scope_Milestones_Risks_QA.md) | §34–§40 MVP, Vertical Slice, milestone planı, riskler ve çözümler, MVP'de kaçınılacaklar, ilk adım; A–J cevapları; karar durumu |

## Temel Kararlar (özet, v0.2)
- **Ürün:** Android first · 1–4 LAN co-op · backend yok · endless survival + extraction · dalga yok · görev sayaçları var (D-019) · takıma eşit XP/coin · kalıcı güç artışı yok
- **Engine:** Unity 6.3 LTS (≥ 6000.3.13f1) · C# · URP (Forward) · IL2CPP ARM64 · Vulkan + GLES3 · Linux Editor (Ubuntu 26.04) · Git + LFS
- **Network:** Mirror (gameplay'den izole, değiştirilebilir), host-authoritative listen-server, KCP/UDP
- **Zombiler:** Burst SoA sim + flow field + VAT GPU instancing; 250 görünür / 300+ simüle hedef; client'a tier'lı 7 B snapshot + ilgi yönetimi + byte bütçesi
- **Performans:** LOW (4–6 GB) ≥ 30 FPS · MID (6–8 GB) 45–60 FPS · HIGH (8–12+ GB) 60 FPS
- **Art:** hibrit (Asset Store + Mixamo + AI destekli texture/UI), önce FPS sonra görsel
- **Localization:** EN varsayılan, TR ikinci; JSON string tabloları
- **Öncelik:** Networking PoC → Rendering benchmark → Horde sim → Combat (MVP kapısı) → Loop → Progression → Polish
- **Geliştirme makinesi:** Lenovo T14 (i5-1135G7, 16 GB, Iris Xe) · OS Ubuntu 26.04 LTS (onaylı, D-012) · repo `git@github.com:kcelikk/lastground.git` (private)
- **Test cihazları:** OnePlus 5T = LOW · Redmi Pad Pro = MID · HIGH cihaz yok · 21 kişilik test grubu (Play kapalı test)

## Kaynak Referanslar
**Nihai görsel hedef bu referanslardır** (karanlık, gerçekçi zombiler; sodyum ışıkları; yoğun kan — TDD_01 §0.13). M2 test zombileri yalnızca placeholder'dır.
- `reference/lastground-img-1.png` — peak horde gameplay kompozisyonu
- `reference/lastground-img.png` — 10 panelli akış/UI board'u (TR)
- `reference/lastground-board-2.png` — 10 panel (EN): 4 kişilik co-op, loadout, harita, boss, revive, extraction
- `reference/lastground-board-3.png` — 10 panel (TR): lobi, erken oyun, yoğun horde, görev paneli, harita görevleri
