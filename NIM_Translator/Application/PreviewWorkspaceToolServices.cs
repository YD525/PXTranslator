using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Reads and writes bounded, key-addressed Phoenix translation tables.
    /// </summary>
    internal sealed class PreviewTranslationTableService
    {
        private const int MaximumTableRows = 500000;
        private const long MaximumTableBytes = 64L * 1024L * 1024L;

        /// <summary>
        /// Imports targets only for draft entries so reviewed or existing content is never replaced implicitly.
        /// </summary>
        /// <param name="path">The bounded UTF-8 TSV input.</param>
        /// <param name="entries">The normalized project entries addressed by stable key.</param>
        /// <returns>The previous targets for entries changed by the import.</returns>
        internal Dictionary<PreviewTranslationEntry, string> ImportDraftTargets(
            string path,
            IReadOnlyList<PreviewTranslationEntry> entries)
        {
            ValidateReadableTable(path);
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length > MaximumTableRows + 1)
            {
                throw new InvalidDataException("The translation table contains too many rows.");
            }

            Dictionary<string, PreviewTranslationEntry> entriesByKey = entries
                .GroupBy(entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var changes = new Dictionary<PreviewTranslationEntry, string>();
            for (int index = 1; index < lines.Length; index++)
            {
                string[] fields = lines[index].Split(new[] { '\t' }, 4);
                if (fields.Length != 4)
                {
                    throw new InvalidDataException("The translation table contains a malformed row.");
                }

                PreviewTranslationEntry entry;
                if (entriesByKey.TryGetValue(Unescape(fields[0]), out entry) && entry.IsDraft)
                {
                    string target = Unescape(fields[3]);
                    if (!string.Equals(target, entry.TargetText, StringComparison.Ordinal))
                    {
                        if (!changes.ContainsKey(entry))
                        {
                            changes.Add(entry, entry.TargetText);
                        }

                        entry.TargetText = target;
                    }
                }
            }

            return changes;
        }

        /// <summary>
        /// Writes a complete translation table through a temporary file and refuses implicit replacement.
        /// </summary>
        /// <param name="path">The new TSV destination.</param>
        /// <param name="entries">The normalized project entries to export.</param>
        internal void Export(string path, IReadOnlyList<PreviewTranslationEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("An export path is required.", nameof(path));
            }

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(
                directory,
                "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var writer = new StreamWriter(temporaryPath, false, new UTF8Encoding(false)))
                {
                    writer.WriteLine("Key\tType\tSource\tTarget");
                    foreach (PreviewTranslationEntry entry in entries)
                    {
                        writer.WriteLine(string.Join(
                            "\t",
                            Escape(entry.Key),
                            Escape(entry.Type),
                            Escape(entry.SourceText),
                            Escape(entry.TargetText)));
                    }
                }

                if (File.Exists(path))
                {
                    throw new IOException("The selected export file already exists.");
                }

                File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static void ValidateReadableTable(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !File.Exists(path) ||
                new FileInfo(path).Length > MaximumTableBytes)
            {
                throw new InvalidDataException(
                    "The translation table is unavailable or exceeds the supported size limit.");
            }
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\t", "\\t")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string Unescape(string value)
        {
            var result = new StringBuilder();
            bool escaped = false;
            foreach (char character in value ?? string.Empty)
            {
                if (escaped)
                {
                    result.Append(
                        character == 't'
                            ? '\t'
                            : character == 'r' ? '\r' : character == 'n' ? '\n' : character);
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else
                {
                    result.Append(character);
                }
            }

            if (escaped)
            {
                result.Append('\\');
            }

            return result.ToString();
        }
    }

    /// <summary>Reads and writes bounded files compatible with the Phoenix RamCache exchange format.</summary>
    internal sealed class PreviewRamCacheService
    {
        private const int MaximumRows = 500000;
        private const int MaximumTextCharacters = 4 * 1024 * 1024;
        private const long MaximumBytes = 64L * 1024L * 1024L;

        /// <summary>Imports matching draft targets and returns their previous values for undo.</summary>
        /// <param name="path">The selected bounded RamCache JSON file.</param>
        /// <param name="entries">The active normalized project entries.</param>
        /// <returns>The previous targets for entries changed by the import.</returns>
        internal Dictionary<PreviewTranslationEntry, string> ImportDraftTargets(
            string path,
            IReadOnlyList<PreviewTranslationEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) ||
                new FileInfo(path).Length > MaximumBytes)
            {
                throw new InvalidDataException("The RamCache file is unavailable or exceeds the supported limit.");
            }

            List<PreviewRamCacheRecord> records;
            using (var stream = new StreamReader(path, Encoding.UTF8, true))
            using (var reader = new JsonTextReader(stream))
            {
                records = new JsonSerializer().Deserialize<List<PreviewRamCacheRecord>>(reader) ??
                    new List<PreviewRamCacheRecord>();
            }

            if (records.Count > MaximumRows || records.Any(record => !IsValid(record)) ||
                records.GroupBy(record => record.Key, StringComparer.Ordinal).Any(group => group.Count() > 1))
            {
                throw new InvalidDataException("The RamCache file contains invalid or excessive records.");
            }

            Dictionary<string, PreviewTranslationEntry> byKey = entries
                .GroupBy(entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var changes = new Dictionary<PreviewTranslationEntry, string>();
            foreach (PreviewRamCacheRecord record in records)
            {
                PreviewTranslationEntry entry;
                if (!byKey.TryGetValue(record.Key, out entry) || !entry.IsDraft ||
                    !string.Equals(entry.SourceText, record.SourceText, StringComparison.Ordinal) ||
                    string.Equals(record.SourceText, record.TransText, StringComparison.Ordinal) ||
                    string.Equals(entry.TargetText, record.TransText, StringComparison.Ordinal))
                {
                    continue;
                }

                changes.Add(entry, entry.TargetText);
                entry.TargetText = record.TransText;
            }

            return changes;
        }

        /// <summary>Exports normalized entries through an atomic, non-overwriting RamCache JSON write.</summary>
        /// <param name="path">The new RamCache destination.</param>
        /// <param name="entries">The active normalized project entries.</param>
        internal void Export(string path, IReadOnlyList<PreviewTranslationEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A RamCache export path is required.", nameof(path));
            }

            string destination = Path.GetFullPath(path);
            if (File.Exists(destination))
            {
                throw new IOException("The selected RamCache export already exists.");
            }

            string directory = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(directory);
            string temporaryPath = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var records = entries.Select(entry => new PreviewRamCacheRecord
                {
                    Height = 50,
                    Type = entry.Type,
                    Key = entry.Key,
                    SourceText = entry.SourceText,
                    RealSource = string.Empty,
                    TransText = entry.TargetText,
                    Score = entry.Score
                }).ToList();
                File.WriteAllText(
                    temporaryPath,
                    JsonConvert.SerializeObject(records, Formatting.Indented),
                    new UTF8Encoding(false));
                File.Move(temporaryPath, destination);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static bool IsValid(PreviewRamCacheRecord record)
        {
            return record != null && !string.IsNullOrEmpty(record.Key) &&
                record.Key.Length <= 1024 && (record.Type ?? string.Empty).Length <= 128 &&
                (record.SourceText ?? string.Empty).Length <= MaximumTextCharacters &&
                (record.TransText ?? string.Empty).Length <= MaximumTextCharacters;
        }

        private sealed class PreviewRamCacheRecord
        {
            /// <summary>Gets or sets the legacy editor row height.</summary>
            public double Height { get; set; }

            /// <summary>Gets or sets the normalized record type.</summary>
            public string Type { get; set; }

            /// <summary>Gets or sets the stable translation entry key.</summary>
            public string Key { get; set; }

            /// <summary>Gets or sets the source text used to validate the import.</summary>
            public string SourceText { get; set; }

            /// <summary>Gets or sets the optional unnormalized source text.</summary>
            public string RealSource { get; set; }

            /// <summary>Gets or sets the translated target text.</summary>
            public string TransText { get; set; }

            /// <summary>Gets or sets the translation confidence score.</summary>
            public double Score { get; set; }
        }
    }

    /// <summary>
    /// Creates and validates privacy-preserving manual provider exchanges.
    /// </summary>
    internal sealed class PreviewInteractiveExchangeService
    {
        private static readonly Regex RequestIdRegex = new Regex(
            @"<!--\s*Request ID:\s*([A-Za-z0-9-]+)\s*-->",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Creates a request containing a non-content correlation identifier.
        /// </summary>
        /// <param name="source">The selected source text.</param>
        /// <param name="requestId">Receives the identifier required in the response.</param>
        /// <returns>The complete copyable request.</returns>
        internal string Prepare(string source, out string requestId)
        {
            requestId = Guid.NewGuid().ToString("N");
            return string.Format(
                "Translate the following text and preserve the request comment exactly.\r\n\r\n" +
                "{0}\r\n\r\n<!-- Request ID: {1} -->",
                source ?? string.Empty,
                requestId);
        }

        /// <summary>
        /// Validates response identity and removes the transport-only request marker.
        /// </summary>
        /// <param name="response">The untrusted manual provider response.</param>
        /// <param name="requestId">The identifier issued with the request.</param>
        /// <param name="target">Receives the normalized target without the request marker.</param>
        /// <returns><c>true</c> only when the response contains the matching identifier.</returns>
        internal bool TryExtractTarget(string response, string requestId, out string target)
        {
            target = string.Empty;
            if (string.IsNullOrWhiteSpace(response) || string.IsNullOrWhiteSpace(requestId))
            {
                return false;
            }

            Match match = RequestIdRegex.Match(response);
            if (!match.Success || !string.Equals(match.Groups[1].Value, requestId, StringComparison.Ordinal))
            {
                return false;
            }

            target = RequestIdRegex.Replace(response, string.Empty).Trim();
            return !string.IsNullOrWhiteSpace(target);
        }
    }
}
