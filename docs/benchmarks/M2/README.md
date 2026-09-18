# M2 Benchmark CSV'leri

`PerfBenchmarkRunner` çıktıları (`-lg-quality N -lg-bench 30`: adım başına 4 s ısınma + 30 s ölçüm, 20→300 hedef).
Sütunlar: cihaz, GPU, API, OS, RAM, kalite, render_scale, fps_cap, hedef, çizilen (sürü + ceset), frame sayısı, avg_fps, avg/p50/p99/max ms, GC B/frame (dev build — Mirror KCP IMGUI dahil), bellek MB, pil °C başlangıç/bitiş.

| Dosya | Koşul | Durum |
|---|---|---|
| oneplus5t_…142423_LOW | 30 FPS kilidi, lighting grid yok | ilk deneme |
| oneplus5t_…143304_LOW / 143728_MEDIUM / 144151_HIGH | 60 cap, lighting grid yok | ara |
| **oneplus5t_…150318_LOW / 150741_MEDIUM** | 60 cap, lighting grid | **nihai** |
| redmipadpro_…145547_LOW | ilk tur (bağlantı koptu, eksik) | geçersiz |
| redmipadpro_…150318_LOW | 60 cap, lighting grid | **nihai (LOW)** |
| redmipadpro_…150733_MEDIUM / 151148_HIGH | 1080 satır sınırı yok (2560×1600 tam çözünürlük) | karşılaştırma |
| redmipadpro_…152144_MEDIUM / 152600_HIGH | eski APK (kurulum düştü) — sınır yok | geçersiz |
| **redmipadpro_…153040_MEDIUM / 153456_HIGH** | 1080 satır sınırı | **nihai** |
