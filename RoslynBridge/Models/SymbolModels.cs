using System.Collections.Generic;

namespace RoslynBridge.Models
{
    public class SymbolInfo
    {
        public string? Name { get; set; }
        public string? Kind { get; set; }
        public string? Type { get; set; }
        public string? ContainingType { get; set; }
        public string? ContainingNamespace { get; set; }
        public List<LocationInfo>? Locations { get; set; }
        public string? Documentation { get; set; }
        public List<string>? Modifiers { get; set; }
    }

    public class MemberInfo
    {
        public string? Name { get; set; }
        public string? Kind { get; set; }
        public string? ReturnType { get; set; }
        public string? Signature { get; set; }
        public string? Documentation { get; set; }
        public List<string>? Modifiers { get; set; }
        public string? Accessibility { get; set; }
        public bool IsStatic { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsVirtual { get; set; }
        public bool IsOverride { get; set; }
    }

    public class TypeHierarchyInfo
    {
        public string? TypeName { get; set; }
        public string? FullName { get; set; }
        public List<string>? BaseTypes { get; set; }
        public List<string>? Interfaces { get; set; }
        public List<string>? DerivedTypes { get; set; }
    }

    public class NamespaceTypeInfo
    {
        public string? Name { get; set; }
        public string? Kind { get; set; }
        public string? FullName { get; set; }
        public string? Summary { get; set; }
    }

    public class SymbolContextInfo
    {
        public string? ContainingClass { get; set; }
        public string? ContainingMethod { get; set; }
        public string? ContainingNamespace { get; set; }
        public string? SymbolAtPosition { get; set; }
        public List<string>? LocalVariables { get; set; }
        public List<string>? Parameters { get; set; }
    }
}
