using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace JhipsterSampleApplication.Domain.Entities
{
	[Table("supreme")]
	public class Supreme : CategorizedEntity<string>
	{
		public string? Name { get; set; }
		public string? Docket_Number { get; set; }
		public string? Manner_Of_Jurisdiction { get; set; }
		public string? Lower_Court { get; set; }
		public string? Facts_Of_The_Case { get; set; }
		public string? Question { get; set; }
		public string? Conclusion { get; set; }
		public string? Decision { get; set; }
		public string? Description { get; set; }
		public string? Dissent { get; set; }
		public string? Heard_By { get; set; }
		public int? Term { get; set; }
		public string? Justia_Url { get; set; }
		public string? Opinion { get; set; }
		public string? Argument2_Url { get; set; }
		public string? Appellant { get; set; }
		public string? Appellee { get; set; }
		public string? Petitioner { get; set; }
		public string? Respondent { get; set; }
		public List<string>? Recused { get; set; }
		public List<string>? Majority { get; set; }
		public List<string>? Minority { get; set; }
		public List<string>? Advocates { get; set; }

		public override bool Equals(object? obj)
		{
			if (this == obj) return true;
			if (obj == null || GetType() != obj.GetType()) return false;
			var other = obj as Supreme;
			if (other?.Id == null || Id == null) return false;
			return EqualityComparer<string>.Default.Equals(Id, other.Id);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Id);
		}

		public override string ToString()
		{
			return "Supreme{" +
				$"ID='{Id}'" +
				$", Name='{Name}'" +
				$", Term='{Term}'" +
				$", Docket='{Docket_Number}'" +
				"}";
		}
	}
}