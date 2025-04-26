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
    public class BirthdayRepository : GenericRepository<Birthday, string>, IBirthdayRepository
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
            if (string.IsNullOrEmpty(birthday.Id))
            {
                var response = await _elasticSearchService.IndexAsync(birthday);
                if (response.IsValid && !string.IsNullOrEmpty(response.Id))
                {
                    birthday.Id = response.Id;
                }
            }
            else
            {
                await _elasticSearchService.UpdateAsync(birthday.Id, birthday);
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
            Birthday? birthday = null!;
            if (id == null)
            {
                return null;
            }

            // Convert the ID to string for Elasticsearch
            string idString = id.ToString() ?? throw new ArgumentException("ID cannot be converted to string", nameof(id));
            
            // Try to get the document directly by ID first
            try
            {
                var response = await _elasticClient.GetAsync<Birthday>(idString);
                if (response.IsValid && response.Source != null)
                {
                    birthday = response.Source;
                    if (bText)
                    {
                        birthday.Text = await GetOneTextAsync(id);
                    }
                    return birthday;
                }
            }
            catch (Exception)
            {
                // If direct retrieval fails, fall back to search
            }
            
            // Fall back to search if direct retrieval fails
            var searchRequest = _queryBuilder
                .WithFilter("_id", idString)
                .Build();

            var searchResponse = await _elasticSearchService.SearchAsync(searchRequest);
            birthday = searchResponse.Documents.FirstOrDefault();

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

            // Convert the ID to string for Elasticsearch
            string idString = id.ToString() ?? throw new ArgumentException("ID cannot be converted to string", nameof(id));
            
            // Try to get the document directly by ID first
            try
            {
                var response = await _elasticClient.GetAsync<Birthday>(idString);
                if (response.IsValid && response.Source != null)
                {
                    return response.Source.Text ?? string.Empty;
                }
            }
            catch (Exception)
            {
                // If direct retrieval fails, fall back to search
            }
            
            // Fall back to search if direct retrieval fails
            var searchRequest = _queryBuilder
                .WithFilter("_id", idString)
                .Build();

            var searchResponse = await _elasticSearchService.SearchAsync(searchRequest);
            var birthday = searchResponse.Documents.FirstOrDefault();
            return birthday?.Text ?? string.Empty;
        }

        public async Task<List<Birthday>?> GetReferencesFromAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            // Ensure the ID is a string for Elasticsearch
            string idString = id;

            var searchRequest = _queryBuilder
                .WithFilter("references.from", idString)
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

            // Ensure the ID is a string for Elasticsearch
            string idString = id;

            var searchRequest = _queryBuilder
                .WithFilter("references.to", idString)
                .Build();

            var response = await _elasticSearchService.SearchAsync(searchRequest);
            return response.Documents?.ToList() ?? new List<Birthday>();
        }

        public override async Task<Birthday?> GetOneAsync(string id)
        {
            return await GetOneAsync(id, false);
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
