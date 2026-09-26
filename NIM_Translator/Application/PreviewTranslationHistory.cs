using System;
using System.Globalization;

namespace NIM.ApplicationLayer
{
    /// <summary>Represents one content-bearing project translation history record.</summary>
    internal sealed class PreviewTranslationHistoryItem
    {
        internal PreviewTranslationHistoryItem(
            int rowId,
            string entryKey,
            string sourceText,
            string targetText,
            bool isCurrent,
            DateTime timestampUtc)
        {
            RowId = rowId;
            EntryKey = entryKey ?? string.Empty;
            SourceText = sourceText ?? string.Empty;
            TargetText = targetText ?? string.Empty;
            IsCurrent = isCurrent;
            TimestampUtc = timestampUtc.Kind == DateTimeKind.Utc
                ? timestampUtc
                : timestampUtc.ToUniversalTime();
        }

        /// <summary>Gets the engine history row identity.</summary>
        public int RowId { get; private set; }

        /// <summary>Gets the project-local translation entry identity.</summary>
        public string EntryKey { get; private set; }

        /// <summary>Gets the original source text for comparison.</summary>
        public string SourceText { get; private set; }

        /// <summary>Gets the historical translated value.</summary>
        public string TargetText { get; private set; }

        /// <summary>Gets whether the engine marks this row as current.</summary>
        public bool IsCurrent { get; private set; }

        /// <summary>Gets the history timestamp in UTC.</summary>
        public DateTime TimestampUtc { get; private set; }

        /// <summary>Gets a localized timestamp suitable for the timeline.</summary>
        public string TimestampText => TimestampUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

        /// <summary>Gets a localized current or historical state label.</summary>
        public string StateText => PreviewMessageCatalog.Get(
            IsCurrent ? "TranslationHistory_State_Current" : "TranslationHistory_State_Historical");
    }
}
