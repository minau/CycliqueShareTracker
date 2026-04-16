using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Infrastructure.Persistence;
using CycliqueShareTracker.Infrastructure.Providers;
using CycliqueShareTracker.Infrastructure.Repositories;
using CycliqueShareTracker.Infrastructure.Services;
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

        services.Configure<MarketDataOptions>(configuration.GetSection(MarketDataOptions.SectionName));
        services.AddSingleton<ProviderSymbolMapper>();

        services.AddHttpClient<YahooFinanceDataProvider>();
        services.AddHttpClient<AlphaVantageDataProvider>();
        services.AddScoped<IMarketDataSource>(sp => sp.GetRequiredService<YahooFinanceDataProvider>());
        services.AddScoped<IMarketDataSource>(sp => sp.GetRequiredService<AlphaVantageDataProvider>());
        services.AddScoped<IDataProvider, FallbackMarketDataProvider>();

        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IPriceRepository, PriceRepository>();
        services.AddScoped<IIndicatorRepository, IndicatorRepository>();
        services.AddScoped<IIndicatorSettingsService, IndicatorSettingsService>();

        return services;
    }
}
