using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PostTradeSystem.Infrastructure.Data;
using PostTradeSystem.Infrastructure.Repositories;

namespace PostTradeSystem.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PostTradeDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                    sqlOptions.CommandTimeout(30);

                    sqlOptions.MigrationsAssembly(typeof(PostTradeDbContext).Assembly.GetName().Name);
                    sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory");
                });

            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(true);
        });

        // Note: IEventSerializer is registered in AddSerializationManagement extension method
        // This ensures proper dependency resolution with SerializationManagementService
        
        services.AddScoped<IEventStoreRepository, EventStoreRepository>();
        
        services.AddScoped(typeof(IAggregateRepository<>), typeof(AggregateRepository<>));
        
        services.AddScoped<Func<string, string, IEnumerable<Core.Events.IDomainEvent>, Core.Aggregates.TradeAggregate>>(
            provider => (id, partitionKey, events) => Core.Aggregates.TradeAggregate.FromHistory(id, partitionKey, events));
        

        return services;
    }
}