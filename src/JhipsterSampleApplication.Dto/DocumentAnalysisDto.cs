using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace JhipsterSampleApplication.Dto
{
    public class DocumentAnalysisDto
    {
        public IList<string> ids { get; set; } = new List<string>();
    }
} 