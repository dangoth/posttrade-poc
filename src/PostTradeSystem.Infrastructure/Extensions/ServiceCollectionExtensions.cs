using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PostTradeSystem.Core.Schemas;
using PostTradeSystem.Core.Services;
using PostTradeSystem.Infrastructure.Data;
using PostTradeSystem.Infrastructure.Repositories;
using PostTradeSystem.Infrastructure.Services;

namespace PostTradeSystem.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();

        services.AddDbContext<PostTradeDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions =>
                {
                    sqlOptions.CommandTimeout(30);

                    sqlOptions.MigrationsAssembly(typeof(PostTradeDbContext).Assembly.GetName().Name);
                    sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory");
                });

            options.EnableSensitiveDataLogging(false);
            options.EnableDetailedErrors(true);
        });

        services.AddScoped(typeof(IAggregateRepository<>), typeof(AggregateRepository<>));
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IEventStoreRepository, EventStoreRepository>();
        services.AddScoped<IOutboxService, OutboxService>();
        services.AddScoped<IRetryService, RetryService>();
        
        services.AddHostedService<DatabaseMigrationService>();
        services.AddHostedService<SerializationInitializationService>();
        
        services.AddScoped<Func<string, string, IEnumerable<Core.Events.IDomainEvent>, Core.Aggregates.TradeAggregate>>(
            provider => (id, partitionKey, events) => Core.Aggregates.TradeAggregate.FromHistory(id, partitionKey, events));
        
        services.AddSingleton<JsonSchemaValidator>(provider =>
        {
            var validator = new JsonSchemaValidator();
            
            // Register message schemas
            validator.RegisterSchema("EquityTradeMessage", MessageSchemas.EquityTradeMessageSchema);
            validator.RegisterSchema("FxTradeMessage", MessageSchemas.FxTradeMessageSchema);
            validator.RegisterSchema("OptionTradeMessage", MessageSchemas.OptionTradeMessageSchema);
            validator.RegisterSchema("TradeMessage", MessageSchemas.TradeMessageSchema);
            validator.RegisterSchema("TradeMessageEnvelope", MessageSchemas.TradeMessageEnvelopeSchema);
            
            return validator;
        });

        return services;
    }
}