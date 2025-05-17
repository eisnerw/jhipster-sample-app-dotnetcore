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
                .ForMember(dest => dest.PrimaryView, opt => opt.MapFrom(src => src.PrimaryView));
            CreateMap<ViewDto, View>()
                .ForMember(dest => dest.PrimaryView, opt => opt.MapFrom(src => src.PrimaryView));
        }
    }
} 