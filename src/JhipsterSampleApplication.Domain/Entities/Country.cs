using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JhipsterSampleApplication.Domain.Entities
{
    [Table("country")]
    public class Country : BaseEntity<long?>
    {
        public string? CountryName { get; set; }
        public long? RegionId { get; set; }
        public Region? Region { get; set; }

        // jhipster-needle-entity-add-field - JHipster will add fields here, do not remove

        public override bool Equals(object? obj)
        {
            if (this == obj) return true;
            if (obj == null || GetType() != obj.GetType()) return false;
            var country = obj as Country;
            if (country == null || country.Id == 0 || Id == null || Id == 0) return false;
            return EqualityComparer<long>.Equals(Id!, country.Id!);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        public override string ToString()
        {
            return "Country{" +
                    $"ID='{Id}'" +
                    $", CountryName='{CountryName}'" +
                    "}";
        }
    }
}
