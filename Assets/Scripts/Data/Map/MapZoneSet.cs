using System;
using UnityEngine;

namespace LastGround.Data.Map
{
    /// <summary>Named rectangular areas of a map (TDD_01 §11.3 regions): objectives, events, map screen, culling.</summary>
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
            /// <summary>1 (calm) … 5 (deadly): map screen label and event weighting.</summary>
            [Range(1, 5)] public int DangerLevel;
            /// <summary>Concept art for the map screen card (docs/reference/maps, D-020). Optional.</summary>
            public Texture2D Concept;

            public bool Contains(float x, float z) =>
                Mathf.Abs(x - Center.x) <= HalfSize.x && Mathf.Abs(z - Center.y) <= HalfSize.y;
        }

        public Zone[] Zones;
    }
}
