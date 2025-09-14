using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace JhipsterSampleApplication.Domain.Search
{
    public class SortSpec
    {
        public string Field { get; set; } = "_id";
        public string Order { get; set; } = "asc"; // "asc" or "desc"
        public string? Script { get; set; } // optional painless script for script sort
        public string? ScriptType { get; set; } // e.g., "number", "string"
    }

    public class SearchSpec<T> where T : class
    {
        public int? From { get; set; }
        public int? Size { get; set; }
        public string? Sort { get; set; }
        public List<SortSpec>? Sorts { get; set; }
        public JToken? RawQuery { get; set; }
        public string? Id { get; set; }
        public List<string>? Ids { get; set; }
        public bool IncludeDetails { get; set; }
        public string? PitId { get; set; }
        public List<object>? SearchAfter { get; set; }
    }
}
