namespace RoslynBridge.Models
{
    public class DiagnosticInfo
    {
        public string? Id { get; set; }
        public string? Severity { get; set; }
        public string? Message { get; set; }
        public LocationInfo? Location { get; set; }
    }

    public class CodeFixInfo
    {
        public string? Title { get; set; }
        public string? DiagnosticId { get; set; }
        public string? Description { get; set; }
    }
}
