# LastGround.Save

Cihaz yerel kayıt (TDD_02 §24). Bağımlılık: Core, Newtonsoft.Json.

- `JsonFileSaveStore` — `persistentDataPath/save/{key}.json`; `.tmp` → flush → atomik replace, önceki sürüm `.bak`, CRC32 başlığı (`LGSAVE1 <crc>`).
- `SaveService` — `SettingsData` (dil, kalite, FPS); 1 s debounce; sürüm alanı + `Migrate`.
- M9'da `ProfileData` (meta ilerleme) eklenir. **Run durumu asla yazılmaz** (D-005).

**Değiştirirsen etkilenenler:** `SettingsData` alanı eklemek serbest; alan silmek/yeniden adlandırmak `Version` artışı + migrasyon gerektirir.

**M9:** `profile.json` (`ProfileData`, `ProfileStats`, `OutfitChoice`) — Hurda, açılanlar, rozetler, kuşanılanlar, istatistikler. `ProfileMigrator` sürüm zinciri (JSON ağacı üzerinde adım adım); daha yeni sürümün yazdığı ya da yükseltilemeyen profil okunur ama üzerine yazılmaz (`ProfileReadOnly`). JSON sınıfları `link.xml` + `LinkXmlTests` listesinde.
