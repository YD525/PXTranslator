using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Resolves and validates bounded visual assets beneath a project's directory.
    /// </summary>
    internal static class PreviewAssetContextLoader
    {
        private const long MaximumAssetBytes = 8L * 1024L * 1024L;
        private const long MaximumAssetPixels = 40000000L;
        private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".bmp" };

        /// <summary>
        /// Loads a safe in-memory asset snapshot when the source text names a supported relative image.
        /// </summary>
        /// <param name="projectPath">The private absolute path of the active project.</param>
        /// <param name="sourceText">The untrusted source text that may contain a relative image path.</param>
        /// <param name="cancellationToken">Cancels file and image validation cooperatively.</param>
        /// <returns>The validated snapshot, or <c>null</c> when no supported reachable asset is named.</returns>
        /// <exception cref="InvalidDataException">The named asset exceeds a safety limit or is malformed.</exception>
        internal static PreviewAssetContext Load(
            string projectPath,
            string sourceText,
            CancellationToken cancellationToken)
        {
            string candidate = (sourceText ?? string.Empty).Trim().Trim('"', '\'');
            string extension = Path.GetExtension(candidate);
            if (string.IsNullOrWhiteSpace(candidate) || Path.IsPathRooted(candidate) ||
                !SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }

            string projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath));
            string root = projectDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string assetPath = Path.GetFullPath(Path.Combine(
                root,
                candidate.Replace('/', Path.DirectorySeparatorChar)));
            if (!assetPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(assetPath))
            {
                return null;
            }

            var fileInfo = new FileInfo(assetPath);
            if (fileInfo.Length > MaximumAssetBytes)
            {
                throw new InvalidDataException("The related visual asset exceeds the supported size limit.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            byte[] content = File.ReadAllBytes(assetPath);
            try
            {
                using (var stream = new MemoryStream(content, false))
                using (Image image = Image.FromStream(stream, true, true))
                {
                    if ((long)image.Width * image.Height > MaximumAssetPixels)
                    {
                        throw new InvalidDataException("The related visual asset exceeds the supported pixel limit.");
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    return new PreviewAssetContext(Path.GetFileName(assetPath), content, image.Width, image.Height);
                }
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("The related visual asset is malformed.", exception);
            }
        }
    }
}
