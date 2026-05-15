using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using OhUsings.Models;
using OhUsings.Services;
using Task = System.Threading.Tasks.Task;
using Tasks = System.Threading.Tasks;

namespace OhUsings.Commands
{
    /// <summary>
    /// The scope at which to import missing usings.
    /// </summary>
    internal enum ImportScope
    {
        CurrentFile = 0,
        CurrentProject = 1,
        Solution = 2
    }

    /// <summary>
    /// Command handler for the three OhUsings import commands:
    /// Add in Current File, Add in Current Project, Add in Solution.
    /// Automatically adds all unambiguous usings and silently skips ambiguous ones.
    /// </summary>
    internal sealed class ImportAllMissingUsingsCommand
    {
        public static readonly Guid CommandSet = new Guid("e5f4a3b2-c6d7-4e8f-9a0b-1c2d3e4f5a61");

        public const int ImportCurrentFileCommandId = 0x0100;
        public const int ImportCurrentProjectCommandId = 0x0101;
        public const int ImportSolutionCommandId = 0x0102;

        private readonly AsyncPackage _package;
        private readonly VisualStudioWorkspace _workspace;
        private readonly IActiveDocumentService _activeDocumentService;
        private readonly IMissingUsingsAnalyzer _analyzer;
        private readonly IUsingDirectiveApplier _applier;
        private readonly INotificationService _notificationService;

        private ImportAllMissingUsingsCommand(
            AsyncPackage package,
            OleMenuCommandService commandService,
            VisualStudioWorkspace workspace,
            IActiveDocumentService activeDocumentService,
            IMissingUsingsAnalyzer analyzer,
            IUsingDirectiveApplier applier,
            INotificationService notificationService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _activeDocumentService = activeDocumentService ?? throw new ArgumentNullException(nameof(activeDocumentService));
            _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
            _applier = applier ?? throw new ArgumentNullException(nameof(applier));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));

