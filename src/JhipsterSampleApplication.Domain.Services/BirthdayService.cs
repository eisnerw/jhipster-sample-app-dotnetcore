using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Domain.Services.Interfaces;
using Nest;

namespace JhipsterSampleApplication.Domain.Services;

public class BirthdayService : EntityService<Birthday>, IBirthdayService
{
    public BirthdayService(IElasticClient elasticClient, IBqlService<Birthday> bqlService, IViewService viewService)
        : base("birthdays", "wikipedia", elasticClient, bqlService, viewService)
    {
    }
}
