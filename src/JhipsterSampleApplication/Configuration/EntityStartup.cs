using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.IO;
using System;
using System.Linq;
using System.Text.Json.Nodes;

#nullable enable
public sealed record HtmlTemplateSpec(string? TitleExpr, string? BodyField, string? EmptyHtml);
public sealed record EntitySpec(
    string Name,
    string Index,
    string? Title,
    string? IdField,
    string[] DescriptiveFields,
    HtmlTemplateSpec? HtmlTemplate);

public interface IEntitySpecRegistry
{
    bool TryGet(string entity, out EntitySpec spec);
}

public sealed class EntitySpecRegistry : IEntitySpecRegistry
{
    private readonly Dictionary<string, EntitySpec> _map;
    public EntitySpecRegistry(IConfiguration cfg)
    {
        // Load JSON files from a folder, e.g. ./EntitySpecs/*.json
        var folder = Path.Combine(AppContext.BaseDirectory, "Resources", "Entities");
        _map = Directory.EnumerateFiles(folder, "*.json")
            .Select(f => (file: f, json: JsonNode.Parse(File.ReadAllText(f))!.AsObject()))
            .Select(t =>
            {
                var o = t.json;
                var name = o["name"]!.GetValue<string>();
                var index = (o["elasticSearchIndex"] ?? o["index"])!.GetValue<string>();
                var title = o["title"]?.GetValue<string>();
                var idField = o["idField"]?.GetValue<string>();
                var details = (o["descriptiveFields"] ?? o["descriptiveFieds"]) is JsonArray arr
                    ? arr.Select(x => x!.GetValue<string>()).ToArray()
                    : Array.Empty<string>();
                HtmlTemplateSpec? html = null;
                if (o["htmlTemplate"] is JsonObject h)
                    html = new HtmlTemplateSpec(
                        h["titleExpr"]?.GetValue<string>(),
                        h["bodyField"]?.GetValue<string>(),
                        h["emptyHtml"]?.GetValue<string>()
                    );
                return new EntitySpec(name, index, title, idField, details, html);
            })
            .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGet(string entity, out EntitySpec spec) => _map.TryGetValue(entity, out spec!);
}
