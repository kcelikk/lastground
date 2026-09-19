# LastGround.Meta

Kalıcı ilerleme (M9, TDD_01 §14.7, D-005): hiçbir unlock güç vermez.

- `MetaProfile` — kayıtlı profil + katalog: sahiplik (varsayılan/satın alınmış/kazanılmış unvan), kuşanılanlar (sahip değilse geri düşer), kuşanma.
- `UnlockService` — Hurda ile satın alma (fiyat, ön koşul); unvanlar satılmaz, kazanılır.
- `MetaProgressionService` — run bankalama: Hurda, ömür boyu istatistikler (toplam/maksimum), rozetler (bir kez).
- `IMetaStore` — menülerin ihtiyacı (uygulama `App/MetaService` ile uygular: kaydeder, lobby seçimini günceller).
- `RunSummary`, `BankReport`, `UnlockResult`.

**Bağımlılıklar:** LastGround.Core, LastGround.Data (`Data/Meta` tanımları), LastGround.Save (`ProfileData`).
**Değiştirirsen etkilenenler:** katalog dizi sırası ağ indeksidir (lobby meta paketi) — yalnızca sona ekle. Perk sınırları `MetaTests` ile doğrulanır.
