using System;
using System.Collections.Generic;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Identifies the observable loading state of entry context.
    /// </summary>
    internal enum PreviewContextState
    {
        /// <summary>The inspector has no selected entry.</summary>
        Empty,
        /// <summary>The inspector is loading bounded context.</summary>
        Loading,
        /// <summary>The inspector loaded at least one supported context source.</summary>
        Ready,
        /// <summary>The inspector could not load parser or asset context.</summary>
        Failed
    }

    /// <summary>
    /// Describes one technical metadata value and its supporting repository source.
    /// </summary>
    internal sealed class PreviewContextMetadata
    {
        internal PreviewContextMetadata(string label, string value, string source)
        {
            Label = label ?? string.Empty;
            Value = value ?? string.Empty;
            Source = source ?? string.Empty;
        }

        /// <summary>Gets the short user-facing metadata label.</summary>
        public string Label { get; private set; }

        /// <summary>Gets the bounded metadata value.</summary>
        public string Value { get; private set; }

        /// <summary>Gets the repository boundary that supplied the value.</summary>
        public string Source { get; private set; }
    }

    /// <summary>
    /// Describes a stable related translation entry.
    /// </summary>
    internal sealed class PreviewContextRelation
    {
        internal PreviewContextRelation(
            string entryKey,
            string title,
            string detail,
            string source,
            string relationship)
        {
            EntryKey = entryKey ?? string.Empty;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            Source = source ?? string.Empty;
            Relationship = relationship ?? string.Empty;
        }

        /// <summary>Gets the stable project-local key of the related entry.</summary>
        public string EntryKey { get; private set; }

        /// <summary>Gets the safe related-entry title.</summary>
        public string Title { get; private set; }

        /// <summary>Gets bounded parser detail about the relationship.</summary>
        public string Detail { get; private set; }

        /// <summary>Gets the repository boundary that supplied the relationship.</summary>
        public string Source { get; private set; }

        /// <summary>Gets the semantic relationship label.</summary>
        public string Relationship { get; private set; }
    }

    /// <summary>
    /// Describes one NPC owner linked to a stable translation entry.
    /// </summary>
    internal sealed class PreviewNpcContext
    {
        internal PreviewNpcContext(string entryKey, string name, string gender, string voiceType)
        {
            EntryKey = entryKey ?? string.Empty;
            Name = name ?? string.Empty;
            Gender = gender ?? string.Empty;
            VoiceType = voiceType ?? string.Empty;
        }

        /// <summary>Gets the stable project-local key owned by the NPC context.</summary>
        public string EntryKey { get; private set; }

        /// <summary>Gets the NPC display name.</summary>
        public string Name { get; private set; }

        /// <summary>Gets the parser-provided NPC gender.</summary>
        public string Gender { get; private set; }

        /// <summary>Gets the parser-provided voice type.</summary>
        public string VoiceType { get; private set; }
    }

    /// <summary>
    /// Owns a bounded validated visual asset snapshot without exposing its absolute path.
    /// </summary>
    internal sealed class PreviewAssetContext
    {
        internal PreviewAssetContext(string displayName, byte[] content, int width, int height)
        {
            DisplayName = displayName ?? string.Empty;
            Content = content ?? new byte[0];
            Width = width;
            Height = height;
        }

        /// <summary>Gets the safe asset file name without its private path.</summary>
        public string DisplayName { get; private set; }

        /// <summary>Gets the bounded validated image bytes owned by this snapshot.</summary>
        public byte[] Content { get; private set; }

        /// <summary>Gets the decoded image width in pixels.</summary>
        public int Width { get; private set; }

        /// <summary>Gets the decoded image height in pixels.</summary>
        public int Height { get; private set; }

        /// <summary>Gets the formatted decoded pixel dimensions.</summary>
        public string Dimensions => string.Format("{0} × {1}", Width, Height);
    }

    /// <summary>
    /// Contains bounded technical context for one normalized translation entry.
    /// </summary>
    internal sealed class PreviewEntryContext
    {
        internal PreviewEntryContext(
            string code,
            string codeSource,
            IReadOnlyList<PreviewContextMetadata> metadata,
            IReadOnlyList<PreviewContextRelation> relations,
            IReadOnlyList<PreviewNpcContext> npcs,
            PreviewAssetContext asset)
        {
            Code = code ?? string.Empty;
            CodeSource = codeSource ?? string.Empty;
            Metadata = metadata ?? new PreviewContextMetadata[0];
            Relations = relations ?? new PreviewContextRelation[0];
            Npcs = npcs ?? new PreviewNpcContext[0];
            Asset = asset;
        }

        /// <summary>Gets bounded decompiled code associated with the entry.</summary>
        public string Code { get; private set; }

        /// <summary>Gets the repository boundary that supplied the code.</summary>
        public string CodeSource { get; private set; }

        /// <summary>Gets bounded parser and normalized record metadata.</summary>
        public IReadOnlyList<PreviewContextMetadata> Metadata { get; private set; }

        /// <summary>Gets bounded stable relationships to other normalized entries.</summary>
        public IReadOnlyList<PreviewContextRelation> Relations { get; private set; }

        /// <summary>Gets bounded NPC ownership context.</summary>
        public IReadOnlyList<PreviewNpcContext> Npcs { get; private set; }

        /// <summary>Gets the validated visual asset snapshot, or <c>null</c>.</summary>
        public PreviewAssetContext Asset { get; private set; }

        /// <summary>Gets whether at least one supported context source supplied data.</summary>
        public bool HasAnyContext => !string.IsNullOrWhiteSpace(Code) ||
            Metadata.Count > 0 || Relations.Count > 0 || Npcs.Count > 0 || Asset != null;
    }

    /// <summary>
    /// Describes one bounded exact code-search result.
    /// </summary>
    internal sealed class PreviewCodeSearchResult
    {
        internal PreviewCodeSearchResult(int lineNumber, string text)
        {
            LineNumber = lineNumber;
            Text = text ?? string.Empty;
        }

        /// <summary>Gets the exact one-based source line number.</summary>
        public int LineNumber { get; private set; }

        /// <summary>Gets the bounded matching source line text.</summary>
        public string Text { get; private set; }

        /// <summary>Gets the formatted line number and matching text.</summary>
        public string DisplayText => string.Format("{0}: {1}", LineNumber, Text);
    }
}
