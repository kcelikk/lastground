# LastGround.Input

Dokunmatik / gamepad / klavye → `PlayerInputFrame` (Core). Gameplay cihaz kodu görmez (TDD_01 §3.1). Bağımlılık: Core, Input System.

- `FloatingJoystick` — yüzen stick mantığı (dead zone 0.12).
- `TouchMoveInput` — sol alt %75 bölgede hareket stick'i + WASD yedeği; Input tick fazında okunur. M4'te sağ nişan stick'i, butonlar, aim assist eklenir.
