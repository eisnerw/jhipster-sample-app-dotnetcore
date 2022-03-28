using System.Collections.Generic;
namespace Jhipster.Dto
{
    public class AnalysisResultDto
    {
        public string result { get; set; }
        public List<AnalysisMatchDto> matches  { get; set; } = new List<AnalysisMatchDto>();
    }

    public class AnalysisMatchDto
    {
        public AnalysisMatchType type  { get; set; }
        public string title { get; set; }
        public SelectorDto selector { get; set; }
        public List<string> ids { get; set; } = new List<string>();
    }

    public enum AnalysisMatchType {
        none = 0,
        single,
        multiple
    }
}
