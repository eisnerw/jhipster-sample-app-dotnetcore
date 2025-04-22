using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace JhipsterSampleApplication.Domain.Entities
{
    [Table("birthday")]
    public class Birthday : BaseEntity<long>
    {
        public string? ElasticId { get; set;}
        public string? Lname { get; set; }
        public string? Fname { get; set; }
        public string? Sign { get; set; }
        public DateTime? Dob { get; set; }
        public bool? IsAlive { get; set; }
        public string? Text { get; set; }
        public List<Category> Categories { get; set; } = new List<Category>();

        // jhipster-needle-entity-add-field - JHipster will add fields here, do not remove

        public override bool Equals(object? obj)
        {
            if (this == obj) return true;
            if (obj == null || GetType() != obj.GetType()) return false;
            var birthday = obj as Birthday;
            if (birthday?.Id == null || birthday?.Id == 0 || Id == 0) return false;
            if (birthday == null || birthday.Id == 0 ||  Id == 0) return false;
            return EqualityComparer<long?>.Default.Equals(Id!, birthday.Id!);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        public override string ToString()
        {
            return "Birthday{" +
                    $"ID='{Id}'" +
                    $", Lname='{Lname}'" +
                    $", Fname='{Fname}'" +
                    $", Sign='{Sign}'" +
                    $", Dob='{Dob}'" +
                    $", IsAlive='{IsAlive}'" +
                    "}";
        }
    }
}
