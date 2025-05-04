using System.Threading.Tasks;
using System.Collections.Generic;
using Nest;
using JhipsterSampleApplication.Domain.Entities;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface IBirthdayService : IGenericElasticSearchService<Birthday>
    {
    }
} 