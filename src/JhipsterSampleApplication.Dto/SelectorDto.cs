using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JhipsterSampleApplication.Dto
{
    public class SelectorDto
    {
        public long Id { get; set; }

        public string Name { get; set; }
        public string RulesetName { get; set; }
        public string Action { get; set; }
        public string ActionParameter { get; set; }
        public string Description { get; set; }

        // jhipster-needle-dto-add-field - JHipster will add fields here, do not remove
    }
} 