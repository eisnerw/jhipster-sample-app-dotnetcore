using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JhipsterSampleApplication.Domain.Entities
{
    [Table("named_query")]
    public class NamedQuery : BaseEntity<long>
    {
        [Required]
        [StringLength(100)]
        public required string Name { get; set; }

        [Required]
        [Column(TypeName = "text")]
        public required string Text { get; set; }

        [Required]
        [StringLength(50)]
        public string? Owner { get; set; }

        [Required]
        [StringLength(50)]
        public required string Entity { get; set; }

        public bool? IsSystem { get; set; }

        public override bool Equals(object? obj)
        {
            if (this == obj) return true;
            if (obj == null || GetType() != obj.GetType()) return false;
            var namedQuery = obj as NamedQuery;
            if (namedQuery?.Id == 0 || Id == 0) return false;
            return EqualityComparer<long>.Default.Equals(Id, namedQuery!.Id);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        public override string ToString()
        {
            return "NamedQuery{" +
                    $"ID='{Id}'" +
                    $", Name='{Name}'" +
                    $", Text='{Text}'" +
                    $", Owner='{Owner}'" +
                    $", Entity='{Entity}'" +
                    "}";
        }
    }
}