using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace NIM.ApplicationLayer
{
    /// <summary>Validates SQL accepted by the guarded read-only database surface.</summary>
    internal static class PreviewDatabaseStatementGuard
    {
        /// <summary>Checks whether a statement is a single read-only SELECT query.</summary>
        /// <param name="sql">The user-entered SQL statement.</param>
        /// <returns><c>true</c> only for one SELECT statement without a second statement delimiter.</returns>
        internal static bool IsReadOnly(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return false;
            string statement = sql.TrimStart();
            int delimiter = statement.IndexOf(';');
            if (delimiter >= 0 && !string.IsNullOrWhiteSpace(statement.Substring(delimiter + 1))) return false;
            return statement.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Contains one bounded, display-ready database result row.</summary>
    internal sealed class PreviewDatabaseResultRow
    {
        /// <summary>Creates a database result row without retaining mutable engine values.</summary>
        /// <param name="displayText">The bounded column and value representation.</param>
        internal PreviewDatabaseResultRow(string displayText)
        {
            DisplayText = displayText ?? string.Empty;
        }

        /// <summary>Gets the bounded column and value representation.</summary>
        public string DisplayText { get; private set; }
    }

    /// <summary>Identifies one intent-oriented Advanced Tools page.</summary>
    internal enum PreviewAdvancedToolsPage
    {
        ProviderPipeline,
        CustomProvider,
        TerminologyDatabase,
        Telemetry,
        Presets
    }

    /// <summary>Describes one staged provider-pipeline entry without secret values.</summary>
    internal sealed class PreviewPipelineEntry : INotifyPropertyChanged
    {
        private bool _isEnabled;

        internal PreviewPipelineEntry(int key, string name, string group, bool isEnabled, bool isCustom)
        {
            Key = key;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Group = group ?? throw new ArgumentNullException(nameof(group));
            _isEnabled = isEnabled;
            IsCustom = isCustom;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Gets the stable engine configuration key.</summary>
        public int Key { get; private set; }

        /// <summary>Gets the safe provider display name.</summary>
        public string Name { get; private set; }

        /// <summary>Gets the semantic provider group.</summary>
        public string Group { get; private set; }

        /// <summary>Gets whether the entry represents a custom provider.</summary>
        public bool IsCustom { get; private set; }

        /// <summary>Gets or sets whether the staged provider is enabled.</summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                {
                    return;
                }

                _isEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            }
        }

        internal PreviewPipelineEntry Clone()
        {
            return new PreviewPipelineEntry(Key, Name, Group, IsEnabled, IsCustom);
        }
    }

    /// <summary>Contains a staged custom-provider definition without credentials or response content.</summary>
    internal sealed class PreviewCustomProviderDraft : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _group = "Cloud AI";
        private string _endpoint = string.Empty;
        private string _model = string.Empty;
        private string _responseField = string.Empty;
        private string _headers = string.Empty;
        private string _payload = string.Empty;
        private bool _usePost = true;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name { get => _name; set => Set(ref _name, value); }
        public string Group { get => _group; set => Set(ref _group, value); }
        public string Endpoint { get => _endpoint; set => Set(ref _endpoint, value); }
        public string Model { get => _model; set => Set(ref _model, value); }
        public string ResponseField { get => _responseField; set => Set(ref _responseField, value); }
        public string Headers { get => _headers; set => Set(ref _headers, value); }
        public string Payload { get => _payload; set => Set(ref _payload, value); }
        public bool UsePost { get => _usePost; set => Set(ref _usePost, value); }

        internal PreviewCustomProviderDraft Clone()
        {
            return new PreviewCustomProviderDraft
            {
                Name = Name,
                Group = Group,
                Endpoint = Endpoint,
                Model = Model,
                ResponseField = ResponseField,
                Headers = Headers,
                Payload = Payload,
                UsePost = UsePost
            };
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>Provides safe persistence and operation boundaries for the Advanced Tools workspace.</summary>
    internal interface IPreviewAdvancedToolsStore
    {
        IReadOnlyList<PreviewPipelineEntry> LoadPipeline();
        void SavePipeline(IReadOnlyList<PreviewPipelineEntry> entries);
        Task<PreviewProviderTestResult> TestCustomProviderAsync(
            PreviewCustomProviderDraft draft,
            CancellationToken cancellationToken);
        void SaveCustomProvider(PreviewCustomProviderDraft draft);
        IReadOnlyList<PreviewDatabaseResultRow> ExecuteDatabaseQuery(string sql, bool allowMutation);
        IReadOnlyList<KeyValuePair<string, long>> ReadTokenUsage();
        void ClearTokenUsage();
    }
}
