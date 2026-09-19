# LastGround.UI

Menüler, HUD ve dev araçları. Bağımlılık: Core, Localization, Gameplay, TextMeshPro, UGUI. Gameplay'i yalnızca okur.

- `Menu/MainMenuScreen` — Solo, Local Co-op, dil, çıkış; kopma nedeni mesajı.
- `Menu/LocalCoopScreen` — bulunan oyunlar (discovery), IP ile katıl, oyun kur.
- `Menu/LobbyScreen` — oyuncular + ping, host adresi, BAŞLAT (host).
- `Run/RunHud` — rol/oyuncu sayısı, bekleme, AYRIL, dev net istatistikleri (alt orta).
- `Run/CombatHud` — HP barı, mermi/reload halkası, hasar vinyeti, "yere düştün" sayacı, otomatik ateş düğmesi.
- `Run/RunStatusHud` — `SURVIVAL · HORDE · THREAT` satırı + tehdit banner'ı (CharLine, GC yok).
- `Run/TeamPanel`, `Run/TeammateIndicators` — takım listesi, ekran dışı takım okları.
- `Run/LevelUpPanel`, `Run/XpBar` — 3 kartlık upgrade seçimi (solo'da duraklatır), takım XP'si.
- `Run/ObjectivePanel`, `Run/ObjectiveIndicator` — görev başlığı + sayaç, bölgeye ok.
- `Run/MinimapHud` — sağ üst radar: önceden render edilmiş harita + 5 Hz nokta katmanı (zombi/takım) + uzak sürülerin yönü için 12 dilimli halka.
- `Run/RegionCard` — yeni bölgeye girince konsept görsel + ad + tehlike seviyesi (D-020).
- `Run/ObjectivePanel`, `Run/ObjectiveIndicator` — olay başlığı/sayacı ve ekran kenarı yön oku (çapalı olaylar).
- `Run/BossHealthBar` — boss adı + can barı, faz işaretleri, sersemken yanıp söner.
- `Run/ExtractionHud` — ikinci durum satırı (TAHLİYE süre/tutma → bölge), tutma çubuğu, mavi kenar oku.
- `Run/ResultsScreen` — run sonu istatistikleri, ana menü.
- `Common/CharLine` — yerelleştirilmiş kelime + sayı satırları için tahsissiz karakter tamponu.
- `Run/DamageNumbers` — yerel oyuncunun isabet sayıları, dünya uzayında TMP havuzu (preset `DamageNumberCap`).
- `Common/ScreenRouter`, `Common/NetMessageKeys` (kopma/red nedeni → localization key).
- `Common/SafeAreaFitter` — çentik/punch-hole güvenli alanı.
- `Diagnostics/PerfHud` — FPS, frame ms, GC/frame, bellek, kalite; alt ortada (yalnızca Editor + development build).

Kural: kullanıcıya görünen her metin localization key'inden; sayılar TMP `SetText` ile.
