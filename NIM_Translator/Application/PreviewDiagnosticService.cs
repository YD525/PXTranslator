using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Identifies a privacy-safe diagnostic event severity.
    /// </summary>
    internal enum PreviewDiagnosticSeverity
    {
        /// <summary>Records normal lifecycle information.</summary>
        Information,
        /// <summary>Records a recoverable degraded state.</summary>
        Warning,
        /// <summary>Records a failed operation boundary.</summary>
        Error
    }

    /// <summary>
    /// Contains one bounded diagnostic event without project content or machine-specific paths.
    /// </summary>
    internal sealed class PreviewDiagnosticEntry
    {
        internal PreviewDiagnosticEntry(
            DateTime timestampUtc,
            PreviewDiagnosticSeverity severity,
            string eventId,
            string detail)
        {
            TimestampUtc = timestampUtc;
            Severity = severity;
            EventId = eventId ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        /// <summary>Gets the event timestamp in UTC.</summary>
        public DateTime TimestampUtc { get; private set; }

        /// <summary>Gets the semantic severity.</summary>
        public PreviewDiagnosticSeverity Severity { get; private set; }

        /// <summary>Gets the stable event identifier.</summary>
        public string EventId { get; private set; }

        /// <summary>Gets bounded, redacted technical detail.</summary>
        public string Detail { get; private set; }

        /// <summary>Gets a culture-invariant display timestamp.</summary>
        public string TimestampText => TimestampUtc.ToString("u", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Describes one authoritative loaded or deployed component version.
    /// </summary>
    internal sealed class PreviewComponentVersion
    {
        internal PreviewComponentVersion(string component, string assemblyName, string version, string status)
        {
            Component = component ?? string.Empty;
            AssemblyName = assemblyName ?? string.Empty;
            Version = version ?? string.Empty;
            Status = status ?? string.Empty;
        }

        /// <summary>Gets the product-facing component name.</summary>
        public string Component { get; private set; }

        /// <summary>Gets the authoritative assembly identity.</summary>
        public string AssemblyName { get; private set; }

        /// <summary>Gets the assembly version or an unavailable marker.</summary>
        public string Version { get; private set; }

        /// <summary>Gets whether the component was loaded or found beside the application.</summary>
        public string Status { get; private set; }
    }

    /// <summary>
    /// Retains and exports bounded privacy-safe diagnostics for the current application lifetime.
    /// </summary>
    internal sealed class PreviewDiagnosticService
    {
        private const int MaximumEntries = 200;
        private const int MaximumDetailCharacters = 500;
        private static readonly Regex SecretPattern = new Regex(
            @"(?i)\b(api[_ -]?key|authorization|password|secret|token)\b\s*[:=]\s*[^\s,;]+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex WindowsPathPattern = new Regex(
            @"(?i)(?:[a-z]:\\|\\\\)[^\r\n\t<>|""?*]+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly object _sync = new object();
        private readonly List<PreviewDiagnosticEntry> _entries = new List<PreviewDiagnosticEntry>();

        /// <summary>
        /// Records one bounded event after redacting secret-like values and absolute Windows paths.
        /// </summary>
        /// <param name="severity">The semantic event severity.</param>
        /// <param name="eventId">A stable non-content event identifier.</param>
        /// <param name="detail">Optional technical detail that must not intentionally contain project content.</param>
        internal void Record(PreviewDiagnosticSeverity severity, string eventId, string detail = null)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                throw new ArgumentException("A diagnostic event identifier is required.", nameof(eventId));
            }

            var entry = new PreviewDiagnosticEntry(
                DateTime.UtcNow,
                severity,
                Sanitize(eventId),
                Sanitize(detail));
            lock (_sync)
            {
                _entries.Add(entry);
                if (_entries.Count > MaximumEntries)
                {
                    _entries.RemoveRange(0, _entries.Count - MaximumEntries);
                }
            }
        }

        /// <summary>
        /// Returns a stable newest-first snapshot of retained diagnostic events.
        /// </summary>
        /// <returns>A detached bounded event snapshot.</returns>
        internal IReadOnlyList<PreviewDiagnosticEntry> GetEntries()
        {
            lock (_sync)
            {
                return _entries.AsEnumerable().Reverse().ToArray();
            }
        }

        /// <summary>
        /// Returns authoritative product and dependency versions without exposing file-system paths.
        /// </summary>
        /// <returns>The known component versions in dependency order.</returns>
        internal IReadOnlyList<PreviewComponentVersion> GetComponentVersions()
        {
            return new[]
            {
                GetComponentVersion("NIM Translator", "NIMTranslator"),
                GetComponentVersion("NIM Engine", "NIMEngine"),
                GetComponentVersion("PexInterface", "PexInterface"),
                GetComponentVersion("PexReader", "PEX.Interop"),
                GetComponentVersion("EspReader", "EspReader")
            };
        }

        /// <summary>
        /// Exports retained diagnostics using atomic replacement and a content-free text format.
        /// </summary>
        /// <param name="path">The private destination selected by the user.</param>
        /// <exception cref="ArgumentException"><paramref name="path"/> is empty.</exception>
        internal void Export(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A diagnostic export path is required.", nameof(path));
            }

            string destination = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(directory, "." + Path.GetFileName(destination) + ".tmp");
            try
            {
                var builder = new StringBuilder();
                builder.AppendLine("NIM Translator diagnostic report");
                builder.AppendLine("Generated (UTC): " + DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture));
                builder.AppendLine();
                builder.AppendLine("Components");
                foreach (PreviewComponentVersion component in GetComponentVersions())
                {
                    builder.AppendLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}\t{1}\t{2}\t{3}",
                        component.Component,
                        component.AssemblyName,
                        component.Version,
                        component.Status));
                }

                builder.AppendLine();
                builder.AppendLine("Events");
                foreach (PreviewDiagnosticEntry entry in GetEntries().Reverse())
                {
                    builder.AppendLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}\t{1}\t{2}\t{3}",
                        entry.TimestampText,
                        entry.Severity,
                        entry.EventId,
                        entry.Detail));
                }

                File.WriteAllText(temporaryPath, builder.ToString(), new UTF8Encoding(false));
                if (File.Exists(destination))
                {
                    File.Replace(temporaryPath, destination, null);
                }
                else
                {
                    File.Move(temporaryPath, destination);
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

        /// <summary>
        /// Redacts secret-like assignments and absolute Windows paths, removes line breaks, and bounds output.
        /// </summary>
        /// <param name="value">Potentially unsafe technical detail.</param>
        /// <returns>A single-line bounded value suitable for retained diagnostics.</returns>
        internal static string Sanitize(string value)
        {
            string sanitized = value ?? string.Empty;
            sanitized = SecretPattern.Replace(sanitized, "$1=<redacted>");
            sanitized = WindowsPathPattern.Replace(sanitized, "<path>");
            sanitized = sanitized.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            return sanitized.Length <= MaximumDetailCharacters
                ? sanitized
                : sanitized.Substring(0, MaximumDetailCharacters);
        }

        private static PreviewComponentVersion GetComponentVersion(string component, string assemblyName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(candidate =>
                string.Equals(candidate.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
            if (assembly != null)
            {
                return new PreviewComponentVersion(
                    component,
                    assemblyName,
                    assembly.GetName().Version.ToString(),
                    PreviewMessageCatalog.Get("ShellServices_Component_Loaded"));
            }

            string candidatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyName + ".dll");
            if (File.Exists(candidatePath))
            {
                try
                {
                    AssemblyName deployedAssembly = AssemblyName.GetAssemblyName(candidatePath);
                    return new PreviewComponentVersion(
                        component,
                        assemblyName,
                        deployedAssembly.Version.ToString(),
                        PreviewMessageCatalog.Get("ShellServices_Component_Available"));
                }
                catch (BadImageFormatException)
                {
                }
                catch (FileLoadException)
                {
                }

                try
                {
                    string fileVersion = FileVersionInfo.GetVersionInfo(candidatePath).FileVersion;
                    return new PreviewComponentVersion(
                        component,
                        assemblyName,
                        string.IsNullOrWhiteSpace(fileVersion)
                            ? PreviewMessageCatalog.Get("ShellServices_Component_UnknownVersion")
                            : fileVersion,
                        PreviewMessageCatalog.Get("ShellServices_Component_Available"));
                }
                catch (Exception exception) when (exception is IOException ||
                    exception is UnauthorizedAccessException || exception is SecurityException ||
                    exception is Win32Exception)
                {
                }
            }

            return new PreviewComponentVersion(
                component,
                assemblyName,
                PreviewMessageCatalog.Get("ShellServices_Component_UnknownVersion"),
                PreviewMessageCatalog.Get("ShellServices_Component_Unavailable"));
        }
    }
}
