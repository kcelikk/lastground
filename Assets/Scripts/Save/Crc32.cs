using System.Text;

namespace LastGround.Save
{
    /// <summary>IEEE CRC-32 used to detect truncated or corrupted save files (not an anti-cheat measure).</summary>
    public static class Crc32
    {
        static readonly uint[] Table = BuildTable();

        public static uint Compute(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < bytes.Length; i++)
                crc = Table[(crc ^ bytes[i]) & 0xFF] ^ (crc >> 8);
            return ~crc;
        }

        static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }
    }
}
