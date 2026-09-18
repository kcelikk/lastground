# LastGround.Editor

Yalnızca Editor. Menü: **LastGround/…**

- `Setup/ProjectSetup` — idempotent proje ayarı: Player (IL2CPP, ARM64, API 26, Vulkan+GLES3, ASTC, Input System), URP LOW/MEDIUM/HIGH, kalite seviyeleri, sahneler. Ayar değişikliği bu dosyada yapılır.
- `Setup/SceneBuilder` — Boot/Menu sahnelerini üretir; **mevcut sahnenin üzerine yazmaz** (yeniden üretmek için sahneyi sil).
- `Build/BuildScripts` — `BuildAndroidDevelopment` → `Builds/Android/LastGround-dev.apk`.
- `Localization/LocalizationValidator` — eksik/fazla key, boş değer, `{0}` uyuşmazlığı.

Komutlar: kök `AGENTS.md` §5.
