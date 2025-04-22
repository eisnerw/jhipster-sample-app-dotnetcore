using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace JhipsterSampleApplication.Domain.Entities
{
    [Table("category")]
    public class Category : BaseEntity<long?>
    {
        [Required]
        public string? CategoryName { get; set; }
        public bool? selected { get; set; }
        public bool? notCategorized { get; set; }
        public FocusType? focusType { get; set; }
        public string? focusId { get; set; }
        public string? jsonString { get; set; }
        public string? description { get; set; }
        [JsonIgnore]
        public IList<Birthday> Birthdays { get; set; } = new List<Birthday>();

        // jhipster-needle-entity-add-field - JHipster will add fields here, do not remove

        public override bool Equals(object? obj)
        {
            if (this == obj) return true;
            if (obj == null || GetType() != obj.GetType()) return false;
            var category = obj as Category;
            if (category ==  null || category.Id == null || category.Id == 0 || Id == 0) return false;
            return EqualityComparer<long?>.Default.Equals(Id!, category.Id!);
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
                    $", selected='{selected}'" +
                    $", notCategorized='{notCategorized}'" +
                    $", focusType='{focusType}'" +
                    $", focusId='{focusId}'" +
                    $", jsonString='{jsonString}'" +
                    $", description='{description}'" +
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
