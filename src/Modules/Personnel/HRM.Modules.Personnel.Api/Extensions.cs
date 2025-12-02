using System.Reflection;
using HRM.Modules.Personnel.Application.DAL;
using HRM.Modules.Personnel.Application.Services;
using HRM.Modules.Personnel.Infrastructure.DAL;
using HRM.Modules.Personnel.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HRM.Modules.Personnel.Api
{
    public static class Extensions
    {
        public static IServiceCollection AddPersonnelModule(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Application.AssemblyReference.Assembly));

            services.AddHttpClient("OrganizationApi", (serviceProvider, client) =>
            {
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                var baseUrl = configuration.GetValue<string>("ApiSettings:BaseUrl");
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    client.BaseAddress = new Uri(baseUrl);
                }
            });

            services.AddDbContext<PersonnelDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("PersonnelDb")));

            services.AddScoped<IPersonnelDbContext>(provider => provider.GetRequiredService<PersonnelDbContext>());
            services.AddScoped<HRM.Shared.Kernel.Interfaces.IModuleDbContext>(provider => provider.GetRequiredService<PersonnelDbContext>());

            services.AddScoped<IOrganizationService, OrganizationService>();

            return services;
        }
    }
}

namespace HRM.Modules.Personnel.Application
{
    public static class AssemblyReference
    {
        public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
    }
}
