using AutoMapper;
using Core.Contracts;
using ProductService.Models;

namespace ProductService.Contracts;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        CreateMap<ProductModel, ProductContract>().ReverseMap();

        CreateMap<string, ProductCategory>().ConvertUsing(new String2EnumConverter<ProductCategory>());
        CreateMap<ProductCategory, string>().ConvertUsing(new Enum2StringConverter<ProductCategory>());
    }
}
