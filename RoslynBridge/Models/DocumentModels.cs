#nullable enable
using System.Collections.Generic;

namespace RoslynBridge.Models
{
    public class DocumentInfo
    {
        public string? FilePath { get; set; }
        public string? Name { get; set; }
        public string? ProjectName { get; set; }
        public List<string>? Usings { get; set; }
        public List<string>? Classes { get; set; }
        public List<string>? Interfaces { get; set; }
        public List<string>? Enums { get; set; }
    }

    public class DocumentChangeInfo
    {
        public string? FilePath { get; set; }
        public List<TextChangeInfo>? Changes { get; set; }
        public string? NewText { get; set; }
    }

    public class TextChangeInfo
    {
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
        public string? OldText { get; set; }
        public string? NewText { get; set; }
    }
}
