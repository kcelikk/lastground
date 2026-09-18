using System.Globalization;
using Newtonsoft.Json;

namespace LastGround.Localization
{
    /// <summary>Culture-invariant JSON settings; Turkish devices must parse data exactly like English ones.</summary>
    static class JsonSettings
    {
        public static readonly JsonSerializerSettings Invariant = new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture,
        };
    }
}
