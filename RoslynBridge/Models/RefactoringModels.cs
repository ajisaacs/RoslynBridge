#nullable enable
using System.Collections.Generic;

namespace RoslynBridge.Models
{
    public class RenameResult
    {
        public List<DocumentChangeInfo>? ChangedDocuments { get; set; }
        public int TotalChanges { get; set; }
    }
}
