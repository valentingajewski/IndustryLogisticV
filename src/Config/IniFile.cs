using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GTA.Math;

namespace IndustryLogisticV.Config
{
    public sealed class IniFile
    {
        private readonly Dictionary<string, Dictionary<string, string>> _data;

        private IniFile(Dictionary<string, Dictionary<string, string>> data)
        {
            _data = data;
        }

        public IEnumerable<string> Sections
        {
            get { return _data.Keys; }
        }

        public static IniFile Load(string path)
        {
            if (!File.Exists(path))
            {
                return new IniFile(new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase));
            }

            return LoadFromLines(File.ReadAllLines(path));
        }

        public static IniFile LoadFromString(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new IniFile(new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase));
            }

            var normalized = content
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
            return LoadFromLines(normalized.Split('\n'));
        }

        private static IniFile LoadFromLines(IEnumerable<string> lines)
        {
            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            string currentSection = "Global";
            data[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawLine in lines)
            {
                if (rawLine == null)
                {
                    continue;
                }

                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                {
                    continue;
                }

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                    if (!data.ContainsKey(currentSection))
                    {
                        data[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }

                    continue;
                }

                var idx = line.IndexOf('=');
                if (idx <= 0)
                {
                    continue;
                }

                var key = line.Substring(0, idx).Trim();
                var value = idx + 1 < line.Length ? line.Substring(idx + 1).Trim() : string.Empty;
                data[currentSection][key] = value;
            }

            return new IniFile(data);
        }

        public bool HasSection(string section)
        {
            return _data.ContainsKey(section);
        }

        public bool HasKey(string section, string key)
        {
            Dictionary<string, string> block;
            if (!_data.TryGetValue(section, out block))
            {
                return false;
            }

            return block.ContainsKey(key);
        }

        public Dictionary<string, string> GetSection(string section)
        {
            Dictionary<string, string> block;
            if (_data.TryGetValue(section, out block))
            {
                return new Dictionary<string, string>(block, StringComparer.OrdinalIgnoreCase);
            }

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string GetString(string section, string key, string defaultValue)
        {
            Dictionary<string, string> block;
            if (!_data.TryGetValue(section, out block))
            {
                return defaultValue;
            }

            string value;
            if (!block.TryGetValue(key, out value) || string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            return value.Trim();
        }

        public bool GetBool(string section, string key, bool defaultValue)
        {
            var raw = GetString(section, key, defaultValue ? "true" : "false");
            bool parsed;
            if (bool.TryParse(raw, out parsed))
            {
                return parsed;
            }

            if (raw.Equals("1", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (raw.Equals("0", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return defaultValue;
        }

        public float GetFloat(string section, string key, float defaultValue)
        {
            var raw = GetString(section, key, string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            float parsed;
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
            {
                return parsed;
            }

            return defaultValue;
        }

        public List<string> GetStringList(string section, string key)
        {
            var raw = GetString(section, key, string.Empty);
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            var split = raw.Split(',');
            for (int i = 0; i < split.Length; i++)
            {
                var value = split[i].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.Add(value);
                }
            }

            return result;
        }

        public Vector3 GetVector3(string section, string key, Vector3 defaultValue)
        {
            var raw = GetString(section, key, string.Empty);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            var split = raw.Split(',');
            if (split.Length < 3)
            {
                return defaultValue;
            }

            var zRaw = split[2].Trim();
            if (split.Length > 3)
            {
                for (int i = 3; i < split.Length; i++)
                {
                    zRaw += "," + split[i].Trim();
                }
            }

            float x;
            float y;
            float z;
            if (!TryParseFloatComponent(split[0].Trim(), out x))
            {
                return defaultValue;
            }

            if (!TryParseFloatComponent(split[1].Trim(), out y))
            {
                return defaultValue;
            }

            if (!TryParseFloatComponent(zRaw, out z))
            {
                return defaultValue;
            }

            return new Vector3(x, y, z);
        }

        private static bool TryParseFloatComponent(string raw, out float value)
        {
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return true;
            }

            var normalized = (raw ?? string.Empty).Trim().Replace(',', '.');
            if (float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            value = 0f;
            return false;
        }
    }
}
