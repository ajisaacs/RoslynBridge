#nullable enable

namespace RoslynBridge.Models
{
    /// <summary>
    /// Represents a detected code smell
    /// </summary>
    public class CodeSmellInfo
    {
        /// <summary>
        /// Type of code smell (e.g., "LongMethod", "HighComplexity", "TooManyParameters")
        /// </summary>
        public string? SmellType { get; set; }

        /// <summary>
        /// Severity level ("Low", "Medium", "High", "Critical")
        /// </summary>
        public string? Severity { get; set; }

        /// <summary>
        /// Priority score (0-100) - higher is more urgent
        /// </summary>
        public int PriorityScore { get; set; }

        /// <summary>
        /// Full name of the symbol (e.g., "MyNamespace.MyClass.MyMethod")
        /// </summary>
        public string? SymbolName { get; set; }

        /// <summary>
        /// Simple name of the symbol
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Symbol kind (e.g., "Method", "Class", "Property")
        /// </summary>
        public string? SymbolKind { get; set; }

        /// <summary>
        /// Containing project name
        /// </summary>
        public string? ProjectName { get; set; }

        /// <summary>
        /// Location of the code smell
        /// </summary>
        public LocationInfo? Location { get; set; }

        /// <summary>
        /// Actual measured value of the metric
        /// </summary>
        public int ActualValue { get; set; }

        /// <summary>
        /// Threshold value that was exceeded
        /// </summary>
        public int ThresholdValue { get; set; }

        /// <summary>
        /// Human-readable message describing the code smell
        /// </summary>
        public string? Message { get; set; }
    }

    /// <summary>
    /// Configurable thresholds for code smell detection
    /// </summary>
    public class CodeSmellThresholds
    {
        /// <summary>
        /// Maximum acceptable lines of code in a method (default: 50)
        /// </summary>
        public int MethodLength { get; set; } = 50;

        /// <summary>
        /// Maximum acceptable cyclomatic complexity (default: 10)
        /// </summary>
        public int CyclomaticComplexity { get; set; } = 10;

        /// <summary>
        /// Maximum acceptable number of parameters (default: 5)
        /// </summary>
        public int ParameterCount { get; set; } = 5;

        /// <summary>
        /// Maximum acceptable nesting depth (default: 4)
        /// </summary>
        public int NestingDepth { get; set; } = 4;

        /// <summary>
        /// Maximum acceptable number of members in a class (default: 20)
        /// </summary>
        public int ClassMembers { get; set; } = 20;

        /// <summary>
        /// Maximum acceptable lines of code in a class (default: 300)
        /// </summary>
        public int ClassLength { get; set; } = 300;
    }

    /// <summary>
    /// Summary of code smells detected across the codebase
    /// </summary>
    public class CodeSmellSummary
    {
        public int TotalSmells { get; set; }
        public int LongMethods { get; set; }
        public int HighComplexity { get; set; }
        public int TooManyParameters { get; set; }
        public int DeepNesting { get; set; }
        public int LargeClasses { get; set; }
        public int LongClasses { get; set; }
        public CodeSmellThresholds? Thresholds { get; set; }
    }

    /// <summary>
    /// Represents a detected code duplication
    /// </summary>
    public class DuplicateCodeInfo
    {
        /// <summary>
        /// First occurrence of the duplicate
        /// </summary>
        public DuplicateLocation? Original { get; set; }

        /// <summary>
        /// Second occurrence of the duplicate
        /// </summary>
        public DuplicateLocation? Duplicate { get; set; }

        /// <summary>
        /// Similarity percentage (0-100)
        /// </summary>
        public int SimilarityPercent { get; set; }

        /// <summary>
        /// Number of lines in the duplicated block
        /// </summary>
        public int LineCount { get; set; }

        /// <summary>
        /// Number of tokens matched
        /// </summary>
        public int TokenCount { get; set; }

        /// <summary>
        /// Description of the duplication
        /// </summary>
        public string? Message { get; set; }
    }

    /// <summary>
    /// Location information for a duplicate code block
    /// </summary>
    public class DuplicateLocation
    {
        /// <summary>
        /// Project name
        /// </summary>
        public string? ProjectName { get; set; }

        /// <summary>
        /// File path
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Method or class name
        /// </summary>
        public string? SymbolName { get; set; }

        /// <summary>
        /// Symbol kind (Method, Class, etc.)
        /// </summary>
        public string? SymbolKind { get; set; }

        /// <summary>
        /// Starting line number
        /// </summary>
        public int StartLine { get; set; }

        /// <summary>
        /// Ending line number
        /// </summary>
        public int EndLine { get; set; }
    }
}
