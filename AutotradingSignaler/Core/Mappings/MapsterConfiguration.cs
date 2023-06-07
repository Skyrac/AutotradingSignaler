using AutotradingSignaler.Contracts.Dtos;
using Mapster;
using System.Reflection;

namespace AutotradingSignaler.Core.Mappings;

public static class MapsterConfiguration
{
    public static void AddMapster(this IServiceCollection services)
    {
        var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
        Assembly applicationAssembly = typeof(BaseDto<,>).Assembly;
        typeAdapterConfig.Scan(applicationAssembly);
    }
}

