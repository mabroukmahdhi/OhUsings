namespace OhUsings.Options
{
    /// <summary>
    /// Configuration options for OhUsings. Extensible for future settings page.
    /// </summary>
    public sealed class OhUsingsOptions
    {
        /// <summary>
        /// Whether to sort using directives alphabetically after adding. Default: true.
        /// </summary>
        public bool SortUsings { get; set; } = true;

        /// <summary>
        /// Whether to place System namespaces first. Default: true.
        /// </summary>
        public bool PlaceSystemFirst { get; set; } = true;

        /// <summary>
        /// Maximum number of diagnostics to process per document. Default: 200.
        /// </summary>
        public int MaxDiagnosticsPerDocument { get; set; } = 200;
    }
}
