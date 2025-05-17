using AutoMapper;
using JhipsterSampleApplication.Dto;
using JhipsterSampleApplication.Domain.Entities;

namespace JhipsterSampleApplication.Configuration.AutoMapper
{
    public class ViewProfile : Profile
    {
        public ViewProfile()
        {
            CreateMap<View, ViewDto>().ReverseMap();
        }
    }
} 