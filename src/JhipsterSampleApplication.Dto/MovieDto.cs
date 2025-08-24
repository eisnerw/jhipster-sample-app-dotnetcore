using System.Collections.Generic;

namespace JhipsterSampleApplication.Dto
{
    public class MovieDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Title { get; set; }
        public int? ReleaseYear { get; set; }
        public List<string>? Genres { get; set; }
        public int? RuntimeMinutes { get; set; }
        public string? Country { get; set; }
        public List<string>? Languages { get; set; }
        public double? BudgetUsd { get; set; }
        public double? GrossUsd { get; set; }
        public double? RottenTomatoesScores { get; set; }
        public string? Summary { get; set; }
        public string? Synopsis { get; set; }
        public List<string>? Categories { get; set; }
    }
}
