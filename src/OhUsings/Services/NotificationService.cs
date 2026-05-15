using System;
using System.Diagnostics;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using OhUsings.Models;

namespace OhUsings.Services
{
    /// <summary>
    /// Displays import results via the Visual Studio status bar and Output window.
    /// </summary>
    public sealed class NotificationService : INotificationService
    {
        private static readonly Guid OutputPaneGuid = new Guid("f3a7b8c9-d0e1-4f2a-b3c4-d5e6f7a8b9c0");
        private readonly AsyncPackage _package;
        private IVsOutputWindowPane? _outputPane;

        public NotificationService(AsyncPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        /// <inheritdoc />
        public void ShowResult(ImportResult result)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SetStatusBarText(result.Message);
            WriteOutputLine(result.Message);

            if (result.AmbiguousImports.Count > 0)
            {
                foreach (var ambiguous in result.AmbiguousImports)
                {
                    WriteOutputLine(
                        $"  Ambiguous: '{ambiguous.TypeName}' -> " +
                        string.Join(", ", ambiguous.CandidateNamespaces));
                }
            }

            if (result.UnresolvedNames.Count > 0)
            {
                WriteOutputLine(
                    $"  Unresolved: {string.Join(", ", result.UnresolvedNames)}");
            }
        }

        /// <inheritdoc />
        public void ShowInfo(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SetStatusBarText(message);
            Debug.WriteLine($"[OhUsings] {message}");
        }

        /// <inheritdoc />
        public void ShowError(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SetStatusBarText($"OhUsings Error: {message}");
            WriteOutputLine($"ERROR: {message}");
        }

        /// <inheritdoc />
        public void WriteOutputLine(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var pane = GetOrCreateOutputPane();
                if (pane != null)
                {
                    pane.OutputStringThreadSafe(message + Environment.NewLine);
                    pane.Activate();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OhUsings] Failed to write to Output pane: {ex.Message}");
            }
        }

        private IVsOutputWindowPane? GetOrCreateOutputPane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_outputPane != null)
                return _outputPane;

            var outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow == null)
                return null;

            var paneGuid = OutputPaneGuid;
            int hr = outputWindow.GetPane(ref paneGuid, out _outputPane);
            if (ErrorHandler.Failed(hr) || _outputPane == null)
            {
                outputWindow.CreatePane(ref paneGuid, "OhUsings", 1, 1);
                outputWindow.GetPane(ref paneGuid, out _outputPane);
            }

            return _outputPane;
        }

        private void SetStatusBarText(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var statusBar = Package.GetGlobalService(typeof(SVsStatusbar)) as IVsStatusbar;
                if (statusBar != null)
                {
                    int frozen;
                    statusBar.IsFrozen(out frozen);
                    if (frozen == 0)
                    {
                        statusBar.SetText(text);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OhUsings] Failed to set status bar text: {ex.Message}");
            }
        }
    }
}
