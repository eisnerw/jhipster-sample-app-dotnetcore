using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JhipsterSampleApplication.Domain.Entities
{
    [Table("department")]
    public class Department : BaseEntity<long?>
    {
        [Required]
        public string? DepartmentName { get; set; }
        public long? LocationId { get; set; }
        public Location? Location { get; set; }
        public IList<Employee> Employees { get; set; } = new List<Employee>();

        // jhipster-needle-entity-add-field - JHipster will add fields here, do not remove

        public override bool Equals(object? obj)
        {
            if (this == obj) return true;
            if (obj == null || GetType() != obj.GetType()) return false;
            var department = obj as Department;
            if (department == null || department.Id == null || department.Id == 0 || Id == null ||  Id == 0) return false;
            return EqualityComparer<long>.Equals(Id!, department.Id!);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }

        public override string ToString()
        {
            return "Department{" +
                    $"ID='{Id}'" +
                    $", DepartmentName='{DepartmentName}'" +
                    "}";
        }
    }
}
