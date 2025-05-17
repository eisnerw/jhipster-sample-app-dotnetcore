using AutoMapper;
using JhipsterSampleApplication.Dto;
using JhipsterSampleApplication.Domain.Entities;

namespace JhipsterSampleApplication.Configuration.AutoMapper
{
    public class ViewProfile : Profile
    {
        public ViewProfile()
        {
            CreateMap<View, ViewDto>()
                .ForMember(dest => dest.SecondLevelView, opt => opt.MapFrom(src => src.SecondLevelView));
            CreateMap<ViewDto, View>()
                .ForMember(dest => dest.SecondLevelView, opt => opt.MapFrom(src => src.SecondLevelView));
        }
    }
} 