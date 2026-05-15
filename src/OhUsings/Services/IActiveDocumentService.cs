using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace OhUsings.Services
{
    /// <summary>
    /// Provides access to C# documents in Visual Studio.
    /// </summary>
    public interface IActiveDocumentService
    {
        /// <summary>
        /// Gets the Roslyn <see cref="Document"/> for the currently active editor document,
        /// or null if no C# document is active.
        /// </summary>
        Task<Document?> GetActiveDocumentAsync();

        /// <summary>
        /// Gets all C# documents in the project that contains the active document.
        /// Returns empty if no C# project is active.
        /// </summary>
        Task<IReadOnlyList<Document>> GetCurrentProjectDocumentsAsync();

        /// <summary>
        /// Gets all C# documents across the entire solution.
        /// </summary>
        Task<IReadOnlyList<Document>> GetSolutionDocumentsAsync();
    }
}
