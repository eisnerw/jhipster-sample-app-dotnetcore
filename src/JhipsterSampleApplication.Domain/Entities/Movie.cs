using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace JhipsterSampleApplication.Domain.Entities
{
    [Table("movies")]
    public class Movie : CategorizedEntity<string>
    {
        public string? Title { get; set; }
        public int? ReleaseYear { get; set; }
        public List<string>? Genres { get; set; }
        public int? RuntimeMinutes { get; set; }
        public string? Country { get; set; }
        public List<string>? Languages { get; set; }
        public List<string>? Directors { get; set; }
        public List<string>? Producers { get; set; }
        public List<string>? Writers { get; set; }
        public List<string>? Cast { get; set; }
        public long? BudgetUsd { get; set; }
        public long? GrossUsd { get; set; }
        public int? RottenTomatoesScore { get; set; }
        public string? Summary { get; set; }
        public string? Synopsis { get; set; }

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
