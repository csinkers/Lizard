using System;
using System.Runtime.InteropServices;

namespace SharpFileDialog
{
    /// <summary>
    /// Prompt the user to select a file via a modal dialog
    /// </summary>
    public class OpenFileDialog : IOpenFileDialogBackend
    {
        private readonly IOpenFileDialogBackend _backend;

        /// <summary>
        /// Create a new modal file picker dialog with the given title
        /// </summary>
        /// <param name="title">The title to use (if any)</param>
        public OpenFileDialog(string title = null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _backend = new Win.WinOpenFileDialog(title);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    _backend = new Zenity.ZenityOpenFileDialog(title);
                }
                catch
                {
                    _backend = new Gtk.GtkOpenFileDialog(title);
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
        /// <param name="callback">The method to call when the dialog completes</param>
        /// <param name="filter">The filter(s) to use</param>
        public void Open(Action<DialogResult> callback, string filter = "All files(*.*) | *.*")
        {
            _backend.Open(callback, filter);
        }
    }
}
