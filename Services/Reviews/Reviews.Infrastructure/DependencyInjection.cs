using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Reviews.Application.Common.Interfaces;
using Reviews.Domain.Interfaces;
using Reviews.Infrastructure.Persistence;
using Reviews.Infrastructure.Seeding;

namespace Reviews.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        MongoMappings.Register();

        services.Configure<MongoDbOptions>(configuration.GetSection("MongoDb"));
        services.PostConfigure<MongoDbOptions>(options =>
        {
            var connectionString = configuration.GetConnectionString("ReviewsMongo");
            if (!string.IsNullOrWhiteSpace(connectionString))
                options.ConnectionString = connectionString;
        });

        services.AddSingleton<MongoDbContext>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IReviewQueries, ReviewQueries>();
        services.AddScoped<IDataSeeder, ReviewSeeder>();
        services.AddHostedService<IndexCreationService>();

        return services;
    }
}
