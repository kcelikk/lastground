namespace LastGround.UI.Common
{
    /// <summary>
    /// Reusable character buffer for HUD lines that mix localized words and numbers without allocating
    /// (feed it to TMP_Text.SetCharArray). Rich-text tags can be appended like any other text.
    /// </summary>
    public sealed class CharLine
    {
        static readonly string[] Roman =
        {
            "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X",
            "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX",
        };

        readonly char[] _buffer;

        public CharLine(int capacity = 160)
        {
            _buffer = new char[capacity];
        }

        public char[] Buffer => _buffer;
        public int Length { get; private set; }

        public CharLine Clear()
        {
            Length = 0;
            return this;
        }

        public CharLine Append(string text)
        {
            if (text == null) return this;
            for (int i = 0; i < text.Length && Length < _buffer.Length; i++) _buffer[Length++] = text[i];
            return this;
        }

        public CharLine Append(char c)
        {
            if (Length < _buffer.Length) _buffer[Length++] = c;
            return this;
        }

        /// <summary>Non-negative integer; <paramref name="minDigits"/> pads with zeros.</summary>
        public CharLine Append(int value, int minDigits = 1)
        {
            if (value < 0) value = 0;
            int digits = 1;
            for (int v = value; v >= 10; v /= 10) digits++;
            if (digits < minDigits) digits = minDigits;
            if (Length + digits > _buffer.Length) return this;
            for (int i = digits - 1; i >= 0; i--)
            {
                _buffer[Length + i] = (char)('0' + value % 10);
                value /= 10;
            }
            Length += digits;
            return this;
        }

        /// <summary>mm:ss, or h:mm:ss from one hour (TDD_01 §9.11).</summary>
        public CharLine AppendClock(int totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            int hours = totalSeconds / 3600;
            int minutes = totalSeconds / 60 % 60;
            int seconds = totalSeconds % 60;
            if (hours > 0) Append(hours).Append(':');
            return Append(minutes, 2).Append(':').Append(seconds, 2);
        }

        /// <summary>Roman numerals up to XX, digits beyond (threat levels).</summary>
        public CharLine AppendRoman(int value)
        {
            return value > 0 && value < Roman.Length ? Append(Roman[value]) : Append(value);
        }
    }
}
