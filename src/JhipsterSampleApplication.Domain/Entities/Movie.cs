using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Nest;

namespace JhipsterSampleApplication.Domain.Entities
{
    [Table("movies")]
    public class Movie : BaseEntity<string>
    {
        public string? Title { get; set; }
        [PropertyName("release_year")] public int? ReleaseYear { get; set; }
        public List<string>? Genres { get; set; }
        [PropertyName("runtime_minutes")] public int? RuntimeMinutes { get; set; }
        public string? Country { get; set; }
        public List<string>? Languages { get; set; }
        [PropertyName("budget_usd")] public double? BudgetUsd { get; set; }
        [PropertyName("gross_usd")] public double? GrossUsd { get; set; }
        [PropertyName("rotten_tomatoes_scores")] public double? RottenTomatoesScores { get; set; }
        public string? Summary { get; set; }
        public string? Synopsis { get; set; }
        [PropertyName("categories")] public List<string> Categories { get; set; } = new List<string>();

        public override bool Equals(object? obj)
        {
            if (this == obj) return true;
            if (obj == null || GetType() != obj.GetType()) return false;
            var other = obj as Movie;
            if (other?.Id == null || Id == null) return false;
            return EqualityComparer<string>.Default.Equals(Id, other.Id);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        public override string ToString()
        {
            return "Movie{" +
                    $"ID='{Id}'" +
                    $", Title='{Title}'" +
                    $", ReleaseYear='{ReleaseYear}'" +
                    "}";
        }
    }
}
