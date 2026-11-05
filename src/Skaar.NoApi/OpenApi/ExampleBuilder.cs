using System.Text.Json.Nodes;
using Microsoft.OpenApi;

namespace Skaar.NoApi.OpenApi;

/// <summary>
/// Builds a plausible value from a schema: its example, default, const or first enum value if present,
/// otherwise a value of the right type. The values are fixed, so the responses are the same every time.
/// </summary>
internal static class ExampleBuilder
{
    // Limits recursive schemas (e.g. a tree node referring to itself).
    private const int MaxDepth = 6;

    public static JsonNode? Build(IOpenApiSchema? schema) => Build(schema, 0);

    private static JsonNode? Build(IOpenApiSchema? schema, int depth)
    {
        if (schema is null || depth > MaxDepth) return null;
        if (schema.Examples is [{ } first, ..]) return first.DeepClone();
#pragma warning disable CS0618 // Example is obsolete, but still used for OpenAPI 3.0 documents.
        if (schema.Example is { } example) return example.DeepClone();
#pragma warning restore CS0618
        if (schema.Default is { } defaultValue) return defaultValue.DeepClone();
        if (schema.Const is { } constant) return JsonValue.Create(constant);
        if (schema.Enum is [{ } enumValue, ..]) return enumValue.DeepClone();
        if (schema.AllOf is { Count: > 0 } allOf) return Merge(allOf.Append(WithoutAllOf(schema)), depth);
        if (schema.OneOf is [var one, ..]) return Build(one, depth + 1);
        if (schema.AnyOf is [var any, ..]) return Build(any, depth + 1);

        var type = (schema.Type ?? Infer(schema)) & ~JsonSchemaType.Null;
        if (type.HasFlag(JsonSchemaType.Object)) return BuildObject(schema, depth);
        if (type.HasFlag(JsonSchemaType.Array)) return BuildArray(schema, depth);
        if (type.HasFlag(JsonSchemaType.String)) return BuildString(schema.Format);
        if (type.HasFlag(JsonSchemaType.Integer)) return JsonValue.Create(0);
        if (type.HasFlag(JsonSchemaType.Number)) return JsonValue.Create(0.0);
        if (type.HasFlag(JsonSchemaType.Boolean)) return JsonValue.Create(true);
        return null;
    }

    private static JsonSchemaType Infer(IOpenApiSchema schema) =>
        schema.Properties is { Count: > 0 } || schema.AdditionalProperties is not null ? JsonSchemaType.Object :
        schema.Items is not null ? JsonSchemaType.Array :
        0;

    private static JsonObject BuildObject(IOpenApiSchema schema, int depth)
    {
        var result = new JsonObject();
        foreach (var (name, property) in schema.Properties ?? new Dictionary<string, IOpenApiSchema>())
        {
            if (property.WriteOnly) continue;
            result[name] = Build(property, depth + 1);
        }

        // A map: one entry.
        if (result.Count == 0 && schema.AdditionalProperties is { } additional)
        {
            result["property"] = Build(additional, depth + 1);
        }

        return result;
    }

    private static JsonArray BuildArray(IOpenApiSchema schema, int depth) =>
        Build(schema.Items, depth + 1) is { } item ? [item] : [];

    /// <summary>The properties of all parts combined (for objects), or the first value (for anything else).</summary>
    private static JsonNode? Merge(IEnumerable<IOpenApiSchema> parts, int depth)
    {
        JsonNode? result = null;
        foreach (var value in parts.Select(p => Build(p, depth + 1)))
        {
            if (result is JsonObject target && value is JsonObject source)
            {
                foreach (var (name, property) in source.ToList())
                {
                    source.Remove(name);
                    target[name] = property;
                }
            }
            else
            {
                result ??= value;
            }
        }

        return result;
    }

    // The schema's own properties next to its allOf.
    private static OpenApiSchema WithoutAllOf(IOpenApiSchema schema) => new()
    {
        Type = schema.Type,
        Properties = schema.Properties is null ? null : new Dictionary<string, IOpenApiSchema>(schema.Properties),
        Items = schema.Items
    };

    private static JsonValue BuildString(string? format) => JsonValue.Create(format switch
    {
        "date-time" => "2026-01-01T12:00:00Z",
        "date" => "2026-01-01",
        "time" => "12:00:00",
        "duration" => "PT1H",
        "uuid" => "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "email" => "user@example.com",
        "uri" or "url" or "iri" => "https://example.com",
        "hostname" => "example.com",
        "ipv4" => "192.0.2.1",
        "ipv6" => "2001:db8::1",
        "byte" => "ZXhhbXBsZQ==",
        "binary" => "",
        "password" => "secret",
        _ => "string"
    });
}
