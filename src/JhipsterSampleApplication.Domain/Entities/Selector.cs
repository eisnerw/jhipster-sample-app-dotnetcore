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
        public long Id { get; set; }

        public string Name { get; set; }
        public string RulesetName { get; set; }
        public string Action { get; set; }
        public string ActionParameter { get; set; }
        public string Description { get; set; }

        // jhipster-needle-entity-add-field - JHipster will add fields here, do not remove

        public override bool Equals(object obj)
        {
            if (this == obj) return true;
            if (obj == null || GetType() != obj.GetType()) return false;
            var selector = obj as Selector;
            if (selector?.Id == null || selector?.Id == 0 || Id == 0) return false;
            return EqualityComparer<long>.Default.Equals(Id, selector.Id);
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
