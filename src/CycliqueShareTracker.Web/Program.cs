using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Services;
using CycliqueShareTracker.Infrastructure.DependencyInjection;
using CycliqueShareTracker.Infrastructure.Persistence;
using CycliqueShareTracker.Web.Background;
using CycliqueShareTracker.Web.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<WatchlistOptions>(builder.Configuration.GetSection(WatchlistOptions.SectionName));
builder.Services.Configure<DashboardOptions>(builder.Configuration.GetSection(DashboardOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<SchedulerOptions>(builder.Configuration.GetSection(SchedulerOptions.SectionName));
builder.Services.Configure<BacktestExportOptions>(builder.Configuration.GetSection(BacktestExportOptions.SectionName));

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"] ?? "/var/cyclique/keys";
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("CycliqueShareTracker");

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<IIndicatorCalculator, IndicatorCalculator>();
builder.Services.AddScoped<ISignalAlgorithm, CycliqueShareTracker.Application.Algorithms.RsiMeanReversionAlgorithm>();
builder.Services.AddScoped<ISignalAlgorithm, CycliqueShareTracker.Application.Algorithms.EmaCrossoverAlgorithm>();
builder.Services.AddScoped<ISignalAlgorithm, CycliqueShareTracker.Application.Algorithms.MacdReversalAlgorithm>();
builder.Services.AddScoped<ISignalAlgorithm, CycliqueShareTracker.Application.Algorithms.TrendFollowingAlgorithm>();
builder.Services.AddScoped<ISignalAlgorithm, CycliqueShareTracker.Application.Algorithms.PullbackInTrendAlgorithm>();
builder.Services.AddScoped<ISignalAlgorithm, CycliqueShareTracker.Application.Algorithms.DrawdownReboundAlgorithm>();
builder.Services.AddScoped<ISignalAlgorithmRegistry, SignalAlgorithmRegistry>();
builder.Services.AddScoped<ISignalService, SignalService>();
builder.Services.AddScoped<IExitSignalService, ExitSignalService>();
builder.Services.AddScoped<IDataSyncService, DataSyncService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IBacktestEngine, BacktestEngine>();
builder.Services.AddScoped<IBacktestService, BacktestService>();

builder.Services.AddHostedService<DailyUpdateBackgroundService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.AccessDeniedPath = "/auth/login";
        options.Cookie.Name = "cyclique_auth";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
    });

builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews();

var app = builder.Build();

MigrateDatabaseWithRetry(app);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!string.IsNullOrWhiteSpace(builder.Configuration["ASPNETCORE_HTTPS_PORT"]))
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static void MigrateDatabaseWithRetry(WebApplication app)
{
    const int maxAttempts = 10;
    var delay = TimeSpan.FromSeconds(3);

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
            app.Logger.LogInformation("Database migration completed.");
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            app.Logger.LogWarning(ex, "Database migration attempt {Attempt}/{MaxAttempts} failed. Retrying in {DelaySeconds}s.", attempt, maxAttempts, delay.TotalSeconds);
            Thread.Sleep(delay);
        }
    }

    using var finalScope = app.Services.CreateScope();
    var finalDb = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
    finalDb.Database.Migrate();
}
