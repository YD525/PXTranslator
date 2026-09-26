using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using NIM.ApplicationLayer;

namespace NIM.UIManagement.Preview
{
    /// <summary>
    /// Implements the preview modal boundary with owner-aware keyboard focus restoration.
    /// </summary>
    internal sealed class WpfPreviewDialogService : IPreviewDialogService
    {
        private readonly Func<Window> _getOwner;

        /// <summary>
        /// Creates the WPF modal boundary.
        /// </summary>
        /// <param name="getOwner">Returns the current owner window.</param>
        internal WpfPreviewDialogService(Func<Window> getOwner)
        {
            _getOwner = getOwner ?? throw new ArgumentNullException(nameof(getOwner));
        }

        /// <inheritdoc />
        public bool Show(PreviewDialogRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            Window owner = _getOwner();
            IInputElement previousFocus = Keyboard.FocusedElement;
            var dialog = new PreviewDialogWindow(request);
            if (owner != null)
            {
                dialog.Owner = owner;
            }

            bool result = dialog.ShowDialog() == true;
            RestoreFocus(owner, previousFocus);
            return result;
        }

        private static void RestoreFocus(Window owner, IInputElement previousFocus)
        {
            Dispatcher dispatcher = owner?.Dispatcher ?? Application.Current?.Dispatcher;
            dispatcher?.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() => previousFocus?.Focus()));
        }
    }
}
