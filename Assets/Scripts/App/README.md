# LastGround.App

Composition root. Tüm assembly'leri görebilen tek runtime assembly.

- `AppRoot` — ilk sahneden önce (`RuntimeInitializeOnLoadMethod`) oluşur: save → localization (ilk açılışta Türkçe cihazda `tr`) → kalite (RAM'e göre LOW/MEDIUM/HIGH) → PerfHud. Böylece her sahne Editor'de doğrudan oynatılabilir.
- `BootLoader` — Boot sahnesinden Menu'ye geçer.
- `QualityDefaults` — ilk açılış tier seçimi; M2'de `DeviceTierDetector` + benchmark ile değişir.
- M1: `RunInstaller` (role göre sistem kurulumu) burada olacak.
