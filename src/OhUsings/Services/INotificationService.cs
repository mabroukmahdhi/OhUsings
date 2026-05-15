using OhUsings.Models;

namespace OhUsings.Services
{
    /// <summary>
    /// Displays operation results to the user in Visual Studio.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Shows a summary of the import operation result.
        /// </summary>
        void ShowResult(ImportResult result);

        /// <summary>
        /// Shows an informational message.
        /// </summary>
        void ShowInfo(string message);

        /// <summary>
        /// Shows an error message.
        /// </summary>
        void ShowError(string message);
    }
}
