# LastGround.UI

Menüler, HUD ve dev araçları. Bağımlılık: Core, Localization, Gameplay, TextMeshPro, UGUI. Gameplay'i yalnızca okur.

- `Menu/MainMenuScreen` — Solo, Local Co-op, dil, çıkış; kopma nedeni mesajı.
- `Menu/LocalCoopScreen` — bulunan oyunlar (discovery), IP ile katıl, oyun kur.
- `Menu/LobbyScreen` — oyuncular + ping, host adresi, BAŞLAT (host).
- `Run/RunHud` — rol/oyuncu sayısı, bekleme, AYRIL, dev net istatistikleri (alt orta).
- `Run/CombatHud` — HP barı, mermi/reload halkası, hasar vinyeti, "yere düştün" sayacı, otomatik ateş düğmesi.
- `Run/DamageNumbers` — yerel oyuncunun isabet sayıları, dünya uzayında TMP havuzu (preset `DamageNumberCap`).
- `Common/ScreenRouter`, `Common/NetMessageKeys` (kopma/red nedeni → localization key).
- `Common/SafeAreaFitter` — çentik/punch-hole güvenli alanı.
- `Diagnostics/PerfHud` — FPS, frame ms, GC/frame, bellek, kalite; alt ortada (yalnızca Editor + development build).

Kural: kullanıcıya görünen her metin localization key'inden; sayılar TMP `SetText` ile.
