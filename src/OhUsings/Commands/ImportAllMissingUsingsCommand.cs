using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using OhUsings.Models;
using OhUsings.Services;
using Task = System.Threading.Tasks.Task;

namespace OhUsings.Commands
{
    /// <summary>
    /// Command handler for "OhUsings: Import All Missing Usings".
    /// Registered in both the Tools menu and the C# editor context menu.
    /// </summary>
    internal sealed class ImportAllMissingUsingsCommand
    {
        /// <summary>
        /// Command set GUID — must match the guidOhUsingsCmdSet in the .vsct file.
        /// </summary>
        public static readonly Guid CommandSet = new Guid("e5f4a3b2-c6d7-4e8f-9a0b-1c2d3e4f5a61");

        /// <summary>
        /// Tools menu command ID — must match ImportAllMissingUsingsCommandId in .vsct.
        /// </summary>
        public const int ToolsMenuCommandId = 0x0100;

        /// <summary>
        /// Context menu command ID — must match ImportAllMissingUsingsContextCommandId in .vsct.
        /// </summary>
        public const int ContextMenuCommandId = 0x0101;

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

            // Register the tools menu command
            var toolsMenuId = new CommandID(CommandSet, ToolsMenuCommandId);
            var toolsMenuItem = new OleMenuCommand(Execute, toolsMenuId);
            toolsMenuItem.BeforeQueryStatus += OnBeforeQueryStatus;
            commandService.AddCommand(toolsMenuItem);

            // Register the context menu command
            var contextMenuId = new CommandID(CommandSet, ContextMenuCommandId);
            var contextMenuItem = new OleMenuCommand(Execute, contextMenuId);
            contextMenuItem.BeforeQueryStatus += OnBeforeQueryStatus;
            commandService.AddCommand(contextMenuItem);
        }

        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static ImportAllMissingUsingsCommand? Instance { get; private set; }

        /// <summary>
        /// Initializes the command and registers it with the command service.
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

        /// <summary>
        /// Controls command visibility — only enabled when a C# document is active.
        /// </summary>
        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (sender is OleMenuCommand command)
            {
                // Enable by default; the Execute handler will validate the document
                command.Visible = true;
                command.Enabled = true;
            }
        }

        /// <summary>
        /// Executes the command — the main entry point for the Import All Missing Usings operation.
        /// </summary>
        private void Execute(object sender, EventArgs e)
        {
            // Fire-and-forget with proper exception handling
            _ = _package.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await ExecuteAsync();
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

        private async Task ExecuteAsync()
        {
            // Step 1: Get the active C# document
            var document = await _activeDocumentService.GetActiveDocumentAsync();
            if (document == null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _notificationService.ShowInfo("OhUsings: No active C# document found.");
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _notificationService.ShowInfo("OhUsings: Analyzing document...");

            // Step 2: Analyze the document for missing usings
            var candidates = await _analyzer.AnalyzeAsync(document, CancellationToken.None);

            if (candidates.Count == 0)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _notificationService.ShowInfo("OhUsings: No missing using directives found.");
                return;
            }

            // Step 3: Categorize results
            var safeNamespaces = new List<string>();
            var ambiguousImports = new List<AmbiguousImport>();
            var unresolvedNames = new List<string>();

            foreach (var candidate in candidates)
            {
                if (candidate.IsUnambiguous)
                {
                    safeNamespaces.Add(candidate.CandidateNamespaces[0]);
                }
                else if (candidate.IsAmbiguous)
                {
                    ambiguousImports.Add(new AmbiguousImport(
                        candidate.TypeName,
                        candidate.CandidateNamespaces));
                }
                else if (candidate.IsUnresolved)
                {
                    unresolvedNames.Add(candidate.TypeName);
                }
            }

            // Deduplicate safe namespaces
            safeNamespaces = safeNamespaces.Distinct(StringComparer.Ordinal).ToList();

            // Step 4: Apply safe usings
            if (safeNamespaces.Count > 0)
            {
                await _applier.ApplyAsync(document, safeNamespaces, CancellationToken.None);
            }

            // Step 5: Show result
            var result = new ImportResult(safeNamespaces, ambiguousImports, unresolvedNames);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _notificationService.ShowResult(result);
        }
    }
}
