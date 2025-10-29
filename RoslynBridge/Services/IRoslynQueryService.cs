#nullable enable
using System.Threading.Tasks;
using RoslynBridge.Models;

namespace RoslynBridge.Services
{
    public interface IRoslynQueryService
    {
        Task InitializeAsync();
        Task<QueryResponse> ExecuteQueryAsync(QueryRequest request);
    }
}
