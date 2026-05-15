// ----------------------------------------------------------------------
// Copyright (c) 2026 Mabrouk Mahdhi & Wiem Ksaier. All rights reserved.
// ----------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;

namespace OhUsings.Services
{
    /// <summary>
    /// Retrieves the active C# document from Visual Studio using DTE and the Roslyn workspace.
    /// </summary>
    public sealed class ActiveDocumentService : IActiveDocumentService
    {
        private readonly AsyncPackage _package;
        private readonly VisualStudioWorkspace _workspace;

        public ActiveDocumentService(AsyncPackage package, VisualStudioWorkspace workspace)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        /// <inheritdoc />
        public async Task<Microsoft.CodeAnalysis.Document?> GetActiveDocumentAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = await _package.GetServiceAsync(typeof(DTE)) as DTE2;
            if (dte?.ActiveDocument == null)
            {
                return null;
            }

            string activeFilePath = dte.ActiveDocument.FullName;

            if (string.IsNullOrEmpty(activeFilePath))
            {
                return null;
            }

            // Only handle C# files
            if (!activeFilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string normalizedPath = Path.GetFullPath(activeFilePath);

            var document = _workspace.CurrentSolution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d =>
                    d.FilePath != null &&
                    string.Equals(
                        Path.GetFullPath(d.FilePath),
                        normalizedPath,
                        StringComparison.OrdinalIgnoreCase));

            return document;
        }
    }
}
