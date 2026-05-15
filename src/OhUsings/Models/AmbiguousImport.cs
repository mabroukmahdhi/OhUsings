// ----------------------------------------------------------------------
// Copyright (c) 2026 Mabrouk Mahdhi & Wiem Ksaier. All rights reserved.
// ----------------------------------------------------------------------

using System.Collections.Generic;

namespace OhUsings.Models
{
    /// <summary>
    /// Represents a type name that resolved to multiple candidate namespaces.
    /// </summary>
    public sealed class AmbiguousImport
    {
        /// <summary>
        /// The unresolved type name.
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// The candidate namespaces that contain a matching type.
        /// </summary>
        public IReadOnlyList<string> CandidateNamespaces { get; }

        public AmbiguousImport(string typeName, IReadOnlyList<string> candidateNamespaces)
        {
            TypeName = typeName;
            CandidateNamespaces = candidateNamespaces;
        }
    }
}
