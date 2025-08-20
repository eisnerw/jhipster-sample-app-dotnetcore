using System.Collections.Generic;

namespace JhipsterSampleApplication.Dto
{
    public class SupremeDto
    {
        public string Id { get; set; } = string.Empty;
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
        public List<string> Categories { get; set; } = new List<string>();
    }
}
