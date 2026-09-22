using System;

namespace PhoenixTranslator.ApplicationLayer
{
    /// <summary>
    /// Identifies a reusable translation-engine configuration or an explicitly customized configuration.
    /// </summary>
    public enum TranslationPreset
    {
        /// <summary>Preserves values selected individually by the user.</summary>
        Custom = 0,

        /// <summary>Balances translation quality, speed, and context size.</summary>
        Balanced = 1,

        /// <summary>Prioritizes context completeness and translation quality.</summary>
        QualityFirst = 2,

        /// <summary>Prioritizes throughput and reduced context overhead.</summary>
        SpeedFirst = 3
    }

    /// <summary>Contains the engine values controlled together by a translation preset.</summary>
    internal sealed class TranslationPresetSettings
    {
        /// <summary>Creates one complete preset-controlled settings snapshot.</summary>
        internal TranslationPresetSettings(
            int contextLimit,
            int bucketLengthLimit,
            bool preserveConversationContext,
            bool forceContextDeduplication,
            bool strictLinkBucketPurity)
        {
            ContextLimit = contextLimit;
            BucketLengthLimit = bucketLengthLimit;
            PreserveConversationContext = preserveConversationContext;
            ForceContextDeduplication = forceContextDeduplication;
            StrictLinkBucketPurity = strictLinkBucketPurity;
        }

        /// <summary>Gets the maximum generated context length in characters.</summary>
        internal int ContextLimit { get; }

        /// <summary>Gets the maximum translation bucket length in characters.</summary>
        internal int BucketLengthLimit { get; }

        /// <summary>Gets whether translated conversation lines remain in subsequent context.</summary>
        internal bool PreserveConversationContext { get; }

        /// <summary>Gets whether confirmed relationship contexts remove duplicate lines.</summary>
        internal bool ForceContextDeduplication { get; }

        /// <summary>Gets whether link buckets reject unrelated capacity backfill.</summary>
        internal bool StrictLinkBucketPurity { get; }
    }

    /// <summary>Combines one validated settings snapshot with bounded values for its visual summary.</summary>
    internal sealed class TranslationPresetProfile
    {
        /// <summary>Creates one immutable preset profile.</summary>
        internal TranslationPresetProfile(
            TranslationPresetSettings settings,
            double translationQuality,
            double translationSpeed)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            TranslationQuality = translationQuality;
            TranslationSpeed = translationSpeed;
        }

        /// <summary>Gets the complete engine settings controlled by the profile.</summary>
        internal TranslationPresetSettings Settings { get; }

        /// <summary>Gets the visual quality score in the inclusive range from zero to one hundred.</summary>
        internal double TranslationQuality { get; }

