using System;
using System.Windows.Input;

namespace NIM.ApplicationLayer
{
    /// <summary>
    /// Adapts a synchronous shell action to the WPF command contract.
    /// </summary>
    internal sealed class PreviewShellCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        /// <summary>
        /// Creates a command with an optional availability predicate.
        /// </summary>
        /// <param name="execute">The action invoked with the command parameter.</param>
        /// <param name="canExecute">The optional predicate that controls command availability.</param>
        /// <exception cref="ArgumentNullException"><paramref name="execute"/> is <see langword="null"/>.</exception>
        internal PreviewShellCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <inheritdoc />
        public event EventHandler CanExecuteChanged;

        /// <inheritdoc />
        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        /// <inheritdoc />
        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        /// <summary>
        /// Notifies WPF that command availability may have changed.
        /// </summary>
        internal void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
