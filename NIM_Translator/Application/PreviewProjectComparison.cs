using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Identifies how one stable project entry changed between two revisions.
    /// </summary>
    internal enum PreviewRevisionComparisonState
    {
        Unchanged,
        Added,
        Removed,
        Changed,
        Reusable,
        Conflict
    }

    /// <summary>
    /// Describes one stable-identity comparison result without assuming source order.
    /// </summary>
    internal sealed class PreviewProjectComparisonItem
    {
        /// <summary>
        /// Creates a comparison item for current and previous entry versions.
        /// </summary>
        /// <param name="key">The stable project-local identity.</param>
        /// <param name="state">The classified comparison state.</param>
        /// <param name="currentEntry">The current entry, or <see langword="null"/> when removed.</param>
        /// <param name="previousEntry">The previous entry, or <see langword="null"/> when added.</param>
        internal PreviewProjectComparisonItem(
            string key,
            PreviewRevisionComparisonState state,
            PreviewTranslationEntry currentEntry,
            PreviewTranslationEntry previousEntry)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            State = state;
            CurrentEntry = currentEntry;
            PreviousEntry = previousEntry;
        }

        /// <summary>
        /// Gets the stable project-local identity.
        /// </summary>
        public string Key { get; private set; }

        /// <summary>
        /// Gets the classified revision state.
        /// </summary>
        public PreviewRevisionComparisonState State { get; private set; }

        /// <summary>
        /// Gets the current entry, or <see langword="null"/> when it was removed.
        /// </summary>
        public PreviewTranslationEntry CurrentEntry { get; private set; }

        /// <summary>
        /// Gets the previous entry, or <see langword="null"/> when it was added.
        /// </summary>
        public PreviewTranslationEntry PreviousEntry { get; private set; }

        /// <summary>
        /// Gets the localized comparison-state label.
        /// </summary>
        public string StateText => PreviewMessageCatalog.Get("Update_State_" + State);

        /// <summary>
        /// Gets the user-safe record identity available in either revision.
        /// </summary>
        public string Record => CurrentEntry?.Record ?? PreviousEntry?.Record ?? Key;

        /// <summary>
        /// Gets the current source text, or an empty value when removed.
        /// </summary>
        public string CurrentSourceText => CurrentEntry?.SourceText ?? string.Empty;

        /// <summary>
        /// Gets the previous source text, or an empty value when added.
        /// </summary>
        public string PreviousSourceText => PreviousEntry?.SourceText ?? string.Empty;

        /// <summary>
        /// Gets the current target text, or an empty value when removed.
        /// </summary>
        public string CurrentTargetText => CurrentEntry?.TargetText ?? string.Empty;

        /// <summary>
        /// Gets the previous target text, or an empty value when added.
        /// </summary>
        public string PreviousTargetText => PreviousEntry?.TargetText ?? string.Empty;

        /// <summary>
        /// Gets whether a prior target can be explicitly staged in the current project.
        /// </summary>
        public bool CanReuse => CurrentEntry != null && PreviousEntry != null &&
            !string.IsNullOrWhiteSpace(PreviousEntry.TargetText) &&
            (State == PreviewRevisionComparisonState.Reusable || State == PreviewRevisionComparisonState.Conflict);
    }

    /// <summary>
    /// Compares compatible project revisions using stable entry identities.
    /// </summary>
    internal sealed class PreviewProjectComparisonService
    {
        /// <summary>
        /// Compares current and previous entries without using their source order.
        /// </summary>
        /// <param name="currentEntries">The active project entries.</param>
        /// <param name="previousEntries">The selected previous revision entries.</param>
        /// <param name="cancellationToken">Cancels comparison between stable entry identities.</param>
        /// <returns>Stable ordered comparison results.</returns>
        internal IReadOnlyList<PreviewProjectComparisonItem> Compare(
            IReadOnlyList<PreviewTranslationEntry> currentEntries,
            IReadOnlyList<PreviewTranslationEntry> previousEntries,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (currentEntries == null)
            {
                throw new ArgumentNullException(nameof(currentEntries));
            }

            if (previousEntries == null)
            {
                throw new ArgumentNullException(nameof(previousEntries));
            }

            var ambiguousKeys = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, PreviewTranslationEntry> current = IndexEntries(
                currentEntries, ambiguousKeys, cancellationToken);
            Dictionary<string, PreviewTranslationEntry> previous = IndexEntries(
                previousEntries, ambiguousKeys, cancellationToken);
            return current.Keys.Union(previous.Keys, StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .Select(key => ThrowIfCancelled(key, cancellationToken))
                .Select(key => ambiguousKeys.Contains(key)
                    ? new PreviewProjectComparisonItem(
                        key, PreviewRevisionComparisonState.Conflict, Get(current, key), Get(previous, key))
                    : CreateItem(key, Get(current, key), Get(previous, key)))
                .ToList();
        }

        private static Dictionary<string, PreviewTranslationEntry> IndexEntries(
            IEnumerable<PreviewTranslationEntry> entries,
            ISet<string> ambiguousKeys,
            CancellationToken cancellationToken)
        {
            var indexed = new Dictionary<string, PreviewTranslationEntry>(StringComparer.Ordinal);
            foreach (PreviewTranslationEntry entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry == null || string.IsNullOrEmpty(entry.Key))
                {
                    continue;
                }

                if (indexed.ContainsKey(entry.Key))
                {
                    ambiguousKeys.Add(entry.Key);
                }
                else
                {
                    indexed.Add(entry.Key, entry);
                }
            }

            return indexed;
        }

        private static string ThrowIfCancelled(string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return key;
        }

        private static PreviewTranslationEntry Get(
            IDictionary<string, PreviewTranslationEntry> entries,
            string key)
        {
            PreviewTranslationEntry entry;
            return entries.TryGetValue(key, out entry) ? entry : null;
        }

        private static PreviewProjectComparisonItem CreateItem(
            string key,
            PreviewTranslationEntry current,
            PreviewTranslationEntry previous)
        {
            PreviewRevisionComparisonState state;
            if (current == null)
            {
                state = PreviewRevisionComparisonState.Removed;
            }
            else if (previous == null)
            {
                state = PreviewRevisionComparisonState.Added;
            }
            else if (string.Equals(current.SourceText, previous.SourceText, StringComparison.Ordinal))
            {
                if (string.Equals(current.TargetText, previous.TargetText, StringComparison.Ordinal))
                {
                    state = PreviewRevisionComparisonState.Unchanged;
                }
                else if (string.IsNullOrWhiteSpace(current.TargetText) &&
                    !string.IsNullOrWhiteSpace(previous.TargetText))
                {
                    state = PreviewRevisionComparisonState.Reusable;
                }
                else
                {
                    state = PreviewRevisionComparisonState.Conflict;
                }
            }
            else
            {
                state = string.IsNullOrWhiteSpace(current.TargetText) &&
                    current.ReviewState == PreviewReviewState.Unreviewed
                    ? PreviewRevisionComparisonState.Changed
                    : PreviewRevisionComparisonState.Conflict;
            }

            return new PreviewProjectComparisonItem(key, state, current, previous);
        }
    }
}
