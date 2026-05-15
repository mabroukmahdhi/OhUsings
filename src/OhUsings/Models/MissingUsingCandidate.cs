using System.Collections.Generic;

namespace OhUsings.Models
{
    /// <summary>
    /// Represents a missing type name and its candidate namespace(s).
    /// </summary>
    public sealed class MissingUsingCandidate
    {
        /// <summary>
        /// The unresolved type name found in the document.
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// Candidate namespaces that contain a type with this name.
        /// </summary>
        public IReadOnlyList<string> CandidateNamespaces { get; }

        /// <summary>
        /// True if exactly one candidate namespace exists (safe to auto-import).
        /// </summary>
        public bool IsUnambiguous => CandidateNamespaces.Count == 1;

        /// <summary>
        /// True if multiple candidate namespaces exist.
        /// </summary>
        public bool IsAmbiguous => CandidateNamespaces.Count > 1;

        /// <summary>
        /// True if no candidate namespace was found.
        /// </summary>
        public bool IsUnresolved => CandidateNamespaces.Count == 0;

        public MissingUsingCandidate(string typeName, IReadOnlyList<string> candidateNamespaces)
        {
            TypeName = typeName;
            CandidateNamespaces = candidateNamespaces;
        }
    }
}
