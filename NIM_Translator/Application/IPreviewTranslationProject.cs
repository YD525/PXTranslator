using System;
using System.Collections.Generic;
using System.Threading;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Defines the parser, provider, and persistence boundary consumed by the preview workspace.
    /// </summary>
    internal interface IPreviewTranslationProject : IDisposable
    {
        /// <summary>
        /// Gets the private absolute path used only at parser and persistence boundaries.
        /// </summary>
        string Path { get; }

        /// <summary>
        /// Gets the safe project display name.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets the normalized editable project records.
        /// </summary>
        IReadOnlyList<PreviewTranslationEntry> Entries { get; }

        /// <summary>
        /// Translates one entry through the configured provider pipeline.
        /// </summary>
        /// <param name="entry">The entry to translate.</param>
        /// <param name="cancellationToken">Cancels provider work cooperatively.</param>
        /// <returns>The provider-produced target text.</returns>
        string Translate(PreviewTranslationEntry entry, CancellationToken cancellationToken);

        /// <summary>
        /// Loads bounded code, record, NPC, relationship, and asset context for one stable entry.
        /// </summary>
        /// <param name="entry">The selected normalized entry.</param>
        /// <param name="cancellationToken">Cancels parser and asset work cooperatively.</param>
        /// <returns>The supported context snapshot, which may be empty.</returns>
        PreviewEntryContext LoadContext(PreviewTranslationEntry entry, CancellationToken cancellationToken);

        /// <summary>
        /// Persists staged targets through the format-specific writer and backup boundary.
        /// </summary>
        void Save();

        /// <summary>
        /// Writes the staged project to a new destination without replacing an existing file.
        /// </summary>
        /// <param name="path">The new project destination.</param>
        /// <param name="cancellationToken">Cancels work before the atomic final move.</param>
        void Export(string path, CancellationToken cancellationToken);

        /// <summary>Clears selected project translation caches through the engine persistence boundary.</summary>
        /// <param name="clearProviderCache">Whether provider-produced cached targets are cleared.</param>
        /// <param name="clearUserCache">Whether user-entered cached targets are cleared.</param>
        /// <param name="cancellationToken">Cancels before and between cache operations.</param>
        void ClearTranslationCaches(
            bool clearProviderCache,
            bool clearUserCache,
            CancellationToken cancellationToken);

        /// <summary>Loads content-bearing translation history for the active project and language.</summary>
        /// <param name="cancellationToken">Cancels history normalization between records.</param>
        /// <returns>The oldest-first translation history records.</returns>
        IReadOnlyList<PreviewTranslationHistoryItem> LoadTranslationHistory(CancellationToken cancellationToken);

        /// <summary>Stages one historical target in the active normalized entry and marks it current.</summary>
        /// <param name="rowId">The engine history row identity.</param>
        /// <param name="cancellationToken">Cancels before staging the historical target.</param>
        /// <returns>The updated normalized entry, or <see langword="null"/> when unavailable.</returns>
        PreviewTranslationEntry RestoreTranslationHistory(int rowId, CancellationToken cancellationToken);

        /// <summary>Marks one engine history row current without changing staged content.</summary>
        /// <param name="rowId">The engine history row identity.</param>
        void SetCurrentTranslationHistory(int rowId);

        /// <summary>Deletes one engine history row.</summary>
        /// <param name="rowId">The engine history row identity.</param>
        void DeleteTranslationHistory(int rowId);

        /// <summary>Clears all engine translation history for the active project.</summary>
        void ClearTranslationHistory();
    }
}
