using System;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Identifies the impact of one normalized quality finding.
    /// </summary>
    internal enum PreviewFindingSeverity
    {
        Information,
        Warning,
        Error
    }

    /// <summary>
    /// Identifies the user-controlled resolution of a quality finding.
    /// </summary>
    internal enum PreviewFindingResolution
    {
        Open,
        Acknowledged,
        Resolved
    }

    /// <summary>
    /// Describes a project finding independently from its parser or analyzer source.
    /// </summary>
    internal sealed class PreviewQualityFinding
    {
        /// <summary>
        /// Creates a normalized finding with a stable project-local identity.
        /// </summary>
        /// <param name="ruleId">The stable rule identifier.</param>
        /// <param name="severity">The finding impact.</param>
        /// <param name="source">The component that produced the finding.</param>
        /// <param name="entry">The affected translation entry.</param>
        /// <param name="title">The localized finding title.</param>
        /// <param name="guidance">The localized corrective guidance.</param>
        internal PreviewQualityFinding(
            string ruleId,
            PreviewFindingSeverity severity,
            string source,
            PreviewTranslationEntry entry,
            string title,
            string guidance)
        {
            RuleId = ruleId ?? throw new ArgumentNullException(nameof(ruleId));
            Severity = severity;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Guidance = guidance ?? throw new ArgumentNullException(nameof(guidance));
            StableId = ruleId + ":" + entry.Key + ":" + PreviewReviewStateStore.Fingerprint(
                entry.SourceText + "\0" + entry.TargetText);
            Resolution = PreviewFindingResolution.Open;
        }

        /// <summary>
        /// Gets the stable analyzer rule identifier.
        /// </summary>
        public string RuleId { get; private set; }

        /// <summary>
        /// Gets the stable project-local finding identifier.
        /// </summary>
        public string StableId { get; private set; }

        /// <summary>
        /// Gets the finding impact.
        /// </summary>
        public PreviewFindingSeverity Severity { get; private set; }

        /// <summary>
        /// Gets the component that produced the finding.
        /// </summary>
        public string Source { get; private set; }

        /// <summary>
        /// Gets the affected entry.
        /// </summary>
        public PreviewTranslationEntry Entry { get; private set; }

        /// <summary>
        /// Gets the localized finding title.
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// Gets the localized remediation guidance.
        /// </summary>
        public string Guidance { get; private set; }

        /// <summary>
        /// Gets the current finding resolution.
        /// </summary>
        public PreviewFindingResolution Resolution { get; private set; }

        /// <summary>
        /// Gets the localized severity label.
        /// </summary>
        public string SeverityText => PreviewMessageCatalog.Get("Quality_Severity_" + Severity);

        /// <summary>
        /// Gets the localized resolution label.
        /// </summary>
        public string ResolutionText => PreviewMessageCatalog.Get("Quality_Resolution_" + Resolution);

        /// <summary>
        /// Gets whether this open finding blocks export.
        /// </summary>
        public bool IsBlocking => Severity == PreviewFindingSeverity.Error && Resolution != PreviewFindingResolution.Resolved;

        /// <summary>
        /// Restores an acknowledgement after validation regenerated the same stable finding.
        /// </summary>
        internal void Acknowledge()
        {
            if (Severity != PreviewFindingSeverity.Error)
            {
                Resolution = PreviewFindingResolution.Acknowledged;
            }
        }
    }
}
