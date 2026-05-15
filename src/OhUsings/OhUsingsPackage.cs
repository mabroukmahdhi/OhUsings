using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using OhUsings.Commands;
using OhUsings.Services;
using Task = System.Threading.Tasks.Task;

namespace OhUsings
{
    /// <summary>
    /// The OhUsings Visual Studio package. Loads asynchronously and registers the
    /// "Import All Missing Usings" command.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(
        Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.SolutionExists,
        PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class OhUsingsPackage : AsyncPackage
    {
        /// <summary>
        /// Package GUID — must match guidOhUsingsPackage in the .vsct file.
        /// </summary>
        public const string PackageGuidString = "d4e3f2a1-b5c6-4d7e-8f9a-0b1c2d3e4f50";

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            try
            {
                // Resolve VisualStudioWorkspace from MEF
                var componentModel = await GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
                if (componentModel == null)
                {
                    Debug.WriteLine("[OhUsings] Failed to get IComponentModel. Extension will not function.");
                    return;
                }

                var workspace = componentModel.GetService<VisualStudioWorkspace>();
                if (workspace == null)
                {
                    Debug.WriteLine("[OhUsings] Failed to get VisualStudioWorkspace. Extension will not function.");
                    return;
                }

                // Create services
                var activeDocumentService = new ActiveDocumentService(this, workspace);
                var analyzer = new MissingUsingsAnalyzer();
                var applier = new UsingDirectiveApplier();
                var notificationService = new NotificationService(this);

                // Initialize the command
                await ImportAllMissingUsingsCommand.InitializeAsync(
                    this,
                    activeDocumentService,
                    analyzer,
                    applier,
                    notificationService);

                Debug.WriteLine("[OhUsings] Package initialized successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OhUsings] Package initialization failed: {ex}");
            }
        }
    }
}
