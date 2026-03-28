using System.Text;
using System.Text.Json;

namespace RoslynBridge.Mcp.Tools;

/// <summary>
/// Shared utility for limiting large JSON array results to prevent token overflow.
/// </summary>
internal static class ResultLimiter
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    /// <summary>
    /// Truncates the 'data' array in a standard API response to the specified limit.
    /// Returns the full result if limit is 0 (unlimited) or the array is within the limit.
    /// If data is not an array, returns the full formatted result unchanged.
    /// </summary>
    public static string LimitArrayResult(JsonDocument doc, int limit)
    {
        if (limit <= 0)
            return JsonSerializer.Serialize(doc.RootElement, SerializerOptions);

        var root = doc.RootElement;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return JsonSerializer.Serialize(doc.RootElement, SerializerOptions);

        var total = data.GetArrayLength();
        if (total <= limit)
            return JsonSerializer.Serialize(doc.RootElement, SerializerOptions);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name == "data")
                {
                    writer.WriteStartArray("data");
                    var i = 0;
                    foreach (var item in data.EnumerateArray())
                    {
                        if (i >= limit) break;
                        item.WriteTo(writer);
                        i++;
                    }
                    writer.WriteEndArray();
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }
            writer.WriteBoolean("_truncated", true);
            writer.WriteNumber("_showing", limit);
            writer.WriteNumber("_total", total);
            writer.WriteString("_message", $"Showing {limit} of {total} results. Use limit=0 for all.");
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
