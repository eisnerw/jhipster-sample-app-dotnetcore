using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

#nullable enable

namespace JhipsterSampleApplication.Domain.Services;

/// <summary>
/// Registry for entity specifications loaded from JSON files.  The registry
/// exposes individual properties from the specification for a given entity
/// without imposing a fixed schema so that new attributes can be added to the
/// JSON files without requiring code changes.
/// </summary>
public interface IEntitySpecRegistry
{
    /// <summary>Attempts to get a string property from an entity specification.</summary>
    bool TryGetString(string entity, string property, out string value);

    /// <summary>Attempts to get an array of strings from an entity specification.</summary>
    bool TryGetStringArray(string entity, string property, out string[] value);

    /// <summary>Attempts to get an object node from an entity specification.</summary>
    bool TryGetObject(string entity, string property, out JsonObject value);

    /// <summary>Attempts to get the full specification object for an entity.</summary>
    bool TryGetSpec(string entity, out JsonObject value);

    /// <summary>Attempts to get an array node from an entity specification.</summary>
    bool TryGetArray(string entity, string property, out JsonArray value);

    /// <summary>Returns all entity names registered.</summary>
    IEnumerable<string> GetEntityNames();
}

/// <summary>
/// Concrete implementation of <see cref="IEntitySpecRegistry"/>.  At start up
/// all JSON files under <c>Resources/Entities</c> are loaded and made available
/// via case-insensitive lookup by entity name.
/// </summary>
public sealed class EntitySpecRegistry : IEntitySpecRegistry
{
    private readonly Dictionary<string, JsonObject> _specs;
    private static readonly Regex TemplateTokenRegex = new(@"\{([^{}:\s]+)(?::[^{}]+)?\}", RegexOptions.Compiled);

    public EntitySpecRegistry(IConfiguration cfg)
    {
        var computedTemplateValues = BuildComputedTemplateValues(cfg);
        var folders = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Entities"),
            Path.Combine(AppContext.BaseDirectory, "Resources", "Entities")
        };

        // Prefer the most recent copy of a spec when files exist in both the
        // working directory and the output (AppContext.BaseDirectory).
        var fileByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder)) continue;
            foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
            {
                var name = Path.GetFileName(file);
                var lastWrite = File.GetLastWriteTimeUtc(file);
                if (!fileByName.TryGetValue(name, out var existing) || lastWrite > File.GetLastWriteTimeUtc(existing))
                {
                    fileByName[name] = file;
                }
            }
        }

        _specs = fileByName.Values
            .Select(f => ResolveComputedTemplates(JsonNode.Parse(File.ReadAllText(f))!.AsObject(), computedTemplateValues))
            .Where(o => o["name"] is JsonValue)
            .ToDictionary(o => o["name"]!.GetValue<string>(), o => o, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGetString(string entity, string property, out string value)
    {
        value = default!;
        if (_specs.TryGetValue(entity, out var obj) &&
            obj[property] is JsonValue val &&
            val.TryGetValue<string>(out var str))
        {
            value = str;
            return true;
        }
        return false;
    }

    public bool TryGetStringArray(string entity, string property, out string[] value)
    {
        value = Array.Empty<string>();
        if (_specs.TryGetValue(entity, out var obj) && obj[property] is JsonArray arr)
        {
            value = arr
                .Where(n => n is JsonValue)
                .Select(n => n!.GetValue<string>())
                .ToArray();
            return true;
        }
        return false;
    }

    public bool TryGetObject(string entity, string property, out JsonObject value)
    {
        value = default!;
        if (_specs.TryGetValue(entity, out var obj) && obj[property] is JsonObject o)
        {
            value = o;
            return true;
        }
        return false;
    }

    public bool TryGetSpec(string entity, out JsonObject value)
    {
        value = default!;
        if (_specs.TryGetValue(entity, out var obj))
        {
            value = obj.DeepClone().AsObject();
            return true;
        }
        return false;
    }

    public bool TryGetArray(string entity, string property, out JsonArray value)
    {
        value = default!;
        if (_specs.TryGetValue(entity, out var obj) && obj[property] is JsonArray a)
        {
            value = a;
            return true;
        }
        return false;
    }

    public IEnumerable<string> GetEntityNames() => _specs.Keys;

    private static Dictionary<string, string> BuildComputedTemplateValues(IConfiguration cfg)
    {
        var environmentName =
            cfg["ASPNETCORE_ENVIRONMENT"]
            ?? cfg["DOTNET_ENVIRONMENT"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? string.Empty;

        var isDevelopment = string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["devprod"] = isDevelopment ? "dev" : "prod"
        };
    }

    private static JsonObject ResolveComputedTemplates(JsonObject spec, IReadOnlyDictionary<string, string> values)
    {
        ResolveComputedTemplatesInNode(spec, values);
        return spec;
    }

    private static void ResolveComputedTemplatesInNode(JsonNode? node, IReadOnlyDictionary<string, string> values)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToList())
                {
                    if (property.Value is JsonValue value && value.TryGetValue<string>(out var str))
                    {
                        obj[property.Key] = ResolveComputedTemplateString(str, values);
                    }
                    else
                    {
                        ResolveComputedTemplatesInNode(property.Value, values);
                    }
                }
                break;
            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is JsonValue value && value.TryGetValue<string>(out var str))
                    {
                        arr[i] = ResolveComputedTemplateString(str, values);
                    }
                    else
                    {
                        ResolveComputedTemplatesInNode(arr[i], values);
                    }
                }
                break;
        }
    }

    private static string ResolveComputedTemplateString(string template, IReadOnlyDictionary<string, string> values)
    {
        return TemplateTokenRegex.Replace(template, match =>
        {
            var token = match.Groups[1].Value;
            return values.TryGetValue(token, out var value) ? value : match.Value;
        });
    }
}
