using System;
using System.Runtime.InteropServices;

namespace SharpFileDialog
{
    /// <summary>
    /// Prompt the user to select a file to be saved via a modal dialog
    /// </summary>
    public class SaveFileDialog : ISaveFileDialogBackend
    {
        private readonly ISaveFileDialogBackend _backend;

        /// <summary>
        /// The default filename
        /// </summary>
        public string DefaultFileName
        {
            set => _backend.DefaultFileName = value;
        }

        /// <summary>
        /// Create a new modal file picker dialog with the given title
        /// </summary>
        /// <param name="title">The title to use (if any)</param>
        public SaveFileDialog(string title = null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _backend = new Win.WinSaveFileDialog(title);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    _backend = new Zenity.ZenitySaveFileDialog(title);
                }
                catch
                {
                    _backend = new Gtk.GtkSaveFileDialog(title);
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
        public void Save(Action<DialogResult> callback, string filter = "All files(*.*) | *.*")
        {
            _backend.Save(callback, filter);
        }
    }
}
