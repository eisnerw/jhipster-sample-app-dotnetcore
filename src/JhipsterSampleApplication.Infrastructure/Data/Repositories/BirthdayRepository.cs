using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using JHipsterNet.Core.Pagination;
using JHipsterNet.Core.Pagination.Extensions;
using JhipsterSampleApplication.Domain;
using JhipsterSampleApplication.Domain.Repositories.Interfaces;
using JhipsterSampleApplication.Infrastructure.Data.Extensions;
using System;
using Nest;
using JhipsterSampleApplication.Infrastructure.Data;
using System.Linq.Expressions;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Query;
using Newtonsoft.Json.Linq;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;

namespace JhipsterSampleApplication.Infrastructure.Data.Repositories
{
    public class BirthdayRepository : GenericRepository<Birthday, long>, IBirthdayRepository
    {
        private readonly IElasticSearchService _elasticSearchService;
        private readonly IQueryBuilder _queryBuilder;
        private readonly IElasticClient _elasticClient;

        public BirthdayRepository(
            IUnitOfWork context,
            IElasticSearchService elasticSearchService,
            IQueryBuilder queryBuilder,
            IElasticClient elasticClient) : base(context)
        {
            _elasticSearchService = elasticSearchService;
            _queryBuilder = queryBuilder;
            _elasticClient = elasticClient;
        }

        static Dictionary<string, List<string>> refKeys = new Dictionary<string, List<string>>{
            {"Hank Aaron",  new List<string>{"Hank Aaron"}},
             {"Amy Winehouse",  new List<string>{"Amy Winehouse"}},
            {"Oprah Winfrey",  new List<string>{"Oprah Winfrey"}},
            {"Kate Winslet",  new List<string>{"Kate Winslet"}},
            {"Anna Wintour",  new List<string>{"Anna Wintour"}},
            {"Tom Wolfe",  new List<string>{"Tom Wolfe"}},
            {"Paul D Wolfowitz",  new List<string>{"Paul Wolfowitz"}},
            {"Stevie Wonder",  new List<string>{"Stevie Wonder"}},
            {"Tiger Woods",  new List<string>{"Tiger Woods"}},
            {"Bob Woodward",  new List<string>{"Bob Woodward"}},
            {"Joanne Woodward",  new List<string>{"Joanne Woodward"}},
            {"Virginia Woolf",  new List<string>{"Virginia Woolf"}},
            {"Frank Lloyd Wright",  new List<string>{"Frank Lloyd Wright"}},
            {"Andrew Wyeth",  new List<string>{"Andrew Wyeth"}},
            {"William Butler Yeats",  new List<string>{"William Butler Yeats"}},
            {"Francesca Zambello",  new List<string>{"Francesca Zambello"}},
            {"Frank Zappa",  new List<string>{"Frank Zappa"}},
            {"Renee Zellweger",  new List<string>{"Renee Zellweger"}},
            {"Catherine Zeta-Jones",  new List<string>{"Catherine Zeta-Jones"}},
            {" Zhao Ziyang",  new List<string>{"Zhao Ziyang"}},
            {"Pinchas Zukerman",  new List<string>{"Pinchas Zukerman"}}
        };

        public override async Task<Birthday> CreateOrUpdateAsync(Birthday birthday)
        {
            if (string.IsNullOrEmpty(birthday.ElasticId))
            {
                await _elasticSearchService.IndexAsync(birthday);
            }
            else
            {
                await _elasticSearchService.UpdateAsync(birthday.ElasticId, birthday);
            }

            return birthday;
        }

        public override async Task<IPage<Birthday>> GetPageAsync(IPageable pageable)
        {
            var searchRequest = _queryBuilder
                .WithPagination(pageable.PageNumber, pageable.PageSize)
                .Build();

            var response = await _elasticSearchService.SearchAsync(searchRequest);
            
            return new Page<Birthday>(
                response.Documents.ToList(),
                pageable,
                (int)response.Total);
        }

        public async Task<IPage<Birthday>> GetPageFilteredAsync(IPageable pageable, string query)
        {
            var ruleset = JsonConvert.DeserializeObject<RulesetOrRule>(query ?? "{}");
            if (ruleset == null)
            {
                return new Page<Birthday>(new List<Birthday>(), pageable, 0);
            }

            var response = await _elasticSearchService.SearchWithRulesetAsync(ruleset, pageable.PageSize);
            
            return new Page<Birthday>(
                response.Documents.ToList(),
                pageable,
                (int)response.Total);
        }

        public async Task<List<string>> GetUniqueFieldValuesAsync(string field)
        {
            var values = await _elasticSearchService.GetUniqueFieldValuesAsync(field);
            return values.ToList();
        }

        public async Task<Birthday?> GetOneAsync(object id, bool bText)
        {
            if (id == null)
            {
                return null;
            }

            var searchRequest = _queryBuilder
                .WithFilter("_id", id.ToString())
                .Build();

            var response = await _elasticSearchService.SearchAsync(searchRequest);
            var birthday = response.Documents.FirstOrDefault();

            if (birthday != null && bText)
            {
                birthday.Text = await GetOneTextAsync(id);
            }

            return birthday;
        }

        public async Task<string> GetOneTextAsync(object id)
        {
            if (id == null)
            {
                return string.Empty;
            }

            var searchRequest = _queryBuilder
                .WithFilter("_id", id.ToString())
                .Build();

            var response = await _elasticSearchService.SearchAsync(searchRequest);
            var birthday = response.Documents.FirstOrDefault();
            return birthday?.Text ?? string.Empty;
        }

        public async Task<List<Birthday>?> GetReferencesFromAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var searchRequest = _queryBuilder
                .WithFilter("references.from", id)
                .Build();

            var response = await _elasticSearchService.SearchAsync(searchRequest);
            return response.Documents?.ToList();
        }

        public async Task<List<Birthday>> GetReferencesToAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return new List<Birthday>();
            }

            var searchRequest = _queryBuilder
                .WithFilter("references.to", id)
                .Build();

            var response = await _elasticSearchService.SearchAsync(searchRequest);
            return response.Documents?.ToList() ?? new List<Birthday>();
        }

        public override async Task<Birthday?> GetOneAsync(long id)
        {
            return await GetOneAsync(id.ToString(), false);
        }

        public async Task<IReadOnlyCollection<Birthday>> GetAllAsync(ISearchRequest searchRequest)
        {
            var response = await _elasticClient.SearchAsync<Birthday>(searchRequest);
            return response?.Documents ?? Array.Empty<Birthday>();
        }

        public async Task<long> CountAsync(ISearchRequest searchRequest)
        {
            var response = await _elasticClient.SearchAsync<Birthday>(searchRequest);
            return response?.Total ?? 0;
        }
    }
}
