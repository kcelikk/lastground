# LastGround.App

Composition root. Tüm assembly'leri görebilen tek runtime assembly.

- `AppRoot` — ilk sahneden önce (`RuntimeInitializeOnLoadMethod`) oluşur: save → localization (ilk açılışta Türkçe cihazda `tr`) → kalite (RAM'e göre LOW/MEDIUM/HIGH) → PerfHud. Böylece her sahne Editor'de doğrudan oynatılabilir.
- `BootLoader` — Boot sahnesinden Menu'ye geçer.
- `QualityDefaults` — ilk açılış tier seçimi; M2'de `DeviceTierDetector` + benchmark ile değişir.
- `SessionService` — session + discovery sahibi; menü → lobby → run akışı (LoadRun → RunReady → RunStart, 10 s zaman aşımı), kopmada menüye dönüş ve neden mesajı. Sahne geçişleri yalnızca burada.
- `RunInstaller` — Run sahnesi composition root: role göre sistem kümesi (host: dummy sürü sim + sender; client: replica + receiver; herkes: motor, PlayerSync, render, kamera, HUD).
- `Dev/DevAutomation` (yalnızca dev build) — `-lg-host`, `-lg-join IP`, `-lg-start-at N`, `-lg-solo`, `-lg-wander`, `-lg-quit-after SN`; Android'de `-e lgargs "..."`.
- `Dev/RunTelemetry` (yalnızca dev build) — 5 s'de bir `[NetStats]` log satırı (soak ölçümü).
