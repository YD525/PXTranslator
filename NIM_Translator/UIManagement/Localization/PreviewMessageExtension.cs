using System;
using System.Windows.Markup;
using NIM.ApplicationLayer;

namespace NIM.UIManagement.Localization
{
    /// <summary>
    /// Resolves a stable preview message identifier from XAML.
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class PreviewMessageExtension : MarkupExtension
    {
        /// <summary>
        /// Creates a message extension for the specified semantic identifier.
        /// </summary>
        /// <param name="id">The identifier stored in the compiled source catalogue.</param>
        /// <exception cref="ArgumentException"><paramref name="id"/> is empty.</exception>
        public PreviewMessageExtension(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A message identifier is required.", nameof(id));
            }

            Id = id;
        }

        /// <summary>
        /// Gets the semantic source-catalogue identifier.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Resolves the message for the current UI culture.
        /// </summary>
        /// <param name="serviceProvider">The XAML service provider for this markup-extension invocation.</param>
        /// <returns>The localized string registered for <see cref="Id"/>.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return PreviewMessageCatalog.Get(Id);
        }
    }
}
