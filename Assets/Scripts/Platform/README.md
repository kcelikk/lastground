# LastGround.Platform

Platform köprüleri. Bağımlılık: Core.

- `Net/INetworkInterfaces` — IPv4 arayüzleri + directed broadcast. Android: `java.net.NetworkInterface` (Android 11+'da .NET API güvenilmez); Editor/masaüstü: `System.Net`.
- `Net/IMulticastLock` — Android `WifiManager.MulticastLock` (broadcast paketleri için); yalnızca discovery ekranlarında tutulur.
- `Net/PlatformNet` — platforma göre implementasyon, host ekranında gösterilecek adres (wlan → ap/swlan).

Sonraki: termal izleme, cihaz tier tespiti (M2/M13).
