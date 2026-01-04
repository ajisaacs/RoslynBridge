#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using RoslynBridge.Models;

namespace RoslynBridge.Services
{
    /// <summary>
    /// Service for workspace-level operations like refreshing documents from disk.
    /// </summary>
    public class WorkspaceService
    {
        private readonly AsyncPackage _package;

        public WorkspaceService(AsyncPackage package)
        {
            _package = package;
        }

        /// <summary>
        /// Refreshes the workspace by reloading all open documents from disk.
        /// </summary>
        public async Task<QueryResponse> RefreshWorkspaceAsync(QueryRequest request)
        {
            try
            {
                await _package.JoinableTaskFactory.SwitchToMainThreadAsync();

                var dte = await _package.GetServiceAsync(typeof(DTE)) as DTE;
                if (dte == null)
                    return ErrorResponse("Could not access Visual Studio automation (DTE)");

                var result = new RefreshResult();
                RefreshOpenDocuments(dte, result);
                RefreshSpecificFile(dte, request.FilePath, result);

                return SuccessResponse(result);
            }
            catch (Exception ex)
            {
                return ErrorResponse($"Error refreshing workspace: {ex.Message}");
            }
        }

        private void RefreshOpenDocuments(DTE dte, RefreshResult result)
        {
            foreach (Document doc in dte.Documents)
            {
                if (!doc.Saved || !File.Exists(doc.FullName))
                    continue;

                TryRefreshDocument(dte, doc.FullName, result, closeFirst: true);
            }
        }

        private void RefreshSpecificFile(DTE dte, string? filePath, RefreshResult result)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            if (!result.RefreshedFiles.Contains(filePath))
                TryRefreshDocument(dte, filePath, result, closeFirst: false);
        }

        private void TryRefreshDocument(DTE dte, string path, RefreshResult result, bool closeFirst)
        {
            try
            {
                if (closeFirst)
                {
                    var doc = dte.Documents.Item(path);
                    doc?.Close(vsSaveChanges.vsSaveChangesNo);
                }
                dte.ItemOperations.OpenFile(path);
                result.RefreshedFiles.Add(path);
            }
            catch (Exception ex)
            {
                result.FailedFiles.Add($"{path}: {ex.Message}");
            }
        }

        private static QueryResponse ErrorResponse(string error) =>
            new QueryResponse { Success = false, Error = error };

        private static QueryResponse SuccessResponse(RefreshResult result) =>
            new QueryResponse
            {
                Success = true,
                Message = $"Refreshed {result.RefreshedFiles.Count} document(s)",
                Data = new
                {
                    RefreshedCount = result.RefreshedFiles.Count,
                    RefreshedFiles = result.RefreshedFiles,
                    FailedCount = result.FailedFiles.Count,
                    FailedFiles = result.FailedFiles
                }
            };

        private class RefreshResult
        {
            public List<string> RefreshedFiles { get; } = new List<string>();
            public List<string> FailedFiles { get; } = new List<string>();
        }
    }
}
