using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using Nest;

namespace JhipsterSampleApplication.Domain.Services
{
    public class MovieService : EntityService<Movie>, IMovieService
    {
        public MovieService(IElasticClient elasticClient, IBqlService<Movie> bqlService, IViewService viewService)
            : base("movies", "synopsis", elasticClient, bqlService, viewService)
        {
        }
    }
}
