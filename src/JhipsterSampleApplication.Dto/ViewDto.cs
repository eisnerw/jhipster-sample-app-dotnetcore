using System;
using System.ComponentModel.DataAnnotations;

namespace JhipsterSampleApplication.Dto
{
    public class ViewDto
    {
        public string? Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Field { get; set; }

        public string? Aggregation { get; set; }

        public string? Query { get; set; }

        public string? CategoryQuery { get; set; }

        public string? Script { get; set; }

        public string? PrimaryViewId { get; set; }

        public ViewDto? PrimaryView { get; set; }
    }
} 