using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Describes one privacy-preserving project revision event.
    /// </summary>
    internal sealed class PreviewRevisionHistoryEntry
    {
        /// <summary>
        /// Creates an auditable entry-change event.
        /// </summary>
        /// <param name="timestampUtc">The UTC event time.</param>
        /// <param name="actionId">The stable localized action identifier suffix.</param>
        /// <param name="entryKey">The project-local affected entry identity.</param>
        /// <param name="record">The user-safe record identity.</param>
        /// <param name="origin">The user-safe content origin.</param>
        /// <param name="sourceFingerprint">The source content fingerprint.</param>
        /// <param name="targetFingerprint">The target content fingerprint.</param>
        internal PreviewRevisionHistoryEntry(
            DateTime timestampUtc,
            string actionId,
            string entryKey,
            string record,
            string origin,
            string sourceFingerprint,
            string targetFingerprint)
        {
            TimestampUtc = timestampUtc.Kind == DateTimeKind.Utc ? timestampUtc : timestampUtc.ToUniversalTime();
            ActionId = actionId ?? throw new ArgumentNullException(nameof(actionId));
            EntryKey = entryKey ?? throw new ArgumentNullException(nameof(entryKey));
            Record = record ?? string.Empty;
            Origin = origin ?? string.Empty;
            SourceFingerprint = sourceFingerprint ?? string.Empty;
            TargetFingerprint = targetFingerprint ?? string.Empty;
        }

        /// <summary>Gets the UTC time at which the event occurred.</summary>
        public DateTime TimestampUtc { get; private set; }

        /// <summary>Gets the stable localized action identifier suffix.</summary>
        public string ActionId { get; private set; }

        /// <summary>Gets the project-local entry identity.</summary>
        public string EntryKey { get; private set; }

        /// <summary>Gets the user-safe record identity.</summary>
        public string Record { get; private set; }

        /// <summary>Gets the user-safe target origin.</summary>
        public string Origin { get; private set; }

        /// <summary>Gets the source-content fingerprint.</summary>
        public string SourceFingerprint { get; private set; }

        /// <summary>Gets the target-content fingerprint.</summary>
        public string TargetFingerprint { get; private set; }

        /// <summary>Gets the localized action label.</summary>
        public string ActionText => PreviewMessageCatalog.Get("History_Action_" + ActionId);

        /// <summary>Gets the event time formatted in the current local culture.</summary>
        public string TimestampText => TimestampUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Persists bounded project history without project paths or translated content.
    /// </summary>
    internal sealed class PreviewRevisionHistoryStore
    {
        private const int MaximumEntries = 2500;
        private const long MaximumFileBytes = 5 * 1024 * 1024;
        private readonly string _storageDirectory;

        internal PreviewRevisionHistoryStore()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NIM",
                "RevisionHistory"))
        {
        }

        internal PreviewRevisionHistoryStore(string storageDirectory)
        {
            _storageDirectory = storageDirectory ?? throw new ArgumentNullException(nameof(storageDirectory));
        }

        internal IReadOnlyList<PreviewRevisionHistoryEntry> Load(string projectPath)
        {
            string path = GetPath(projectPath);
            if (!File.Exists(path) || new FileInfo(path).Length > MaximumFileBytes)
            {
                return new List<PreviewRevisionHistoryEntry>();
            }

            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = MaximumFileBytes
                };
                using (XmlReader reader = XmlReader.Create(path, settings))
                {
                    XElement root = XDocument.Load(reader, LoadOptions.None).Root;
                    if (root == null || root.Name != "history")
                    {
                        return new List<PreviewRevisionHistoryEntry>();
                    }

                    List<XElement> elements = root.Elements("event").ToList();
                    return elements.Skip(Math.Max(0, elements.Count - MaximumEntries))
                        .Select(Parse).Where(entry => entry != null)
                        .OrderByDescending(entry => entry.TimestampUtc).ToList();
                }
            }
            catch (IOException)
            {
                return new List<PreviewRevisionHistoryEntry>();
            }
            catch (UnauthorizedAccessException)
            {
                return new List<PreviewRevisionHistoryEntry>();
            }
            catch (XmlException)
            {
                return new List<PreviewRevisionHistoryEntry>();
            }
        }

        internal void Append(string projectPath, PreviewRevisionHistoryEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            var entries = Load(projectPath).Reverse().ToList();
            entries.Add(entry);
            if (entries.Count > MaximumEntries)
            {
                entries.RemoveRange(0, entries.Count - MaximumEntries);
            }

            Save(projectPath, entries);
        }

        internal static PreviewRevisionHistoryEntry Create(
            string actionId,
            PreviewTranslationEntry entry,
            DateTime timestampUtc)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            return new PreviewRevisionHistoryEntry(
                timestampUtc,
                actionId,
                entry.Key,
                entry.Record,
                entry.Provenance,
                PreviewReviewStateStore.Fingerprint(entry.SourceText),
                PreviewReviewStateStore.Fingerprint(entry.TargetText));
        }

        private void Save(string projectPath, IEnumerable<PreviewRevisionHistoryEntry> entries)
        {
            Directory.CreateDirectory(_storageDirectory);
            string path = GetPath(projectPath);
            string temporaryPath = path + ".tmp";
            var root = new XElement("history", new XAttribute("version", "1"),
                entries.Select(entry => new XElement("event",
                    new XAttribute("time", entry.TimestampUtc.ToString("o", CultureInfo.InvariantCulture)),
                    new XAttribute("action", Limit(entry.ActionId, 64)),
                    new XAttribute("key", Limit(entry.EntryKey, 128)),
                    new XAttribute("record", Limit(entry.Record, 128)),
                    new XAttribute("origin", Limit(entry.Origin, 64)),
                    new XAttribute("source", Limit(entry.SourceFingerprint, 64)),
                    new XAttribute("target", Limit(entry.TargetFingerprint, 64)))));
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                CloseOutput = false
            }))
            {
                new XDocument(root).Save(writer);
            }

            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }

        private string GetPath(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                throw new ArgumentException("A project path is required.", nameof(projectPath));
            }

            return Path.Combine(
                _storageDirectory,
                PreviewReviewStateStore.Fingerprint(Path.GetFullPath(projectPath).ToUpperInvariant()) + ".xml");
        }

        private static PreviewRevisionHistoryEntry Parse(XElement element)
        {
            DateTime timestamp;
            string action = (string)element.Attribute("action");
            string key = (string)element.Attribute("key");
            if (!DateTime.TryParse((string)element.Attribute("time"), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out timestamp) || string.IsNullOrEmpty(action) ||
                string.IsNullOrEmpty(key) || action.Length > 128 || key.Length > 1024)
            {
                return null;
            }

            return new PreviewRevisionHistoryEntry(
                timestamp,
                action,
                key,
                (string)element.Attribute("record"),
                (string)element.Attribute("origin"),
                (string)element.Attribute("source"),
                (string)element.Attribute("target"));
        }

        private static string Limit(string value, int maximumLength)
        {
            string normalized = value ?? string.Empty;
            return normalized.Length <= maximumLength ? normalized : normalized.Substring(0, maximumLength);
        }
    }
}
