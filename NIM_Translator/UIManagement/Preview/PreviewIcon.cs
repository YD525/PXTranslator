using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

namespace NIM.UIManagement.Preview
{
    /// <summary>Identifies an application-semantic icon independently from its upstream glyph name.</summary>
    public enum PreviewIconName
    {
        /// <summary>Represents projects and project selection.</summary>
        Projects,
        /// <summary>Represents translation work.</summary>
        Translate,
        /// <summary>Represents review decisions.</summary>
        Review,
        /// <summary>Represents quality validation.</summary>
        Quality,
        /// <summary>Represents revision history.</summary>
        History,
        /// <summary>Represents project updates and synchronization.</summary>
        ProjectUpdate,
        /// <summary>Represents application settings.</summary>
        Settings,
        /// <summary>Represents progressively disclosed expert tools.</summary>
        AdvancedTools,
        /// <summary>Represents informational content and diagnostics.</summary>
        Information,
        /// <summary>Represents opening a project or resource.</summary>
        Open,
        /// <summary>Represents adding a staged item.</summary>
        Add,
        /// <summary>Represents removing a reference or item.</summary>
        Remove,
        /// <summary>Represents dismissing or closing a surface.</summary>
        Dismiss,
        /// <summary>Represents saving current work.</summary>
        Save,
        /// <summary>Represents exporting project data.</summary>
        Export,
        /// <summary>Represents importing project data.</summary>
        Import,
        /// <summary>Represents searching content.</summary>
        Search,
        /// <summary>Represents filtering a collection.</summary>
        Filter,
        /// <summary>Represents a recoverable warning.</summary>
        Warning,
        /// <summary>Represents an operation error.</summary>
        Error,
        /// <summary>Represents a successful operation.</summary>
        Success,
        /// <summary>Represents a translation provider.</summary>
        Provider,
        /// <summary>Represents terminology database access.</summary>
        Database,
        /// <summary>Represents token telemetry.</summary>
        Telemetry,
        /// <summary>Represents a translation preset.</summary>
        Preset,
        /// <summary>Represents moving an item earlier.</summary>
        MoveUp,
        /// <summary>Represents moving an item later.</summary>
        MoveDown,
        /// <summary>Represents applying staged changes.</summary>
        Apply,
        /// <summary>Represents pausing live updates.</summary>
        Pause,
        /// <summary>Represents resuming live updates.</summary>
        Resume,
        /// <summary>Represents refreshing current data.</summary>
        Refresh,
        /// <summary>Represents a document or file.</summary>
        Document,
        /// <summary>Represents Add File. </summary>
        AddFile,
        /// <summary> Represents statistical charts.</summary>
        Chart
    }

    /// <summary>Renders one semantic icon from the embedded Fluent font subset with a text fallback.</summary>
    public sealed class PreviewIcon : TextBlock
    {
        private static readonly FontFamily FluentFont = new FontFamily(
            new Uri("pack://application:,,,/NIMTranslator;component/", UriKind.Absolute),
            "./Assets/Fonts/#FluentSystemIcons-Regular");
        private static readonly bool IsEmbeddedFontAvailable = CheckEmbeddedFont();

        /// <summary>Identifies the semantic icon dependency property.</summary>
        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon),
            typeof(PreviewIconName),
            typeof(PreviewIcon),
            new FrameworkPropertyMetadata(PreviewIconName.Information, OnIconChanged));

        /// <summary>Creates an icon that inherits size and color from its surrounding Phoenix control.</summary>
        public PreviewIcon()
        {
            Focusable = false;
            IsHitTestVisible = false;
            TextAlignment = TextAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
            UpdateGlyph();
        }

        /// <summary>Gets or sets the application-semantic icon to render.</summary>
        public PreviewIconName Icon
        {
            get => (PreviewIconName)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        /// <summary>Excludes decorative icon glyphs from the automation tree.</summary>
        /// <returns><see langword="null"/> because the owning labeled control exposes the accessible meaning.</returns>
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return null;
        }

        private static void OnIconChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ((PreviewIcon)sender).UpdateGlyph();
        }

        private void UpdateGlyph()
        {
            Text = PreviewIconRegistry.Resolve(Icon, IsEmbeddedFontAvailable);
            FontFamily = IsEmbeddedFontAvailable ? FluentFont : SystemFonts.MessageFontFamily;
        }

        private static bool CheckEmbeddedFont()
        {
            try
            {
                var resource = Application.GetResourceStream(
                    new Uri("/NIMTranslator;component/Assets/Fonts/NIMFluentIcons.ttf", UriKind.Relative));
                resource?.Stream.Dispose();
                return resource != null;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>Resolves semantic icon names through the generated pinned Fluent mapping.</summary>
    internal static partial class PreviewIconRegistry
    {
        internal static string Resolve(PreviewIconName icon, bool isFontAvailable)
        {
            PreviewIconDefinition definition;
            if (!Definitions.TryGetValue(icon, out definition))
            {
                return "?";
            }
            return isFontAvailable ? char.ConvertFromUtf32(definition.Codepoint) : definition.Fallback;
        }

        internal static IReadOnlyList<PreviewIconDefinition> GetDefinitions()
        {
            return Definitions.Values.OrderBy(definition => definition.SemanticName, StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>Describes one generated semantic-to-upstream icon mapping.</summary>
    internal sealed class PreviewIconDefinition
    {
        internal PreviewIconDefinition(int codepoint, string semanticName, string upstreamName, string fallback)
        {
            Codepoint = codepoint;
            SemanticName = semanticName ?? throw new ArgumentNullException(nameof(semanticName));
            UpstreamName = upstreamName ?? throw new ArgumentNullException(nameof(upstreamName));
            Fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        }

        internal int Codepoint { get; private set; }
        internal string SemanticName { get; private set; }
        internal string UpstreamName { get; private set; }
        internal string Fallback { get; private set; }
    }
}
