using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemBeritaAcara.Core.Entities;
using SistemBeritaAcara.Core.Interfaces;
using SistemBeritaAcara.Infrastructure.Data;
using SistemBeritaAcara.Infrastructure.Jobs;
using SistemBeritaAcara.Infrastructure.Services;

namespace SistemBeritaAcara.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        string connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' tidak ditemukan.");

        // Factory (Singleton) + AppDbContext (Scoped) — AddDbContextFactory mendaftarkan keduanya.
        services.AddDbContextFactory<AppDbContext>(opt =>
            opt.UseSqlServer(connectionString));

        services.AddIdentity<ApplicationUser, IdentityRole<int>>(opt =>
        {
            opt.Password.RequireDigit = false;
            opt.Password.RequireLowercase = false;
            opt.Password.RequireUppercase = false;
            opt.Password.RequireNonAlphanumeric = false;
            opt.Password.RequiredLength = 6;
            opt.User.RequireUniqueEmail = true;
            // Kunci akun 5 menit setelah 5x salah password
            opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            opt.Lockout.MaxFailedAccessAttempts = 5;
            opt.Lockout.AllowedForNewUsers = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders()
        .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>();

        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            options.ValidationInterval = TimeSpan.FromMinutes(2);
        });

        services.AddHangfire(hf => hf
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));

        services.AddHangfireServer();

        services.AddHttpClient("Gotenberg", (sp, client) =>
        {
            var url = config["Gotenberg:ServerUrl"] ?? "http://localhost:3000";
            client.BaseAddress = new Uri(url.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IBACounterService, BACounterService>();
        services.AddScoped<IDeaktivasiService, DeaktivasiService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IExcelService, ExcelService>();
        services.AddScoped<DueDateCheckerJob>();
        services.AddScoped<AutoApproveJob>();
        return services;
    }
}
