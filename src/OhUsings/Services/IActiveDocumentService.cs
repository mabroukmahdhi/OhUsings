using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace OhUsings.Services
{
    /// <summary>
    /// Provides access to the currently active C# document in Visual Studio.
    /// </summary>
    public interface IActiveDocumentService
    {
        /// <summary>
        /// Gets the Roslyn <see cref="Document"/> for the currently active editor document,
        /// or null if no C# document is active.
        /// </summary>
        Task<Document?> GetActiveDocumentAsync();
    }
}
