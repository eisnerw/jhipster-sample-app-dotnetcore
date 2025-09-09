using System.Collections.Generic;
using System.Threading.Tasks;
using JhipsterSampleApplication.Domain.Entities;
using Nest;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface IMovieService : IEntityService<Movie>
    {
    }
}
