# Last Ground — Üçüncü Taraf ve AI Kaynak Kaydı

Her üçüncü taraf / AI üretimi içerik için kaynak, lisans ve kullanım yeri (TDD_02 §26.4). Yeni asset eklenince satır eklenir.

| Asset | Sürüm | Kaynak | Lisans | Konum | Eklenme | Not |
|---|---|---|---|---|---|---|
| Mirror Networking | 96.11.2 | github.com/MirrorNetworking/Mirror/releases (unitypackage, sha256 `509c3439…628e201`) | MIT | `Assets/ThirdParty/Mirror` | 2026-09-18 | Examples, Hosting (Edgegap), Presets, ScriptTemplates alınmadı. Yalnızca `LastGround.Networking` referans eder (D-001) |
| TextMesh Pro Essential Resources | ugui 2.0.0 | Unity paket içeriği | Unity Companion License; LiberationSans: SIL OFL 1.1 | `Assets/TextMesh Pro` | 2026-09-18 | Condensed OFL font M11'de |
| Quaternius — Zombie Apocalypse Kit (4 zombi: Basic, Arm, Chubby, Ribcage + atlas) | Mart 2024 | quaternius.com/packs/zombieapocalypsekit.html (Google Drive) | CC0 1.0 | `Assets/ThirdParty/Quaternius/ZombieKit` | 2026-09-18 | **Placeholder** (stilize low-poly; referans atmosferine uymuyor). M2 animasyon/render pipeline'ı ve benchmark için. Nihai zombi asset'i aynı pipeline'dan geçirilecek (D-009) |
| UnityMeshSimplifier (Whinarn) | 3.1.1 | github.com/Whinarn/UnityMeshSimplifier (UPM git) | MIT | `Packages/manifest.json` | 2026-09-18 | Yalnızca Editor'de LOD üretimi (skin ağırlıklarını korur) |
| Özgün zombi karakter paftaları (Walker, Runner, Tank, Spitter, Exploder, Brute) | v1 | Yerleşik OpenAI image_gen; proje referansları görsel olarak incelenerek yazılan istemler | AI üretimi; kullanılan hizmetin kullanım koşulları kapsamında, üçüncü taraf asset lisansı atanmadı | `docs/reference/models` | 2026-09-18 | 6 PNG modelleme konsepti; runtime 3D model değildir. İstemler ve modelleme notları aynı klasörde |
