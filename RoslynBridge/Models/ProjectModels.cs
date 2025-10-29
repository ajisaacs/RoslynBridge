#nullable enable
using System.Collections.Generic;

namespace RoslynBridge.Models
{
    public class ProjectInfo
    {
        public string? Name { get; set; }
        public string? FilePath { get; set; }
        public List<string>? Documents { get; set; }
        public List<string>? References { get; set; }
    }

    public class ProjectSummary
    {
        public string? Name { get; set; }
        public int FileCount { get; set; }
        public List<string>? TopNamespaces { get; set; }
    }

    public class SolutionOverview
    {
        public int ProjectCount { get; set; }
        public int DocumentCount { get; set; }
        public List<string>? TopLevelNamespaces { get; set; }
        public List<ProjectSummary>? Projects { get; set; }
    }
}
