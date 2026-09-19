using UnityEngine;

namespace LastGround.Data.Meta
{
    /// <summary>Emote (TDD_01 §14.7): a short player animation and a bubble icon, sent as one small network message.</summary>
    [CreateAssetMenu(menuName = "LastGround/Meta/Emote")]
    public sealed class EmoteDefinition : MetaItem
    {
        /// <summary>Player body animation slot (PlayerClipId).</summary>
        public byte Clip;
        public float Seconds = 2f;
        /// <summary>Short text shown in the bubble (localization key).</summary>
        public string BubbleKey;
    }
}
