using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JhipsterSampleApplication.Domain.Interfaces;

namespace JhipsterSampleApplication.Domain.Entities
{
    [Table("view")]
    public class View : IEntity<string>
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = string.Empty;

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("field")]
        public string? Field { get; set; }

        [Column("aggregation")]
        public string? Aggregation { get; set; }

        [Column("query")]
        public string? Query { get; set; }

        [Column("category_query")]
        public string? CategoryQuery { get; set; }

        [Column("script")]
        public string? Script { get; set; }

        [Column("primary_view_id")]
        public string? parentViewId { get; set; }

        [Required]
        [Column("domain")]
        public string Domain { get; set; } = string.Empty;

        public void SetIdFromName()
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw new InvalidOperationException("View name cannot be null or empty");
            }

            if (!string.IsNullOrEmpty(parentViewId))
            {
                Id = $"{parentViewId.ToLowerInvariant()}.{Name.ToLowerInvariant()}";
            }
            else
            {
                Id = Name.ToLowerInvariant();
            }
        }
    }
}