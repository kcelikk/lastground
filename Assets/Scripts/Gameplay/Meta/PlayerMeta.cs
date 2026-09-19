namespace LastGround.Gameplay.Meta
{
    /// <summary>
    /// A player's meta selection as it travels in the lobby roster (M9): catalog indices of character, outfit, perk,
    /// loadout and title, and which Arsenal weapons the player owns (they join the run's drop pool). Packed into the
    /// session's opaque 64-bit value; 0 means "no selection" (defaults apply).
    /// Bits: 0–3 character, 4–7 outfit, 8–12 perk, 13–17 loadout, 18–23 title, 24–39 owned weapons, 63 valid.
    /// </summary>
    public struct PlayerMeta
    {
        public const byte None = 255;
        const ulong ValidBit = 1UL << 63;

        public byte Character;
        public byte Outfit;
        public byte Perk;
        public byte Loadout;
        public byte Title;
        public ushort OwnedWeapons;

        public static PlayerMeta Default => new PlayerMeta { Perk = None, Loadout = None, Title = None };

        public ulong Pack()
        {
            ulong value = ValidBit;
            value |= (ulong)(uint)(Character & 0xF);
            value |= (ulong)(uint)(Outfit & 0xF) << 4;
            value |= (ulong)(uint)Field(Perk, 31) << 8;
            value |= (ulong)(uint)Field(Loadout, 31) << 13;
            value |= (ulong)(uint)Field(Title, 63) << 18;
            value |= (ulong)OwnedWeapons << 24;
            return value;
        }

        public static PlayerMeta Unpack(ulong value)
        {
            if ((value & ValidBit) == 0) return Default;
            return new PlayerMeta
            {
                Character = (byte)(value & 0xF),
                Outfit = (byte)((value >> 4) & 0xF),
                Perk = Back((int)((value >> 8) & 31), 31),
                Loadout = Back((int)((value >> 13) & 31), 31),
                Title = Back((int)((value >> 18) & 63), 63),
                OwnedWeapons = (ushort)((value >> 24) & 0xFFFF),
            };
        }

        /// <summary>"None" is stored as the field's all-ones value.</summary>
        static int Field(byte index, int max) => index == None || index >= max ? max : index;

        static byte Back(int field, int max) => field == max ? None : (byte)field;
    }
}
