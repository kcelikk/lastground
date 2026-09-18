# Last Ground — Yeni Geliştirme Makinesi: OS Seçimi, Kurulum ve Taşıma

> Hedef makine: **Lenovo ThinkPad T14, Intel i5-1135G7 (4 çekirdek / 8 thread, Tiger Lake), 16 GB RAM, 512 GB SSD, Intel Iris Xe (80 EU) dahili GPU.**
> Bu dosya, yeni makine kurulurken adım adım takip edilecek referanstır.

---

## 1. Kaynaklar yeterli mi?

**Evet, bu proje için yeterli.** Eski makineye (i5-6500T / 7 GB / HD 530) göre her kritik boyutta ilerleme var.

| Bileşen | Eski (ProDesk 600 G2) | Yeni (T14) | Bu proje için |
|---|---|---|---|
| CPU | 4 çekirdek / 4 thread, 2015 | 4 çekirdek / **8 thread**, 2020, daha yüksek IPC | ✅ IL2CPP Android build tahmini: ilk build 12–20 dk, sonraki build'ler 3–7 dk |
| RAM | 7.1 GB | **16 GB** | ✅ Unity Editor + Android build + VS Code + Claude Code aynı anda rahat çalışır |
| GPU | HD 530 (Gen9, 24 EU) | **Iris Xe (Gen12, 80 EU)** — kabaca 3–4× | ✅ Editor Scene view, URP, 250 instanced zombi önizlemesi sorunsuz; lightmap bake kabul edilebilir sürede |
| Disk | 240 GB SATA SSD | **512 GB NVMe SSD** (muhtemelen) | ✅ Unity + SDK/NDK + Library + asset'ler rahat sığar, I/O hızlı |
| Taşınabilirlik | Sabit | Laptop | ✅ Telefon testlerini masaüstünden bağımsız yapabilirsin |

**Sınırlar (bilinçli kabul ediyoruz):**
- Laptop termal sınırı: uzun IL2CPP build'lerinde CPU 15–28 W'a iner, süre biraz uzar. Güç profilini "Performance" yapmak ve cihazı sert/düz zeminde tutmak yeterli.
- Iris Xe ayrık ekran kartı değil: çok büyük hazır ortam sahnelerinde editör yavaşlayabilir. Bizim bölge bazlı, karanlık ve mütevazı sahnelerimizde sorun beklemiyorum.
- **Oyun performans ölçümü hiçbir zaman bu makineden yapılmaz**; sadece gerçek telefonlardan (§ Doğrulama).

**Opsiyonel iyileştirme:** T14 modellerinin çoğunda 8 GB lehimli + 1 adet SO-DIMM yuvası vardır. Yuvadaki modülü 16 GB yaparsan toplam 24 GB olur. Şart değil; 16 GB ile rahat çalışırız. Cihaz eline geçtiğinde `sudo dmidecode -t memory` çıktısına bakıp söyleyebilirim.

---

## 2. Hangi OS?

### Karar (D-012, 2026-09-18): **Ubuntu 26.04 LTS Desktop (x86_64)** — makinede kurulu, onaylandı

| Neden | Açıklama |
|---|---|
| Unity desteği | Unity'nin resmî listesinde (6.0–6.6) Linux için yalnızca **Ubuntu 22.04 ve 24.04** var. 26.04'teki tek bilinen engel (`libxml2.so.2` eksik → Editor açılmıyor) Unity tarafında **6000.3.13f1 / 6000.0.76f1** ve sonrasında düzeltildi. Bu yüzden **Unity 6.3 LTS ≥ 6000.3.13f1** kullanılır; 6000.6.0f1'de aynı hata (libxml2 + ICU 67) yeniden görüldü (issue #24671) → 6.6 serisi kullanılmaz. Editor'de açıklanamayan hata olursa önce OS kaynaklı mı diye bakılır; son çare 24.04'e dönüş. |
| Otomasyon | Ben (Claude Code) burada bash, adb, Unity batchmode ve script'lerle çalışıyorum. Linux'ta build/test/cihaz otomasyonunu sürtünmesiz yürütürüm; sen sadece Unity arayüzündeki işleri yaparsın. |
| Bellek | Boşta ~1.5 GB RAM kullanır (Windows ~4 GB). 16 GB'ın daha büyük kısmı Unity'ye kalır. |
| Android geliştirme | ADB için sürücü derdi yok (udev kuralı yeterli). SDK/NDK/JDK Unity Hub'dan gelir. |
| Maliyet / kontrol | Lisans yok, sistem tekrar kurulabilir, her şey script'lenebilir. |

**Sürüm notu:** Makinede Ubuntu 26.04.1 LTS kurulu; yeniden kurulum yapılmıyor. Unity Hub'dan yalnızca 6000.3.x (≥ 13f1) patch'leri kurulur.

### Ne zaman Windows 11 Pro tercih edilmeli?
- Aynı makinede **Windows'a özel içerik araçları** kullanacaksan (Adobe Photoshop/Substance Painter'ın Windows sürümü, bazı Asset Store editör eklentileri yalnızca Windows DLL'i ile gelir).
- Play Console/Android tarafında bir fark yok, ikisinde de sorunsuz.
- Windows'u seçersen: Claude Code Windows'ta native çalışır. Projeyi **WSL içinde tutma** — Unity Windows tarafında, proje NTFS'te olmalı; WSL üzerinden erişim dosya I/O'sunu ciddi yavaşlatır.

