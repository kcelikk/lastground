# LastGround.Localization

JSON string tabloları (D-010, TDD_02 §24A). Bağımlılık: Core, TextMeshPro, Newtonsoft.Json.

- `JsonLocalizationService` — `languages.json` kataloğu + dil başına tablolar; fallback zinciri (tr → en).
- `ResourcesLocalizationSource` — `Assets/Resources/Localization/{dil}/{tablo}.json`.
- `LocalizedText` — TMP metnini key'e bağlar, dil değişince yeniler.
- Editor: `LastGround/Localization/Validate` (`LocalizationValidator`).

**Yeni dil:** `languages.json`'a satır + klasörü kopyala + çevir. Kod değişikliği yok.
**Değiştirirsen etkilenenler:** key yeniden adlandırma → sahnelerdeki `LocalizedText` key'leri ve her iki dil tablosu.
