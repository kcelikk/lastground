# LastGround.Audio

Ses (TDD_02 §23). M4 = temel savaş sesleri; mixer, müzik, ambiyans M12.

- `SfxPlayer` — sabit AudioSource havuzu (preset `AudioVoices`), ses başına min aralık + eşzamanlı ses sınırı, yerel oyuncuya göre mesafe/pan.
- `CombatAudio` — atış, isabet, ölüm, oyuncu hasarı event'lerini dinler (kaynağı sim mi ağ mı bilmez).
- `BossAudio` — boss kükremesi (giriş, çağırma, hücum hazırlığı), yere vurma darbesi, duvara çarpma; tahliye alanı tutulurken yaklaşan helikopterin rotoru (M8).
- `ProceduralSfx` — run başında sentezlenen placeholder klipler (asset import yok); M12'de gerçek SFX ile değişir, `SfxId` tablosu kalır.

**Bağımlılıklar:** LastGround.Core, LastGround.Data, LastGround.Gameplay (yalnızca event okur)
