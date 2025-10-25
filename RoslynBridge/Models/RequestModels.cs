using System.Collections.Generic;

namespace RoslynBridge.Models
{
    public class QueryRequest
    {
        public string? QueryType { get; set; }
        public string? FilePath { get; set; }
        public string? SymbolName { get; set; }
        public int? Line { get; set; }
        public int? Column { get; set; }
        public Dictionary<string, string>? Parameters { get; set; }

        // Project operation parameters
        public string? ProjectName { get; set; }
        public string? PackageName { get; set; }
        public string? Version { get; set; }
        public string? Configuration { get; set; }
        public string? DirectoryPath { get; set; }
    }

    public class QueryResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public object? Data { get; set; }
        public string? Error { get; set; }
    }
}
