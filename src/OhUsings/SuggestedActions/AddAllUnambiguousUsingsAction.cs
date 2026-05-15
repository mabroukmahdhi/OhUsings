using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using OhUsings.Services;

namespace OhUsings.SuggestedActions
{
    /// <summary>
    /// Lightbulb action that adds all unambiguous missing usings to the current file.
    /// </summary>
    internal sealed class AddAllUnambiguousUsingsAction : ISuggestedAction
    {
        private readonly Workspace _workspace;
        private readonly Document _document;
        private readonly IReadOnlyList<string> _namespaces;

        public AddAllUnambiguousUsingsAction(
            Workspace workspace,
            Document document,
            IReadOnlyList<string> namespaces)
        {
            _workspace = workspace;
            _document = document;
            _namespaces = namespaces;
        }

        public string DisplayText =>
            _namespaces.Count == 1
                ? $"Add using {_namespaces[0]}"
                : $"Add all missing usings ({_namespaces.Count} namespaces)";

        public ImageMoniker IconMoniker => KnownMonikers.AddNamespace;
        public string? IconAutomationText => "Add namespace";
        public string? InputGestureText => null;
        public bool HasActionSets => false;
        public bool HasPreview => false;

        public Task<IEnumerable<SuggestedActionSet>?> GetActionSetsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<SuggestedActionSet>?>(null);

        public Task<object?> GetPreviewAsync(CancellationToken cancellationToken)
            => Task.FromResult<object?>(null);

        public void Invoke(CancellationToken cancellationToken)
        {
            var applier = new UsingDirectiveApplier();
            Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                // Re-fetch document from current workspace to avoid stale references
                var currentDoc = FindDocument();
                if (currentDoc != null)
                {
                    await applier.ApplyAsync(currentDoc, _namespaces, cancellationToken);
                }
            });
        }

        private Document? FindDocument()
        {
            if (_document.FilePath == null) return null;
            foreach (var project in _workspace.CurrentSolution.Projects)
            {
                foreach (var doc in project.Documents)
                {
                    if (string.Equals(doc.FilePath, _document.FilePath, StringComparison.OrdinalIgnoreCase))
                        return doc;
                }
            }
            return null;
        }

        public bool TryGetTelemetryId(out Guid telemetryId) { telemetryId = Guid.Empty; return false; }
        public void Dispose() { }
    }
}
