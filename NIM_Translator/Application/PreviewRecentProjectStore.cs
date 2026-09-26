using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Describes one private recent-project reference while exposing only safe display metadata to WPF.
    /// </summary>
    internal sealed class PreviewRecentProject
    {
        /// <summary>
        /// Creates a bounded recent-project entry.
        /// </summary>
        /// <param name="path">The private absolute project path used only at the file boundary.</param>
        /// <param name="lastOpenedUtc">The UTC time at which the project was opened successfully.</param>
        internal PreviewRecentProject(string path, DateTime lastOpenedUtc)
        {
            Path = path ?? string.Empty;
            LastOpenedUtc = lastOpenedUtc.Kind == DateTimeKind.Utc
                ? lastOpenedUtc
                : lastOpenedUtc.ToUniversalTime();
            DisplayName = System.IO.Path.GetFileName(Path);
            Format = System.IO.Path.GetExtension(Path).TrimStart('.').ToUpperInvariant();
            IsAvailable = File.Exists(Path);
        }

        /// <summary>
        /// Gets the safe file name shown in the Project Hub.
        /// </summary>
        public string DisplayName { get; private set; }

        /// <summary>
        /// Gets the normalized project format label.
        /// </summary>
        public string Format { get; private set; }

        /// <summary>
        /// Gets a localized last-opened timestamp.
        /// </summary>
        public string LastOpenedText => LastOpenedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentUICulture);

        /// <summary>
        /// Gets whether the referenced file is currently available.
        /// </summary>
        public bool IsAvailable { get; private set; }

        /// <summary>
        /// Gets the localized availability label without exposing the private path.
        /// </summary>
        public string AvailabilityText => PreviewMessageCatalog.Get(
            IsAvailable ? "Projects_Recent_Available" : "Projects_Recent_Unavailable");

        /// <summary>
        /// Gets the private path used only to reopen the file.
        /// </summary>
        internal string Path { get; private set; }

        /// <summary>
        /// Gets the ordering timestamp stored in UTC.
        /// </summary>
        internal DateTime LastOpenedUtc { get; private set; }
    }

    /// <summary>
    /// Persists a bounded private recent-project list for the current user.
    /// </summary>
    internal interface IPreviewRecentProjectStore
    {
        /// <summary>
        /// Loads newest-first recent-project references.
        /// </summary>
        /// <returns>The bounded stored references.</returns>
        IReadOnlyList<PreviewRecentProject> Load();

        /// <summary>
        /// Replaces the stored recent-project references atomically.
        /// </summary>
        /// <param name="projects">The newest-first references to persist.</param>
        void Save(IReadOnlyList<PreviewRecentProject> projects);
    }

    /// <summary>
    /// Stores recent projects under local application data with bounded and hardened XML parsing.
    /// </summary>
    internal sealed class PreviewRecentProjectStore : IPreviewRecentProjectStore
    {
        private const int MaximumProjects = 12;
        private const long MaximumFileBytes = 1024L * 1024L;
        private readonly string _path;

        /// <summary>
        /// Creates the default per-user recent-project store.
        /// </summary>
        internal PreviewRecentProjectStore()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NIM",
                "preview-recent-projects.xml"))
        {
        }

        /// <summary>
        /// Creates a recent-project store at an explicit testable location.
        /// </summary>
        /// <param name="path">The private persistence path.</param>
        internal PreviewRecentProjectStore(string path)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
        }

        /// <inheritdoc />
        public IReadOnlyList<PreviewRecentProject> Load()
        {
            try
            {
                if (!File.Exists(_path) || new FileInfo(_path).Length > MaximumFileBytes)
                {
                    return new PreviewRecentProject[0];
                }

                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    MaxCharactersInDocument = MaximumFileBytes,
                    XmlResolver = null
                };
                using (XmlReader reader = XmlReader.Create(_path, settings))
                {
                    return XDocument.Load(reader)
                        .Root?
                        .Elements("project")
                        .Select(ParseProject)
                        .Where(project => project != null)
                        .GroupBy(project => project.Path, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.OrderByDescending(project => project.LastOpenedUtc).First())
                        .OrderByDescending(project => project.LastOpenedUtc)
                        .Take(MaximumProjects)
                        .ToArray() ?? new PreviewRecentProject[0];
                }
            }
            catch (IOException)
            {
                return new PreviewRecentProject[0];
            }
            catch (UnauthorizedAccessException)
            {
                return new PreviewRecentProject[0];
            }
            catch (XmlException)
            {
                return new PreviewRecentProject[0];
            }
        }

        /// <inheritdoc />
        public void Save(IReadOnlyList<PreviewRecentProject> projects)
        {
            string directory = Path.GetDirectoryName(_path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("The recent-project store requires a parent directory.");
            }

            Directory.CreateDirectory(directory);
            string temporaryPath = _path + ".tmp";
            var document = new XDocument(
                new XElement(
                    "recentProjects",
                    (projects ?? new PreviewRecentProject[0])
                        .Where(project => project != null && !string.IsNullOrWhiteSpace(project.Path))
                        .GroupBy(project => project.Path, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.OrderByDescending(project => project.LastOpenedUtc).First())
                        .OrderByDescending(project => project.LastOpenedUtc)
                        .Take(MaximumProjects)
                        .Select(project => new XElement(
                            "project",
                            new XAttribute("path", project.Path),
                            new XAttribute("lastOpenedUtc", project.LastOpenedUtc.ToString("o", CultureInfo.InvariantCulture))))));

            try
            {
                document.Save(temporaryPath);
                if (File.Exists(_path))
                {
                    File.Replace(temporaryPath, _path, null);
                }
                else
                {
                    File.Move(temporaryPath, _path);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static PreviewRecentProject ParseProject(XElement element)
        {
            string path = (string)element.Attribute("path");
            string timestamp = (string)element.Attribute("lastOpenedUtc");
            DateTime lastOpenedUtc;
            if (string.IsNullOrWhiteSpace(path) ||
                path.Length > 32767 ||
                !Path.IsPathRooted(path) ||
                !DateTime.TryParse(
                    timestamp,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out lastOpenedUtc))
            {
                return null;
            }

            try
            {
                return new PreviewRecentProject(path, lastOpenedUtc);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
