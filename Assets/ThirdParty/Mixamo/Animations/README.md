# Animasyon kaynakları

İndirme hedefi: FBX for Unity, Without Skin, 30 fps, Keyframe Reduction: none. Hareket kliplerinde In Place seçeneği varsa açın. Kaynak dosya adlarını koruyun; farklı karakter iskeleti için indirilen aynı adlı klipleri karaktere ait alt klasörde saklayın.

İndirilen temel set: `Zombie_Idle`, `Zombie_Walk`, `Zombie_Walk_Creeping`, `Zombie_Run`, `Zombie_Attack_RightHand`, `Zombie_Attack_Swipe`, `Zombie_Hit`, `Zombie_Crawl`, `Zombie_Death_Back`, `Zombie_Death_Forward`, `Zombie_Scream`; mutant seti: `Mutant_Idle`, `Mutant_Walk`, `Mutant_Run`, `Mutant_Punch`, `Mutant_Swipe`, `Mutant_Roar`, `Mutant_Death`. Ayrı FBX klipleri mevcut `CrowdBaker` tarafından otomatik bağlanmıyor; entegrasyon sonrası bake gerekir.

Bağımlılıklar: hedef karakter iskeleti/avatarı ve `Editor/Crowd` klip örnekleme hattı. Değiştirirsen etkilenenler: root motion, saldırı zamanlaması, loop dikişi ve kemik texture bake. Mevcut bake kodu bu klasörü otomatik tüketmez; karakterin kendi FBX'inden klip okur.
