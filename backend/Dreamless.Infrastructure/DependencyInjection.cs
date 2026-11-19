using Dreamless.Infrastructure.CodeGen;
using Dreamless.Infrastructure.Identity;
using Dreamless.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dreamless.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDreamless(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<CodeGenOptions>(configuration.GetSection("CodeGen"));
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ISqlSugarClient>(sp => SqlSugarSetup.Create(sp, configuration));
        services.AddScoped<PasswordService>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<DbSeeder>();

        var assembly = typeof(DependencyInjection).Assembly;
        foreach (var implementation in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IAppService).IsAssignableFrom(t)))
        {
            var service = implementation.GetInterfaces().FirstOrDefault(i => i != typeof(IAppService) && typeof(IAppService).IsAssignableFrom(i));
            if (service != null)
            {
                services.AddScoped(service, implementation);
            }
        }

        services.AddAuthorization();
        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
        await seeder.SeedAsync();
    }
}
