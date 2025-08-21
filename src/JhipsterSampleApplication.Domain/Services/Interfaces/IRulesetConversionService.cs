using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using JhipsterSampleApplication.Domain.Entities;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface IRulesetConversionService
    {
        Task<JObject> ConvertRulesetToElasticSearch(Ruleset rr, string indexName, Func<string, string>? fieldMapper = null, IEnumerable<string>? documentFields = null);
        Task<List<string>> GetUniqueFieldValuesAsync(string indexName, string field);
    }
}
