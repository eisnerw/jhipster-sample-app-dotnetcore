using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace JhipsterSampleApplication.Domain.Entities
{
    [Table("birthday")]
    public class Birthday : BaseEntity<string>
    {
        public string? Lname { get; set; }
        public string? Fname { get; set; }
        public string? Sign { get; set; }
        public DateTime? Dob { get; set; }
        public bool? IsAlive { get; set; }
        public string? Text { get; set; }
        public string? Wikipedia { get; set; }
        public List<string> Categories { get; set; } = new List<string>();

        // jhipster-needle-entity-add-field - JHipster will add fields here, do not remove

        public override bool Equals(object? obj)
        {
            if (this == obj) return true;
            if (obj == null || GetType() != obj.GetType()) return false;
            var birthday = obj as Birthday;
            if (birthday?.Id == null || Id == null) return false;
            return EqualityComparer<string>.Default.Equals(Id, birthday.Id);
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