            RegisterCommand(commandService, ImportCurrentFileCommandId, ImportScope.CurrentFile);
            RegisterCommand(commandService, ImportCurrentProjectCommandId, ImportScope.CurrentProject);
            RegisterCommand(commandService, ImportSolutionCommandId, ImportScope.Solution);
        }

        private void RegisterCommand(OleMenuCommandService commandService, int commandId, ImportScope scope)
        {
            var menuCommandId = new CommandID(CommandSet, commandId);
            var menuItem = new OleMenuCommand(
                (s, e) => ExecuteWithScope(scope), menuCommandId);
            menuItem.BeforeQueryStatus += OnBeforeQueryStatus;
            commandService.AddCommand(menuItem);
        }

        public static ImportAllMissingUsingsCommand? Instance { get; private set; }

        public static async Task InitializeAsync(
            AsyncPackage package,
            VisualStudioWorkspace workspace,
            IActiveDocumentService activeDocumentService,
            IMissingUsingsAnalyzer analyzer,
            IUsingDirectiveApplier applier,
            INotificationService notificationService)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService))
                as OleMenuCommandService;

            if (commandService != null)
            {
                Instance = new ImportAllMissingUsingsCommand(
                    package,
                    commandService,
                    workspace,
                    activeDocumentService,
                    analyzer,
                    applier,
                    notificationService);
            }
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (sender is OleMenuCommand command)
            {
                command.Visible = true;
                command.Enabled = true;
            }
        }

        private void ExecuteWithScope(ImportScope scope)
        {
            _ = _package.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await ExecuteAsync(scope);
                }
                catch (OperationCanceledException)
                {
                    // Silently ignore cancellation
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OhUsings] Unexpected error: {ex}");
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    _notificationService.ShowError($"An unexpected error occurred: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Collects all document file paths for the given scope, then processes each one
        /// by re-fetching the document from the workspace before each analysis + apply cycle.
        /// This ensures we always work with the latest solution snapshot.
        /// </summary>
        private async Task ExecuteAsync(ImportScope scope)
        {
            // Collect document file paths (stable identifiers that survive workspace changes)
            var docPaths = await GetDocumentPathsAsync(scope);

            if (docPaths.Count == 0)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _notificationService.ShowInfo(
                    scope == ImportScope.CurrentFile
                        ? "OhUsings: No active C# document found."
                        : $"OhUsings: No C# documents found in {scope}.");
                return;
            }

            string scopeLabel = scope == ImportScope.CurrentFile ? "file"
                : scope == ImportScope.CurrentProject ? "project" : "solution";

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _notificationService.ShowInfo(
                docPaths.Count == 1
                    ? "OhUsings: Analyzing document..."
                    : $"OhUsings: Analyzing {docPaths.Count} documents in {scopeLabel}...");

            var totalAdded = new List<string>();
            var totalAmbiguous = new List<AmbiguousImport>();
            var totalUnresolved = new List<string>();
            int filesChanged = 0;

            foreach (var filePath in docPaths)
            {
                // Re-fetch the document from the CURRENT workspace snapshot
                var document = FindDocumentByPath(filePath);
                if (document == null)
                    continue;

                var candidates = await _analyzer.AnalyzeAsync(document, CancellationToken.None);
                if (candidates.Count == 0)
                    continue;

                var safeNamespaces = new List<string>();

                foreach (var candidate in candidates)
                {
                    if (candidate.IsUnambiguous)
                    {
                        safeNamespaces.Add(candidate.CandidateNamespaces[0]);
                    }
                    else if (candidate.IsAmbiguous)
                    {
                        totalAmbiguous.Add(new AmbiguousImport(
                            candidate.TypeName, candidate.CandidateNamespaces));
                    }
                    else if (candidate.IsUnresolved)
                    {
                        totalUnresolved.Add(candidate.TypeName);
                    }
                }

                safeNamespaces = safeNamespaces.Distinct(StringComparer.Ordinal).ToList();

                if (safeNamespaces.Count > 0)
                {
                    // Re-fetch again right before applying (another file might have changed the solution)
                    document = FindDocumentByPath(filePath);
                    if (document != null)
                    {
                        await _applier.ApplyAsync(document, safeNamespaces, CancellationToken.None);
                        totalAdded.AddRange(safeNamespaces);
                        filesChanged++;
                    }
                }
            }

            // Deduplicate
            var uniqueAdded = totalAdded.Distinct(StringComparer.Ordinal).ToList();
            var uniqueUnresolved = totalUnresolved.Distinct(StringComparer.Ordinal).ToList();
            var uniqueAmbiguous = totalAmbiguous
                .GroupBy(a => a.TypeName, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();

            var result = new ImportResult(uniqueAdded, uniqueAmbiguous, uniqueUnresolved);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Write summary to Output window
            _notificationService.WriteOutputLine("─────────────────────────────────────");
            _notificationService.WriteOutputLine(
                $"OhUsings: Scanned {docPaths.Count} file(s) in {scopeLabel}.");

            if (uniqueAdded.Count > 0)
            {
                _notificationService.WriteOutputLine(
                    $"  Added {uniqueAdded.Count} using(s) across {filesChanged} file(s):");
                foreach (var ns in uniqueAdded)
                    _notificationService.WriteOutputLine($"    + using {ns};");
            }
            else
            {
                _notificationService.WriteOutputLine("  No unambiguous usings to add.");
            }

            if (uniqueAmbiguous.Count > 0)
            {
                _notificationService.WriteOutputLine(
                    $"  Skipped {uniqueAmbiguous.Count} ambiguous type(s) — resolve manually via lightbulb:");
                foreach (var a in uniqueAmbiguous)
                {
                    _notificationService.WriteOutputLine(
                        $"    ? '{a.TypeName}' could be: {string.Join(", ", a.CandidateNamespaces)}");
                }
            }

            if (uniqueUnresolved.Count > 0)
            {
                _notificationService.WriteOutputLine(
                    $"  Could not resolve: {string.Join(", ", uniqueUnresolved)}");
            }

            // Status bar summary
            string message = result.Message;
            if (filesChanged > 0 && docPaths.Count > 1)
            {
                message += $" ({filesChanged} file(s) changed)";
            }
            _notificationService.ShowInfo(message);
        }

        /// <summary>
        /// Gets file paths for all documents in the requested scope.
        /// </summary>
        private async Tasks::Task<IReadOnlyList<string>> GetDocumentPathsAsync(ImportScope scope)
        {
            IReadOnlyList<Document> docs;
            switch (scope)
            {
                case ImportScope.CurrentProject:
                    docs = await _activeDocumentService.GetCurrentProjectDocumentsAsync();
                    break;
                case ImportScope.Solution:
                    docs = await _activeDocumentService.GetSolutionDocumentsAsync();
                    break;
                default:
                    var activeDoc = await _activeDocumentService.GetActiveDocumentAsync();
                    docs = activeDoc != null ? new[] { activeDoc } : Array.Empty<Document>();
                    break;
            }

            return docs
                .Where(d => d.FilePath != null)
                .Select(d => Path.GetFullPath(d.FilePath!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Finds a document in the current workspace snapshot by its file path.
        /// </summary>
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
    }
}
