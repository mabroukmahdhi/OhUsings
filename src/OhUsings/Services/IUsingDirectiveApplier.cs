using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using OhUsings.Models;

namespace OhUsings.Services
{
    /// <summary>
    /// Applies resolved using directives to a Roslyn document.
    /// </summary>
    public interface IUsingDirectiveApplier
    {
        /// <summary>
        /// Adds the specified namespaces as using directives, formats the document,
        /// and applies the changes to the workspace.
        /// </summary>
        /// <returns>The updated <see cref="Document"/> after changes are applied.</returns>
        Task<Document> ApplyAsync(
            Document document,
            IReadOnlyList<string> namespacesToAdd,
            CancellationToken cancellationToken = default);
    }
}
