using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Contains project-scoped review decisions and acknowledged finding identifiers.
    /// </summary>
    internal sealed class PreviewReviewStateSnapshot
    {
        internal PreviewReviewStateSnapshot()
        {
            Decisions = new Dictionary<string, PreviewReviewDecision>(StringComparer.Ordinal);
            AcknowledgedFindingIds = new HashSet<string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Gets decisions indexed by stable project-local entry key.
        /// </summary>
        internal IDictionary<string, PreviewReviewDecision> Decisions { get; private set; }

        /// <summary>
        /// Gets acknowledged stable finding identifiers.
        /// </summary>
        internal ISet<string> AcknowledgedFindingIds { get; private set; }
    }

    /// <summary>
    /// Describes a review decision bound to the exact target content it assessed.
    /// </summary>
    internal sealed class PreviewReviewDecision
    {
        internal PreviewReviewDecision(PreviewReviewState state, string targetFingerprint)
        {
            State = state;
            TargetFingerprint = targetFingerprint ?? throw new ArgumentNullException(nameof(targetFingerprint));
        }

        /// <summary>
        /// Gets the recorded human decision.
        /// </summary>
        internal PreviewReviewState State { get; private set; }

        /// <summary>
        /// Gets the SHA-256 fingerprint of the target assessed by the decision.
        /// </summary>
        internal string TargetFingerprint { get; private set; }
    }

    /// <summary>
    /// Persists privacy-preserving review metadata outside source projects.
    /// </summary>
    internal sealed class PreviewReviewStateStore
    {
        private const long MaximumStateFileBytes = 5 * 1024 * 1024;
        private readonly string _storageDirectory;

        /// <summary>
        /// Creates the default per-user review-state store.
        /// </summary>
        internal PreviewReviewStateStore()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NIM",
                "ReviewState"))
        {
        }

        /// <summary>
        /// Creates a review-state store at an explicit testable boundary.
        /// </summary>
        /// <param name="storageDirectory">The private metadata directory.</param>
        internal PreviewReviewStateStore(string storageDirectory)
        {
            _storageDirectory = storageDirectory ?? throw new ArgumentNullException(nameof(storageDirectory));
        }

        /// <summary>
        /// Loads metadata for one project without exposing its path or translated content in the stored document.
        /// </summary>
        /// <param name="projectPath">The private absolute project path.</param>
        /// <returns>The validated metadata snapshot, or an empty snapshot when none can be loaded.</returns>
        internal PreviewReviewStateSnapshot Load(string projectPath)
        {
            var snapshot = new PreviewReviewStateSnapshot();
            string statePath = GetStatePath(projectPath);
            if (!File.Exists(statePath) || new FileInfo(statePath).Length > MaximumStateFileBytes)
            {
                return snapshot;
            }

            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = MaximumStateFileBytes
                };
                using (XmlReader reader = XmlReader.Create(statePath, settings))
                {
                    XDocument document = XDocument.Load(reader, LoadOptions.None);
                    XElement root = document.Root;
                    if (root == null || root.Name != "reviewState")
                    {
                        return snapshot;
                    }

                    foreach (XElement element in root.Elements("decision"))
                    {
                        string key = (string)element.Attribute("key");
                        string fingerprint = (string)element.Attribute("target");
                        PreviewReviewState state;
                        if (!string.IsNullOrEmpty(key) && key.Length <= 1024 &&
                            !string.IsNullOrEmpty(fingerprint) && fingerprint.Length == 64 &&
                            Enum.TryParse((string)element.Attribute("state"), out state) &&
                            state != PreviewReviewState.Unreviewed)
                        {
                            snapshot.Decisions[key] = new PreviewReviewDecision(state, fingerprint);
                        }
                    }

                    foreach (XElement element in root.Elements("acknowledgement"))
                    {
                        string id = (string)element.Attribute("id");
                        if (!string.IsNullOrEmpty(id) && id.Length <= 2048)
                        {
                            snapshot.AcknowledgedFindingIds.Add(id);
                        }
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (XmlException)
            {
            }

            return snapshot;
        }

        /// <summary>
        /// Atomically saves decisions and acknowledgements without source text, target text, or project paths.
        /// </summary>
        /// <param name="projectPath">The private absolute project path.</param>
        /// <param name="entries">The entries whose decisions are persisted.</param>
        /// <param name="acknowledgedFindingIds">Stable finding identifiers acknowledged by the user.</param>
        internal void Save(
            string projectPath,
            IEnumerable<PreviewTranslationEntry> entries,
            IEnumerable<string> acknowledgedFindingIds)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            Directory.CreateDirectory(_storageDirectory);
            string statePath = GetStatePath(projectPath);
            string temporaryPath = statePath + ".tmp";
            var root = new XElement("reviewState", new XAttribute("version", "1"));
            foreach (PreviewTranslationEntry entry in entries.Where(entry => entry.ReviewState != PreviewReviewState.Unreviewed))
            {
                root.Add(new XElement("decision",
                    new XAttribute("key", entry.Key),
                    new XAttribute("state", entry.ReviewState),
                    new XAttribute("target", Fingerprint(entry.TargetText))));
            }

            foreach (string id in (acknowledgedFindingIds ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal))
            {
                root.Add(new XElement("acknowledgement", new XAttribute("id", id)));
            }

            var document = new XDocument(root);
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                CloseOutput = false
            }))
            {
                document.Save(writer);
            }

            if (File.Exists(statePath))
            {
                File.Replace(temporaryPath, statePath, null);
            }
            else
            {
                File.Move(temporaryPath, statePath);
            }
        }

        /// <summary>
        /// Computes the content fingerprint used to invalidate stale decisions after an edit.
        /// </summary>
        /// <param name="targetText">The target content to fingerprint.</param>
        /// <returns>A lowercase SHA-256 fingerprint.</returns>
        internal static string Fingerprint(string targetText)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                return ToHex(algorithm.ComputeHash(Encoding.UTF8.GetBytes(targetText ?? string.Empty)));
            }
        }

        private string GetStatePath(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                throw new ArgumentException("A project path is required.", nameof(projectPath));
            }

            string normalizedPath = Path.GetFullPath(projectPath).ToUpperInvariant();
            return Path.Combine(_storageDirectory, Fingerprint(normalizedPath) + ".xml");
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
