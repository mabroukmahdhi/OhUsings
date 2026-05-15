using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OhUsings.Models;

namespace OhUsings.Services
{
    /// <summary>
    /// Analyzes a Roslyn document to find unresolved type names and their candidate namespaces.
    /// </summary>
    public interface IMissingUsingsAnalyzer
    {
        /// <summary>
        /// Analyzes the document and returns candidates for each missing using directive.
        /// </summary>
        Task<IReadOnlyList<MissingUsingCandidate>> AnalyzeAsync(
            Document document,
            CancellationToken cancellationToken = default);
    }
}
