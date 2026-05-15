using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Shell;
using OhUsings.Models;
using OhUsings.Services;
using OhUsings.UI;
using Task = System.Threading.Tasks.Task;

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
    /// </summary>
    internal sealed class ImportAllMissingUsingsCommand
    {
        /// <summary>
        /// Command set GUID — must match guidOhUsingsCmdSet in the .vsct file.
        /// </summary>
        public static readonly Guid CommandSet = new Guid("e5f4a3b2-c6d7-4e8f-9a0b-1c2d3e4f5a61");

        public const int ImportCurrentFileCommandId = 0x0100;
        public const int ImportCurrentProjectCommandId = 0x0101;
        public const int ImportSolutionCommandId = 0x0102;

        private readonly AsyncPackage _package;
        private readonly IActiveDocumentService _activeDocumentService;
        private readonly IMissingUsingsAnalyzer _analyzer;
        private readonly IUsingDirectiveApplier _applier;
        private readonly INotificationService _notificationService;

        private ImportAllMissingUsingsCommand(
            AsyncPackage package,
            OleMenuCommandService commandService,
            IActiveDocumentService activeDocumentService,
            IMissingUsingsAnalyzer analyzer,
            IUsingDirectiveApplier applier,
            INotificationService notificationService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            _activeDocumentService = activeDocumentService ?? throw new ArgumentNullException(nameof(activeDocumentService));
            _analyzer = analyzer ?? throw new ArgumentNullException(nameof(analyzer));
            _applier = applier ?? throw new ArgumentNullException(nameof(applier));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));

            if (commandService == null)
                throw new ArgumentNullException(nameof(commandService));

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

        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static ImportAllMissingUsingsCommand? Instance { get; private set; }

        /// <summary>
        /// Initializes and registers all three commands.
        /// </summary>
        public static async Task InitializeAsync(
            AsyncPackage package,
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
                    // User or system cancellation — do nothing
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OhUsings] Unexpected error: {ex}");

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    _notificationService.ShowError(
                        $"An unexpected error occurred: {ex.Message}");
                }
            });
        }

        private async Task ExecuteAsync(ImportScope scope)
        {
            IReadOnlyList<Document> documents;
            string scopeLabel;

            switch (scope)
            {
                case ImportScope.CurrentProject:
                    documents = await _activeDocumentService.GetCurrentProjectDocumentsAsync();
                    scopeLabel = "project";
                    break;

                case ImportScope.Solution:
                    documents = await _activeDocumentService.GetSolutionDocumentsAsync();
                    scopeLabel = "solution";
                    break;

                default: // CurrentFile
                    var activeDoc = await _activeDocumentService.GetActiveDocumentAsync();
                    documents = activeDoc != null
                        ? new[] { activeDoc }
                        : Array.Empty<Document>();
                    scopeLabel = "file";
                    break;
            }

            if (documents.Count == 0)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _notificationService.ShowInfo(
                    scope == ImportScope.CurrentFile
                        ? "OhUsings: No active C# document found."
                        : $"OhUsings: No C# documents found in {scopeLabel}.");
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _notificationService.ShowInfo(
                documents.Count == 1
                    ? "OhUsings: Analyzing document..."
                    : $"OhUsings: Analyzing {documents.Count} documents in {scopeLabel}...");

            var totalAdded = new List<string>();
            var totalAmbiguous = new List<AmbiguousImport>();
            var totalUnresolved = new List<string>();
            int filesChanged = 0;

            foreach (var document in documents)
            {
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
                            candidate.TypeName,
                            candidate.CandidateNamespaces));
                    }
                    else if (candidate.IsUnresolved)
                    {
                        totalUnresolved.Add(candidate.TypeName);
                    }
                }

                safeNamespaces = safeNamespaces.Distinct(StringComparer.Ordinal).ToList();

                if (safeNamespaces.Count > 0)
                {
                    await _applier.ApplyAsync(document, safeNamespaces, CancellationToken.None);
                    totalAdded.AddRange(safeNamespaces);
                    filesChanged++;
                }
            }

            // Deduplicate totals for the summary
            var uniqueAdded = totalAdded.Distinct(StringComparer.Ordinal).ToList();
            var uniqueUnresolved = totalUnresolved.Distinct(StringComparer.Ordinal).ToList();

            // Deduplicate ambiguous by type name
            var uniqueAmbiguous = totalAmbiguous
                .GroupBy(a => a.TypeName, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();

            // If there are ambiguous imports, show the resolution dialog
            if (uniqueAmbiguous.Count > 0)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var dialog = new AmbiguousUsingsDialog(uniqueAmbiguous, scope);
                dialog.ShowModal();

                if (dialog.Applied)
                {
                    var resolved = dialog.GetResolvedImports();
                    if (resolved.Count > 0)
                    {
                        var namespacesToApply = resolved
                            .Select(r => r.Namespace)
                            .Distinct(StringComparer.Ordinal)
                            .ToList();

                        var applyScope = dialog.SelectedScope;

                        IReadOnlyList<Document> applyDocuments;
                        switch (applyScope)
                        {
                            case ImportScope.CurrentProject:
                                applyDocuments = await _activeDocumentService.GetCurrentProjectDocumentsAsync();
                                break;
                            case ImportScope.Solution:
                                applyDocuments = await _activeDocumentService.GetSolutionDocumentsAsync();
                                break;
                            default:
                                var activeDoc = await _activeDocumentService.GetActiveDocumentAsync();
                                applyDocuments = activeDoc != null
                                    ? new[] { activeDoc }
                                    : Array.Empty<Document>();
                                break;
                        }

                        int resolvedFilesChanged = 0;
                        foreach (var doc in applyDocuments)
                        {
                            await _applier.ApplyAsync(doc, namespacesToApply, CancellationToken.None);
                            resolvedFilesChanged++;
                        }

                        totalAdded.AddRange(namespacesToApply);
                        filesChanged += resolvedFilesChanged;

                        // Recalculate uniqueAdded
                        uniqueAdded = totalAdded.Distinct(StringComparer.Ordinal).ToList();

                        // Remove resolved entries from ambiguous list
                        var resolvedTypes = new HashSet<string>(
                            resolved.Select(r => r.TypeName), StringComparer.Ordinal);
                        uniqueAmbiguous = uniqueAmbiguous
                            .Where(a => !resolvedTypes.Contains(a.TypeName))
                            .ToList();
                    }
                }
            }

            var result = new ImportResult(uniqueAdded, uniqueAmbiguous, uniqueUnresolved);

            // Enhance message for multi-file scopes
            string message = result.Message;
            if (filesChanged > 0 && documents.Count > 1)
            {
                message += $" ({filesChanged} file(s) changed)";
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _notificationService.ShowInfo(message);
        }
    }
}
