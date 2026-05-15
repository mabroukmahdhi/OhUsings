using System.Collections.Generic;
using System.Linq;

namespace OhUsings.Models
{
    /// <summary>
    /// The result of an "Import All Missing Usings" operation.
    /// </summary>
    public sealed class ImportResult
    {
        /// <summary>
        /// Namespaces that were successfully added.
        /// </summary>
        public IReadOnlyList<string> AddedNamespaces { get; }

        /// <summary>
        /// Types that mapped to multiple candidate namespaces and were skipped.
        /// </summary>
        public IReadOnlyList<AmbiguousImport> AmbiguousImports { get; }

        /// <summary>
        /// Type names that could not be resolved to any namespace.
        /// </summary>
        public IReadOnlyList<string> UnresolvedNames { get; }

        /// <summary>
        /// True if at least one using directive was added.
        /// </summary>
        public bool Changed => AddedNamespaces.Count > 0;

        /// <summary>
        /// A human-readable summary of the operation.
        /// </summary>
        public string Message { get; }

        public ImportResult(
            IReadOnlyList<string> addedNamespaces,
            IReadOnlyList<AmbiguousImport> ambiguousImports,
            IReadOnlyList<string> unresolvedNames)
        {
            AddedNamespaces = addedNamespaces;
            AmbiguousImports = ambiguousImports;
            UnresolvedNames = unresolvedNames;
            Message = BuildMessage();
        }

        private string BuildMessage()
        {
            if (!Changed && AmbiguousImports.Count == 0 && UnresolvedNames.Count == 0)
            {
                return "OhUsings: No missing using directives found.";
            }

            var parts = new List<string>();

            if (AddedNamespaces.Count > 0)
            {
                string namespaces = string.Join(", ", AddedNamespaces.Take(5));
                string suffix = AddedNamespaces.Count > 5 ? ", ..." : "";
                parts.Add($"added {AddedNamespaces.Count} using directive(s): {namespaces}{suffix}");
            }

            if (AmbiguousImports.Count > 0)
            {
                parts.Add($"{AmbiguousImports.Count} name(s) were ambiguous");
            }

            if (UnresolvedNames.Count > 0)
            {
                parts.Add($"{UnresolvedNames.Count} name(s) could not be resolved");
            }

            return "OhUsings " + string.Join(". ", parts) + ".";
        }
    }
}
