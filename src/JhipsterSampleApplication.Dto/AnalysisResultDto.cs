using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using JhipsterSampleApplication.Crosscutting.Enums;

namespace JhipsterSampleApplication.Dto
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
}
