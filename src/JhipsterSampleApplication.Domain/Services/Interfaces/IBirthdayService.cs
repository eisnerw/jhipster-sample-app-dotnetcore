using System.Threading.Tasks;
using System.Collections.Generic;
using Nest;
using JhipsterSampleApplication.Domain.Entities;
using Newtonsoft.Json.Linq;
using JhipsterSampleApplication.Dto;

namespace JhipsterSampleApplication.Domain.Services.Interfaces
{
    public interface IBirthdayService : IEntityService<Birthday>
    {
    }
}