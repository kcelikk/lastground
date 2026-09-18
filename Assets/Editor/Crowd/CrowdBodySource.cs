using LastGround.Data.Crowd;

namespace LastGround.EditorTools.Crowd
{
    /// <summary>
    /// Which models become crowd bodies and which of their clips fill each animation slot.
    /// M2 placeholder: Quaternius Zombie Apocalypse Kit (CC0). Swapping the zombie asset later only changes this table.
    /// </summary>
    sealed class CrowdBodySource
    {
        public readonly struct ClipSource
        {
            public readonly CrowdClipId Id;
            public readonly string Name;
            public readonly bool Loop;

            public ClipSource(CrowdClipId id, string name, bool loop)
            {
                Id = id;
                Name = name;
                Loop = loop;
            }
        }

        const string Kit = "Assets/ThirdParty/Quaternius/ZombieKit/";
        const string Prefix = "CharacterArmature|";

        public string Id;
        public string ModelPath;
        public float TargetHeight = 1.8f;
        public ClipSource[] Clips;

        public static readonly CrowdBodySource[] All =
        {
            Body("zombie_basic", "Zombie_Basic.fbx", attack: "Punch"),
            Body("zombie_arm", "Zombie_Arm.fbx", attack: "Punch"),
            Body("zombie_chubby", "Zombie_Chubby.fbx", attack: "Punch"),
            Body("zombie_ribcage", "Zombie_Ribcage.fbx", attack: null),
        };

        static CrowdBodySource Body(string id, string file, string attack)
        {
            var clips = new System.Collections.Generic.List<ClipSource>
            {
                // Walk first: it is the fallback for any missing slot.
                new ClipSource(CrowdClipId.Walk, Prefix + "Walk", true),
                new ClipSource(CrowdClipId.Idle, Prefix + "Idle", true),
                new ClipSource(CrowdClipId.Run, Prefix + "Run", true),
                new ClipSource(CrowdClipId.Hit, Prefix + "HitReact", false),
                new ClipSource(CrowdClipId.Death, Prefix + "Death", false),
                new ClipSource(CrowdClipId.Crawl, Prefix + "Crawl", true),
            };
            if (attack != null) clips.Add(new ClipSource(CrowdClipId.Attack, Prefix + attack, true));
            return new CrowdBodySource { Id = id, ModelPath = Kit + file, Clips = clips.ToArray() };
        }
    }
}
