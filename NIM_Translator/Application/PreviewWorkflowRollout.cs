using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NIM.ApplicationLayer
{
    /// <summary>Identifies one independently reversible preview workflow.</summary>
    internal enum PreviewWorkflow
    {
        ProjectHub,
        Translation,
        Review,
        Quality,
        History,
        ProjectUpdate,
        Settings,
        AdvancedTools
    }

    /// <summary>Stores whether one preview workflow is enabled.</summary>
    internal sealed class PreviewWorkflowOption : INotifyPropertyChanged
    {
        private bool _isEnabled;

        internal PreviewWorkflowOption(PreviewWorkflow workflow, bool isEnabled)
        {
            Workflow = workflow;
            _isEnabled = isEnabled;
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Gets the stable workflow identity.</summary>
        public PreviewWorkflow Workflow { get; private set; }

        /// <summary>Gets the localized workflow label.</summary>
        public string Label => PreviewMessageCatalog.Get("Rollout_Workflow_" + Workflow);

        /// <summary>Gets or sets whether the preview implementation is selected.</summary>
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

        internal PreviewWorkflowOption Clone()
        {
            return new PreviewWorkflowOption(Workflow, IsEnabled);
        }
    }

    /// <summary>Persists versioned workflow rollout state with atomic backup recovery.</summary>
    internal sealed class PreviewWorkflowRolloutStore
    {
        private const int CurrentVersion = 1;
        private const long MaximumFileBytes = 64 * 1024;
        private readonly string _path;

        internal PreviewWorkflowRolloutStore()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NIM",
                "preview-rollout.xml"))
        {
        }

        internal PreviewWorkflowRolloutStore(string path)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
        }

        internal IReadOnlyList<PreviewWorkflowOption> Load()
        {
            IReadOnlyList<PreviewWorkflowOption> loaded;
            if (TryLoad(_path, out loaded))
            {
                return loaded;
            }

            if (TryLoad(_path + ".bak", out loaded))
            {
                return loaded;
            }

            return CreateDefaults();
        }

        internal void Save(IEnumerable<PreviewWorkflowOption> options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            Dictionary<PreviewWorkflow, PreviewWorkflowOption> normalized = options
                .GroupBy(option => option.Workflow)
                .ToDictionary(group => group.Key, group => group.Last());
            if (Enum.GetValues(typeof(PreviewWorkflow)).Cast<PreviewWorkflow>()
                .Any(workflow => !normalized.ContainsKey(workflow)))
            {
                throw new InvalidDataException("Every preview workflow requires an explicit rollout state.");
            }

            string directory = Path.GetDirectoryName(Path.GetFullPath(_path));
            Directory.CreateDirectory(directory);
            string temporaryPath = _path + ".tmp";
            var root = new XElement("previewRollout", new XAttribute("version", CurrentVersion),
                normalized.OrderBy(pair => pair.Key).Select(pair => new XElement(
                    "workflow",
                    new XAttribute("id", pair.Key),
                    new XAttribute("enabled", pair.Value.IsEnabled))));
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                CloseOutput = false
            }))
            {
                new XDocument(root).Save(writer);
            }

            if (File.Exists(_path))
            {
                File.Replace(temporaryPath, _path, _path + ".bak", true);
            }
            else
            {
                File.Move(temporaryPath, _path);
            }
        }

        internal static IReadOnlyList<PreviewWorkflowOption> CreateDefaults()
        {
            return Enum.GetValues(typeof(PreviewWorkflow)).Cast<PreviewWorkflow>()
                .Select(workflow => new PreviewWorkflowOption(workflow, true)).ToList();
        }

        private static bool TryLoad(string path, out IReadOnlyList<PreviewWorkflowOption> options)
        {
            options = null;
            if (!File.Exists(path) || new FileInfo(path).Length > MaximumFileBytes)
            {
                return false;
            }

            try
            {
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = MaximumFileBytes
                };
                using (XmlReader reader = XmlReader.Create(path, settings))
                {
                    XElement root = XDocument.Load(reader, LoadOptions.None).Root;
                    int version;
                    if (root == null || root.Name != "previewRollout" ||
                        !int.TryParse((string)root.Attribute("version"), out version) ||
                        version != CurrentVersion)
                    {
                        return false;
                    }

                    var loaded = new Dictionary<PreviewWorkflow, PreviewWorkflowOption>();
                    foreach (XElement element in root.Elements("workflow"))
                    {
                        PreviewWorkflow workflow;
                        bool enabled;
                        if (!Enum.TryParse((string)element.Attribute("id"), false, out workflow) ||
                            !bool.TryParse((string)element.Attribute("enabled"), out enabled) ||
                            loaded.ContainsKey(workflow))
                        {
                            return false;
                        }

                        loaded.Add(workflow, new PreviewWorkflowOption(workflow, enabled));
                    }

                    if (loaded.Count != Enum.GetValues(typeof(PreviewWorkflow)).Length)
                    {
                        return false;
                    }

                    options = loaded.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList();
                    return true;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is XmlException || exception is ArgumentException)
            {
                return false;
            }
        }
    }

    /// <summary>Owns staged and applied workflow rollout state for shell routing and Settings.</summary>
    internal sealed class PreviewWorkflowRolloutViewModel : INotifyPropertyChanged
    {
        private readonly PreviewWorkflowRolloutStore _store;
        private IReadOnlyList<PreviewWorkflowOption> _applied;

        internal PreviewWorkflowRolloutViewModel(PreviewWorkflowRolloutStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            Load();
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Occurs after new workflow routing state is applied.</summary>
        internal event EventHandler Applied;

        /// <summary>Gets the staged workflow choices shown in Settings.</summary>
        public IReadOnlyList<PreviewWorkflowOption> Options { get; private set; }

        /// <summary>Gets whether staged workflow choices differ from applied routing.</summary>
        public bool IsModified => Options.Where((option, index) => option.IsEnabled != _applied[index].IsEnabled).Any();

        internal bool IsEnabled(PreviewWorkflow workflow)
        {
            return _applied.First(option => option.Workflow == workflow).IsEnabled;
        }

        internal void Apply()
        {
            _store.Save(Options);
            _applied = Options.Select(option => option.Clone()).ToList();
            OnPropertyChanged(nameof(IsModified));
            Applied?.Invoke(this, EventArgs.Empty);
        }

        internal void Cancel()
        {
            Options = _applied.Select(option => option.Clone()).ToList();
            Subscribe();
            OnPropertyChanged(nameof(Options));
            OnPropertyChanged(nameof(IsModified));
        }

        private void Load()
        {
            _applied = _store.Load().Select(option => option.Clone()).ToList();
            Options = _applied.Select(option => option.Clone()).ToList();
            Subscribe();
        }

        private void Subscribe()
        {
            foreach (PreviewWorkflowOption option in Options)
            {
                option.PropertyChanged += OptionPropertyChanged;
            }
        }

        private void OptionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsModified));
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
