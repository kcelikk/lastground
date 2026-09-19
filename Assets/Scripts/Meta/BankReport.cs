using System.Collections.Generic;
using LastGround.Data.Meta;

namespace LastGround.Meta
{
    /// <summary>Result of banking a run: Scrap gained and the badges earned by it.</summary>
    public sealed class BankReport
    {
        public int ScrapGained;
        public readonly List<TitleDefinition> NewBadges = new List<TitleDefinition>();
    }
}
