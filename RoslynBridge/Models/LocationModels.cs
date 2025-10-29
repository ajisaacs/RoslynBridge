#nullable enable

namespace RoslynBridge.Models
{
    public class LocationInfo
    {
        public string? FilePath { get; set; }
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
    }
}
