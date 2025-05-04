using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace JhipsterSampleApplication.Domain.Entities
{
    [Table("category")]
    public class Category : BaseEntity<long>
    {
        [Required]
        [StringLength(100)]
        public required string CategoryName { get; set; }

        public bool? Selected { get; set; }
        public bool? NotCategorized { get; set; }
        
        [StringLength(50)]
        public required string FocusType { get; set; }
        
        [StringLength(100)]
        public required string FocusId { get; set; }
        
        [Column(TypeName = "text")]
        public required string JsonString { get; set; }
        
        [StringLength(500)]
        public required string Description { get; set; }

        [JsonIgnore]
        public IList<Birthday> Birthdays { get; set; } = new List<Birthday>();

        // jhipster-needle-entity-add-field - JHipster will add fields here, do not remove

        public override bool Equals(object? obj)
        {
            if (this == obj) return true;
            if (obj is not Category category) return false;
            if (category.Id == 0 || Id == 0) return false;
            return EqualityComparer<long>.Default.Equals(Id, category.Id);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        public override string ToString()
        {
            return "Category{" +
                    $"ID='{Id}'" +
                    $", CategoryName='{CategoryName}'" +
                    $", Selected='{Selected}'" +
                    $", NotCategorized='{NotCategorized}'" +
                    $", FocusType='{FocusType}'" +
                    $", FocusId='{FocusId}'" +
                    $", JsonString='{JsonString}'" +
                    $", Description='{Description}'" +
                    "}";
        }
    }

    public enum FocusType {
        NONE,
        FOCUS,
        REFERENCESTO,
        REFERENCESFROM
    }
}
