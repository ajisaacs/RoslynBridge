namespace RoslynBridge.Constants
{
    public static class ServerConstants
    {
        public const int DefaultPort = 59123;
        public const string LocalhostUrl = "http://localhost";
        public const string QueryEndpoint = "/query";
        public const string HealthEndpoint = "/health";
        public const string ContentTypeJson = "application/json";

        public static string GetServerUrl(int port = DefaultPort) => $"{LocalhostUrl}:{port}/";
    }

    public static class QueryTypes
    {
        // Query endpoints
        public const string GetSymbol = "getsymbol";
        public const string GetDocument = "getdocument";
        public const string GetProjects = "getprojects";
        public const string GetDiagnostics = "getdiagnostics";
        public const string FindReferences = "findreferences";
        public const string GetSemanticModel = "getsemanticmodel";
        public const string GetSyntaxTree = "getsyntaxtree";

        // Discovery endpoints
        public const string FindSymbol = "findsymbol";
        public const string GetTypeMembers = "gettypemembers";
        public const string GetTypeHierarchy = "gettypehierarchy";
        public const string FindImplementations = "findimplementations";
        public const string GetNamespaceTypes = "getnamespacetypes";
        public const string GetCallHierarchy = "getcallhierarchy";
        public const string GetSolutionOverview = "getsolutionoverview";
        public const string GetSymbolContext = "getsymbolcontext";
        public const string SearchCode = "searchcode";

        // Editing endpoints
        public const string ApplyCodeFix = "applycodefix";
        public const string FormatDocument = "formatdocument";
        public const string RenameSymbol = "renamesymbol";
        public const string OrganizeUsings = "organizeusings";
        public const string AddMissingUsing = "addmissingusing";

        // Project operation endpoints
        public const string AddNuGetPackage = "addnugetpackage";
        public const string RemoveNuGetPackage = "removenugetpackage";
        public const string BuildProject = "buildproject";
        public const string CleanProject = "cleanproject";
        public const string RestorePackages = "restorepackages";
        public const string CreateDirectory = "createdirectory";

        // Code smell analysis endpoints
        public const string GetCodeSmells = "getcodesmells";
        public const string GetCodeSmellSummary = "getcodesmellsummary";
    }
}
