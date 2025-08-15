using System.Collections.Generic;

namespace JhipsterSampleApplication.Dto
{
    public class CategorizeMultipleRequestDto
    {
        public List<string> Rows { get; set; } = new List<string>();
        public List<string> Add { get; set; } = new List<string>();
        public List<string> Remove { get; set; } = new List<string>();
    }
}
