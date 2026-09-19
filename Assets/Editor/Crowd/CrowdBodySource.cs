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
        /// <summary>Turn saturated cyan (sci-fi glow details) into dull flesh tones.</summary>
        public bool MuteCyan;

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

        static ClipSource Clip(CrowdClipId id, string motion, bool loop) => new ClipSource(id, Motions + motion + ".fbx", loop);

        static string[] Tiles(string model, string[] files)
        {
            var paths = new string[files.Length];
            for (int i = 0; i < files.Length; i++) paths[i] = Characters + model + "_Textures/" + files[i];
            return paths;
        }
    }
}
