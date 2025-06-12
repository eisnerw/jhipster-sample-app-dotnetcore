using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JhipsterSampleApplication.Dto
{
    public class SearchResultDto<T>
    {
        public List<T> Hits { get; set; } = new();
        public string HitType { get; set; } = "hit";
        public string? ViewName { get; set; }
        public string? viewCategory { get; set; }
        public long TotalHits { get; set; }
        public ICollection<object>? searchAfter { get; set; }
        public string? PitId { get; set; }
    }
}