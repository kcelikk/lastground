# M5 — Director yoğunluk kayıtları

Development build'lerde host, her saniye bir director örneği kaydeder (`DirectorLog`, `persistentDataPath/director_<seed>.csv`).

| Dosya | Run | Cihaz | Süre |
|---|---|---|---|
| `director_coop_redmi_host.csv` | 2 oyuncu (Redmi host + OnePlus client), otomatik ateş + gezinme | Redmi Pad Pro (MID) | 15.4 dk |
| `director_solo_redmi.csv` | Solo, otomatik ateş + otomatik seçim | Redmi Pad Pro | 6.2 dk |
| `director_solo_oneplus.csv` | Solo, otomatik ateş + otomatik seçim | OnePlus 5T (LOW) | 6.2 dk |

Sütunlar: `time` (run saniyesi), `intensity` (takım yoğunluğu 0–1), `state` (Calm/BuildUp/Peak/PeakHold/Relax), `horde` (HUD etiketi), `threat`, `alive`, `maxAlive`, `rate` (spawn puanı/s).

`director_runs.svg`: üç run'ın yoğunluğu (düz çizgi), yaşayan zombi / maksimum (kesikli) ve Peak başlangıçları (üstte noktalar).

İlk 6 dakikadaki Peak başlangıçları: co-op 83 · 173 · 351 s, OnePlus solo 45 · 164 · 281 s, Redmi solo 58 · 160 · 287 s. Botlar erken oyunda güçlü olduğundan yoğunluk düşük kalıyor (ort. 0.01–0.08); 1.0'a sıçramalar oyuncunun yere düştüğü anlar.
