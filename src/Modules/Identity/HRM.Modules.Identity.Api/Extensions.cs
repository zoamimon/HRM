using System.Reflection;
using HRM.Modules.Identity.Application.DAL;
using HRM.Modules.Identity.Application.Services;
using HRM.Modules.Identity.Infrastructure.DAL;
using HRM.Modules.Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HRM.Modules.Identity.Api
{
    public static class Extensions
    {
        public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
        {
            // MediatR
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Application.AssemblyReference.Assembly));

            // DbContext
            services.AddDbContext<IdentityDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("IdentityDb")));

            services.AddScoped<IIdentityDbContext>(provider => provider.GetRequiredService<IdentityDbContext>());

            // Services
            services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
            services.AddScoped<ITokenService, JwtTokenService>();

            // JWT Settings
            services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

            return services;
        }
    }
}

namespace HRM.Modules.Identity.Application
{
    public static class AssemblyReference
    {
        public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
    }
}
