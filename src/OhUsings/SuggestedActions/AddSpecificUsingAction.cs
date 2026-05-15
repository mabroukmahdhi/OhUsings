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
    /// Lightbulb action that adds a specific namespace for an ambiguous type.
    /// Shown once per candidate namespace so the user can pick the right one.
    /// </summary>
    internal sealed class AddSpecificUsingAction : ISuggestedAction
    {
        private readonly Workspace _workspace;
        private readonly Document _document;
        private readonly string _typeName;
        private readonly string _namespace;

        public AddSpecificUsingAction(
            Workspace workspace,
            Document document,
            string typeName,
            string ns)
        {
            _workspace = workspace;
            _document = document;
            _typeName = typeName;
            _namespace = ns;
        }

        public string DisplayText => $"using {_namespace};  (for {_typeName})";

        public ImageMoniker IconMoniker => KnownMonikers.Namespace;
        public string? IconAutomationText => "Namespace";
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
                var currentDoc = FindDocument();
                if (currentDoc != null)
                {
                    await applier.ApplyAsync(
                        currentDoc,
                        new[] { _namespace },
                        cancellationToken);
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
