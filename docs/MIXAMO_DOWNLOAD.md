# Mixamo indirme listesi (M6 — gerçekçi zombi gövdeleri)

Hedef görünüm: `docs/reference/lastground-img-1.png`, board-2/board-3 — gerçekçi, yırtık sivil kıyafetli, kanlı zombiler; iri etli mutantlar. Stilize/çizgi film modeller **alınmaz**.

Lisans: Mixamo karakter ve animasyonları oyuna gömülü olarak ticari kullanılabilir; tek başına yeniden dağıtılamaz (ASSET_SOURCES.md'ye eklenecek).

## Klasörler
```
Assets/ThirdParty/Mixamo/Characters/   ← karakter FBX'leri
Assets/ThirdParty/Mixamo/Animations/   ← animasyon FBX'leri
```
Klasörler ve Unity `.meta` dosyaları 2026-09-19 tarihinde oluşturuldu. Henüz karakter/animasyon FBX'i yok. Aynı gün Playwright + sistem Chromium ile etkileşimli tarayıcı erişimi kuruldu; Adobe hesap adımının kullanıcı tarafından tamamlanması bekleniyor. Dosyalar edinildikten sonra bu yollara kopyalanabilir (Unity açık olmasa da olur). Gerçek durum: [MIXAMO_STATUS.md](MIXAMO_STATUS.md).

## 1. Karakterler — mixamo.com → Characters → aramaya `zombie`, sonra `mutant`
İndirme ayarı: **Format: FBX for Unity (.fbx) · Pose: T-pose**

| Rol | Kaç tane | Seçim ölçütü |
|---|---|---|
| Walker | 3–4 | Farklı siluetler: erkek/kadın, sivil, polis/asker kıyafeti. Gerçekçi doku, yırtık kıyafet, kan |
| Runner | 1 | Zayıf/çevik görünen bir zombi (Walker'lardan biri de olabilir, ben inceltirim) |
| Tank | 1 | İri, kaslı mutant (Mixamo'da "Mutant" adlı karakter bu role uygun) |
| Spitter | 1 | Deforme / parazitli görünen bir zombi |
| Exploder | 0–1 | Şişkin/bloated bir model varsa al; yoksa bir Walker'ı şişirip turuncu parlayan püstüllerle ayırırım |

Emin olmadığın modelleri de indir; ben referansa ve mobil bütçeye (poligon, texture) göre elerim. Dosya adlarını değiştirme.

## 2. Animasyonlar — mixamo.com → Animations
İndirme ayarı: herhangi bir zombi karakteri seçiliyken **Format: FBX for Unity · Skin: Without Skin · Frames per second: 30 · Keyframe reduction: none**. Yürüme/koşu animasyonlarında **In Place** kutusunu işaretle.

| Slot | Arama | Not |
|---|---|---|
| Idle | `zombie idle` | |
| Walk | `zombie walk` | 2 farklı varyant alırsan çeşitlilik artar |
| Run | `zombie running` | Runner ve Exploder sprint'i |
| Attack | `zombie attack` veya `zombie punching` | |
| Hit | `zombie reaction hit` | |
| Crawl | `zombie crawl` | |
| Death | `zombie death` / `zombie dying` | 2 varyant iyi olur |
| Tank | `mutant walking`, `mutant run`, `mutant punch` (veya `mutant swiping`), `mutant dying`, `mutant roaring` | Tank'a özel set |
| Runner lunge | `zombie biting` veya `jump attack` | Sıçrayarak saldırı |
| Spitter | `zombie scream` veya `throw` | Tükürme telegraph'ı |

Dosyalar geldikten sonra import, iskelet/retarget eşleşmesi, ayrı FBX klip bağlama, kaplama birleştirme ve kemik texture + LOD bake doğrulanmalıdır. Mevcut `CrowdBaker` yalnızca karakter FBX'inin içindeki klipleri okur; `CrowdCatalogBuilder` Quaternius atlasına sabittir. Texture array desteği bu belgede tamamlanmış kabul edilmez. Sadece dosya kopyalamak gerçekçi karakterleri oyuna bağlamaz.
