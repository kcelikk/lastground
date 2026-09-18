using System;
using System.Globalization;
using System.IO;
using System.Text;
using LastGround.Core.Logging;

namespace LastGround.Save
{
    /// <summary>
    /// File store under persistentDataPath/save. Each write goes to a .tmp file, is flushed, then atomically
    /// replaces the live file while the previous version is kept as .bak. Reads verify a CRC header and fall
    /// back to .bak when the live file is damaged.
    /// </summary>
    public sealed class JsonFileSaveStore : ISaveStore
    {
        const string Magic = "LGSAVE1";

        readonly string _directory;

        public JsonFileSaveStore(string directory)
        {
            _directory = directory ?? throw new ArgumentNullException(nameof(directory));
            Directory.CreateDirectory(_directory);
        }

        public bool TryRead(string key, out string data)
        {
            string path = PathFor(key);
            if (TryReadFile(path, out data)) return true;
            if (TryReadFile(path + ".bak", out data))
            {
                Log.Warning(LogCategory.Save, "Recovered '" + key + "' from backup.");
                return true;
            }
            data = null;
            return false;
        }

        public void Write(string key, string data)
        {
            string path = PathFor(key);
            string tmp = path + ".tmp";
            string header = Magic + " " + Crc32.Compute(data).ToString("x8", CultureInfo.InvariantCulture) + "\n";

            using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(header + data);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            if (File.Exists(path))
                File.Replace(tmp, path, path + ".bak");
            else
                File.Move(tmp, path);
        }

        string PathFor(string key)
        {
            return Path.Combine(_directory, key + ".json");
        }

        static bool TryReadFile(string path, out string data)
        {
            data = null;
            if (!File.Exists(path)) return false;

            string content;
            try
            {
                content = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (IOException)
            {
                return false;
            }

            int newline = content.IndexOf('\n');
            if (newline <= 0) return false;
            string[] header = content.Substring(0, newline).Split(' ');
            if (header.Length != 2 || !string.Equals(header[0], Magic, StringComparison.Ordinal)) return false;
            if (!uint.TryParse(header[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint expected))
                return false;

            string payload = content.Substring(newline + 1);
            if (Crc32.Compute(payload) != expected) return false;

            data = payload;
            return true;
        }
    }
}
