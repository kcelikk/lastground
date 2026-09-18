# LastGround.Input

Dokunmatik / gamepad / klavye → `PlayerInputFrame` (Core). Gameplay cihaz kodu görmez (TDD_01 §3.1). Bağımlılık: Core, Input System.

- `FloatingJoystick` — yüzen stick mantığı (dead zone 0.12).
- `TouchTwinStickInput` — sol yarı: hareket stick'i, sağ yarı: nişan stick'i (0.25 nişan, 0.55 ateş, 0.15 s flick), ekranın üst %25'i butonlara ayrılmış. Yedek: WASD/oklar hareket, IJKL nişan+ateş, sol fare tuşu imlece ateş. Aim assist ve otomatik ateş `Gameplay/Combat/AimResolver`'da.
