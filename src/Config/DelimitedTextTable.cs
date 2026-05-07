using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LSOL.Config
{
    internal sealed class DelimitedTextTable
    {
        private readonly Dictionary<string, int> _headerIndexByName;

        private DelimitedTextTable(string sourceName, char delimiter, List<string> headers, List<DelimitedTextRow> rows)
        {
            SourceName = sourceName;
            Delimiter = delimiter;
            Headers = headers;
            Rows = rows;
            _headerIndexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < headers.Count; i++)
            {
                var normalized = NormalizeHeader(headers[i]);
                if (!_headerIndexByName.ContainsKey(normalized))
                {
                    _headerIndexByName[normalized] = i;
                }
            }
        }

        public string SourceName { get; }
        public char Delimiter { get; }
        public List<string> Headers { get; }
        public List<DelimitedTextRow> Rows { get; }

        public bool HasHeader(params string[] aliases)
        {
            if (aliases == null)
            {
                return false;
            }

            for (int i = 0; i < aliases.Length; i++)
            {
                if (_headerIndexByName.ContainsKey(NormalizeHeader(aliases[i])))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetColumnIndex(out int index, params string[] aliases)
        {
            index = -1;
            if (aliases == null)
            {
                return false;
            }

            for (int i = 0; i < aliases.Length; i++)
            {
                if (_headerIndexByName.TryGetValue(NormalizeHeader(aliases[i]), out index))
                {
                    return true;
                }
            }

            return false;
        }

        public static DelimitedTextTable Load(string filePath, ICollection<string> validationMessages)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            return LoadLines(Path.GetFileName(filePath), File.ReadAllLines(filePath), validationMessages);
        }

        public static DelimitedTextTable LoadFromString(string sourceName, string content, ICollection<string> validationMessages)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            var normalizedContent = content.Replace("\r\n", "\n").Replace('\r', '\n');
            var rawLines = normalizedContent.Split(new[] { '\n' }, StringSplitOptions.None);
            return LoadLines(sourceName, rawLines, validationMessages);
        }

        private static DelimitedTextTable LoadLines(string sourceName, string[] rawLines, ICollection<string> validationMessages)
        {
            var nonEmpty = rawLines
                .Select((line, index) => new { Line = line, Index = index + 1 })
                .Where(x => !string.IsNullOrWhiteSpace(x.Line))
                .ToList();

            if (nonEmpty.Count == 0)
            {
                validationMessages?.Add((sourceName ?? "table") + " is empty.");
                return null;
            }

            var delimiter = DetectDelimiter(nonEmpty.Select(x => x.Line));
            var header = Split(nonEmpty[0].Line, delimiter);
            if (header.Count == 0)
            {
                validationMessages?.Add((sourceName ?? "table") + " does not contain a readable header row.");
                return null;
            }

            var rows = new List<DelimitedTextRow>();
            for (int i = 1; i < nonEmpty.Count; i++)
            {
                var values = Split(nonEmpty[i].Line, delimiter);
                rows.Add(new DelimitedTextRow(nonEmpty[i].Index, values));
            }

            return new DelimitedTextTable(sourceName ?? string.Empty, delimiter, header, rows);
        }

        private static char DetectDelimiter(IEnumerable<string> lines)
        {
            var candidates = new[] { ',', '\t', ';', '|' };
            var sample = lines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Take(4)
                .ToList();

            char bestDelimiter = ',';
            var bestScore = -1;

            for (int i = 0; i < candidates.Length; i++)
            {
                var delimiter = candidates[i];
                var score = sample.Sum(line => CountDelimiter(line, delimiter));
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDelimiter = delimiter;
                }
            }

            return bestDelimiter;
        }

        private static int CountDelimiter(string line, char delimiter)
        {
            var count = 0;
            var inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        i += 1;
                        continue;
                    }

                    inQuotes = !inQuotes;
                    continue;
                }

                if (!inQuotes && ch == delimiter)
                {
                    count += 1;
                }
            }

            return count;
        }

        private static List<string> Split(string line, char delimiter)
        {
            var values = new List<string>();
            var current = string.Empty;
            var inQuotes = false;

            for (int i = 0; i < (line ?? string.Empty).Length; i++)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current += '"';
                        i += 1;
                        continue;
                    }

                    inQuotes = !inQuotes;
                    continue;
                }

                if (!inQuotes && ch == delimiter)
                {
                    values.Add(current.Trim());
                    current = string.Empty;
                    continue;
                }

                current += ch;
            }

            values.Add(current.Trim());
            return values;
        }

        internal static string NormalizeHeader(string raw)
        {
            return (raw ?? string.Empty)
                .Trim()
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace("/", string.Empty)
                .Replace(".", string.Empty)
                .ToLowerInvariant();
        }
    }

    internal sealed class DelimitedTextRow
    {
        private readonly List<string> _values;

        public DelimitedTextRow(int lineNumber, List<string> values)
        {
            LineNumber = lineNumber;
            _values = values ?? new List<string>();
        }

        public int LineNumber { get; }

        public string GetString(DelimitedTextTable table, params string[] aliases)
        {
            int index;
            if (table == null || !table.TryGetColumnIndex(out index, aliases) || index < 0 || index >= _values.Count)
            {
                return string.Empty;
            }

            return (_values[index] ?? string.Empty).Trim();
        }

        public bool TryGetFloat(DelimitedTextTable table, out float value, params string[] aliases)
        {
            value = 0f;
            var raw = GetString(table, aliases);
            if (string.IsNullOrWhiteSpace(raw) || raw.Equals("NaN", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return true;
            }

            return false;
        }

        public bool TryGetInt(DelimitedTextTable table, out int value, params string[] aliases)
        {
            value = 0;
            int parsedInt;
            var raw = GetString(table, aliases);
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedInt))
            {
                value = parsedInt;
                return true;
            }

            float parsedFloat;
            if (TryGetFloat(table, out parsedFloat, aliases))
            {
                value = (int)Math.Round(parsedFloat);
                return true;
            }

            return false;
        }

        public bool TryGetBool(DelimitedTextTable table, out bool value, params string[] aliases)
        {
            value = false;
            var raw = GetString(table, aliases);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            if (bool.TryParse(raw, out value))
            {
                return true;
            }

            if (raw.Equals("1", StringComparison.OrdinalIgnoreCase) || raw.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                value = true;
                return true;
            }

            if (raw.Equals("0", StringComparison.OrdinalIgnoreCase) || raw.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                value = false;
                return true;
            }

            return false;
        }
    }
}