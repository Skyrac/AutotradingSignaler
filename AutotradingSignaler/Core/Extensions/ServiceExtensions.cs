using AutotradingSignaler.Persistence.UnitsOfWork;
using AutotradingSignaler.Persistence.UnitsOfWork.Interfaces;
using Database.Utils.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AutotradingSignaler.Core.Extensions;

public static class ServiceExtensions
{
    private static IServiceCollection AddUnitOfWork<T, TImpl>(this IServiceCollection services)
        where T : class, IUnitOfWork
        where TImpl : UnitOfWork, T
    {
        return services.AddScoped<T, TImpl>();
    }


    public static IServiceCollection AddDatabaseContext<T>(this IServiceCollection services, IConfiguration configuration)
        where T : DbContext, IDatabaseContext
    {
        var postgreSqlConnectionString = $"Host={configuration["PGHOST"]};Port={configuration["PGPORT"]};Username={configuration["PGUSER"]};Password={configuration["PGPASSWORD"]};Database={configuration["PGABOATDB"]};Pooling=true;SSL Mode=Require;TrustServerCertificate=True;Include Error Detail=True";
        return services.AddDbContext<T>(options =>
        {
            options.UseInMemoryDatabase("Test").UseLowerCaseNamingConvention();
            //options.UseNpgsql(postgreSqlConnectionString, serverOptions =>
            //{
            //    serverOptions.EnableRetryOnFailure();
            //});
        });
    }

    public static IServiceCollection AddPersistence<TDbContext, TUnitOfWork, TUnitOfWorkImpl>(this IServiceCollection services, IConfiguration configuration)
        where TDbContext : DbContext, IDatabaseContext
        where TUnitOfWork : class, IUnitOfWork
        where TUnitOfWorkImpl : UnitOfWork, TUnitOfWork
    {
        return services.AddDatabaseContext<TDbContext>(configuration).AddUnitOfWork<TUnitOfWork, TUnitOfWorkImpl>();
    }
}
