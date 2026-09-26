using System;
using PhoenixEngine;

namespace NIM.ApplicationLayer
{
    /// <summary>Adapts existing engine and local settings persistence to preset coordination.</summary>
    internal sealed class LegacyTranslationPresetStore : ITranslationPresetStore
    {
        /// <inheritdoc />
        public TranslationPreset Preset
        {
            get => PhoenixApp.SelfSetting.Preset;
            set => PhoenixApp.SelfSetting.Preset = value;
        }

        /// <inheritdoc />
        public TranslationPresetSettings ReadSettings()
        {
            return new TranslationPresetSettings(
                PhoenixApp.EngineSetting.ContextLimit,
                PhoenixApp.EngineSetting.BucketLengthLimit,
                PhoenixApp.EngineSetting.PreserveConversationContext,
                PhoenixApp.EngineSetting.ForceContextDeduplication,
                PhoenixApp.EngineSetting.StrictLinkBucketPurity);
        }

        /// <inheritdoc />
        public void ApplySettings(TranslationPresetSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            PhoenixApp.EngineSetting.ContextLimit = settings.ContextLimit;
            PhoenixApp.EngineSetting.BucketLengthLimit = settings.BucketLengthLimit;
            PhoenixApp.EngineSetting.PreserveConversationContext = settings.PreserveConversationContext;
            PhoenixApp.EngineSetting.ForceContextDeduplication = settings.ForceContextDeduplication;
            PhoenixApp.EngineSetting.StrictLinkBucketPurity = settings.StrictLinkBucketPurity;
        }

        /// <inheritdoc />
        public void Save()
        {
            PhoenixApp.SelfSetting.SaveConfig();
        }
    }
}
