using System;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using OhUsings.Models;

namespace OhUsings.Services
{
    /// <summary>
    /// Displays import results via the Visual Studio status bar and output window.
    /// </summary>
    public sealed class NotificationService : INotificationService
    {
        private readonly AsyncPackage _package;

        public NotificationService(AsyncPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        /// <inheritdoc />
        public void ShowResult(ImportResult result)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            SetStatusBarText(result.Message);

            // Write detailed info to Debug output for diagnostics
            Debug.WriteLine($"[OhUsings] {result.Message}");

            if (result.AmbiguousImports.Count > 0)
            {
                foreach (var ambiguous in result.AmbiguousImports)
                {
                    Debug.WriteLine(
                        $"[OhUsings] Ambiguous: '{ambiguous.TypeName}' -> " +
                        string.Join(", ", ambiguous.CandidateNamespaces));
                }
            }

            if (result.UnresolvedNames.Count > 0)
            {
                Debug.WriteLine(
                    $"[OhUsings] Unresolved: {string.Join(", ", result.UnresolvedNames)}");
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
            Debug.WriteLine($"[OhUsings] ERROR: {message}");
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
