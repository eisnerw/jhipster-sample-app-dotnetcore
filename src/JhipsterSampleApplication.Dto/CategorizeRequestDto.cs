using System.Collections.Generic;

namespace JhipsterSampleApplication.Dto
{
    public class CategorizeRequestDto
    {
        public List<string> Ids { get; set; } = new List<string>();
        public string Category { get; set; } = string.Empty;
        public bool RemoveCategory { get; set; }
    }
}
