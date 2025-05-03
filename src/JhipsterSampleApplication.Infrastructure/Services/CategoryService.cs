using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Nest;

namespace JhipsterSampleApplication.Infrastructure.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly ILogger<CategoryService> _log;
        private readonly IElasticSearchService _elasticSearchService;
        private readonly IQueryBuilder _queryBuilder;

        public CategoryService(ILogger<CategoryService> log, IElasticSearchService elasticSearchService, IQueryBuilder queryBuilder)
        {
            _log = log;
            _elasticSearchService = elasticSearchService;
            _queryBuilder = queryBuilder;
        }

        public async Task<Category> Save(Category category)
        {
            _log.LogDebug($"Request to save Category : {category}");
            // Convert Category to Birthday for ElasticSearch
            // TODO: determine how to save

            return category;
        }

        public async Task<IPage<Category>> FindAll(IPageable pageable, string query)
        {
            _log.LogDebug($"Request to get all Categories");
            // TODO: fingure out how to get categories
            var searchRequest = _queryBuilder
                .WithFilter("_id", "")
                .Build();            
            var response = await _elasticSearchService.SearchAsync(searchRequest);
            List<Category> categories = null;
            return new Page<Category>(categories, pageable, (int)response.Total);
        }

        public async Task<Category> FindOne(long id)
        {
            _log.LogDebug($"Request to get Category : {id}");
            // TODO: What does FindOne mean for Category            
            return null;
        }

        public async Task Delete(long id)
        {
            _log.LogDebug($"Request to delete Category : {id}");
            await _elasticSearchService.DeleteAsync(id.ToString());
        }

        public async Task<AnalysisResultDto> Analyze(IList<string> ids)
        {
            _log.LogDebug($"Request to analyze {ids.Count} documents");
            // TODO: perform analysis            
            // Process the results and create AnalysisResultDto
            var searchRequest = _queryBuilder
                .WithFilter("_id", "")
                .Build();            
            var response = await _elasticSearchService.SearchAsync(searchRequest);
            var result = new AnalysisResultDto
            {
                result = $"Analyzed {response.Documents.Count} documents",
                matches = new List<AnalysisMatchDto>()
            };
            
            // Add your analysis logic here
            
            return result;
        }
    }
} 