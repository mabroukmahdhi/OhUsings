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
    /// Lightbulb parent action for an ambiguous type with a specific namespace candidate.
    /// Expands into File / Project / Solution sub-actions.
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
        public bool HasActionSets => true;
        public bool HasPreview => false;

        public Task<IEnumerable<SuggestedActionSet>?> GetActionSetsAsync(CancellationToken cancellationToken)
        {
            var namespaces = new[] { _namespace };
            var subActions = new ISuggestedAction[]
            {
                new ApplyUsingScopeAction(_workspace, _document, namespaces, ApplyUsingScopeAction.ApplyScope.File),
                new ApplyUsingScopeAction(_workspace, _document, namespaces, ApplyUsingScopeAction.ApplyScope.Project),
                new ApplyUsingScopeAction(_workspace, _document, namespaces, ApplyUsingScopeAction.ApplyScope.Solution),
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
