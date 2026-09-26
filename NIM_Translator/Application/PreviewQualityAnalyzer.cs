using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Produces format-independent preview findings through stable analyzer rules.
    /// </summary>
    internal sealed class PreviewQualityAnalyzer
    {
        private static readonly Regex PlaceholderPattern = new Regex(
            @"\{[^{}]+\}|%(?:\d+\$)?[a-zA-Z]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex TechnicalStringPattern = new Regex(
            @"^(?:https?://\S+|(?=\S*[_./:\\-])[A-Za-z_][A-Za-z0-9_.:/\\-]*)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Analyzes normalized entries without changing their content.
        /// </summary>
        /// <param name="entries">The project entries to inspect.</param>
        /// <param name="cancellationToken">Cancels validation between normalized entries.</param>
        /// <returns>A stable ordered set of normalized findings.</returns>
        internal IReadOnlyList<PreviewQualityFinding> Analyze(
            IReadOnlyList<PreviewTranslationEntry> entries,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var findings = new List<PreviewQualityFinding>();
            var firstTargets = new Dictionary<string, string>(StringComparer.Ordinal);
            var inconsistentSources = new HashSet<string>(StringComparer.Ordinal);
            foreach (PreviewTranslationEntry entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(entry.SourceText))
                {
                    continue;
                }

                string target = entry.TargetText ?? string.Empty;
                string firstTarget;
                if (firstTargets.TryGetValue(entry.SourceText, out firstTarget))
                {
                    if (!string.Equals(firstTarget, target, StringComparison.Ordinal))
                    {
                        inconsistentSources.Add(entry.SourceText);
                    }
                }
                else
                {
                    firstTargets.Add(entry.SourceText, target);
                }
            }

            foreach (PreviewTranslationEntry entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(entry.TargetText))
                {
                    findings.Add(Create(entry, "untranslated", PreviewFindingSeverity.Error,
                        "Translator", "Quality_Finding_Untranslated_Title", "Quality_Finding_Untranslated_Guidance"));
                }

                if (ContainsUnsafeCharacter(entry.SourceText) || ContainsUnsafeCharacter(entry.TargetText))
                {
                    findings.Add(Create(entry, "encoding", PreviewFindingSeverity.Error,
                        "Text validation", "Quality_Finding_Encoding_Title", "Quality_Finding_Encoding_Guidance"));
                }

                if (!PlaceholdersMatch(entry.SourceText, entry.TargetText))
                {
                    findings.Add(Create(entry, "placeholder-mismatch", PreviewFindingSeverity.Error,
                        "Placeholder validation", "Quality_Finding_Placeholder_Title", "Quality_Finding_Placeholder_Guidance"));
                }

                if (inconsistentSources.Contains(entry.SourceText))
                {
                    findings.Add(Create(entry, "duplicate-inconsistent", PreviewFindingSeverity.Warning,
                        "Consistency validation", "Quality_Finding_Duplicate_Title", "Quality_Finding_Duplicate_Guidance"));
                }

                if (TechnicalStringPattern.IsMatch(entry.SourceText ?? string.Empty))
                {
                    findings.Add(Create(entry, "technical-string", PreviewFindingSeverity.Warning,
                        "Technical-string analysis", "Quality_Finding_Technical_Title", "Quality_Finding_Technical_Guidance"));
                }

                if (entry.Score < 50)
                {
                    findings.Add(Create(entry, "low-confidence", PreviewFindingSeverity.Warning,
                        entry.Type, "Quality_Finding_Confidence_Title", "Quality_Finding_Confidence_Guidance"));
                }
            }

            findings.Sort((left, right) => CompareFindings(left, right, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            return findings;
        }

        private static int CompareFindings(
            PreviewQualityFinding left,
            PreviewQualityFinding right,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int comparison = right.Severity.CompareTo(left.Severity);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = StringComparer.OrdinalIgnoreCase.Compare(left.Entry.Type, right.Entry.Type);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = StringComparer.Ordinal.Compare(left.Entry.Key, right.Entry.Key);
            return comparison != 0
                ? comparison
                : StringComparer.Ordinal.Compare(left.RuleId, right.RuleId);
        }

        private static PreviewQualityFinding Create(
            PreviewTranslationEntry entry,
            string ruleId,
            PreviewFindingSeverity severity,
            string source,
            string titleId,
            string guidanceId)
        {
            return new PreviewQualityFinding(
                ruleId,
                severity,
                source,
                entry,
                PreviewMessageCatalog.Get(titleId),
                PreviewMessageCatalog.Get(guidanceId));
        }

        private static bool ContainsUnsafeCharacter(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.IndexOf('\uFFFD') >= 0 || value.Any(character =>
                char.IsControl(character) && character != '\r' && character != '\n' && character != '\t');
        }

        private static bool PlaceholdersMatch(string source, string target)
        {
            string[] sourcePlaceholders = PlaceholderPattern.Matches(source ?? string.Empty)
                .Cast<Match>().Select(match => match.Value).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string[] targetPlaceholders = PlaceholderPattern.Matches(target ?? string.Empty)
                .Cast<Match>().Select(match => match.Value).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            return sourcePlaceholders.SequenceEqual(targetPlaceholders, StringComparer.Ordinal);
        }
    }
}