### Kurulum ayarları (Ubuntu 26.04)
| Ayar | Değer |
|---|---|
| Sürüm | Ubuntu 26.04.x LTS **Desktop** (Server değil) — kurulu |
| Oturum | GNOME **Wayland** (26.04'te Xorg oturumu yok). Unity Editor XWayland üzerinden çalışır; Unity'nin native Wayland desteği deneysel olduğu için açılmaz. Çizim hatası görülürse not edilir |
| Disk | Tek ext4 root; ayrı `/home` şart değil |
| Şifreleme | LUKS tam disk şifreleme önerilir (laptop) — build sürelerine etkisi ihmal edilebilir |
| Swap | 16 GB swapfile önerilir (şu an `/swap.img` 4 GB; IL2CPP build'lerinde bellek sıkışırsa büyütülür) |
| Güncelleme | Kurulum sırasında "Install third-party software" işaretli (firmware/wifi) |
| Güç | Ayarlar → Güç: fişteyken otomatik uyku **kapalı** (uzun build'ler için) |
| Türkçe | Sistem dili tercih sana ait; ancak **terminal/geliştirme dili İngilizce** kalsın (hata mesajlarını aramak kolaylaşır) |

---

## 3. Kurulacak yazılımlar (onay sonrası ben kuracağım)

### A. Sistem paketleri
```
git  git-lfs  curl  unzip  build-essential  scrcpy  android-tools-adb (geçici)
```
- `git-lfs` binary asset'ler için zorunlu.
- `scrcpy` telefon ekranını masaüstüne yansıtır (test kaydı, ekran görüntüsü).

### B. Unity
1. **Unity Hub** — Unity'nin resmî apt deposu + imza anahtarı ile.
2. **Unity 6.3 LTS** (en güncel 6000.3.x patch, en az 6000.3.13f1; 6.6 serisi değil) + modüller: **Android Build Support**, **Android SDK & NDK Tools**, **OpenJDK**, **Linux Build Support (IL2CPP)** (editörde test için), **Documentation** (opsiyonel).
3. Hub'a **senin Unity hesabınla bir kez giriş** yapman gerekiyor (Unity Personal ücretsiz; lisans dosyası oluşunca ben batchmode build alabiliyorum).
4. Unity'nin kurduğu `platform-tools/adb` sistemdeki adb yerine kullanılacak (sürüm çakışmasını önlemek için PATH'te öncelik verilir).

### C. Kod ortamı
- **VS Code** + eklentiler: C#, C# Dev Kit, Unity (Microsoft), EditorConfig.
- **.NET SDK 8** (VS Code C# altyapısı ve Roslyn analizörleri için).
- **Claude Code** (bu makinedeki kurulumun aynısı) + proje `.claude` ayarları.

### D. Android cihaz erişimi
- `udev` kuralları (OnePlus ve Xiaomi USB vendor id'leri) → `adb devices` sudo'suz çalışsın.
- Kablosuz ADB: Redmi Pad Pro'da "Kablosuz hata ayıklama" eşleştirme; OnePlus 5T'de USB sonrası `adb tcpip 5555`.

### E. Kurmayacaklarımız (şimdilik)
Android Studio (Unity'nin SDK'sı yeterli), Docker tabanlı CI, Play Console araçları, Blender eklentileri, VPS yapılandırması.

---

## 4. Taşıma adımları (eski makine → T14)

1. **Bu klasörü kopyala:** `/home/ubuntu/Desktop/AsgardGame/lastground/` (içinde `docs/` ve `image/` var). USB bellek, `scp` veya GitHub üzerinden.
2. **SSH key:** İki seçenek —
   - **Önerilen:** yeni makinede yeni bir key üret (`ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519_github -C "lastground-t14"`) ve GitHub'a ikinci key olarak ekle. Eski makinedeki key'i GitHub'dan silersin.
   - Alternatif: `~/.ssh/id_ed25519_github`, `id_ed25519_github.pub` ve `~/.ssh/config` dosyalarını taşı (dosya izinleri 600 olmalı).
3. **Git:** Proje henüz depoya gönderilmedi. Yeni makinede `git init` + ilk commit + push yapacağız (onayınla). Bu yüzden kopyalamayı kaçırırsan tek kayıp `docs/` ve `image/` olur.
4. **Telefonlar:** Yeni makinede USB hata ayıklama onayını tekrar vermen gerekecek (RSA parmak izi makineye özel).
5. Eski makine yedek/ikinci test makinesi olarak kalabilir (örneğin LAN testlerinde ikinci client veya build arşivi).

---

## 5. Kurulum sonrası doğrulama listesi

| # | Kontrol | Beklenen |
|---|---|---|
| 1 | `nproc`, `free -h`, `df -h` | 8 thread, ~16 GB, ≥ 350 GB boş |
| 2 | `vulkaninfo --summary` | Iris Xe, Vulkan 1.3+ |
| 3 | Unity Hub açılıyor, lisans aktif | Unity 6.3 LTS (6000.3.x) listede |
| 4 | Boş bir URP projesi açılıyor | Editor 2 dk içinde açılıyor |
| 5 | `adb devices` | İki telefon `device` durumunda (sudo'suz) |
| 6 | `ssh -T git@github.com` | "Hi kcelikk!" mesajı |
| 7 | `git lfs version` | Sürüm yazdırıyor |
| 8 | Test APK'sı telefona kuruluyor | `adb install` başarılı, uygulama açılıyor |
| 9 | Unity Profiler telefona bağlanıyor | Canlı frame verisi |
| 10 | İki telefon ve laptop aynı ağda | `ip -4 addr` + `adb shell ip addr` aynı subnet |

Bu on maddeyi ben otomatik çalıştırıp raporlayabilirim; sen sadece 3, 4, 5 ve 6'daki oturum açma/onay adımlarını yaparsın.

---

## 6. Kurulum sonrası ilk iş
**M0 — Architecture & Project Foundation** (bkz. `TDD_03 §36`, `§40.2`): Git deposu, Unity projesi, asmdef'ler, Core altyapısı, localization, telefonda çalışan ilk APK.
