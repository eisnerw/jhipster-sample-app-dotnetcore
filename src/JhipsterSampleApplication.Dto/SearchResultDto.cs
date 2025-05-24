using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JhipsterSampleApplication.Dto
{
    public class SearchResultDto<T>
    {
        public List<T> Hits { get; set; } = new();
        public string hitType { get; set; } = "hit";
        public string? viewName { get; set; }
        public string? viewCategory { get; set; }
    }
}