using System.Reflection;
using HRM.Modules.Organization.Infrastructure.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HRM.Modules.Organization.Api
{
    public static class Extensions
    {
        public static IServiceCollection AddOrganizationModule(this IServiceCollection services, IConfiguration configuration)
        {
            // MediatR (optional for this module's current implementation, but good practice)
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Application.AssemblyReference.Assembly));

            // DbContext
            services.AddDbContext<OrganizationDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("OrganizationDb")));

            services.AddScoped<Application.DAL.IOrganizationDbContext>(provider => provider.GetRequiredService<OrganizationDbContext>());

            return services;
        }
    }
}

namespace HRM.Modules.Organization.Application
{
    public static class AssemblyReference
    {
        public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
    }
}
