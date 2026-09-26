using System;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Identifies the semantic purpose of a preview dialog without coupling application logic to WPF.
    /// </summary>
    internal enum PreviewDialogSeverity
    {
        /// <summary>Communicates neutral information.</summary>
        Information,
        /// <summary>Warns about a reversible risk.</summary>
        Warning,
        /// <summary>Communicates a failed operation.</summary>
        Error,
        /// <summary>Requires explicit confirmation for a destructive or broad change.</summary>
        Destructive
    }

    /// <summary>
    /// Describes one localized, user-safe modal interaction.
    /// </summary>
    internal sealed class PreviewDialogRequest
    {
        /// <summary>
        /// Creates a modal interaction request.
        /// </summary>
        /// <param name="title">The localized user-safe title.</param>
        /// <param name="message">The localized user-safe message.</param>
        /// <param name="severity">The semantic severity.</param>
        /// <param name="requiresConfirmation">Whether the interaction requires an explicit accept or cancel result.</param>
        internal PreviewDialogRequest(
            string title,
            string message,
            PreviewDialogSeverity severity,
            bool requiresConfirmation)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Severity = severity;
            RequiresConfirmation = requiresConfirmation;
        }

        /// <summary>Gets the localized user-safe title.</summary>
        public string Title { get; private set; }

        /// <summary>Gets the localized user-safe message.</summary>
        public string Message { get; private set; }

        /// <summary>Gets the semantic severity independently from color.</summary>
        public PreviewDialogSeverity Severity { get; private set; }

        /// <summary>Gets whether an explicit accept or cancel result is required.</summary>
        public bool RequiresConfirmation { get; private set; }
    }

    /// <summary>
    /// Provides the single modal interaction boundary used by preview workflows.
    /// </summary>
    internal interface IPreviewDialogService
    {
        /// <summary>
        /// Shows one localized interaction and returns whether the user accepted it.
        /// </summary>
        /// <param name="request">The complete user-safe interaction request.</param>
        /// <returns><c>true</c> when the user explicitly accepts the interaction.</returns>
        bool Show(PreviewDialogRequest request);
    }
}
