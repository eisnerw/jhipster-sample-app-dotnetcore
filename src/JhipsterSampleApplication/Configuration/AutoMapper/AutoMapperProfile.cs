using AutoMapper;
using System.Linq;
using JhipsterSampleApplication.Domain.Entities;
using JhipsterSampleApplication.Dto;

namespace JhipsterSampleApplication.Configuration.AutoMapper
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<User, UserDto>()
                .ForMember(userDto => userDto.Roles, opt => opt.MapFrom(user => user.UserRoles.Select(iur => iur.Role.Name).ToHashSet()))
            .ReverseMap()
                .ForPath(user => user.UserRoles, opt => opt.MapFrom(userDto => userDto.Roles.Select(role => new UserRole { Role = new Role { Name = role }, UserId = userDto.Id }).ToHashSet()));

            CreateMap<RulesetDto, Ruleset>()
                .ForMember(dest => dest.rules, opt => opt.MapFrom(src => src.rules))
                .ReverseMap();
            CreateMap<NamedQuery, NamedQueryDto>().ReverseMap();
            CreateMap<Selector, SelectorDto>().ReverseMap();
            CreateMap<Movie, MovieDto>().ReverseMap();
            CreateMap<Birthday, BirthdayDto>().ReverseMap();
        }
    }
}
