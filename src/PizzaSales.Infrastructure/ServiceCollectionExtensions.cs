using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PizzaSales.Application;

namespace PizzaSales.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPizzaSalesInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<PizzaSalesDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IPizzaSalesImportService, PizzaSalesImportService>();
        services.AddScoped<ISalesQueryService, SalesQueryService>();
        return services;
    }
}
