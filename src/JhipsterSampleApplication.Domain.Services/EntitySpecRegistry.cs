using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    /// <summary>Attempts to get an array node from an entity specification.</summary>
    bool TryGetArray(string entity, string property, out JsonArray value);
}

/// <summary>
/// Concrete implementation of <see cref="IEntitySpecRegistry"/>.  At start up
/// all JSON files under <c>Resources/Entities</c> are loaded and made available
/// via case-insensitive lookup by entity name.
/// </summary>
public sealed class EntitySpecRegistry : IEntitySpecRegistry
{
    private readonly Dictionary<string, JsonObject> _specs;

    public EntitySpecRegistry(IConfiguration cfg)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Resources", "Entities");
        _specs = Directory.EnumerateFiles(folder, "*.json")
            .Select(f => JsonNode.Parse(File.ReadAllText(f))!.AsObject())
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
}
