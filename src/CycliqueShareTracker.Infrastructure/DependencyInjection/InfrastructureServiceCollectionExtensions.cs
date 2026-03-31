using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Infrastructure.Persistence;
using CycliqueShareTracker.Infrastructure.Providers;
using CycliqueShareTracker.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CycliqueShareTracker.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is missing.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddHttpClient<IDataProvider, StooqDataProvider>();

        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IPriceRepository, PriceRepository>();
        services.AddScoped<IIndicatorRepository, IndicatorRepository>();
        services.AddScoped<ISignalRepository, SignalRepository>();

        return services;
    }
}
