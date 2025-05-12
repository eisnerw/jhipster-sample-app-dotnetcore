using System;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Dto;
using Microsoft.Extensions.Logging;

namespace JhipsterSampleApplication.Domain.Services
{
    public abstract class GenericBqlService<TDomain> : IGenericBqlService<TDomain> where TDomain : class
    {
        protected readonly ILogger<GenericBqlService<TDomain>> _logger;

        protected GenericBqlService(ILogger<GenericBqlService<TDomain>> logger)
        {
            _logger = logger;
        }

        public abstract Task<RulesetDto> Bql2Ruleset(string bqlQuery);

        public abstract Task<string> Ruleset2Bql(RulesetDto ruleset);

        public virtual Task<object> Ruleset2ElasticSearch(RulesetDto ruleset)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Validates that the BQL query is well-formed
        /// </summary>
        protected virtual bool ValidateBqlQuery(string bqlQuery)
        {
            if (string.IsNullOrWhiteSpace(bqlQuery))
            {
                _logger.LogWarning("Empty BQL query provided");
                return false;
            }

            // Add common BQL validation here
            // For example, check for basic syntax, required keywords, etc.
            return true;
        }

        /// <summary>
        /// Validates that the Ruleset is well-formed
        /// </summary>
        protected virtual bool ValidateRuleset(RulesetDto ruleset)
        {
            if (ruleset == null)
            {
                _logger.LogWarning("Null Ruleset provided");
                return false;
            }

            // Add common Ruleset validation here
            // For example, check for required fields, valid structure, etc.
            return true;
        }
    }
} 