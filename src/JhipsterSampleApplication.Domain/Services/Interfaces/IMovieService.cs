using System.Collections.Generic;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;
using Nest;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface IMovieService : IEntityService<Movie>
    {
        Task<ISearchResponse<Movie>> SearchAsync(ISearchRequest request, bool includeDetails, string? pitId = null);
        Task<ISearchResponse<Movie>> SearchWithRulesetAsync(Ruleset ruleset, int size = 20, int from = 0, IList<ISort>? sort = null, bool includeDetails = false);
    }
}
