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
            get => NIMApp.SelfSetting.Preset;
            set => NIMApp.SelfSetting.Preset = value;
        }

        /// <inheritdoc />
        public TranslationPresetSettings ReadSettings()
        {
            return new TranslationPresetSettings(
                NIMApp.EngineSetting.ContextLimit,
                NIMApp.EngineSetting.BucketLengthLimit,
                NIMApp.EngineSetting.PreserveConversationContext,
                NIMApp.EngineSetting.ForceContextDeduplication,
                NIMApp.EngineSetting.StrictLinkBucketPurity);
        }

        /// <inheritdoc />
        public void ApplySettings(TranslationPresetSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            NIMApp.EngineSetting.ContextLimit = settings.ContextLimit;
            NIMApp.EngineSetting.BucketLengthLimit = settings.BucketLengthLimit;
            NIMApp.EngineSetting.PreserveConversationContext = settings.PreserveConversationContext;
            NIMApp.EngineSetting.ForceContextDeduplication = settings.ForceContextDeduplication;
            NIMApp.EngineSetting.StrictLinkBucketPurity = settings.StrictLinkBucketPurity;
        }

        /// <inheritdoc />
        public void Save()
        {
            NIMApp.SelfSetting.SaveConfig();
        }
    }
}
