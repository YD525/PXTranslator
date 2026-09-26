using System;
using System.Globalization;
using NIM.Properties;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Resolves stable preview message identifiers from the compiled source catalogue.
    /// </summary>
    internal static class PreviewMessageCatalog
    {
        /// <summary>
        /// Resolves the English source text for a stable identifier.
        /// </summary>
        /// <param name="id">The semantic message identifier stored in the source catalogue.</param>
        /// <returns>The localized message for the current UI culture.</returns>
        /// <exception cref="ArgumentException"><paramref name="id"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">The catalogue does not contain <paramref name="id"/>.</exception>
        internal static string Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A message identifier is required.", nameof(id));
            }

            string value = Resources.ResourceManager.GetString(id, Resources.Culture);
            if (value == null)
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "The preview message identifier '{0}' is not registered.",
                    id));
            }

            return value;
        }

        /// <summary>
        /// Formats a catalogue message with values using the current UI culture.
        /// </summary>
        /// <param name="id">The semantic message identifier stored in the source catalogue.</param>
        /// <param name="arguments">The values for the documented composite-format placeholders.</param>
        /// <returns>The formatted localized message.</returns>
        /// <exception cref="ArgumentException"><paramref name="id"/> is empty.</exception>
        /// <exception cref="InvalidOperationException">The catalogue does not contain <paramref name="id"/>.</exception>
        /// <exception cref="FormatException">The arguments do not match the catalogue message placeholders.</exception>
        internal static string Format(string id, params object[] arguments)
        {
            return string.Format(
                CultureInfo.CurrentUICulture,
                Get(id),
                arguments ?? new object[0]);
        }
    }
}
