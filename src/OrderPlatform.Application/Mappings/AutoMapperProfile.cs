using AutoMapper;
using OrderPlatform.Application.Auth.Dtos;
using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Application.Mappings;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        CreateMap<User, UserInfoDto>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()));
    }
}
