using System;
using System.Collections.Generic;
using System.Linq;

namespace JhipsterSampleApplication.Domain.Search
{
    public class AppHit<T>
    {
        public string Id { get; set; } = string.Empty;
        public T Source { get; set; } = default!;
        public List<object> Sorts { get; set; } = new List<object>();
    }

    public class AppSearchResponse<T>
    {
        public List<AppHit<T>> Hits { get; set; } = new List<AppHit<T>>();
        public IReadOnlyCollection<T> Documents => Hits.Select(h => h.Source).ToList();
        public long Total { get; set; }
        public string? PointInTimeId { get; set; }
        public bool IsValid { get; set; } = true;
        public string? Error { get; set; }
    }

    public class WriteResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}

