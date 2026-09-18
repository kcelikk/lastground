using System;
using UnityEngine;

namespace LastGround.Data.Map
{
    /// <summary>Named rectangular areas of a map (TDD_01 §11.3 region markers; M5 uses them for objectives).</summary>
    [CreateAssetMenu(menuName = "LastGround/Map/Zone Set")]
    public sealed class MapZoneSet : ScriptableObject
    {
        [Serializable]
        public struct Zone
        {
            public string Id;
            public string NameKey;
            public Vector2 Center;
            public Vector2 HalfSize;

            public bool Contains(float x, float z) =>
                Mathf.Abs(x - Center.x) <= HalfSize.x && Mathf.Abs(z - Center.y) <= HalfSize.y;
        }

        public Zone[] Zones;
    }
}
