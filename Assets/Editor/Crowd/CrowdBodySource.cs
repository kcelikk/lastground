using LastGround.Data.Crowd;

namespace LastGround.EditorTools.Crowd
{
    /// <summary>
    /// Which models become crowd bodies and which clips fill each animation slot (the only table to change when the
    /// zombie asset changes). Mixamo bodies (docs/MIXAMO_DOWNLOAD.md, sources not in git): realistic characters chosen
    /// against docs/reference/models; Humanoid clips from separate motion files retarget onto each body.
    /// Body order is the catalog index used by CrowdVisualCatalog.TypeLooks.
    /// </summary>
    sealed class CrowdBodySource
    {
        public readonly struct ClipSource
        {
            public readonly CrowdClipId Id;
            /// <summary>Motion FBX (Humanoid) or clip name inside the model (legacy).</summary>
            public readonly string Name;
            public readonly bool Loop;

            public ClipSource(CrowdClipId id, string name, bool loop)
            {
                Id = id;
                Name = name;
                Loop = loop;
            }
        }

        public string Id;
        public string ModelPath;
        /// <summary>Clips are separate Humanoid motion files sampled through the body's avatar.</summary>
        public bool Humanoid;
        public float TargetHeight = 1.8f;
        public ClipSource[] Clips;
        /// <summary>Diffuse textures by UDIM tile (u ∈ [k, k+1) uses entry k); one entry for ordinary UVs.</summary>
        public string[] DiffuseTiles;
        /// <summary>
        /// Several materials with their own 0–1 UVs (not UDIM): material-name fragments in <see cref="DiffuseTiles"/>
        /// order, matched against "renderer name/material name"; a submesh matching entry k is moved to tile k.
        /// Unmatched ones use the last tile.
        /// </summary>
        public string[] MaterialTiles;
        /// <summary>Turn saturated cyan (sci-fi glow details) into dull flesh tones.</summary>
        public bool MuteCyan;
        /// <summary>Boss skin: growths → raw meat, the rest → pale grey flesh.</summary>
        public bool Fleshify;

        const string Characters = MixamoImport.Characters;
        const string Motions = MixamoImport.Animations;

        public static readonly CrowdBodySource[] All =
        {
            Zombie("walker_yaku", "Yaku_J_Ignite", "Yakuzombie_diffuse.png", walk: "Zombie_Walk", attack: "Zombie_Attack_Swipe", death: "Zombie_Death_Back"),
            Zombie("walker_girl", "Zombiegirl_W_Kurniawan", "zombie_diffuse.png", walk: "Zombie_Walk_Creeping", attack: "Zombie_Attack_Swipe", death: "Zombie_Death_Forward"),
            Zombie("walker_cop", "Copzombie_L_Actisdato", "FuzZombie_diffuse.png", walk: "Zombie_Walk", attack: "Zombie_Attack_RightHand", death: "Zombie_Death_Forward"),
            Zombie("walker_soldier", "Warzombie_F_Pedroso", "world_war_zombie_diffuse.png", walk: "Zombie_Walk_Creeping", attack: "Zombie_Attack_RightHand", death: "Zombie_Death_Back"),
            Zombie("runner", "Romero", null, walk: "Zombie_Walk", attack: "Zombie_Attack_Swipe", death: "Zombie_Death_Forward",
                tiles: new[] { "Ch10_1001_Diffuse.png", "Ch10_1002_Diffuse.png" }),
            Tank(),
            Zombie("spitter", "Parasite_L_Starkie", "parasiteZombie_diffuse.png", walk: "Zombie_Walk_Creeping", attack: "Zombie_Scream", death: "Zombie_Death_Back"),
            Zombie("exploder", "Survivor_A_Lusth", "Survivor_diffuse.png", walk: "Zombie_Walk", attack: "Zombie_Attack_Swipe", death: "Zombie_Death_Forward"),
            Brute(),
            Player("player_ranger", "Swat_Guy", null, new[] { "Ch15_1001_Diffuse.png", "Ch15_1002_Diffuse.png" }),
            Player("player_survivor", "Erika_Archer", null, new[] { "Erika_Archer_Clothes_diffuse.png", "FemaleFitA_Body_diffuse.png" },
                // Mixamo's mesh names are shuffled in this file; the materials are right (clothes = Akai_MAT).
                new[] { "/Akai_MAT", "/Body_MAT" }),
        };

