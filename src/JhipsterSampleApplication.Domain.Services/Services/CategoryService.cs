using System;
using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using JhipsterSampleApplication.Dto;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Nest;
using Newtonsoft.Json;

namespace JhipsterSampleApplication.Domain.Services;

public class CategoryService : ICategoryService
{
    private readonly ILogger<CategoryService> _log;
    private readonly IBirthdayService _birthdayService;

    public CategoryService(ILogger<CategoryService> log, IBirthdayService birthdayService)
    {
        _log = log;
        _birthdayService = birthdayService;
    }

    public async Task<Category> Save(Category category)
    {
        _log.LogDebug($"Request to save Category : {category}");
        if (category == null)
        {
            throw new ArgumentNullException(nameof(category));
        }

        // Store category data in Birthday's Text field as JSON
        var birthday = new Birthday
        {
            Id = category.Id.ToString(),
            Text = JsonConvert.SerializeObject(category)
        };
        await _birthdayService.IndexAsync(birthday);
        return category;
    }

    public async Task<IPage<Category>> FindAll(IPageable pageable, string query)
    {
        _log.LogDebug($"Request to get all Categories");
        if (pageable == null)
        {
            throw new ArgumentNullException(nameof(pageable));
        }

        var searchRequest = new SearchRequest
        {
            From = pageable.PageNumber * pageable.PageSize,
            Size = pageable.PageSize,
            Query = new QueryStringQuery { Query = query ?? string.Empty }
        };
        
        var response = await _birthdayService.SearchAsync(searchRequest);
        var categories = response.Documents
            .Where(doc => doc?.Text != null)
            .Select(doc => JsonConvert.DeserializeObject<Category>(doc.Text!))
            .Where(c => c != null)
            .Cast<Category>()
            .ToList();

        return new Page<Category>(categories, pageable, (int)response.Total);
    }

    public async Task<Category?> FindOne(long id)
    {
        _log.LogDebug($"Request to get Category : {id}");
        var searchRequest = new SearchRequest
        {
            Query = new TermQuery { Field = "id", Value = id.ToString() }
        };
        
        var response = await _birthdayService.SearchAsync(searchRequest);
        var birthday = response.Documents.FirstOrDefault();
        
        if (birthday?.Text == null) return null;
        
        return JsonConvert.DeserializeObject<Category>(birthday.Text);
    }

    public async Task Delete(long id)
    {
        _log.LogDebug($"Request to delete Category : {id}");
        await _birthdayService.DeleteAsync(id.ToString());
    }

    public async Task<AnalysisResultDto> Analyze(IList<string> ids)
    {
        _log.LogDebug($"Request to analyze {ids.Count} documents");
        if (ids == null)
        {
            throw new ArgumentNullException(nameof(ids));
        }

        var searchRequest = new SearchRequest
        {
            Query = new TermsQuery { Field = "id", Terms = ids }
        };
        
        var response = await _birthdayService.SearchAsync(searchRequest);
        
        // Process the results and create AnalysisResultDto
        var result = new AnalysisResultDto
        {
            result = $"Analyzed {response.Documents.Count} documents",
            matches = new List<AnalysisMatchDto>()
        };
        
        // Add your analysis logic here
        
        return result;
    }
} 