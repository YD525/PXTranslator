using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Identifies the human review decision recorded for a translation entry.
    /// </summary>
    internal enum PreviewReviewState
    {
        Unreviewed,
        Reviewed,
        Approved,
        Rejected
    }

    /// <summary>
    /// Represents one editable translation record independently from WPF controls and parser implementations.
    /// </summary>
    internal sealed class PreviewTranslationEntry : INotifyPropertyChanged
    {
        private string _targetText;
        private string _savedTargetText;
        private PreviewReviewState _reviewState;
        private string _provenance;

        /// <summary>
        /// Creates an entry from a normalized project record.
        /// </summary>
        /// <param name="key">The stable project-local record key.</param>
        /// <param name="type">The source format or record type.</param>
        /// <param name="record">The user-safe technical record identity.</param>
        /// <param name="sourceText">The source text.</param>
        /// <param name="targetText">The current target text.</param>
        /// <param name="score">The parser or heuristic confidence score.</param>
        internal PreviewTranslationEntry(
            string key,
            string type,
            string record,
            string sourceText,
            string targetText,
            double score)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Type = type ?? string.Empty;
            Record = record ?? string.Empty;
            SourceText = sourceText ?? string.Empty;
            _targetText = targetText ?? string.Empty;
            _savedTargetText = _targetText;
            _reviewState = PreviewReviewState.Unreviewed;
            _provenance = string.IsNullOrWhiteSpace(_targetText) ? "None" : "Imported";
            Score = score;
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the stable project-local record key.
        /// </summary>
        public string Key { get; private set; }

        /// <summary>
        /// Gets the source format or record type.
        /// </summary>
        public string Type { get; private set; }

        /// <summary>
        /// Gets the user-safe technical record identity.
        /// </summary>
        public string Record { get; private set; }

        /// <summary>
        /// Gets the immutable source text.
        /// </summary>
        public string SourceText { get; private set; }

        /// <summary>
        /// Gets or sets the staged target text.
        /// </summary>
        public string TargetText
        {
            get => _targetText;
            set
            {
                string normalizedValue = value ?? string.Empty;
                if (string.Equals(_targetText, normalizedValue, StringComparison.Ordinal))
                {
                    return;
                }

                _targetText = normalizedValue;
                _reviewState = PreviewReviewState.Unreviewed;
                _provenance = "Edited";
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDraft));
                OnPropertyChanged(nameof(IsModified));
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(ReviewState));
                OnPropertyChanged(nameof(ReviewStateText));
                OnPropertyChanged(nameof(Provenance));
            }
        }

        /// <summary>
        /// Gets the parser or heuristic confidence score.
        /// </summary>
        public double Score { get; private set; }

        /// <summary>
        /// Gets whether the entry has no target text.
        /// </summary>
        public bool IsDraft => string.IsNullOrWhiteSpace(_targetText);

        /// <summary>
        /// Gets whether the staged target differs from the last persisted value.
        /// </summary>
        public bool IsModified => !string.Equals(_targetText, _savedTargetText, StringComparison.Ordinal);

        /// <summary>
        /// Gets the localized entry state label.
        /// </summary>
        public string StateText => PreviewMessageCatalog.Get(
            IsModified
                ? "Workspace_State_Modified"
                : IsDraft ? "Workspace_State_Draft" : "Workspace_State_Translated");

        /// <summary>
        /// Gets the current human review decision.
        /// </summary>
        public PreviewReviewState ReviewState => _reviewState;

        /// <summary>
        /// Gets the localized review-state label.
        /// </summary>
        public string ReviewStateText => PreviewMessageCatalog.Get("Review_State_" + _reviewState);

        /// <summary>
        /// Gets the user-safe origin of the current target text.
        /// </summary>
        public string Provenance => PreviewMessageCatalog.Get("Review_Provenance_" + _provenance);

        /// <summary>
        /// Applies a provider-generated target and records its provenance.
        /// </summary>
        /// <param name="targetText">The generated target text.</param>
        internal void ApplyGeneratedTarget(string targetText)
        {
            TargetText = targetText;
            _provenance = "Provider";
            OnPropertyChanged(nameof(Provenance));
        }

        /// <summary>
        /// Applies a target explicitly reused from a compatible project revision.
        /// </summary>
        /// <param name="targetText">The prior revision target selected by the user.</param>
        internal void ApplyReusedTarget(string targetText)
        {
            TargetText = targetText;
            _provenance = "Revision";
            OnPropertyChanged(nameof(Provenance));
        }

        /// <summary>
        /// Records a human review decision for the current target text.
        /// </summary>
        /// <param name="state">The decision to record.</param>
        internal void SetReviewState(PreviewReviewState state)
        {
            if (_reviewState == state)
            {
                return;
            }

            _reviewState = state;
            OnPropertyChanged(nameof(ReviewState));
            OnPropertyChanged(nameof(ReviewStateText));
        }

        /// <summary>
        /// Marks the current target text as persisted.
        /// </summary>
        internal void MarkSaved()
        {
            _savedTargetText = _targetText;
            OnPropertyChanged(nameof(IsModified));
            OnPropertyChanged(nameof(StateText));
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
