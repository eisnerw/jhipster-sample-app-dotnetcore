using System.Threading.Tasks;
using JHipsterNet.Core.Pagination;
using JhipsterSampleApplication.Domain.Entities;
using System.Collections.Generic;
using JhipsterSampleApplication.Dto;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface ICategoryService
    {
        Task<Category> Save(Category category);

        Task<IPage<Category>> FindAll(IPageable pageable, string query);

        Task<Category?> FindOne(long id);

        Task Delete(long id);

        Task<AnalysisResultDto> Analyze(IList<string> ids);
    }
} 