        static CrowdBodySource Zombie(string id, string model, string diffuse, string walk, string attack, string death, string[] tiles = null)
        {
            return new CrowdBodySource
            {
                Id = id,
                ModelPath = Characters + model + ".fbx",
                Humanoid = true,
                DiffuseTiles = Tiles(model, tiles ?? new[] { diffuse }),
                Clips = new[]
                {
                    // Walk first: it is the fallback for any missing slot.
                    Clip(CrowdClipId.Walk, walk, true),
                    Clip(CrowdClipId.Idle, "Zombie_Idle", true),
                    Clip(CrowdClipId.Run, "Zombie_Run", true),
                    Clip(CrowdClipId.Attack, attack, true),
                    Clip(CrowdClipId.Hit, "Zombie_Hit", false),
                    Clip(CrowdClipId.Crawl, "Zombie_Crawl", true),
                    Clip(CrowdClipId.Death, death, false),
                },
            };
        }

        static CrowdBodySource Tank()
        {
            return new CrowdBodySource
            {
                Id = "tank",
                ModelPath = Characters + "Mutant.fbx",
                Humanoid = true,
                MuteCyan = true,
                DiffuseTiles = Tiles("Mutant", new[] { "Mutant_diffuse.png" }),
                Clips = new[]
                {
                    Clip(CrowdClipId.Walk, "Mutant_Walk", true),
                    Clip(CrowdClipId.Idle, "Mutant_Idle", true),
                    Clip(CrowdClipId.Run, "Mutant_Run", true),
                    Clip(CrowdClipId.Attack, "Mutant_Swipe", true),
                    Clip(CrowdClipId.Hit, "Zombie_Hit", false),
                    Clip(CrowdClipId.Crawl, "Zombie_Crawl", true),
                    Clip(CrowdClipId.Death, "Mutant_Death", false),
                },
            };
        }

        /// <summary>
        /// Boss (D-021): the Mutant with its own clip table — Attack = Ground Slam (punch), Hit = Prop Throw (swipe),
        /// Crawl = roar (intro, Summon Scream, charge windup), Run = charge.
        /// </summary>
        static CrowdBodySource Brute()
        {
            return new CrowdBodySource
            {
                Id = "boss_brute",
                ModelPath = Characters + "Mutant.fbx",
                Humanoid = true,
                Fleshify = true,
                DiffuseTiles = Tiles("Mutant", new[] { "Mutant_diffuse.png" }),
                Clips = new[]
                {
                    Clip(CrowdClipId.Walk, "Mutant_Walk", true),
                    Clip(CrowdClipId.Idle, "Mutant_Idle", true),
                    Clip(CrowdClipId.Run, "Mutant_Run", true),
                    Clip(CrowdClipId.Attack, "Mutant_Punch", false),
                    Clip(CrowdClipId.Hit, "Mutant_Swipe", false),
                    Clip(CrowdClipId.Crawl, "Mutant_Roar", false),
                    Clip(CrowdClipId.Death, "Mutant_Death", false),
                },
            };
        }

        /// <summary>
        /// Player character (M9, D-022): aimed rifle clips (Walk = run forward, Run = run backwards), downed crawl,
        /// death and the three emotes. Baked like crowd bodies; no zombie type look points at them.
        /// </summary>
        static CrowdBodySource Player(string id, string model, string diffuse, string[] tiles = null, string[] materials = null)
        {
            return new CrowdBodySource
            {
                Id = id,
                MaterialTiles = materials,
                ModelPath = Characters + model + ".fbx",
                Humanoid = true,
                DiffuseTiles = Tiles(model, tiles ?? new[] { diffuse }),
                Clips = new[]
                {
                    Clip(CrowdClipId.Walk, "Rifle_Run", true),
                    Clip(CrowdClipId.Idle, "Rifle_Aiming_Idle", true),
                    Clip(CrowdClipId.Run, "Rifle_Run_Backwards", true),
                    Clip(CrowdClipId.Attack, "Rifle_Firing", true),
                    Clip(CrowdClipId.Hit, "Rifle_Hit", false),
                    Clip(CrowdClipId.Crawl, "Zombie_Crawl", true),
                    Clip(CrowdClipId.Death, "Rifle_Death", false),
                    Clip(CrowdClipId.EmoteWave, "Emote_Waving", true),
                    Clip(CrowdClipId.EmoteSalute, "Emote_Salute", false),
                    Clip(CrowdClipId.EmoteCheer, "Emote_Cheering", true),
                },
            };
        }

        static ClipSource Clip(CrowdClipId id, string motion, bool loop) => new ClipSource(id, Motions + motion + ".fbx", loop);

        static string[] Tiles(string model, string[] files)
        {
            var paths = new string[files.Length];
            for (int i = 0; i < files.Length; i++) paths[i] = Characters + model + "_Textures/" + files[i];
            return paths;
        }
    }
}
