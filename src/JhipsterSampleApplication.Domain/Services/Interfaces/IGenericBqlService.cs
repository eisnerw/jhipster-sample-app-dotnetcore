using System.Threading.Tasks;
using JhipsterSampleApplication.Dto;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface IGenericBqlService<TDomain>
    {
        /// <summary>
        /// Converts a BQL query string to a Ruleset
        /// </summary>
        /// <param name="bqlQuery">The BQL query string to convert</param>
        /// <returns>A Ruleset representing the BQL query</returns>
        Task<RulesetOrRuleDto> Bql2Ruleset(string bqlQuery);

        /// <summary>
        /// Converts a Ruleset to a BQL query string
        /// </summary>
        /// <param name="ruleset">The Ruleset to convert</param>
        /// <returns>A BQL query string representing the Ruleset</returns>
        Task<string> Ruleset2Bql(RulesetOrRuleDto ruleset);

        /// <summary>
        /// Converts a Ruleset to an Elasticsearch query
        /// </summary>
        /// <param name="ruleset">The Ruleset to convert</param>
        /// <returns>An Elasticsearch query object</returns>
        Task<object> Ruleset2ElasticSearch(RulesetOrRuleDto ruleset);
    }
} 