using UnityEngine;

namespace LastGround.Data.Meta
{
    /// <summary>
    /// Badge + title (TDD_01 §14.7): earned when a profile statistic reaches a threshold ("Extract at Threat V",
    /// "500 revives"); the equipped title shows under the player's name in lobbies. Never bought.
    /// </summary>
    [CreateAssetMenu(menuName = "LastGround/Meta/Title")]
    public sealed class TitleDefinition : MetaItem
    {
        public ProfileStat Stat;
        public int Threshold = 1;
    }
}
