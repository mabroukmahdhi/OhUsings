using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using OhUsings.Services;

namespace OhUsings.SuggestedActions
{
    [Export(typeof(ISuggestedActionsSourceProvider))]
    [Name("OhUsings Suggested Actions")]
    [ContentType("CSharp")]
    internal sealed class OhUsingsSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
    {
        [Import]
        internal VisualStudioWorkspace Workspace { get; set; } = null!;

        public ISuggestedActionsSource? CreateSuggestedActionsSource(
            ITextView textView, ITextBuffer textBuffer)
        {
            if (textView == null || textBuffer == null)
                return null;

            return new OhUsingsSuggestedActionsSource(Workspace, textView, textBuffer);
        }
    }

    internal sealed class OhUsingsSuggestedActionsSource : ISuggestedActionsSource
    {
        private readonly VisualStudioWorkspace _workspace;
        private readonly ITextView _textView;
        private readonly ITextBuffer _textBuffer;
        private readonly MissingUsingsAnalyzer _analyzer = new MissingUsingsAnalyzer();

        public OhUsingsSuggestedActionsSource(
            VisualStudioWorkspace workspace,
            ITextView textView,
            ITextBuffer textBuffer)
        {
            _workspace = workspace;
            _textView = textView;
            _textBuffer = textBuffer;
        }

        public event EventHandler<EventArgs>? SuggestedActionsChanged;

        public void Dispose() { }

        public IEnumerable<SuggestedActionSet>? GetSuggestedActions(
            ISuggestedActionCategorySet requestedActionCategories,
            SnapshotSpan range,
            CancellationToken cancellationToken)
        {
            var document = range.Snapshot.GetOpenDocumentInCurrentContextWithChanges();
            if (document == null)
                return null;

            // Run analysis synchronously (VS calls this on the UI thread with a short timeout)
            var candidates = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.Run(
                () => _analyzer.AnalyzeAsync(document, cancellationToken));

            if (candidates == null || candidates.Count == 0)
                return null;

            var actions = new List<ISuggestedAction>();

            // Unambiguous: single "Add all missing usings" action
            var unambiguous = candidates.Where(c => c.IsUnambiguous).ToList();
            if (unambiguous.Count > 0)
            {
                var namespaces = unambiguous
                    .Select(c => c.CandidateNamespaces[0])
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                actions.Add(new AddAllUnambiguousUsingsAction(
                    _workspace, document, namespaces));
            }

            // Ambiguous: one action per candidate namespace per type
            var ambiguous = candidates.Where(c => c.IsAmbiguous).ToList();
            foreach (var candidate in ambiguous)
            {
                foreach (var ns in candidate.CandidateNamespaces)
                {
                    actions.Add(new AddSpecificUsingAction(
                        _workspace, document, candidate.TypeName, ns));
                }
            }

            if (actions.Count == 0)
                return null;

            return new[]
            {
                new SuggestedActionSet(
                    categoryName: PredefinedSuggestedActionCategoryNames.CodeFix,
                    actions: actions,
                    title: "OhUsings",
                    priority: SuggestedActionSetPriority.Medium,
                    applicableToSpan: range)
            };
        }

        public Task<bool> HasSuggestedActionsAsync(
            ISuggestedActionCategorySet requestedActionCategories,
            SnapshotSpan range,
            CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                var document = range.Snapshot.GetOpenDocumentInCurrentContextWithChanges();
                if (document == null)
                    return false;

                var semanticModel = document.GetSemanticModelAsync(cancellationToken)
                    .GetAwaiter().GetResult();
                if (semanticModel == null)
                    return false;

                var diagnostics = semanticModel.GetDiagnostics(cancellationToken: cancellationToken);
                return diagnostics.Any(d => MissingUsingsAnalyzer.IsSupportedDiagnostic(d.Id));
            }, cancellationToken);
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }
    }
}
