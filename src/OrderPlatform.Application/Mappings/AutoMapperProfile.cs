using AutoMapper;
using OrderPlatform.Application.Auth.Dtos;
using OrderPlatform.Domain.Entities;

namespace OrderPlatform.Application.Mappings;

/// <summary>AutoMapper 映射配置。</summary>
public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        // 用户 → 用户信息 DTO，角色枚举转字符串
        CreateMap<User, UserInfoDto>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()));
    }
}