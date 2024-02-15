using System;
using System.Runtime.InteropServices;

namespace SharpFileDialog
{
    /// <summary>
    /// Prompt the user to select a directory via a modal dialog
    /// </summary>
    public class DirectoryDialog : IDirectoryDialogBackend
    {
        private readonly IDirectoryDialogBackend _backend;

        /// <summary>
        /// Create a new modal directory picker dialog with the given title
        /// </summary>
        /// <param name="title">The title to use (if any)</param>
        public DirectoryDialog(string title = null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _backend = new Win.WinDirectoryDialog(title);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    _backend = new Zenity.ZenityDirectoryDialog(title);
                }
                catch
                {
                    _backend = new Gtk.GtkDirectoryDialog(title);
                }
            }
        }

        /// <summary>
        /// Clean up any resources owned by the dialog
        /// </summary>
        public void Dispose()
        {
            _backend.Dispose();
        }

        /// <summary>
        /// Displays the dialog
        /// </summary>
        /// <param name="callback"></param>
        public void Open(Action<DialogResult> callback)
        {
            _backend.Open(callback);
        }
    }
}
