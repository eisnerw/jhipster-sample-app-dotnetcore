using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JhipsterSampleApplication.Domain.Entities
{
    [Table("history")]
    public class History : BaseEntity<long>
    {
        [StringLength(50)]
        public string? User { get; set; }

        [Required]
        [StringLength(50)]
        public string Domain { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "text")]
        public string Text { get; set; } = string.Empty;

        public override bool Equals(object? obj)
        {
            if (this == obj) return true;
            if (obj == null || GetType() != obj.GetType()) return false;
            var history = obj as History;
            if (history?.Id == 0 || Id == 0) return false;
            return EqualityComparer<long>.Default.Equals(Id, history!.Id);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        public override string ToString()
        {
            return "History{" +
                   $"ID='{Id}'" +
                   $", User='{User}'" +
                   $", Domain='{Domain}'" +
                   $", Text='{Text}'" +
                   "}";
        }
    }
}