        /// <summary>Gets the visual speed score in the inclusive range from zero to one hundred.</summary>
        internal double TranslationSpeed { get; }
    }

    /// <summary>Defines the engine and persistence boundary used by preset coordination.</summary>
    internal interface ITranslationPresetStore
    {
        /// <summary>Gets or sets the persisted preset identity.</summary>
        TranslationPreset Preset { get; set; }

        /// <summary>Reads a complete snapshot of the current engine values.</summary>
        /// <returns>The current preset-controlled engine values.</returns>
        TranslationPresetSettings ReadSettings();

        /// <summary>Applies a complete validated settings snapshot to the engine.</summary>
        /// <param name="settings">The complete settings snapshot to apply.</param>
        void ApplySettings(TranslationPresetSettings settings);

        /// <summary>Persists the current preset identity and engine configuration.</summary>
        void Save();
    }

    /// <summary>Represents the complete preset state projected to the settings view.</summary>
    internal sealed class TranslationPresetSelection
    {
        /// <summary>Creates one immutable settings-view projection.</summary>
        internal TranslationPresetSelection(
            TranslationPreset preset,
            TranslationPresetSettings settings,
            TranslationPresetProfile profile)
        {
            Preset = preset;
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        /// <summary>Gets the reconciled preset identity.</summary>
        internal TranslationPreset Preset { get; }

        /// <summary>Gets the current complete engine settings.</summary>
        internal TranslationPresetSettings Settings { get; }

        /// <summary>Gets the bounded visual profile.</summary>
        internal TranslationPresetProfile Profile { get; }
    }

    /// <summary>
    /// Creates, validates, and reconciles translation configuration presets without UI or persistence access.
    /// </summary>
    internal sealed class TranslationPresetService
    {
        private const int MinimumLength = 1;
        private const int MaximumLength = 100000;

        /// <summary>Gets a validated profile for a named preset.</summary>
        /// <param name="preset">The named preset to resolve.</param>
        /// <param name="profile">Receives the complete profile when the preset is named and supported.</param>
        /// <returns><c>true</c> when a complete named profile was returned.</returns>
        internal bool TryGetProfile(TranslationPreset preset, out TranslationPresetProfile profile)
        {
            switch (preset)
            {
                case TranslationPreset.Balanced:
                    profile = CreateProfile(200, 3900, false, false, false, 88, 75);
                    return true;
                case TranslationPreset.QualityFirst:
                    profile = CreateProfile(1000, 5500, true, false, true, 100, 60);
                    return true;
                case TranslationPreset.SpeedFirst:
                    profile = CreateProfile(200, 5000, false, true, false, 68, 95);
                    return true;
                default:
                    profile = null;
                    return false;
            }
        }

        /// <summary>Reconciles persisted preset identity with the current engine values.</summary>
        /// <param name="persistedPreset">The preset identity stored with local settings.</param>
        /// <param name="settings">The current engine settings.</param>
        /// <returns>
        /// The persisted preset when all values still match; otherwise <see cref="TranslationPreset.Custom"/>.
        /// </returns>
        internal TranslationPreset Reconcile(
            TranslationPreset persistedPreset,
            TranslationPresetSettings settings)
        {
            if (settings == null || persistedPreset == TranslationPreset.Custom)
            {
                return TranslationPreset.Custom;
            }

            TranslationPresetProfile profile;
            return TryGetProfile(persistedPreset, out profile) && AreEquivalent(profile.Settings, settings)
                ? persistedPreset
                : TranslationPreset.Custom;
        }

        /// <summary>Checks whether numeric preset values are within supported application bounds.</summary>
        /// <param name="settings">The settings snapshot to validate.</param>
        /// <returns><c>true</c> when both length limits are supported.</returns>
        internal bool IsValid(TranslationPresetSettings settings)
        {
            return settings != null &&
                IsValidLength(settings.ContextLimit) &&
                IsValidLength(settings.BucketLengthLimit);
        }

        private static TranslationPresetProfile CreateProfile(
            int contextLimit,
            int bucketLengthLimit,
            bool preserveConversationContext,
            bool forceContextDeduplication,
            bool strictLinkBucketPurity,
            double translationQuality,
            double translationSpeed)
        {
            return new TranslationPresetProfile(
                new TranslationPresetSettings(
                    contextLimit,
                    bucketLengthLimit,
                    preserveConversationContext,
                    forceContextDeduplication,
                    strictLinkBucketPurity),
                ClampScore(translationQuality),
                ClampScore(translationSpeed));
        }

        private static bool AreEquivalent(
            TranslationPresetSettings left,
            TranslationPresetSettings right)
        {
            return left.ContextLimit == right.ContextLimit &&
                left.BucketLengthLimit == right.BucketLengthLimit &&
                left.PreserveConversationContext == right.PreserveConversationContext &&
                left.ForceContextDeduplication == right.ForceContextDeduplication &&
                left.StrictLinkBucketPurity == right.StrictLinkBucketPurity;
        }

        private static bool IsValidLength(int value)
        {
            return value >= MinimumLength && value <= MaximumLength;
        }

        private static double ClampScore(double value)
        {
            return Math.Max(0, Math.Min(100, value));
        }
    }

    /// <summary>Coordinates preset selection, custom edits, and persistence without exposing them to WPF.</summary>
    internal sealed class TranslationPresetCoordinator
    {
        private readonly TranslationPresetService _service;
        private readonly ITranslationPresetStore _store;

        /// <summary>Creates a coordinator for the supplied preset rules and storage boundary.</summary>
        internal TranslationPresetCoordinator(
            TranslationPresetService service,
            ITranslationPresetStore store)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>Loads current values without applying or persisting replacement settings.</summary>
        /// <returns>The reconciled state to display.</returns>
        internal TranslationPresetSelection Load()
        {
            TranslationPresetSettings settings = _store.ReadSettings();
            TranslationPreset preset = _service.Reconcile(_store.Preset, settings);
            _store.Preset = preset;
            return CreateSelection(preset, settings);
        }

        /// <summary>Gets the current engine values without changing preset identity.</summary>
        /// <returns>The current complete settings snapshot.</returns>
        internal TranslationPresetSettings GetCurrentSettings()
        {
            return _store.ReadSettings();
        }

        /// <summary>Applies and persists one explicitly selected preset.</summary>
        /// <param name="preset">The preset selected by the user.</param>
        /// <param name="selection">Receives the complete resulting view state.</param>
        /// <returns><c>true</c> when the preset is supported and was persisted.</returns>
        internal bool TrySelect(
            TranslationPreset preset,
            out TranslationPresetSelection selection)
        {
            TranslationPresetSettings settings;
            if (preset == TranslationPreset.Custom)
            {
                settings = _store.ReadSettings();
            }
            else
            {
                TranslationPresetProfile profile;
                if (!_service.TryGetProfile(preset, out profile) ||
                    !_service.IsValid(profile.Settings))
                {
                    selection = null;
                    return false;
                }

                settings = profile.Settings;
                _store.ApplySettings(settings);
            }

            _store.Preset = preset;
            _store.Save();
            selection = CreateSelection(preset, settings);
            return true;
        }

        /// <summary>Applies a complete manual edit and changes the preset identity to custom.</summary>
        /// <param name="settings">The complete edited settings snapshot.</param>
        /// <param name="persistImmediately">Indicates whether the complete state must be saved immediately.</param>
        /// <param name="selection">Receives the complete resulting view state.</param>
        /// <returns><c>true</c> when the edited values are valid and were applied.</returns>
        internal bool TryApplyCustomSettings(
            TranslationPresetSettings settings,
            bool persistImmediately,
            out TranslationPresetSelection selection)
        {
            if (!_service.IsValid(settings))
            {
                selection = null;
                return false;
            }

            _store.ApplySettings(settings);
            _store.Preset = TranslationPreset.Custom;
            if (persistImmediately)
            {
                _store.Save();
            }

            selection = CreateSelection(TranslationPreset.Custom, settings);
            return true;
        }

        private TranslationPresetSelection CreateSelection(
            TranslationPreset preset,
            TranslationPresetSettings settings)
        {
            TranslationPresetProfile profile;
            if (!_service.TryGetProfile(preset, out profile))
            {
                _service.TryGetProfile(TranslationPreset.Balanced, out profile);
            }

            return new TranslationPresetSelection(preset, settings, profile);
        }
    }
}
