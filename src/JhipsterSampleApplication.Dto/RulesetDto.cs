using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JhipsterSampleApplication.Dto
{
    /// <summary>
    /// Data Transfer Object for a ruleset or rule for querying data
    /// </summary>
    public class RulesetDto
    {
        /// <summary>
        /// The field to query on
        /// </summary>
        public string? field { get; set; }

        /// <summary>
        /// The operator to use for comparison (=, !=, contains, etc.)
        /// </summary>
        public string? @operator { get; set; }

        /// <summary>
        /// The value to compare against
        /// </summary>
        [JsonConverter(typeof(AutoValueConverter))]
        public object? value { get; set; }

        /// <summary>
        /// The condition to use when combining multiple rules (and/or)
        /// </summary>
        public string? condition { get; set; }

        /// <summary>
        /// Whether to negate the rule
        /// </summary>
        public bool @not { get; set; }

        /// <summary>
        /// The list of child rules
        /// </summary>
        public List<RulesetDto>? rules { get; set; } = new List<RulesetDto>();

        public string? name { get; set; }

        public override bool Equals(object? obj)
        {
            if (this == obj) return true;
            if (obj == null || GetType() != obj.GetType()) return false;
            var ruleset = obj as RulesetDto;
            if (ruleset == null) return false;

            if (field != ruleset.field) return false;
            if (@operator != ruleset.@operator) return false;
            if (value != null && !value.Equals(ruleset.value)) return false;
            if (condition != ruleset.condition) return false;
            if (@not != ruleset.@not) return false;
            if (rules != null && ruleset.rules != null)
            {
                if (rules.Count != ruleset.rules.Count) return false;
                for (int i = 0; i < rules.Count; i++)
                {
                    if (!rules[i].Equals(ruleset.rules[i])) return false;
                }
            }
            else if (rules != null || ruleset.rules != null)
            {
                return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = new System.HashCode();
            hashCode.Add(field);
            hashCode.Add(@operator);
            hashCode.Add(value);
            hashCode.Add(condition);
            hashCode.Add(@not);
            if (rules != null)
            {
                foreach (var rule in rules)
                {
                    hashCode.Add(rule);
                }
            }
            return hashCode.ToHashCode();
        }

        public override string ToString()
        {
            var result = new List<string>();
            if (field != null)
            {
                result.Add($"field={field}");
            }
            if (@operator != null)
            {
                result.Add($"operator={@operator}");
            }
            if (value != null)
            {
                result.Add($"value={value}");
            }
            if (condition != null)
            {
                result.Add($"condition={condition}");
            }
            if (@not)
            {
                result.Add("not=true");
            }
            if (rules != null)
            {
                result.Add($"rules=[{string.Join(", ", rules)}]");
            }
            return $"RulesetDto{{{string.Join(", ", result)}}}";
        }
    }
    public class AutoValueConverter : JsonConverter<object>
    {
        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Number:
                    if (reader.TryGetInt32(out int intValue))
                    {
                        return intValue;
                    }
                    // You might want to handle other number types (long, double) if needed
                    return reader.GetDouble();
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.StartArray:
                    var stringList = new List<string>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            stringList.Add(reader.GetString());
                        }
                        else if (reader.TokenType == JsonTokenType.Null)
                        {
                            stringList.Add(null); // Or handle nulls differently in the list
                        }
                        else
                        {
                            throw new JsonException($"Unexpected token type within List<string>: {reader.TokenType}.");
                        }
                    }
                    return stringList;
                case JsonTokenType.Null:
                    return null;
                default:
                    throw new JsonException($"Unexpected token type: {reader.TokenType} at {reader.BytesConsumed}");
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            if (value is string stringValue)
            {
                writer.WriteStringValue(stringValue);
            }
            else if (value is int intValue)
            {
                writer.WriteNumberValue(intValue);
            }
            else if (value is bool boolValue)
            {
                writer.WriteBooleanValue(boolValue);
            }
            else if (value is List<string> stringList)
            {
                writer.WriteStartArray();
                foreach (var item in stringList)
                {
                    if (item is string s)
                    {
                        writer.WriteStringValue(s);
                    }
                    else if (item is null)
                    {
                        writer.WriteNullValue();
                    }
                    else
                    {
                        throw new JsonException($"Unexpected type within List<string> for serialization: {item?.GetType()}");
                    }
                }
                writer.WriteEndArray();
            }
            else if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                throw new JsonException($"Unexpected value type: {value?.GetType()} for serialization.");
            }
        }
    }
} 