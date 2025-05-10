using System.Collections.Generic;

namespace JhipsterSampleApplication.Dto
{
    public class RulesetOrRuleDto
    {
        public string? field { get; set; }
        public string? @operator { get; set; }
        public object? value { get; set; }
        public string? condition { get; set; }
        public bool @not { get; set; }
        public List<RulesetOrRuleDto> rules { get; set; } = new List<RulesetOrRuleDto>();
    }
} 