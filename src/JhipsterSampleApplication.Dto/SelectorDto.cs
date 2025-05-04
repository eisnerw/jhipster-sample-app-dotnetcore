using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JhipsterSampleApplication.Dto
{
    public class SelectorDto
    {
        public long Id { get; set; }

        public required string Name { get; set; }
        public required string RulesetName { get; set; }
        public required string Action { get; set; }
        public required string ActionParameter { get; set; }
        public required string Description { get; set; }

        // jhipster-needle-dto-add-field - JHipster will add fields here, do not remove
    }
} 