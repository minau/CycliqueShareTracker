using CycliqueShareTracker.Application.Interfaces;
using CycliqueShareTracker.Application.Models;
using CycliqueShareTracker.Application.Services;
using CycliqueShareTracker.Infrastructure.DependencyInjection;
using CycliqueShareTracker.Infrastructure.Persistence;
using CycliqueShareTracker.Web.Background;
using CycliqueShareTracker.Web.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AssetOptions>(builder.Configuration.GetSection(AssetOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<SchedulerOptions>(builder.Configuration.GetSection(SchedulerOptions.SectionName));

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<IIndicatorCalculator, IndicatorCalculator>();
builder.Services.AddScoped<ISignalService, SignalService>();
builder.Services.AddScoped<IDataSyncService, DataSyncService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
