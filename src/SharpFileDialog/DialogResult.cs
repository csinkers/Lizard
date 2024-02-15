namespace SharpFileDialog
{
    /// <summary>
    /// Contains the results from a file dialog
    /// </summary>
    public struct DialogResult
    {
        /// <summary>
        /// The selected filename if the dialog was successful
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// True if the dialog was successful
        /// </summary>
        public bool Success { get; set; }
    }
}
