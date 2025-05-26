using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JhipsterSampleApplication.Domain.Entities
{
    [Table("selector")]
    public class Selector: BaseEntity<long>
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public new long Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string RulesetName { get; set; } = string.Empty;

        [Required]
        public string Action { get; set; } = string.Empty;

        [Required]
        public string ActionParameter { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        // jhipster-needle-entity-add-field - JHipster will add fields here, do not remove

        public override bool Equals(object? obj)
        {
            if (this == obj) return true;
            if (obj == null || GetType() != obj.GetType()) return false;
            var selector = obj as Selector;
            if (selector?.Id == 0 || Id == 0) return false;
            return EqualityComparer<long>.Default.Equals(Id, selector!.Id);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        public override string ToString()
        {
            return "Selector{" +
                    $"ID='{Id}'" +
                    $", Name='{Name}'" +
                    $", RulesetName='{RulesetName}'" +
                    $", Action='{Action}'" +
                    $", ActionParameter='{ActionParameter}'" +
                    "}";
        }
    }
}
