using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Nest;
using Elasticsearch.Net;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using Newtonsoft.Json.Linq;
using JhipsterSampleApplication.Dto;
using System.Text.RegularExpressions;

namespace JhipsterSampleApplication.Domain.Services
{
	public class SupremeService : EntityService<Supreme>, ISupremeService
	{

		public SupremeService(IElasticClient elasticClient, IBqlService<Supreme> bqlService, IViewService viewService)
		 : base("supreme","justia_url,argument2_url,facts_of_the_case,conclusion", elasticClient, bqlService, viewService)
		{
		}

		private static bool ValidateRuleset(Ruleset rr)
		{
				if (rr.rules == null || rr.rules.Count == 0)
				{
						return !string.IsNullOrWhiteSpace(rr.field);
				}
				return rr.rules.All(ValidateRuleset);
		}

		private static RulesetDto MapToDto(Ruleset rr)
		{
				return new RulesetDto
				{
						field = rr.field,
						@operator = rr.@operator,
						value = rr.value,
						condition = rr.condition,
						@not = rr.@not,
						rules = rr.rules?.Select(MapToDto).ToList() ?? new List<RulesetDto>()
				};
		}
		
	}
}
