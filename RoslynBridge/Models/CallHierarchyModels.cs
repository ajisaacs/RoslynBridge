using System.Collections.Generic;

namespace RoslynBridge.Models
{
    public class CallHierarchyInfo
    {
        public string? SymbolName { get; set; }
        public List<CallInfo>? Calls { get; set; }
    }

    public class CallInfo
    {
        public string? CallerName { get; set; }
        public string? CallerType { get; set; }
        public LocationInfo? Location { get; set; }
    }
}
