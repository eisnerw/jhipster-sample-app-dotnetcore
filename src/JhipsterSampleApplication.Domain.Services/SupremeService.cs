using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using Nest;

namespace JhipsterSampleApplication.Domain.Services
{
    public class SupremeService : EntityService<Supreme>, ISupremeService
    {
        public SupremeService(IElasticClient elasticClient, IBqlService<Supreme> bqlService, IViewService viewService)
            : base("supreme","justia_url,argument2_url,facts_of_the_case,conclusion", elasticClient, bqlService, viewService)
        {
        }
    }
}
