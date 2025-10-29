using System.Text.Json;

namespace RoslynBridge.WebApi.Utilities;

/// <summary>
/// Utility for projecting response data to include only specified fields
/// </summary>
public static class ResponseProjection
{
    /// <summary>
    /// Projects an object to include only the specified fields
    /// </summary>
    /// <param name="data">The data to project</param>
    /// <param name="fields">Comma-separated list of field names to include</param>
    /// <returns>A projected object containing only the specified fields</returns>
    public static object? ProjectFields(object? data, string? fields)
    {
        if (data == null || string.IsNullOrWhiteSpace(fields))
        {
            return data;
        }

        var fieldList = fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                              .Select(f => f.ToLowerInvariant())
                              .ToHashSet();

        if (fieldList.Count == 0)
        {
            return data;
        }

        // Handle JsonElement arrays
        if (data is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.Array)
            {
                var projectedArray = new List<Dictionary<string, object?>>();

                foreach (var item in jsonElement.EnumerateArray())
                {
                    var projected = ProjectJsonElement(item, fieldList);
                    if (projected != null)
                    {
                        projectedArray.Add(projected);
                    }
                }

                return projectedArray;
            }
            else if (jsonElement.ValueKind == JsonValueKind.Object)
            {
                return ProjectJsonElement(jsonElement, fieldList);
            }
        }

        // Handle List<T> or other enumerable types
        if (data is System.Collections.IEnumerable enumerable and not string)
        {
            var projectedList = new List<Dictionary<string, object?>>();

            foreach (var item in enumerable)
            {
                if (item is JsonElement itemElement)
                {
                    var projected = ProjectJsonElement(itemElement, fieldList);
                    if (projected != null)
                    {
                        projectedList.Add(projected);
                    }
                }
                else
                {
                    var projected = ProjectObject(item, fieldList);
                    if (projected != null)
                    {
                        projectedList.Add(projected);
                    }
                }
            }

            return projectedList;
        }

        // Handle regular objects
        return ProjectObject(data, fieldList);
    }

    private static Dictionary<string, object?>? ProjectJsonElement(JsonElement element, HashSet<string> fieldList)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var projected = new Dictionary<string, object?>();

        foreach (var property in element.EnumerateObject())
        {
            if (fieldList.Contains(property.Name.ToLowerInvariant()))
            {
                projected[property.Name] = JsonElementToObject(property.Value);
            }
        }

        return projected.Count > 0 ? projected : null;
    }

    private static Dictionary<string, object?>? ProjectObject(object obj, HashSet<string> fieldList)
    {
        var projected = new Dictionary<string, object?>();
        var properties = obj.GetType().GetProperties();

        foreach (var prop in properties)
        {
            if (fieldList.Contains(prop.Name.ToLowerInvariant()))
            {
                projected[prop.Name] = prop.GetValue(obj);
            }
        }

        return projected.Count > 0 ? projected : null;
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
            _ => null
        };
    }
}
