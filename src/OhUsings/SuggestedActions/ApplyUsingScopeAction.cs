using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Leaf lightbulb action that applies one or more namespaces at a chosen scope
    /// (current file, current project, or entire solution).
    /// </summary>
    internal sealed class ApplyUsingScopeAction : ISuggestedAction
    {
        private readonly Workspace _workspace;
        private readonly Document _document;
        private readonly IReadOnlyList<string> _namespaces;
        private readonly ApplyScope _scope;

        internal enum ApplyScope { File, Project, Solution }

        public ApplyUsingScopeAction(
            Workspace workspace,
            Document document,
            IReadOnlyList<string> namespaces,
            ApplyScope scope)
        {
            _workspace = workspace;
            _document = document;
            _namespaces = namespaces;
            _scope = scope;
        }

        public string DisplayText
        {
            get
            {
                switch (_scope)
                {
                    case ApplyScope.Project: return "Add in Current Project";
                    case ApplyScope.Solution: return "Add in Solution";
                    default: return "Add in Current File";
                }
            }
        }

        public ImageMoniker IconMoniker
        {
            get
            {
                switch (_scope)
                {
                    case ApplyScope.Project: return KnownMonikers.CSProjectNode;
                    case ApplyScope.Solution: return KnownMonikers.Solution;
                    default: return KnownMonikers.CSFileNode;
                }
            }
        }

        public string? IconAutomationText => null;
        public string? InputGestureText => null;
        public bool HasActionSets => false;
        public bool HasPreview => false;

        public Task<IEnumerable<SuggestedActionSet>?> GetActionSetsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<SuggestedActionSet>?>(null);

        public Task<object?> GetPreviewAsync(CancellationToken cancellationToken)
            => Task.FromResult<object?>(null);

        public void Invoke(CancellationToken cancellationToken)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.Run(
                () => ApplyAsync(cancellationToken));
        }

        private async Task ApplyAsync(CancellationToken cancellationToken)
        {
            var filePaths = GetTargetFilePaths();
            var applier = new UsingDirectiveApplier();

            foreach (var path in filePaths)
            {
                var doc = FindDocumentByPath(path);
                if (doc != null)
                {
                    await applier.ApplyAsync(doc, _namespaces, cancellationToken);
                }
            }
        }

        private IReadOnlyList<string> GetTargetFilePaths()
        {
            if (_document.FilePath == null)
                return Array.Empty<string>();

            switch (_scope)
            {
                case ApplyScope.Project:
                    // All C# documents in the same project as the current document
                    var project = FindProject();
                    if (project == null)
                        return new[] { Path.GetFullPath(_document.FilePath) };

                    return project.Documents
                        .Where(d => d.FilePath != null
                            && d.FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                        .Select(d => Path.GetFullPath(d.FilePath!))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                case ApplyScope.Solution:
                    return _workspace.CurrentSolution.Projects
                        .Where(p => p.Language == LanguageNames.CSharp)
                        .SelectMany(p => p.Documents)
                        .Where(d => d.FilePath != null
                            && d.FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                        .Select(d => Path.GetFullPath(d.FilePath!))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                default: // File
                    return new[] { Path.GetFullPath(_document.FilePath) };
            }
        }

        private Project? FindProject()
        {
            if (_document.FilePath == null) return null;
            return _workspace.CurrentSolution.Projects
                .FirstOrDefault(p => p.Documents.Any(d =>
                    d.FilePath != null &&
                    string.Equals(d.FilePath, _document.FilePath,
                        StringComparison.OrdinalIgnoreCase)));
        }

        private Document? FindDocumentByPath(string filePath)
        {
            return _workspace.CurrentSolution.Projects
                .SelectMany(p => p.Documents)
                .FirstOrDefault(d =>
                    d.FilePath != null &&
                    string.Equals(
                        Path.GetFullPath(d.FilePath),
                        filePath,
                        StringComparison.OrdinalIgnoreCase));
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }

        public void Dispose() { }
    }
}
