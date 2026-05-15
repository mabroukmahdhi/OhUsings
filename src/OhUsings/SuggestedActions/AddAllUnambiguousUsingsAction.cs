using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;

namespace OhUsings.SuggestedActions
{
    /// <summary>
    /// Lightbulb parent action for all unambiguous missing usings.
    /// Expands into File / Project / Solution sub-actions.
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
                ? $"using {_namespaces[0]};"
                : $"Add all missing usings ({_namespaces.Count} namespaces)";

        public ImageMoniker IconMoniker => KnownMonikers.AddNamespace;
        public string? IconAutomationText => "Add namespace";
        public string? InputGestureText => null;
        public bool HasActionSets => true;
        public bool HasPreview => false;

        public Task<IEnumerable<SuggestedActionSet>?> GetActionSetsAsync(CancellationToken cancellationToken)
        {
            var subActions = new ISuggestedAction[]
            {
                new ApplyUsingScopeAction(_workspace, _document, _namespaces, ApplyUsingScopeAction.ApplyScope.File),
                new ApplyUsingScopeAction(_workspace, _document, _namespaces, ApplyUsingScopeAction.ApplyScope.Project),
                new ApplyUsingScopeAction(_workspace, _document, _namespaces, ApplyUsingScopeAction.ApplyScope.Solution),
            };

            var result = new[] { new SuggestedActionSet(subActions) };
            return Task.FromResult<IEnumerable<SuggestedActionSet>?>(result);
        }

        public Task<object?> GetPreviewAsync(CancellationToken cancellationToken)
            => Task.FromResult<object?>(null);

        public void Invoke(CancellationToken cancellationToken) { /* sub-actions handle invocation */ }

        public bool TryGetTelemetryId(out Guid telemetryId) { telemetryId = Guid.Empty; return false; }
        public void Dispose() { }
    }
}
