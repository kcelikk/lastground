# LastGround.UI

Menüler, HUD ve dev araçları. Bağımlılık: Core, Localization, TextMeshPro, UGUI. Gameplay'i yalnızca okur.

- `Menu/MainMenuScreen` — M0: dil değiştirme + çıkış; Solo/Co-op/Settings sonraki milestone'larda.
- `Common/SafeAreaFitter` — çentik/punch-hole güvenli alanı.
- `Diagnostics/PerfHud` — FPS, frame ms, GC/frame, bellek, kalite (yalnızca Editor + development build).

Kural: kullanıcıya görünen her metin localization key'inden; sayılar TMP `SetText` ile.
