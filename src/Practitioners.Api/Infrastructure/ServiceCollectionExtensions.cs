using Microsoft.EntityFrameworkCore;
using Practitioners.Api.Data;
using Practitioners.Api.Services;
using StackExchange.Redis;

namespace Practitioners.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>Общая инфраструктура для API: БД, Redis, кэш, сервисы.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cfg.GetConnectionString("Postgres")));

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(cfg.GetConnectionString("Redis")!);
            options.AbortOnConnectFail = false;   // не падаем, если Redis ещё поднимается
            return ConnectionMultiplexer.Connect(options);
        });

        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddScoped<PractitionerService>();
        return services;
    }
}
