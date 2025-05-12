using System.Collections.Generic;

namespace JhipsterSampleApplication.Dto
{
    /// <summary>
    /// Data Transfer Object for a ruleset or rule for querying data
    /// </summary>
    public class RulesetDto
    {
        /// <summary>
        /// The field to query on
        /// </summary>
        public string? field { get; set; }

        /// <summary>
        /// The operator to use for comparison (=, !=, contains, etc.)
        /// </summary>
        public string? @operator { get; set; }

        /// <summary>
        /// The value to compare against
        /// </summary>
        public object? value { get; set; }

        /// <summary>
        /// The condition to use when combining multiple rules (and/or)
        /// </summary>
        public string? condition { get; set; }

        /// <summary>
        /// Whether to negate the rule
        /// </summary>
        public bool @not { get; set; }

        /// <summary>
        /// The list of child rules
        /// </summary>
        public List<RulesetDto> rules { get; set; } = new List<RulesetDto>();
    }
} 